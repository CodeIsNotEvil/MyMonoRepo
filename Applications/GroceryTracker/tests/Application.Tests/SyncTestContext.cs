using System.Text.Json;
using GroceryTracker.Application.Households;
using GroceryTracker.Application.Sync;
using GroceryTracker.Contracts.Sync;
using GroceryTracker.Domain.Sync;
using GroceryTracker.Infrastructure.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace GroceryTracker.Application.Tests;

/// <summary>Test clock so conflict tie-breaking can be driven deliberately.</summary>
public sealed class StubTimeProvider(DateTimeOffset now) : TimeProvider
{
  public DateTimeOffset Now { get; set; } = now;

  public override DateTimeOffset GetUtcNow() => Now;

  public void Advance(TimeSpan by) => Now = Now.Add(by);
}

/// <summary>
/// A real relational database for sync tests.
/// </summary>
/// <remarks>
/// SQLite rather than the in-memory provider on purpose: the sync engine leans on transactions and
/// on raw SQL for stamp allocation, neither of which the in-memory provider models faithfully.
/// </remarks>
public sealed class SyncTestContext : IAsyncDisposable
{
  public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

  private readonly SqliteConnection _connection;

  private SyncTestContext(SqliteConnection connection, StubTimeProvider clock)
  {
    _connection = connection;
    Clock = clock;
  }

  public StubTimeProvider Clock { get; }

  public static async Task<SyncTestContext> CreateAsync()
  {
    var connection = new SqliteConnection("DataSource=:memory:");
    await connection.OpenAsync();

    var clock = new StubTimeProvider(new DateTimeOffset(2026, 3, 1, 12, 0, 0, TimeSpan.Zero));
    var context = new SyncTestContext(connection, clock);

    await using var db = context.CreateDbContext();
    await db.Database.EnsureCreatedAsync();

    return context;
  }

  public GroceryTrackerDbContext CreateDbContext()
  {
    var options = new DbContextOptionsBuilder<GroceryTrackerDbContext>()
      .UseSqlite(_connection)
      .Options;

    return new GroceryTrackerDbContext(options, Clock);
  }

  public SyncService CreateSyncService(GroceryTrackerDbContext db) =>
    new(db, Clock, NullLogger<SyncService>.Instance);

  /// <summary>Runs one full sync round trip against a fresh scope, the way a request would.</summary>
  public async Task<SyncResponse> SyncAsync(long cursor, params SyncOperation[] operations)
  {
    await using var db = CreateDbContext();
    var service = CreateSyncService(db);
    return await service.SynchroniseAsync(new SyncRequest(cursor, operations));
  }

  public async Task<Domain.Model.Household> SeedHouseholdAsync()
  {
    await using var db = CreateDbContext();
    var provisioner = new HouseholdProvisioner(db);
    return await provisioner.EnsureDefaultHouseholdAsync();
  }

  public SyncOperation Upsert<TEntity>(
    TEntity entity,
    SyncEntityType type,
    long baseStamp = 0,
    DateTimeOffset? clientTime = null,
    Guid? operationId = null)
    where TEntity : ISyncEntity => new(
      operationId ?? Guid.NewGuid(),
      type,
      entity.Id,
      SyncOperationKind.Upsert,
      baseStamp,
      clientTime ?? Clock.GetUtcNow(),
      JsonSerializer.Serialize(entity, Json));

  public SyncOperation Delete(
    Guid entityId,
    SyncEntityType type,
    long baseStamp,
    DateTimeOffset? clientTime = null,
    Guid? operationId = null) => new(
      operationId ?? Guid.NewGuid(),
      type,
      entityId,
      SyncOperationKind.Delete,
      baseStamp,
      clientTime ?? Clock.GetUtcNow(),
      null);

  public async ValueTask DisposeAsync() => await _connection.DisposeAsync();
}
