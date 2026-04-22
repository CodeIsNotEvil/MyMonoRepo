using Microsoft.EntityFrameworkCore;
using ShoppingManager.Domain.Model;
using ShoppingManager.Infrastructure.Data.Configurations;

namespace ShoppingManager.Infrastructure.Data;

public class ShoppingManagerDbContext : DbContext
{
    public DbSet<User> Users { get; set; } = null!;
    public DbSet<Payment> Payments { get; set; } = null!;
    public DbSet<PaymentGroup> PaymentGroups { get; set; } = null!;

    public ShoppingManagerDbContext(DbContextOptions<ShoppingManagerDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Apply entity configurations
        modelBuilder.ApplyConfiguration(new EntityUserConfiguration());
        modelBuilder.ApplyConfiguration(new EntityPaymentConfiguration());
        modelBuilder.ApplyConfiguration(new EntityPaymentGroupConfiguration());
    }
}
