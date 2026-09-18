using System.Globalization;
using GroceryTracker.Domain.Model;

namespace GroceryTracker.Domain.Analytics;

/// <summary>
/// Pure spend analytics over already-loaded trips.
/// </summary>
/// <remarks>
/// Deliberately free of EF Core and HTTP so the identical code runs in the BFF against PostgreSQL
/// and inside the browser against the IndexedDB cache. That is what keeps the dashboard showing
/// the same numbers when the phone is offline in the shop.
/// </remarks>
public static class SpendAnalyzer
{
  public const string UncategorisedName = "Uncategorised";
  public const string UncategorisedColor = "#adb5bd";

  private const int DefaultTopItemCount = 10;

  public static SpendSummary Summarise(
    IEnumerable<ShoppingTrip> trips,
    IEnumerable<Store> stores,
    IEnumerable<Category> categories,
    DateOnly from,
    DateOnly to,
    SpendGranularity granularity = SpendGranularity.Month,
    string currencyCode = "EUR",
    int topItemCount = DefaultTopItemCount)
  {
    ArgumentNullException.ThrowIfNull(trips);
    ArgumentNullException.ThrowIfNull(stores);
    ArgumentNullException.ThrowIfNull(categories);

    if (to < from)
    {
      (from, to) = (to, from);
    }

    var storeNames = stores.Where(s => !s.IsDeleted).ToDictionary(s => s.Id, s => s.Name);
    var categoryLookup = categories.Where(c => !c.IsDeleted).ToDictionary(c => c.Id);

    var live = trips.Where(t => !t.IsDeleted).ToList();
    var window = live.Where(t => t.PurchasedOn >= from && t.PurchasedOn <= to).ToList();

    if (window.Count == 0)
    {
      var emptyPrevious = PreviousPeriodSpend(live, from, to);
      return SpendSummary.Empty(from, to, currencyCode) with
      {
        PreviousPeriodSpend = emptyPrevious,
        ChangeVsPreviousPct = PercentChange(0m, emptyPrevious),
      };
    }

    var totalSpend = window.Sum(t => t.TotalAmount);
    var previousSpend = PreviousPeriodSpend(live, from, to);

    return new SpendSummary(
      From: from,
      To: to,
      CurrencyCode: currencyCode,
      TotalSpend: totalSpend,
      TripCount: window.Count,
      AverageTripAmount: decimal.Round(totalSpend / window.Count, 2, MidpointRounding.AwayFromZero),
      PreviousPeriodSpend: previousSpend,
      ChangeVsPreviousPct: PercentChange(totalSpend, previousSpend),
      Series: BuildSeries(window, granularity),
      ByCategory: BuildCategorySlices(window, categoryLookup, totalSpend),
      ByStore: BuildStoreSlices(window, storeNames, totalSpend),
      TopItems: BuildTopItems(window, topItemCount));
  }

  /// <summary>
  /// Spend in the window of equal length ending the day before <paramref name="from"/>, which is
  /// what the dashboard compares against.
  /// </summary>
  private static decimal PreviousPeriodSpend(IEnumerable<ShoppingTrip> trips, DateOnly from, DateOnly to)
  {
    var lengthInDays = to.DayNumber - from.DayNumber + 1;
    var previousTo = from.AddDays(-1);
    var previousFrom = previousTo.AddDays(-(lengthInDays - 1));

    return trips
      .Where(t => t.PurchasedOn >= previousFrom && t.PurchasedOn <= previousTo)
      .Sum(t => t.TotalAmount);
  }

  private static decimal? PercentChange(decimal current, decimal previous)
  {
    if (previous == 0m)
    {
      return null;
    }

    return decimal.Round(((current - previous) / previous) * 100m, 1, MidpointRounding.AwayFromZero);
  }

  private static List<SpendPoint> BuildSeries(List<ShoppingTrip> window, SpendGranularity granularity) =>
    window
      .GroupBy(t => PeriodStart(t.PurchasedOn, granularity))
      .OrderBy(g => g.Key)
      .Select(g => new SpendPoint(
        g.Key,
        FormatLabel(g.Key, granularity),
        g.Sum(t => t.TotalAmount),
        g.Count()))
      .ToList();

  internal static DateOnly PeriodStart(DateOnly date, SpendGranularity granularity) => granularity switch
  {
    SpendGranularity.Day => date,
    SpendGranularity.Week => date.AddDays(-DaysSinceMonday(date.DayOfWeek)),
    SpendGranularity.Month => new DateOnly(date.Year, date.Month, 1),
    _ => throw new ArgumentOutOfRangeException(nameof(granularity), granularity, "Unsupported granularity."),
  };

  private static int DaysSinceMonday(DayOfWeek day) => ((int)day + 6) % 7;

