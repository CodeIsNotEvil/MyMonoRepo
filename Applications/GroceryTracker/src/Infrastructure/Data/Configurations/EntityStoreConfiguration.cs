using CINE.GroceryTracker.Domain.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CINE.GroceryTracker.Infrastructure.Data.Configurations;

public class EntityStoreConfiguration : IEntityTypeConfiguration<Store>
{
  public void Configure(EntityTypeBuilder<Store> builder)
  {
    builder.ToTable("Stores");

    builder.HasKey(s => s.Id);

    builder.Property(s => s.Id)
      .ValueGeneratedNever()
      .IsRequired();

    builder.Property(s => s.Name)
      .HasMaxLength(120)
      .IsRequired();

    builder.Property(s => s.HouseholdId).IsRequired();
    builder.Property(s => s.SyncStamp).IsRequired();
    builder.Property(s => s.UpdatedAtUtc).IsRequired();
    builder.Property(s => s.IsDeleted).IsRequired();

    builder.HasOne(s => s.Household)
      .WithMany(h => h.Stores)
      .HasForeignKey(s => s.HouseholdId)
      .OnDelete(DeleteBehavior.Cascade)
      .IsRequired();

    builder.HasIndex(s => s.SyncStamp);
    builder.HasIndex(s => s.HouseholdId);
  }
}
