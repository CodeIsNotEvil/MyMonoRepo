using GroceryTracker.Domain.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GroceryTracker.Infrastructure.Data.Configurations;

public class EntityExpenseItemConfiguration : IEntityTypeConfiguration<ExpenseItem>
{
  public void Configure(EntityTypeBuilder<ExpenseItem> builder)
  {
    builder.ToTable("Items");

    builder.HasKey(i => i.Id);

    builder.Property(i => i.Id)
      .ValueGeneratedNever()
      .IsRequired();

    builder.Property(i => i.Description)
      .HasMaxLength(200)
      .IsRequired();

    builder.Property(i => i.Amount)
      .HasPrecision(18, 2)
      .IsRequired();

    builder.Property(i => i.Quantity)
      .HasPrecision(18, 3)
      .IsRequired();

    builder.Property(i => i.Unit)
      .HasMaxLength(20);

    builder.Property(i => i.TripId).IsRequired();

    // Denormalised tenant column. No foreign key of its own: an item is already reachable from
    // Household through its trip, and a second cascade path would buy nothing.
    builder.Property(i => i.HouseholdId).IsRequired();

    builder.Property(i => i.SyncStamp).IsRequired();
    builder.Property(i => i.UpdatedAtUtc).IsRequired();
    builder.Property(i => i.IsDeleted).IsRequired();

    builder.HasOne(i => i.Trip)
      .WithMany(t => t.Items)
      .HasForeignKey(i => i.TripId)
      .OnDelete(DeleteBehavior.Cascade)
      .IsRequired();

    builder.HasOne(i => i.Category)
      .WithMany(c => c.Items)
      .HasForeignKey(i => i.CategoryId)
      .OnDelete(DeleteBehavior.SetNull);

    builder.HasIndex(i => i.SyncStamp);
    builder.HasIndex(i => i.HouseholdId);
    builder.HasIndex(i => i.TripId);
  }
}
