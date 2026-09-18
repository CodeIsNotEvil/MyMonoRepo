using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GroceryTracker.Infrastructure.Data.Configurations;

public class EntitySyncCounterConfiguration : IEntityTypeConfiguration<SyncCounter>
{
  public void Configure(EntityTypeBuilder<SyncCounter> builder)
  {
    builder.ToTable("SyncCounters");

    builder.HasKey(c => c.Id);

    builder.Property(c => c.Id)
      .ValueGeneratedNever()
      .IsRequired();

    builder.Property(c => c.LastStamp).IsRequired();

    // Seeded rather than created on demand, so the very first write already has a row to lock.
    builder.HasData(new SyncCounter { Id = SyncCounter.SingletonId, LastStamp = 0 });
  }
}

public class EntityAppliedSyncOperationConfiguration : IEntityTypeConfiguration<AppliedSyncOperation>
{
  public void Configure(EntityTypeBuilder<AppliedSyncOperation> builder)
  {
    builder.ToTable("AppliedSyncOperations");

    builder.HasKey(o => o.OperationId);

    builder.Property(o => o.OperationId)
      .ValueGeneratedNever()
      .IsRequired();

    builder.Property(o => o.EntityId).IsRequired();

    builder.Property(o => o.EntityType)
      .HasMaxLength(50)
      .IsRequired();

    builder.Property(o => o.AppliedAtUtc).IsRequired();

    builder.HasIndex(o => o.AppliedAtUtc);
  }
}
