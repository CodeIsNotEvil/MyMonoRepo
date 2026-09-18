using GroceryTracker.Domain.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GroceryTracker.Infrastructure.Data.Configurations;

public class EntityCategoryConfiguration : IEntityTypeConfiguration<Category>
{
  public void Configure(EntityTypeBuilder<Category> builder)
  {
    builder.ToTable("Categories");

    builder.HasKey(c => c.Id);

    builder.Property(c => c.Id)
      .ValueGeneratedNever()
      .IsRequired();

    builder.Property(c => c.Name)
      .HasMaxLength(120)
      .IsRequired();

    builder.Property(c => c.ColorHex)
      .HasMaxLength(9)
      .IsRequired();

    builder.Property(c => c.HouseholdId).IsRequired();
    builder.Property(c => c.SyncStamp).IsRequired();
    builder.Property(c => c.UpdatedAtUtc).IsRequired();
    builder.Property(c => c.IsDeleted).IsRequired();

    builder.HasOne(c => c.Household)
      .WithMany(h => h.Categories)
      .HasForeignKey(c => c.HouseholdId)
      .OnDelete(DeleteBehavior.Cascade)
      .IsRequired();

    builder.HasIndex(c => c.SyncStamp);
    builder.HasIndex(c => c.HouseholdId);
  }
}
