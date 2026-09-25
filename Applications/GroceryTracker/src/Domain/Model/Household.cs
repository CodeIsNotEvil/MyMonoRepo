using System.Text.Json.Serialization;
using CINE.GroceryTracker.Domain.Sync;

namespace CINE.GroceryTracker.Domain.Model;

public sealed class Household : ISyncEntity
{
  public Guid Id { get; init; } = Guid.NewGuid();

  public string Name { get; set; } = string.Empty;

  public string CurrencyCode { get; set; } = "EUR";

  // Sync metadata
  public long SyncStamp { get; set; }
  public DateTimeOffset UpdatedAtUtc { get; set; }
  public bool IsDeleted { get; set; }

  /// <summary>A household is its own tenant root, so it scopes itself.</summary>
  [JsonIgnore]
  public Guid HouseholdId
  {
    get => Id;
    set { }
  }

  // Relationships
  [JsonIgnore]
  public ICollection<Member> Members { get; init; } = new List<Member>();

  [JsonIgnore]
  public ICollection<Store> Stores { get; init; } = new List<Store>();

  [JsonIgnore]
  public ICollection<Category> Categories { get; init; } = new List<Category>();

  [JsonIgnore]
  public ICollection<ShoppingTrip> Trips { get; init; } = new List<ShoppingTrip>();

  [JsonIgnore]
  public ICollection<Settlement> Settlements { get; init; } = new List<Settlement>();
}
