using System.Text.Json.Serialization;
using CINE.GroceryTracker.Domain.Sync;

namespace CINE.GroceryTracker.Domain.Model;

/// <summary>
/// One visit to a store, i.e. one receipt.
/// </summary>
/// <remarks>
/// <see cref="TotalAmount"/> is what was actually paid and is always authoritative.
/// <see cref="Items"/> is an optional breakdown, so a trip can be logged in five seconds at the
/// till and itemised later. Anything the items do not account for is reported as uncategorised
/// spend rather than being silently dropped or double counted.
/// </remarks>
public sealed class ShoppingTrip : ISyncEntity
{
  public Guid Id { get; init; } = Guid.NewGuid();

  public DateOnly PurchasedOn { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);

  public decimal TotalAmount { get; set; }

  public string? Note { get; set; }

  public Guid StoreId { get; set; }

  public Guid? PaidByMemberId { get; set; }

  // Sync metadata
  public Guid HouseholdId { get; set; }
  public long SyncStamp { get; set; }
  public DateTimeOffset UpdatedAtUtc { get; set; }
  public bool IsDeleted { get; set; }

  // Relationships
  [JsonIgnore]
  public Household Household { get; set; } = null!;

  [JsonIgnore]
  public Store Store { get; set; } = null!;

  [JsonIgnore]
  public Member? PaidByMember { get; set; }

  [JsonIgnore]
  public ICollection<ExpenseItem> Items { get; init; } = new List<ExpenseItem>();
}
