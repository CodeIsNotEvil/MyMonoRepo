using System.Text.Json.Serialization;
using CINE.GroceryTracker.Application.Analytics;
using CINE.GroceryTracker.Application.Households;
using CINE.GroceryTracker.Application.Sync;
using CINE.GroceryTracker.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services
  .AddControllers()
  .AddJsonOptions(options =>
  {
    // The Blazor client sends enums by name, which keeps sync payloads readable in the browser's
    // IndexedDB inspector and survives an enum gaining members.
    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
  });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();

builder.Services.AddDbContext<GroceryTrackerDbContext>(options =>
  options.UseNpgsql(ResolveConnectionString(builder.Configuration)));

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<ISyncService, SyncService>();
builder.Services.AddScoped<IAnalyticsService, AnalyticsService>();
builder.Services.AddScoped<IHouseholdProvisioner, HouseholdProvisioner>();

builder.Services.AddHealthChecks()
  .AddDbContextCheck<GroceryTrackerDbContext>("database");

// The PWA is served from this same origin in production, so CORS is only needed for the Blazor dev
// server on localhost. Keeping it out of production is what keeps this a backend for one frontend
// rather than a public API.
const string DevCorsPolicy = "BlazorDevServer";
builder.Services.AddCors(options => options.AddPolicy(DevCorsPolicy, policy => policy
  .WithOrigins("http://localhost:5173", "https://localhost:5174")
  .AllowAnyMethod()
  .AllowAnyHeader()));

var app = builder.Build();

await using (var scope = app.Services.CreateAsyncScope())
{
  var db = scope.ServiceProvider.GetRequiredService<GroceryTrackerDbContext>();
  var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

  try
  {
    await db.Database.MigrateAsync();

    var provisioner = scope.ServiceProvider.GetRequiredService<IHouseholdProvisioner>();
    await provisioner.EnsureDefaultHouseholdAsync();
  }
  catch (Exception ex)
  {
    // A Pi that boots faster than its PostgreSQL container should not leave a dead process behind:
    // the container restart policy gets another attempt, so log and rethrow rather than limping on
    // with an unmigrated database.
    logger.LogError(ex, "Database migration or provisioning failed.");
    throw;
  }
}

if (app.Environment.IsDevelopment())
{
  app.MapOpenApi();
  app.UseCors(DevCorsPolicy);
}

app.UseAuthorization();

// Serves the Blazor WebAssembly app from this process, so a bare `dotnet run` gives you the whole
// application. Harmless when nginx serves the static files instead.
//
// MapStaticAssets rather than UseStaticFiles + UseBlazorFrameworkFiles: it reads the build's
// fingerprint manifest, which is what the import map in index.html points at during development.
// UseStaticFiles only knows the on-disk names and 404s every fingerprinted request.
app.MapStaticAssets();

app.MapControllers();
app.MapHealthChecks("/health");
app.MapFallbackToFile("index.html");

app.Run();

static string ResolveConnectionString(IConfiguration configuration)
{
  var configured = configuration.GetConnectionString("DefaultConnection");
  if (!string.IsNullOrWhiteSpace(configured))
  {
    return configured;
  }

  // Compose and the systemd unit both supply discrete POSTGRES_* variables.
  var host = configuration["POSTGRES_HOST"] ?? "localhost";
  var port = configuration["POSTGRES_PORT"] ?? "5432";
  var database = configuration["POSTGRES_DB"] ?? "grocerytracker";
  var user = configuration["POSTGRES_USER"] ?? "postgres";
  var password = configuration["POSTGRES_PASSWORD"] ?? "postgres";

  return $"Host={host};Port={port};Database={database};Username={user};Password={password}";
}

/// <summary>Exposed so the API test project can drive the app through <c>WebApplicationFactory</c>.</summary>
public partial class Program;
