using GroceryTracker.Domain.Model;
using GroceryTracker.Domain.Sync;
using Microsoft.EntityFrameworkCore;

namespace GroceryTracker.Infrastructure.Data;

public class GroceryTrackerDbContext : DbContext
{
  private readonly TimeProvider _timeProvider;

  public GroceryTrackerDbContext(DbContextOptions<GroceryTrackerDbContext> options)
    : this(options, TimeProvider.System)
  {
  }

  public GroceryTrackerDbContext(DbContextOptions<GroceryTrackerDbContext> options, TimeProvider timeProvider)
    : base(options)
  {
    _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
  }

  public DbSet<Household> Households { get; set; } = null!;
  public DbSet<Member> Members { get; set; } = null!;
  public DbSet<Store> Stores { get; set; } = null!;
  public DbSet<Category> Categories { get; set; } = null!;
  public DbSet<ShoppingTrip> Trips { get; set; } = null!;
  public DbSet<ExpenseItem> Items { get; set; } = null!;
  public DbSet<Settlement> Settlements { get; set; } = null!;
  public DbSet<SyncCounter> SyncCounters { get; set; } = null!;
  public DbSet<AppliedSyncOperation> AppliedSyncOperations { get; set; } = null!;

  protected override void OnModelCreating(ModelBuilder modelBuilder)
  {
    base.OnModelCreating(modelBuilder);
    modelBuilder.ApplyConfigurationsFromAssembly(typeof(GroceryTrackerDbContext).Assembly);
  }

  public override int SaveChanges()
  {
    // Routed through the async path so stamp allocation has exactly one implementation.
    return SaveChangesAsync().GetAwaiter().GetResult();
  }

  public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
  {
    var pending = ChangeTracker
      .Entries<ISyncEntity>()
      .Where(entry => entry.State is EntityState.Added or EntityState.Modified)
      .Select(entry => entry.Entity)
      .ToList();

    if (pending.Count == 0)
    {
      return await base.SaveChangesAsync(cancellationToken);
    }

    // The counter bump and the entity writes have to land in one transaction, otherwise a client
    // could pull a stamp whose rows are not committed yet and then never ask for them again.
    var ownsTransaction = Database.CurrentTransaction is null;
    var transaction = Database.CurrentTransaction
      ?? await Database.BeginTransactionAsync(cancellationToken);

    try
    {
      var stamp = await AllocateSyncStampAsync(cancellationToken);
      var now = _timeProvider.GetUtcNow();

      foreach (var entity in pending)
      {
        entity.SyncStamp = stamp;
        entity.UpdatedAtUtc = now;
      }

      var written = await base.SaveChangesAsync(cancellationToken);

      if (ownsTransaction)
      {
        await transaction.CommitAsync(cancellationToken);
      }

      return written;
    }
    catch
    {
      if (ownsTransaction)
      {
        await transaction.RollbackAsync(cancellationToken);
      }

      throw;
    }
    finally
    {
      if (ownsTransaction)
      {
        await transaction.DisposeAsync();
      }
    }
  }

  /// <summary>
  /// Claims the next stamp. The UPDATE takes the row lock and the follow-up SELECT reads this
  /// transaction's own uncommitted value, so concurrent writers queue up instead of colliding.
  /// </summary>
  private async Task<long> AllocateSyncStampAsync(CancellationToken cancellationToken)
  {
    var updated = await Database.ExecuteSqlRawAsync(
      """UPDATE "SyncCounters" SET "LastStamp" = "LastStamp" + 1 WHERE "Id" = 1""",
      cancellationToken);

    if (updated == 0)
    {
      throw new InvalidOperationException(
        "The sync counter row is missing. The database was not migrated or seeded correctly.");
    }

    return await Database
      .SqlQueryRaw<long>("""SELECT "LastStamp" AS "Value" FROM "SyncCounters" WHERE "Id" = 1""")
      .SingleAsync(cancellationToken);
  }
}
