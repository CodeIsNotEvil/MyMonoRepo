using CINE.GroceryTracker.Domain.Model;
using CINE.GroceryTracker.Infrastructure.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace CINE.GroceryTracker.Infrastructure.Tests;

/// <summary>
/// Covers the stamp allocation in <see cref="GroceryTrackerDbContext.SaveChangesAsync"/>, which the
/// whole delta-sync protocol rests on: if stamps were ever reused or handed out non-monotonically,
/// a client would silently skip changes.
/// </summary>
public class SyncStampTests : IAsyncLifetime
{
  private SqliteConnection _connection = null!;
  private StubClock _clock = null!;

  public async Task InitializeAsync()
  {
    _connection = new SqliteConnection("DataSource=:memory:");
    await _connection.OpenAsync();
    _clock = new StubClock(new DateTimeOffset(2026, 3, 1, 9, 0, 0, TimeSpan.Zero));

    await using var db = CreateContext();
    await db.Database.EnsureCreatedAsync();
  }

  public async Task DisposeAsync() => await _connection.DisposeAsync();

  private GroceryTrackerDbContext CreateContext() => new(
    new DbContextOptionsBuilder<GroceryTrackerDbContext>().UseSqlite(_connection).Options,
    _clock);

  private static Household NewHousehold() => new() { Name = "Home" };

  [Fact]
  public async Task Saving_assigns_a_stamp_and_the_current_time()
  {
    await using var db = CreateContext();
    var household = NewHousehold();
    db.Households.Add(household);

    await db.SaveChangesAsync();

    Assert.True(household.SyncStamp > 0);
    Assert.Equal(_clock.GetUtcNow(), household.UpdatedAtUtc);
  }

  [Fact]
  public async Task Everything_saved_together_shares_one_stamp()
  {
    await using var db = CreateContext();
    var household = NewHousehold();
    var store = new Store { Name = "Aldi", HouseholdId = household.Id };

    db.Households.Add(household);
    db.Stores.Add(store);
    await db.SaveChangesAsync();

    // One SaveChanges is one atomic change from a puller's point of view, so a single stamp is
    // correct and means a client can never see half of a batch.
    Assert.Equal(household.SyncStamp, store.SyncStamp);
  }

  [Fact]
  public async Task Consecutive_saves_get_strictly_increasing_stamps()
  {
    var stamps = new List<long>();

    await using (var db = CreateContext())
    {
      var household = NewHousehold();
      db.Households.Add(household);
      await db.SaveChangesAsync();
      stamps.Add(household.SyncStamp);

      for (var i = 0; i < 4; i++)
      {
        db.Stores.Add(new Store { Name = $"Store {i}", HouseholdId = household.Id });
        await db.SaveChangesAsync();
        stamps.Add(db.Stores.Local.Max(s => s.SyncStamp));
      }
    }

    Assert.Equal(stamps.OrderBy(s => s), stamps);
    Assert.Equal(stamps.Count, stamps.Distinct().Count());
  }

  [Fact]
  public async Task Updating_a_row_moves_its_stamp_forward()
  {
    await using var db = CreateContext();
    var household = NewHousehold();
    db.Households.Add(household);
    await db.SaveChangesAsync();

    var original = household.SyncStamp;

    _clock.Advance(TimeSpan.FromMinutes(5));
    household.Name = "Renamed";
    await db.SaveChangesAsync();

    Assert.True(household.SyncStamp > original);
    Assert.Equal(_clock.GetUtcNow(), household.UpdatedAtUtc);
  }

  [Fact]
  public async Task A_save_with_nothing_syncable_does_not_burn_a_stamp()
  {
    await using var db = CreateContext();
    db.Households.Add(NewHousehold());
    await db.SaveChangesAsync();

    var before = await db.SyncCounters.AsNoTracking().SingleAsync();

    db.AppliedSyncOperations.Add(new AppliedSyncOperation
    {
      OperationId = Guid.NewGuid(),
      EntityId = Guid.NewGuid(),
      EntityType = "Store",
      AppliedAtUtc = _clock.GetUtcNow(),
    });
    await db.SaveChangesAsync();

    var after = await db.SyncCounters.AsNoTracking().SingleAsync();
    Assert.Equal(before.LastStamp, after.LastStamp);
  }

  [Fact]
  public async Task Tombstoned_rows_stay_in_the_table_so_the_delete_can_be_pulled()
  {
    await using var db = CreateContext();
    var household = NewHousehold();
    var store = new Store { Name = "Aldi", HouseholdId = household.Id };
    db.Households.Add(household);
    db.Stores.Add(store);
    await db.SaveChangesAsync();

    _clock.Advance(TimeSpan.FromMinutes(1));
    store.IsDeleted = true;
    await db.SaveChangesAsync();

    var stored = await db.Stores.AsNoTracking().SingleAsync(s => s.Id == store.Id);
    Assert.True(stored.IsDeleted);
  }

  private sealed class StubClock(DateTimeOffset now) : TimeProvider
  {
    private DateTimeOffset _now = now;

    public override DateTimeOffset GetUtcNow() => _now;

    public void Advance(TimeSpan by) => _now = _now.Add(by);
  }
}
