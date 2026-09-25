using CINE.GroceryTracker.Domain.Model;
using CINE.GroceryTracker.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CINE.GroceryTracker.Application.Households;

public interface IHouseholdProvisioner
{
  Task<Household> EnsureDefaultHouseholdAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Guarantees there is one household with a usable set of categories.
/// </summary>
/// <remarks>
/// There is no sign-up flow, so a device discovers its household by pulling it on the first sync.
/// Seeding it here means a freshly installed PWA can record a trip without any setup screen.
/// </remarks>
public sealed class HouseholdProvisioner : IHouseholdProvisioner
{
  /// <summary>The two categories a new household starts with. More can be added under Manage.</summary>
  public static readonly (string Name, string Color)[] DefaultCategories =
  [
    ("Groceries", "#2a78d6"),
    ("Household", "#eb6834"),
  ];

  private readonly GroceryTrackerDbContext _db;

  public HouseholdProvisioner(GroceryTrackerDbContext db)
  {
    _db = db ?? throw new ArgumentNullException(nameof(db));
  }

  public async Task<Household> EnsureDefaultHouseholdAsync(CancellationToken cancellationToken = default)
  {
    var household = await _db.Households
      .Where(h => !h.IsDeleted)
      .OrderBy(h => h.SyncStamp)
      .FirstOrDefaultAsync(cancellationToken);

    if (household is not null)
    {
      return household;
    }

    household = new Household { Name = "Home", CurrencyCode = "EUR" };
    _db.Households.Add(household);

    foreach (var (name, color) in DefaultCategories)
    {
      _db.Categories.Add(new Category
      {
        Name = name,
        ColorHex = color,
        HouseholdId = household.Id,
      });
    }

    await _db.SaveChangesAsync(cancellationToken);
    return household;
  }
}
