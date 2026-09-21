using GroceryTracker.Domain.Analytics;
using GroceryTracker.Domain.Model;

namespace GroceryTracker.Domain.Tests;

public class SpendAnalyzerTests
{
  private static readonly Guid HouseholdId = Guid.NewGuid();
  private static readonly Guid AldiId = Guid.NewGuid();
  private static readonly Guid RewuId = Guid.NewGuid();
  private static readonly Guid ProduceId = Guid.NewGuid();
  private static readonly Guid DairyId = Guid.NewGuid();

  private static readonly Store[] Stores =
  [
    new() { Id = AldiId, Name = "Aldi", HouseholdId = HouseholdId },
    new() { Id = RewuId, Name = "Rewe", HouseholdId = HouseholdId },
  ];

  private static readonly Category[] Categories =
  [
    new() { Id = ProduceId, Name = "Produce", ColorHex = "#2e7d32", HouseholdId = HouseholdId },
    new() { Id = DairyId, Name = "Dairy", ColorHex = "#0277bd", HouseholdId = HouseholdId },
  ];

  private static ShoppingTrip Trip(
    string date,
    decimal total,
    Guid? storeId = null,
    params (decimal Amount, Guid? CategoryId, string Description)[] items)
  {
    var trip = new ShoppingTrip
    {
      PurchasedOn = DateOnly.Parse(date),
      TotalAmount = total,
      StoreId = storeId ?? AldiId,
      HouseholdId = HouseholdId,
    };

    foreach (var (amount, categoryId, description) in items)
    {
      trip.Items.Add(new ExpenseItem
      {
        Amount = amount,
        CategoryId = categoryId,
        Description = description,
        TripId = trip.Id,
        HouseholdId = HouseholdId,
      });
    }

    return trip;
  }

  [Fact]
  public void Summarise_reports_totals_and_average_basket()
  {
    ShoppingTrip[] trips =
    [
      Trip("2026-03-02", 40m),
      Trip("2026-03-10", 60m),
      Trip("2026-03-20", 35m),
    ];

    var summary = SpendAnalyzer.Summarise(
      trips, Stores, Categories, new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 31));

