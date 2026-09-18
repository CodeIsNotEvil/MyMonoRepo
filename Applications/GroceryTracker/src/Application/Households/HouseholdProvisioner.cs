using GroceryTracker.Domain.Model;
using GroceryTracker.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace GroceryTracker.Application.Households;

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
  /// <summary>
  /// Eight starter categories carrying a categorical palette validated for colour-vision
  /// deficiency, assigned in slot order. Eight is the cap: a ninth hue would have to be cycled,
  /// which is exactly what makes two series indistinguishable.
  /// </summary>
  private static readonly (string Name, string Color)[] DefaultCategories =
  [
    ("Produce", "#2a78d6"),
    ("Dairy & Eggs", "#eb6834"),
    ("Meat & Fish", "#1baf7a"),
    ("Bakery", "#eda100"),
    ("Drinks", "#e87ba4"),
    ("Frozen", "#008300"),
    ("Pantry", "#4a3aa7"),
    ("Household & Care", "#e34948"),
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
