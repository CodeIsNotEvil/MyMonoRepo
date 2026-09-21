using GroceryTracker.Domain.Analytics;
using GroceryTracker.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace GroceryTracker.Application.Analytics;

public interface IAnalyticsService
{
  Task<SpendSummary> SummariseAsync(
    DateOnly from,
    DateOnly to,
    SpendGranularity granularity,
    CancellationToken cancellationToken = default);
}

/// <summary>
/// Loads the rows a summary needs and hands them to <see cref="SpendAnalyzer"/>.
/// </summary>
/// <remarks>
/// The arithmetic deliberately lives in the shared analyzer rather than in SQL, so the browser can
/// produce byte-identical numbers from its IndexedDB cache while the Pi is unreachable.
/// </remarks>
public sealed class AnalyticsService : IAnalyticsService
{
  private readonly GroceryTrackerDbContext _db;

  public AnalyticsService(GroceryTrackerDbContext db)
  {
    _db = db ?? throw new ArgumentNullException(nameof(db));
  }

  public async Task<SpendSummary> SummariseAsync(
    DateOnly from,
    DateOnly to,
    SpendGranularity granularity,
    CancellationToken cancellationToken = default)
  {
    var household = await _db.Households
      .AsNoTracking()
      .Where(h => !h.IsDeleted)
      .OrderBy(h => h.SyncStamp)
      .FirstOrDefaultAsync(cancellationToken);

    if (household is null)
    {
      return SpendSummary.Empty(from, to, "EUR");
    }

    // The previous-period comparison reaches back before `from`, so the analyzer is given the full
    // history and does its own windowing.
    var trips = await _db.Trips
      .AsNoTracking()
      .Include(t => t.Items)
      .Where(t => !t.IsDeleted && t.HouseholdId == household.Id)
      .ToListAsync(cancellationToken);

    var stores = await _db.Stores
      .AsNoTracking()
      .Where(s => s.HouseholdId == household.Id)
      .ToListAsync(cancellationToken);

    var categories = await _db.Categories
      .AsNoTracking()
      .Where(c => c.HouseholdId == household.Id)
      .ToListAsync(cancellationToken);

    return SpendAnalyzer.Summarise(
      trips,
      stores,
      categories,
      from,
      to,
      granularity,
      household.CurrencyCode);
  }
}
