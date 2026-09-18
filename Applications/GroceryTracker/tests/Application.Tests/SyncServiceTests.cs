using GroceryTracker.Contracts.Sync;
using GroceryTracker.Domain.Model;
using Microsoft.EntityFrameworkCore;

namespace GroceryTracker.Application.Tests;

public class SyncServiceTests
{
  [Fact]
  public async Task First_sync_pulls_the_seeded_household_and_categories()
  {
    await using var ctx = await SyncTestContext.CreateAsync();
    var household = await ctx.SeedHouseholdAsync();

    var response = await ctx.SyncAsync(cursor: 0);

    Assert.Equal(household.Id, Assert.Single(response.Payload.Households).Id);
    Assert.NotEmpty(response.Payload.Categories);
    Assert.True(response.Cursor > 0);
  }

  [Fact]
  public async Task Pulling_again_with_the_returned_cursor_yields_nothing()
  {
    await using var ctx = await SyncTestContext.CreateAsync();
    await ctx.SeedHouseholdAsync();

    var first = await ctx.SyncAsync(cursor: 0);
    var second = await ctx.SyncAsync(first.Cursor);

    Assert.Equal(0, second.Payload.Count);
    Assert.Equal(first.Cursor, second.Cursor);
  }

  [Fact]
  public async Task Pushed_store_is_persisted_and_echoed_back_with_a_server_stamp()
  {
    await using var ctx = await SyncTestContext.CreateAsync();
    var household = await ctx.SeedHouseholdAsync();
    var baseline = await ctx.SyncAsync(cursor: 0);

    var store = new Store { Name = "Aldi", HouseholdId = household.Id };
    var operation = ctx.Upsert(store, SyncEntityType.Store);

    var response = await ctx.SyncAsync(baseline.Cursor, operation);

    Assert.Contains(operation.OperationId, response.AppliedOperationIds);
    Assert.Empty(response.Conflicts);

    var echoed = Assert.Single(response.Payload.Stores);
    Assert.Equal("Aldi", echoed.Name);
    Assert.True(echoed.SyncStamp > baseline.Cursor);
    Assert.Equal(echoed.SyncStamp, response.Cursor);
  }

  [Fact]
  public async Task Replaying_the_same_operation_does_not_apply_it_twice()
  {
    await using var ctx = await SyncTestContext.CreateAsync();
    var household = await ctx.SeedHouseholdAsync();
    var baseline = await ctx.SyncAsync(cursor: 0);

    var store = new Store { Name = "Aldi", HouseholdId = household.Id };
    var operation = ctx.Upsert(store, SyncEntityType.Store);

    var first = await ctx.SyncAsync(baseline.Cursor, operation);

    // The device never saw the response, so it sends the very same outbox entry again.
    var renamed = new Store { Id = store.Id, Name = "Renamed by replay", HouseholdId = household.Id };
    var replay = operation with { PayloadJson = System.Text.Json.JsonSerializer.Serialize(renamed, SyncTestContext.Json) };

    var second = await ctx.SyncAsync(baseline.Cursor, replay);

    Assert.Contains(operation.OperationId, second.AppliedOperationIds);
    Assert.Empty(second.Conflicts);

    await using var db = ctx.CreateDbContext();
    var stored = Assert.Single(await db.Stores.ToListAsync());
    Assert.Equal("Aldi", stored.Name);
    Assert.Equal(first.Cursor, second.Cursor);
  }

  [Fact]
  public async Task A_stale_edit_loses_to_the_newer_server_version()
  {
    await using var ctx = await SyncTestContext.CreateAsync();
    var household = await ctx.SeedHouseholdAsync();
    var baseline = await ctx.SyncAsync(cursor: 0);

    var store = new Store { Name = "Aldi", HouseholdId = household.Id };
    var created = await ctx.SyncAsync(baseline.Cursor, ctx.Upsert(store, SyncEntityType.Store));
    var stampAfterCreate = created.Payload.Stores.Single().SyncStamp;

    // Phone A renames the store while online.
    ctx.Clock.Advance(TimeSpan.FromMinutes(10));
    var fromPhoneA = new Store { Id = store.Id, Name = "Aldi Süd", HouseholdId = household.Id };
    await ctx.SyncAsync(created.Cursor, ctx.Upsert(fromPhoneA, SyncEntityType.Store, stampAfterCreate));

    // Phone B was offline and edited the version it still had, five minutes before phone A.
    var fromPhoneB = new Store { Id = store.Id, Name = "Stale name", HouseholdId = household.Id };
    var staleOperation = ctx.Upsert(
      fromPhoneB,
      SyncEntityType.Store,
      baseStamp: stampAfterCreate,
      clientTime: ctx.Clock.GetUtcNow().AddMinutes(-5));

    var response = await ctx.SyncAsync(created.Cursor, staleOperation);

    var conflict = Assert.Single(response.Conflicts);
    Assert.Equal(ConflictOutcome.ServerWon, conflict.Outcome);
    Assert.Equal(store.Id, conflict.EntityId);

    // Reported as applied so the device drops it from the outbox instead of retrying forever.
    Assert.Contains(staleOperation.OperationId, response.AppliedOperationIds);

    await using var db = ctx.CreateDbContext();
    Assert.Equal("Aldi Süd", (await db.Stores.SingleAsync()).Name);
  }