  private static string FormatLabel(DateOnly periodStart, SpendGranularity granularity) => granularity switch
  {
    SpendGranularity.Day => periodStart.ToString("dd MMM", CultureInfo.InvariantCulture),
    SpendGranularity.Week => $"KW {ISOWeek.GetWeekOfYear(periodStart.ToDateTime(TimeOnly.MinValue))}",
    // Two-digit year: a full "May 2026" does not fit a month column on a phone.
    SpendGranularity.Month => periodStart.ToString("MMM yy", CultureInfo.InvariantCulture),
    _ => periodStart.ToString("O", CultureInfo.InvariantCulture),
  };

  /// <summary>
  /// Splits every receipt across categories so the slices always add up to the headline total.
  /// </summary>
  /// <remarks>
  /// A trip's own <see cref="ShoppingTrip.TotalAmount"/> is what was really paid, while its items
  /// are an optional breakdown. Whatever the items leave unaccounted for becomes uncategorised
  /// spend. If the items instead overshoot the receipt (a typo, or a mis-scanned line) their
  /// attributions are scaled down proportionally rather than inventing spend that never happened.
  /// </remarks>
  private static List<CategorySlice> BuildCategorySlices(
    List<ShoppingTrip> window,
    Dictionary<Guid, Category> categoryLookup,
    decimal totalSpend)
  {
    var categorised = new Dictionary<Guid, decimal>();
    var uncategorised = 0m;

    foreach (var trip in window)
    {
      var items = trip.Items.Where(i => !i.IsDeleted).ToList();
      var itemsTotal = items.Sum(i => i.Amount);

      // Scale back an over-itemised receipt so attributed spend never exceeds what was paid.
      var scale = itemsTotal > trip.TotalAmount && itemsTotal > 0m
        ? trip.TotalAmount / itemsTotal
        : 1m;

      foreach (var item in items)
      {
        var attributed = item.Amount * scale;

        if (item.CategoryId.HasValue && categoryLookup.ContainsKey(item.CategoryId.Value))
        {
          categorised[item.CategoryId.Value] = categorised.GetValueOrDefault(item.CategoryId.Value) + attributed;
        }
        else
        {
          uncategorised += attributed;
        }
      }

      var unallocated = trip.TotalAmount - (itemsTotal * scale);
      if (unallocated > 0m)
      {
        uncategorised += unallocated;
      }
    }

    var slices = categorised
      .Where(pair => pair.Value != 0m)
      .Select(pair =>
      {
        var amount = decimal.Round(pair.Value, 2, MidpointRounding.AwayFromZero);
        var category = categoryLookup[pair.Key];

        return new CategorySlice(
          pair.Key,
          category.Name,
          category.ColorHex,
          amount,
          Share(amount, totalSpend));
      })
      .ToList();

    if (uncategorised != 0m)
    {
      var amount = decimal.Round(uncategorised, 2, MidpointRounding.AwayFromZero);
      slices.Add(new CategorySlice(null, UncategorisedName, UncategorisedColor, amount, Share(amount, totalSpend)));
    }

    return slices
      .OrderByDescending(slice => slice.Amount)
      .ToList();
  }

  private static List<StoreSlice> BuildStoreSlices(
    List<ShoppingTrip> window,
    Dictionary<Guid, string> storeNames,
    decimal totalSpend) =>
    window
      .GroupBy(t => t.StoreId)
      .Select(g =>
      {
        var amount = g.Sum(t => t.TotalAmount);
        return new StoreSlice(
          g.Key,
          storeNames.GetValueOrDefault(g.Key, "Unknown store"),
          amount,
          g.Count(),
          Share(amount, totalSpend));
      })
      .OrderByDescending(slice => slice.Amount)
      .ToList();

  private static List<TopItem> BuildTopItems(List<ShoppingTrip> window, int topItemCount) =>
    window
      .SelectMany(t => t.Items)
      .Where(i => !i.IsDeleted && !string.IsNullOrWhiteSpace(i.Description))
      .GroupBy(i => i.Description.Trim(), StringComparer.OrdinalIgnoreCase)
      .Select(g => new TopItem(
        g.First().Description.Trim(),
        decimal.Round(g.Sum(i => i.Amount), 2, MidpointRounding.AwayFromZero),
        g.Count()))
      .OrderByDescending(i => i.TotalAmount)
      .ThenBy(i => i.Description, StringComparer.OrdinalIgnoreCase)
      .Take(topItemCount)
      .ToList();

  private static decimal Share(decimal amount, decimal total) => total == 0m
    ? 0m
    : decimal.Round((amount / total) * 100m, 1, MidpointRounding.AwayFromZero);
}
