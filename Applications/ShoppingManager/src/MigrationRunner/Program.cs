using Microsoft.EntityFrameworkCore;
using ShoppingManager.Infrastructure.Data;

string GetEnv(string key, string defaultValue) => Environment.GetEnvironmentVariable(key) ?? defaultValue;

var host = GetEnv("POSTGRES_HOST", "postgres");
var port = GetEnv("POSTGRES_PORT", "5432");
var database = GetEnv("POSTGRES_DB", "shoppingmanager");
var user = GetEnv("POSTGRES_USER", "postgres");
var password = GetEnv("POSTGRES_PASSWORD", "postgres");

var connectionString = $"Host={host};Port={port};Database={database};Username={user};Password={password}";

var options = new DbContextOptionsBuilder<ShoppingManagerDbContext>()
    .UseNpgsql(connectionString)
    .Options;

using var dbContext = new ShoppingManagerDbContext(options);

Console.WriteLine("Waiting for database connection and applying migrations...");
await dbContext.Database.MigrateAsync();
Console.WriteLine("Database migrations applied successfully.");
