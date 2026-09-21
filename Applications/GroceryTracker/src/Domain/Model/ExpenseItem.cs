using System.Text.Json.Serialization;
using GroceryTracker.Domain.Sync;

namespace GroceryTracker.Domain.Model;

/// <summary>An optional line on a receipt, used to attribute part of a trip to a category.</summary>
public sealed class ExpenseItem : ISyncEntity
{
  public Guid Id { get; init; } = Guid.NewGuid();

  public string Description { get; set; } = string.Empty;

  public decimal Amount { get; set; }

  public decimal Quantity { get; set; } = 1m;

  /// <summary>Free text such as kg, l or pcs. Kept loose on purpose; this is a household app.</summary>
  public string? Unit { get; set; }

  public Guid TripId { get; set; }

  public Guid? CategoryId { get; set; }

  // Sync metadata
  public Guid HouseholdId { get; set; }
  public long SyncStamp { get; set; }
  public DateTimeOffset UpdatedAtUtc { get; set; }
  public bool IsDeleted { get; set; }

  // Relationships
  [JsonIgnore]
  public ShoppingTrip Trip { get; set; } = null!;

  [JsonIgnore]
  public Category? Category { get; set; }
}
