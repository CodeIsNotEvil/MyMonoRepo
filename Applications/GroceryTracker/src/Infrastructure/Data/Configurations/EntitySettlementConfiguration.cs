using CINE.GroceryTracker.Domain.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CINE.GroceryTracker.Infrastructure.Data.Configurations;

public class EntitySettlementConfiguration : IEntityTypeConfiguration<Settlement>
{
  public void Configure(EntityTypeBuilder<Settlement> builder)
  {
    builder.ToTable("Settlements");

    builder.HasKey(s => s.Id);

    builder.Property(s => s.Id)
      .ValueGeneratedNever()
      .IsRequired();

    builder.Property(s => s.Amount)
      .HasPrecision(18, 2)
      .IsRequired();

    builder.Property(s => s.Date).IsRequired();

    builder.Property(s => s.Note)
      .HasMaxLength(500);

    builder.Property(s => s.FromMemberId).IsRequired();
    builder.Property(s => s.ToMemberId).IsRequired();
    builder.Property(s => s.HouseholdId).IsRequired();
    builder.Property(s => s.SyncStamp).IsRequired();
    builder.Property(s => s.UpdatedAtUtc).IsRequired();
    builder.Property(s => s.IsDeleted).IsRequired();

    builder.HasOne(s => s.Household)
      .WithMany(h => h.Settlements)
      .HasForeignKey(s => s.HouseholdId)
      .OnDelete(DeleteBehavior.Cascade)
      .IsRequired();

    // Members are retired by tombstone, never removed, and two cascades onto the same table from one
    // parent would be ambiguous anyway, so both member links restrict.
    builder.HasOne(s => s.FromMember)
      .WithMany()
      .HasForeignKey(s => s.FromMemberId)
      .OnDelete(DeleteBehavior.Restrict)
      .IsRequired();

    builder.HasOne(s => s.ToMember)
      .WithMany()
      .HasForeignKey(s => s.ToMemberId)
      .OnDelete(DeleteBehavior.Restrict)
      .IsRequired();

    builder.HasIndex(s => s.SyncStamp);
    builder.HasIndex(s => s.HouseholdId);
    builder.HasIndex(s => s.FromMemberId);
    builder.HasIndex(s => s.ToMemberId);
  }
}
