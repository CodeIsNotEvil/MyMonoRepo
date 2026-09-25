using CINE.GroceryTracker.Domain.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CINE.GroceryTracker.Infrastructure.Data.Configurations;

public class EntityShoppingTripConfiguration : IEntityTypeConfiguration<ShoppingTrip>
{
  public void Configure(EntityTypeBuilder<ShoppingTrip> builder)
  {
    builder.ToTable("Trips");

    builder.HasKey(t => t.Id);

    builder.Property(t => t.Id)
      .ValueGeneratedNever()
      .IsRequired();

    builder.Property(t => t.PurchasedOn).IsRequired();

    builder.Property(t => t.TotalAmount)
      .HasPrecision(18, 2)
      .IsRequired();

    builder.Property(t => t.Note)
      .HasMaxLength(500);

    builder.Property(t => t.StoreId).IsRequired();
    builder.Property(t => t.HouseholdId).IsRequired();
    builder.Property(t => t.SyncStamp).IsRequired();
    builder.Property(t => t.UpdatedAtUtc).IsRequired();
    builder.Property(t => t.IsDeleted).IsRequired();

    builder.HasOne(t => t.Household)
      .WithMany(h => h.Trips)
      .HasForeignKey(t => t.HouseholdId)
      .OnDelete(DeleteBehavior.Cascade)
      .IsRequired();

    // Stores are retired by tombstone, never removed, so a historic trip keeps pointing at one.
    builder.HasOne(t => t.Store)
      .WithMany(s => s.Trips)
      .HasForeignKey(t => t.StoreId)
      .OnDelete(DeleteBehavior.Restrict)
      .IsRequired();

    builder.HasOne(t => t.PaidByMember)
      .WithMany(m => m.Trips)
      .HasForeignKey(t => t.PaidByMemberId)
      .OnDelete(DeleteBehavior.SetNull);

    builder.HasIndex(t => t.SyncStamp);
    builder.HasIndex(t => t.HouseholdId);
    builder.HasIndex(t => t.PurchasedOn);
  }
}
