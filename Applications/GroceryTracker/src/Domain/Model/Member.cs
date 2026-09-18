using System.Text.Json.Serialization;
using GroceryTracker.Domain.Sync;

namespace GroceryTracker.Domain.Model;

/// <summary>A person in the household who can pay for a shopping trip.</summary>
public sealed class Member : ISyncEntity
{
  public Guid Id { get; init; } = Guid.NewGuid();

  public string DisplayName { get; set; } = string.Empty;

  /// <summary>
  /// This person's portion of the shared spending, relative to everyone else's. 1 and 1 split
  /// evenly; 2 and 1 make one person carry two thirds. Zero means they carry nothing.
  /// </summary>
  public decimal ShareWeight { get; set; } = 1m;

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
