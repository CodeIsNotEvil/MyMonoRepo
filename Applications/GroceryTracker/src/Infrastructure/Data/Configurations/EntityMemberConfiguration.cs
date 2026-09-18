using GroceryTracker.Domain.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GroceryTracker.Infrastructure.Data.Configurations;

public class EntityMemberConfiguration : IEntityTypeConfiguration<Member>
{
  public void Configure(EntityTypeBuilder<Member> builder)
  {
    builder.ToTable("Members");

    builder.HasKey(m => m.Id);

    builder.Property(m => m.Id)
      .ValueGeneratedNever()
      .IsRequired();

    builder.Property(m => m.DisplayName)
      .HasMaxLength(120)
      .IsRequired();

    builder.Property(m => m.HouseholdId).IsRequired();
    builder.Property(m => m.SyncStamp).IsRequired();
    builder.Property(m => m.UpdatedAtUtc).IsRequired();
    builder.Property(m => m.IsDeleted).IsRequired();

    builder.HasOne(m => m.Household)
      .WithMany(h => h.Members)
      .HasForeignKey(m => m.HouseholdId)
      .OnDelete(DeleteBehavior.Cascade)
      .IsRequired();

    builder.HasIndex(m => m.SyncStamp);
    builder.HasIndex(m => m.HouseholdId);
  }
}
