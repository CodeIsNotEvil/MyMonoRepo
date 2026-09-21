using GroceryTracker.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

static string Env(string key, string fallback) =>
  Environment.GetEnvironmentVariable(key) is { Length: > 0 } value ? value : fallback;

var host = Env("POSTGRES_HOST", "postgres");
var port = Env("POSTGRES_PORT", "5432");
var database = Env("POSTGRES_DB", "grocerytracker");
var user = Env("POSTGRES_USER", "postgres");
var password = Env("POSTGRES_PASSWORD", "postgres");

var connectionString = $"Host={host};Port={port};Database={database};Username={user};Password={password}";

var options = new DbContextOptionsBuilder<GroceryTrackerDbContext>()
  // The Pi's PostgreSQL container is often still starting when this runs, so retry instead of
  // failing the whole compose stack on a cold boot.
  .UseNpgsql(connectionString, npgsql => npgsql.EnableRetryOnFailure(10, TimeSpan.FromSeconds(5), null))
  .Options;

await using var db = new GroceryTrackerDbContext(options);

Console.WriteLine($"Applying migrations to {database} on {host}:{port}...");
await db.Database.MigrateAsync();
Console.WriteLine("Database migrations applied successfully.");
