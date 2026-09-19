using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace GroceryTracker.Infrastructure.Data;

/// <summary>
/// Lets `dotnet ef migrations add` build the model without starting the web host. That matters
/// because the API needs the ASP.NET Core targeting pack to build, which not every dev machine has,
/// and generating a migration never touches a database — the connection string below is only there
/// so the provider can be configured.
/// </summary>
public sealed class GroceryTrackerDbContextFactory : IDesignTimeDbContextFactory<GroceryTrackerDbContext>
{
  public GroceryTrackerDbContext CreateDbContext(string[] args)
  {
    var options = new DbContextOptionsBuilder<GroceryTrackerDbContext>()
      .UseNpgsql("Host=localhost;Database=design_time_only")
      .Options;

    return new GroceryTrackerDbContext(options);
  }
}
