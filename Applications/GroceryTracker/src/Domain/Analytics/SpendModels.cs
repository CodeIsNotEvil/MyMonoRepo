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
public sealed record SpendSummary(
  DateOnly From,
  DateOnly To,
  string CurrencyCode,
  decimal TotalSpend,
  int TripCount,
  decimal AverageTripAmount,
  decimal PreviousPeriodSpend,
  decimal? ChangeVsPreviousPct,
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
    Series: [],
    ByCategory: [],
    ByStore: [],
    TopItems: []);
}
