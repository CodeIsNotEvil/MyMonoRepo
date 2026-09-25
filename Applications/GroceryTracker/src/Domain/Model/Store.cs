using System.Text.Json.Serialization;
using CINE.GroceryTracker.Domain.Sync;

namespace CINE.GroceryTracker.Domain.Model;

/// <summary>A shop the household buys groceries from, e.g. Aldi or Rewe.</summary>
public sealed class Store : ISyncEntity
{
  public Guid Id { get; init; } = Guid.NewGuid();

  public string Name { get; set; } = string.Empty;

  // Sync metadata
  public Guid HouseholdId { get; set; }
  public long SyncStamp { get; set; }
  public DateTimeOffset UpdatedAtUtc { get; set; }
  public bool IsDeleted { get; set; }

  // Relationships
  [JsonIgnore]
  public Household Household { get; set; } = null!;

  [JsonIgnore]
  public ICollection<ShoppingTrip> Trips { get; init; } = new List<ShoppingTrip>();
}