  [Fact]
  public async Task A_newer_offline_edit_overwrites_the_server_and_is_still_reported()
  {
    await using var ctx = await SyncTestContext.CreateAsync();
    var household = await ctx.SeedHouseholdAsync();
    var baseline = await ctx.SyncAsync(cursor: 0);

    var store = new Store { Name = "Aldi", HouseholdId = household.Id };
    var created = await ctx.SyncAsync(baseline.Cursor, ctx.Upsert(store, SyncEntityType.Store));
    var stampAfterCreate = created.Payload.Stores.Single().SyncStamp;

    var serverSide = new Store { Id = store.Id, Name = "Server rename", HouseholdId = household.Id };
    await ctx.SyncAsync(created.Cursor, ctx.Upsert(serverSide, SyncEntityType.Store, stampAfterCreate));

    // The offline device's edit happened later in wall-clock terms, so it wins.
    ctx.Clock.Advance(TimeSpan.FromHours(1));
    var fromPhone = new Store { Id = store.Id, Name = "Newer offline edit", HouseholdId = household.Id };
    var response = await ctx.SyncAsync(
      created.Cursor,
      ctx.Upsert(fromPhone, SyncEntityType.Store, baseStamp: stampAfterCreate));

    var conflict = Assert.Single(response.Conflicts);
    Assert.Equal(ConflictOutcome.ClientWon, conflict.Outcome);

    await using var db = ctx.CreateDbContext();
    Assert.Equal("Newer offline edit", (await db.Stores.SingleAsync()).Name);
  }

  [Fact]
  public async Task Deletes_travel_as_tombstones_so_other_devices_learn_about_them()
  {
    await using var ctx = await SyncTestContext.CreateAsync();
    var household = await ctx.SeedHouseholdAsync();
    var baseline = await ctx.SyncAsync(cursor: 0);

    var store = new Store { Name = "Aldi", HouseholdId = household.Id };
    var created = await ctx.SyncAsync(baseline.Cursor, ctx.Upsert(store, SyncEntityType.Store));
    var stamp = created.Payload.Stores.Single().SyncStamp;

    ctx.Clock.Advance(TimeSpan.FromMinutes(1));
    var deleted = await ctx.SyncAsync(created.Cursor, ctx.Delete(store.Id, SyncEntityType.Store, stamp));

    var tombstone = Assert.Single(deleted.Payload.Stores);
    Assert.True(tombstone.IsDeleted);

    // A second device sitting on the pre-delete cursor is told about it.
    var otherDevice = await ctx.SyncAsync(created.Cursor);
    Assert.True(Assert.Single(otherDevice.Payload.Stores).IsDeleted);
  }

  [Fact]
  public async Task A_trip_and_its_items_can_arrive_in_one_batch_regardless_of_order()
  {
    await using var ctx = await SyncTestContext.CreateAsync();
    var household = await ctx.SeedHouseholdAsync();
    var baseline = await ctx.SyncAsync(cursor: 0);

    var store = new Store { Name = "Aldi", HouseholdId = household.Id };
    var storeSync = await ctx.SyncAsync(baseline.Cursor, ctx.Upsert(store, SyncEntityType.Store));

    var trip = new ShoppingTrip
    {
      StoreId = store.Id,
      HouseholdId = household.Id,
      TotalAmount = 42.50m,
      PurchasedOn = new DateOnly(2026, 3, 4),
    };

    var item = new ExpenseItem
    {
      TripId = trip.Id,
      HouseholdId = household.Id,
      Description = "Apples",
      Amount = 3.20m,
    };

    // Item first: the engine must still order the trip ahead of it.
    var response = await ctx.SyncAsync(
      storeSync.Cursor,
      ctx.Upsert(item, SyncEntityType.ExpenseItem),
      ctx.Upsert(trip, SyncEntityType.ShoppingTrip));

    Assert.Empty(response.Conflicts);
    Assert.Single(response.Payload.Trips);
    Assert.Single(response.Payload.Items);

    await using var db = ctx.CreateDbContext();
    var stored = await db.Trips.Include(t => t.Items).SingleAsync();
    Assert.Equal(42.50m, stored.TotalAmount);
    Assert.Equal("Apples", stored.Items.Single().Description);
  }