    Assert.Equal(135m, summary.TotalSpend);
    Assert.Equal(3, summary.TripCount);
    Assert.Equal(45m, summary.AverageTripAmount);
  }

  [Fact]
  public void Summarise_excludes_trips_outside_the_window()
  {
    ShoppingTrip[] trips =
    [
      Trip("2026-02-28", 100m),
      Trip("2026-03-15", 25m),
      Trip("2026-04-01", 100m),
    ];

    var summary = SpendAnalyzer.Summarise(
      trips, Stores, Categories, new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 31));

    Assert.Equal(25m, summary.TotalSpend);
    Assert.Equal(1, summary.TripCount);
  }

  [Fact]
  public void Summarise_ignores_tombstoned_trips_and_items()
  {
    var deletedTrip = Trip("2026-03-05", 80m);
    deletedTrip.IsDeleted = true;

    var trip = Trip("2026-03-06", 30m, AldiId, (10m, ProduceId, "Apples"));
    trip.Items.First().IsDeleted = true;

    var summary = SpendAnalyzer.Summarise(
      [deletedTrip, trip], Stores, Categories, new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 31));

    Assert.Equal(30m, summary.TotalSpend);
    Assert.Equal(1, summary.TripCount);

    // The tombstoned item must not claim any category; the whole receipt is unallocated.
    var slice = Assert.Single(summary.ByCategory);
    Assert.Null(slice.CategoryId);
    Assert.Equal(30m, slice.Amount);
  }

  [Fact]
  public void Category_slices_always_add_up_to_the_total_spend()
  {
    ShoppingTrip[] trips =
    [
      Trip("2026-03-02", 50m, AldiId, (20m, ProduceId, "Apples"), (10m, DairyId, "Milk")),
      Trip("2026-03-09", 30m),
    ];

    var summary = SpendAnalyzer.Summarise(
      trips, Stores, Categories, new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 31));

    Assert.Equal(80m, summary.TotalSpend);
    Assert.Equal(80m, summary.ByCategory.Sum(s => s.Amount));

    var uncategorised = Assert.Single(summary.ByCategory, s => s.CategoryId is null);

    // 20 left over on the first receipt plus the entire second receipt.
    Assert.Equal(50m, uncategorised.Amount);
    Assert.Equal(SpendAnalyzer.UncategorisedName, uncategorised.Name);
  }

  [Fact]
  public void Items_exceeding_the_receipt_total_are_scaled_down_rather_than_inventing_spend()
  {
    // A mis-keyed line: the items claim 120 on a receipt that was actually 60.
    ShoppingTrip[] trips =
    [
      Trip("2026-03-02", 60m, AldiId, (90m, ProduceId, "Apples"), (30m, DairyId, "Milk")),
    ];

    var summary = SpendAnalyzer.Summarise(
      trips, Stores, Categories, new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 31));

    Assert.Equal(60m, summary.TotalSpend);
    Assert.Equal(60m, summary.ByCategory.Sum(s => s.Amount));
    Assert.Equal(45m, summary.ByCategory.Single(s => s.CategoryId == ProduceId).Amount);
    Assert.Equal(15m, summary.ByCategory.Single(s => s.CategoryId == DairyId).Amount);
    Assert.DoesNotContain(summary.ByCategory, s => s.CategoryId is null);
  }

  [Fact]
  public void Items_pointing_at_an_unknown_category_fall_into_uncategorised()
  {
    ShoppingTrip[] trips =
    [
      Trip("2026-03-02", 40m, AldiId, (40m, Guid.NewGuid(), "Mystery")),
    ];

    var summary = SpendAnalyzer.Summarise(
      trips, Stores, Categories, new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 31));

    var slice = Assert.Single(summary.ByCategory);
    Assert.Null(slice.CategoryId);
    Assert.Equal(40m, slice.Amount);
  }

  [Fact]
  public void Store_breakdown_groups_by_store_with_shares()
  {
    ShoppingTrip[] trips =
    [
      Trip("2026-03-02", 60m, AldiId),
      Trip("2026-03-03", 20m, AldiId),
      Trip("2026-03-04", 20m, RewuId),
    ];

    var summary = SpendAnalyzer.Summarise(
      trips, Stores, Categories, new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 31));

    var aldi = summary.ByStore.Single(s => s.StoreId == AldiId);
    Assert.Equal("Aldi", aldi.Name);
    Assert.Equal(80m, aldi.Amount);
    Assert.Equal(2, aldi.TripCount);
    Assert.Equal(80m, aldi.Share);

    Assert.Equal(20m, summary.ByStore.Single(s => s.StoreId == RewuId).Amount);
  }

  [Fact]
  public void Previous_period_is_the_equally_long_window_immediately_before()
  {
    ShoppingTrip[] trips =
    [
      Trip("2026-02-14", 100m),
      Trip("2026-03-14", 150m),
    ];

    var summary = SpendAnalyzer.Summarise(
      trips, Stores, Categories, new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 31));

    Assert.Equal(150m, summary.TotalSpend);
    Assert.Equal(100m, summary.PreviousPeriodSpend);
    Assert.Equal(50m, summary.ChangeVsPreviousPct);
  }

  [Fact]
  public void Change_is_null_when_there_is_nothing_to_compare_against()
  {
    var summary = SpendAnalyzer.Summarise(
      [Trip("2026-03-14", 150m)], Stores, Categories, new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 31));

    Assert.Equal(0m, summary.PreviousPeriodSpend);
    Assert.Null(summary.ChangeVsPreviousPct);
  }

  [Fact]
  public void Empty_window_still_reports_the_previous_period()
  {
    var summary = SpendAnalyzer.Summarise(
      [Trip("2026-02-10", 70m)], Stores, Categories, new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 31));

    Assert.Equal(0m, summary.TotalSpend);
    Assert.Equal(0, summary.TripCount);
    Assert.Equal(70m, summary.PreviousPeriodSpend);
    Assert.Empty(summary.Series);
  }

  [Fact]
  public void Monthly_series_buckets_by_calendar_month_in_order()
  {
    ShoppingTrip[] trips =
    [
      Trip("2026-03-02", 10m),
      Trip("2026-01-15", 30m),
      Trip("2026-03-20", 5m),
      Trip("2026-02-01", 20m),
    ];

    var summary = SpendAnalyzer.Summarise(
      trips, Stores, Categories, new DateOnly(2026, 1, 1), new DateOnly(2026, 3, 31));

    Assert.Equal(3, summary.Series.Count);
    Assert.Equal([new DateOnly(2026, 1, 1), new DateOnly(2026, 2, 1), new DateOnly(2026, 3, 1)],
      summary.Series.Select(p => p.PeriodStart));
    Assert.Equal([30m, 20m, 15m], summary.Series.Select(p => p.Amount));
    Assert.Equal(2, summary.Series.Last().TripCount);
  }

  [Fact]
  public void Weekly_series_starts_buckets_on_monday()
  {
    // 2026-03-04 is a Wednesday, 2026-03-08 the Sunday that closes the same week.
    ShoppingTrip[] trips =
    [
      Trip("2026-03-04", 10m),
      Trip("2026-03-08", 15m),
      Trip("2026-03-09", 7m),
    ];

    var summary = SpendAnalyzer.Summarise(
      trips, Stores, Categories, new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 31),
      SpendGranularity.Week);

    Assert.Equal(2, summary.Series.Count);
    Assert.Equal(new DateOnly(2026, 3, 2), summary.Series[0].PeriodStart);
    Assert.Equal(25m, summary.Series[0].Amount);
    Assert.Equal(new DateOnly(2026, 3, 9), summary.Series[1].PeriodStart);
    Assert.Equal(7m, summary.Series[1].Amount);
  }

  [Fact]
  public void Top_items_merge_by_description_ignoring_case()
  {
    ShoppingTrip[] trips =
    [
      Trip("2026-03-02", 30m, AldiId, (5m, ProduceId, "Apples"), (10m, DairyId, "Milk")),
      Trip("2026-03-09", 20m, AldiId, (6m, ProduceId, "apples "), (2m, null, "Bread")),
    ];

    var summary = SpendAnalyzer.Summarise(
      trips, Stores, Categories, new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 31));

    var apples = summary.TopItems.Single(i => string.Equals(i.Description, "Apples", StringComparison.OrdinalIgnoreCase));
    Assert.Equal(11m, apples.TotalAmount);
    Assert.Equal(2, apples.Occurrences);

    // Ordered by spend, so Milk's single 10 outranks Bread's 2.
    Assert.Equal(["Apples", "Milk", "Bread"], summary.TopItems.Select(i => i.Description));
  }

  [Fact]
  public void Reversed_window_is_normalised_rather_than_returning_nothing()
  {
    var summary = SpendAnalyzer.Summarise(
      [Trip("2026-03-14", 42m)], Stores, Categories, new DateOnly(2026, 3, 31), new DateOnly(2026, 3, 1));

    Assert.Equal(42m, summary.TotalSpend);
    Assert.Equal(new DateOnly(2026, 3, 1), summary.From);
    Assert.Equal(new DateOnly(2026, 3, 31), summary.To);
  }

  [Fact]
  public void Year_to_date_counts_this_year_only_and_ignores_the_window()
  {
    ShoppingTrip[] trips =
    [
      Trip("2025-12-30", 500m),
      Trip("2026-01-04", 40m),
      Trip("2026-03-15", 60m),
      Trip("2026-04-02", 1000m),
    ];

    // A March-only window: year to date still reaches back to 1 January, and stops at `to`.
    var summary = SpendAnalyzer.Summarise(
      trips, Stores, Categories, new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 31));

    Assert.Equal(60m, summary.TotalSpend);
    Assert.Equal(100m, summary.YearToDateSpend);
  }

  [Fact]
  public void The_monthly_average_covers_the_twelve_full_months_before_the_running_one()
  {
    ShoppingTrip[] trips =
    [
      Trip("2025-01-10", 999m),   // 13 months before March 2026: outside the window
      Trip("2025-03-10", 100m),
      Trip("2025-09-10", 200m),
      Trip("2026-02-10", 300m),
      Trip("2026-03-05", 777m),   // the running month never counts towards the average
    ];

    var summary = SpendAnalyzer.Summarise(
      trips, Stores, Categories, new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 20));

    Assert.Equal(12, summary.AverageMonthlyMonths);
    Assert.Equal(50m, summary.AverageMonthlySpend);
  }

  [Fact]
  public void A_short_history_is_averaged_over_the_months_it_actually_covers()
  {
    ShoppingTrip[] trips =
    [
      Trip("2026-01-10", 300m),
      Trip("2026-02-10", 100m),
      Trip("2026-03-05", 999m),
    ];

    var summary = SpendAnalyzer.Summarise(
      trips, Stores, Categories, new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 20));

    Assert.Equal(2, summary.AverageMonthlyMonths);
    Assert.Equal(200m, summary.AverageMonthlySpend);
  }

  [Fact]
  public void Nothing_is_averaged_until_one_month_is_complete()
  {
    ShoppingTrip[] trips = [Trip("2026-03-02", 80m)];

    var summary = SpendAnalyzer.Summarise(
      trips, Stores, Categories, new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 20));

    Assert.Equal(0, summary.AverageMonthlyMonths);
    Assert.Equal(0m, summary.AverageMonthlySpend);
    Assert.Equal(80m, summary.YearToDateSpend);
  }

  [Fact]
  public void Year_to_date_and_the_average_survive_an_empty_window()
  {
    ShoppingTrip[] trips =
    [
      Trip("2026-01-10", 120m),
      Trip("2026-02-10", 80m),
    ];

    // Nothing bought in March, but the year and the average are still worth showing.
    var summary = SpendAnalyzer.Summarise(
      trips, Stores, Categories, new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 31));

    Assert.Equal(0m, summary.TotalSpend);
    Assert.Equal(200m, summary.YearToDateSpend);
    Assert.Equal(2, summary.AverageMonthlyMonths);
    Assert.Equal(100m, summary.AverageMonthlySpend);
  }

  [Fact]
  public void A_daily_series_gives_every_shopping_day_its_own_bucket()
  {
    ShoppingTrip[] trips =
    [
      Trip("2026-03-02", 40m),
      Trip("2026-03-02", 10m),
      Trip("2026-03-05", 25m),
    ];

    var summary = SpendAnalyzer.Summarise(
      trips, Stores, Categories, new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 31),
      SpendGranularity.Day);

    Assert.Equal(2, summary.Series.Count);
    Assert.Equal(50m, summary.Series[0].Amount);
    Assert.Equal(2, summary.Series[0].TripCount);
    Assert.Equal("02 Mar", summary.Series[0].Label);
  }
}
