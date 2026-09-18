using GroceryTracker.Domain.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GroceryTracker.Infrastructure.Data.Configurations;

public class EntityHouseholdConfiguration : IEntityTypeConfiguration<Household>
{
  public void Configure(EntityTypeBuilder<Household> builder)
  {
    builder.ToTable("Households");

    builder.HasKey(h => h.Id);

    builder.Property(h => h.Id)
      .ValueGeneratedNever()
      .IsRequired();

    builder.Property(h => h.Name)
      .HasMaxLength(120)
      .IsRequired();

    builder.Property(h => h.CurrencyCode)
      .HasMaxLength(3)
      .IsRequired();

    builder.Ignore(h => h.HouseholdId);

    builder.Property(h => h.SyncStamp).IsRequired();
    builder.Property(h => h.UpdatedAtUtc).IsRequired();
    builder.Property(h => h.IsDeleted).IsRequired();

    builder.HasIndex(h => h.SyncStamp);
  }
}