  [Fact]
  public async Task An_operation_referencing_a_missing_store_is_rejected_instead_of_failing_the_batch()
  {
    await using var ctx = await SyncTestContext.CreateAsync();
    var household = await ctx.SeedHouseholdAsync();
    var baseline = await ctx.SyncAsync(cursor: 0);

    var orphan = new ShoppingTrip
    {
      StoreId = Guid.NewGuid(),
      HouseholdId = household.Id,
      TotalAmount = 10m,
    };

    var goodStore = new Store { Name = "Rewe", HouseholdId = household.Id };

    var rejected = ctx.Upsert(orphan, SyncEntityType.ShoppingTrip);
    var accepted = ctx.Upsert(goodStore, SyncEntityType.Store);

    var response = await ctx.SyncAsync(baseline.Cursor, rejected, accepted);

    var conflict = Assert.Single(response.Conflicts);
    Assert.Equal(ConflictOutcome.Rejected, conflict.Outcome);
    Assert.Equal(rejected.OperationId, conflict.OperationId);

    // The healthy operation in the same batch still went through.
    Assert.Single(response.Payload.Stores);
    Assert.Empty(response.Payload.Trips);
    Assert.Contains(accepted.OperationId, response.AppliedOperationIds);
  }

  [Fact]
  public async Task A_malformed_payload_is_rejected_rather_than_throwing()
  {
    await using var ctx = await SyncTestContext.CreateAsync();
    await ctx.SeedHouseholdAsync();
    var baseline = await ctx.SyncAsync(cursor: 0);

    var broken = new SyncOperation(
      Guid.NewGuid(),
      SyncEntityType.Store,
      Guid.NewGuid(),
      SyncOperationKind.Upsert,
      BaseSyncStamp: 0,
      ctx.Clock.GetUtcNow(),
      PayloadJson: "{ this is not json ");

    var response = await ctx.SyncAsync(baseline.Cursor, broken);

    Assert.Equal(ConflictOutcome.Rejected, Assert.Single(response.Conflicts).Outcome);
    Assert.Contains(broken.OperationId, response.AppliedOperationIds);
  }

  [Fact]
  public async Task Two_devices_converge_on_the_same_state()
  {
    await using var ctx = await SyncTestContext.CreateAsync();
    var household = await ctx.SeedHouseholdAsync();

    var phoneCursor = (await ctx.SyncAsync(0)).Cursor;
    var laptopCursor = (await ctx.SyncAsync(0)).Cursor;

    var store = new Store { Name = "Aldi", HouseholdId = household.Id };
    phoneCursor = (await ctx.SyncAsync(phoneCursor, ctx.Upsert(store, SyncEntityType.Store))).Cursor;

    var laptopTrip = new ShoppingTrip
    {
      StoreId = store.Id,
      HouseholdId = household.Id,
      TotalAmount = 18m,
      PurchasedOn = new DateOnly(2026, 3, 5),
    };

    // The laptop has to learn about the store before it can reference it.
    var laptopPull = await ctx.SyncAsync(laptopCursor);
    Assert.Contains(laptopPull.Payload.Stores, s => s.Id == store.Id);

    var laptopPush = await ctx.SyncAsync(laptopPull.Cursor, ctx.Upsert(laptopTrip, SyncEntityType.ShoppingTrip));

    var phoneCatchUp = await ctx.SyncAsync(phoneCursor);
    Assert.Contains(phoneCatchUp.Payload.Trips, t => t.Id == laptopTrip.Id);
    Assert.Equal(laptopPush.Cursor, phoneCatchUp.Cursor);
  }

  [Fact]
  public async Task Stamps_increase_monotonically_across_writes()
  {
    await using var ctx = await SyncTestContext.CreateAsync();
    var household = await ctx.SeedHouseholdAsync();

    var cursor = (await ctx.SyncAsync(0)).Cursor;
    var stamps = new List<long>();

    for (var i = 0; i < 5; i++)
    {
      var store = new Store { Name = $"Store {i}", HouseholdId = household.Id };
      var response = await ctx.SyncAsync(cursor, ctx.Upsert(store, SyncEntityType.Store));
      stamps.Add(response.Cursor);
      cursor = response.Cursor;
    }

    Assert.Equal(stamps.OrderBy(s => s), stamps);
    Assert.Equal(stamps.Distinct().Count(), stamps.Count);
  }
}
