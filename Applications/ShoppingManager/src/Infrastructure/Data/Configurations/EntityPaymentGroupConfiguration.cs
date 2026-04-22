using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShoppingManager.Domain.Model;

namespace ShoppingManager.Infrastructure.Data.Configurations;

public class EntityPaymentGroupConfiguration : IEntityTypeConfiguration<PaymentGroup>
{
    public void Configure(EntityTypeBuilder<PaymentGroup> builder)
    {
        builder.HasKey(pg => pg.Id);

        builder.Property(pg => pg.Id)
            .ValueGeneratedNever()
            .IsRequired();

        builder.Property(pg => pg.Name)
            .IsRequired()
            .HasMaxLength(256);

        // Many-to-many: PaymentGroup has many Users
        builder.HasMany(pg => pg.Users)
            .WithMany()
            .UsingEntity(j => j.ToTable("PaymentGroupUsers"));

        // One-to-many: PaymentGroup has many Payments
        builder.HasMany(pg => pg.Payments)
            .WithOne()
            .OnDelete(DeleteBehavior.Cascade);

        builder.ToTable("PaymentGroups");
    }
}
