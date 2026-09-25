using System.Text.Json.Serialization;
using CINE.GroceryTracker.Domain.Sync;

namespace CINE.GroceryTracker.Domain.Model;

/// <summary>A grocery bucket such as Produce, Dairy or Household.</summary>
public sealed class Category : ISyncEntity
{
  public Guid Id { get; init; } = Guid.NewGuid();

  public string Name { get; set; } = string.Empty;

  /// <summary>Hex colour used to keep a category the same colour across every chart.</summary>
  public string ColorHex { get; set; } = "#6c757d";

  // Sync metadata
  public Guid HouseholdId { get; set; }
  public long SyncStamp { get; set; }
  public DateTimeOffset UpdatedAtUtc { get; set; }
  public bool IsDeleted { get; set; }

  // Relationships
  [JsonIgnore]
  public Household Household { get; set; } = null!;

  [JsonIgnore]
  public ICollection<ExpenseItem> Items { get; init; } = new List<ExpenseItem>();
}
