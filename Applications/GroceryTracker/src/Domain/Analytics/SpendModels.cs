namespace GroceryTracker.Domain.Analytics;

public enum SpendGranularity
{
  Day,
  Week,
  Month,
}

/// <summary>One bucket of the spend-over-time series.</summary>
public sealed record SpendPoint(
  DateOnly PeriodStart,
  string Label,
  decimal Amount,
  int TripCount);

/// <summary>
/// Spend attributed to a category. <see cref="CategoryId"/> is null for the uncategorised bucket,
/// which collects both items without a category and the part of a receipt no item accounts for.
/// </summary>
public sealed record CategorySlice(
  Guid? CategoryId,
  string Name,
  string ColorHex,
  decimal Amount,
  decimal Share);

public sealed record StoreSlice(
  Guid StoreId,
  string Name,
  decimal Amount,
  int TripCount,
  decimal Share);

public sealed record TopItem(
  string Description,
  decimal TotalAmount,
  int Occurrences);

/// <summary>
/// The full analytics payload. Computed by <see cref="SpendAnalyzer"/> on the server from
/// PostgreSQL and on the client from the IndexedDB cache, so the numbers match while offline.
/// </summary>
/// <param name="YearToDateSpend">
/// Spend since 1 January of the year <see cref="To"/> falls in. Independent of the selected
/// window, so switching range does not change it.
/// </param>
/// <param name="AverageMonthlySpend">
/// Typical spend per month over the twelve full months before <see cref="To"/>'s month. The
/// running month is left out: two days into it, its total would drag the average down.
/// </param>
/// <param name="AverageMonthlyMonths">
/// How many months that average actually covers — fewer than twelve when the history is shorter.
/// </param>
public sealed record SpendSummary(
  DateOnly From,
  DateOnly To,
  string CurrencyCode,
  decimal TotalSpend,
  int TripCount,
  decimal AverageTripAmount,
  decimal PreviousPeriodSpend,
  decimal? ChangeVsPreviousPct,
  decimal YearToDateSpend,
  decimal AverageMonthlySpend,
  int AverageMonthlyMonths,
  IReadOnlyList<SpendPoint> Series,
  IReadOnlyList<CategorySlice> ByCategory,
  IReadOnlyList<StoreSlice> ByStore,
  IReadOnlyList<TopItem> TopItems)
{
  public static SpendSummary Empty(DateOnly from, DateOnly to, string currencyCode) => new(
    from,
    to,
    currencyCode,
    TotalSpend: 0m,
    TripCount: 0,
    AverageTripAmount: 0m,
    PreviousPeriodSpend: 0m,
    ChangeVsPreviousPct: null,
    YearToDateSpend: 0m,
    AverageMonthlySpend: 0m,
    AverageMonthlyMonths: 0,
    Series: [],
    ByCategory: [],
    ByStore: [],
    TopItems: []);
}
