using System.Text.Json.Serialization;
using GroceryTracker.Domain.Sync;

namespace GroceryTracker.Domain.Model;

/// <summary>
/// Money one member handed to another to even out who has paid more of the shared groceries.
/// </summary>
/// <remarks>
/// Kept separate from <see cref="ShoppingTrip"/> on purpose: a transfer is not spending, so it must
/// never show up in the spend analytics, only in the balance between people.
/// </remarks>
public sealed class Settlement : ISyncEntity
{
  public Guid Id { get; init; } = Guid.NewGuid();

  /// <summary>The member who paid the money.</summary>
  public Guid FromMemberId { get; set; }

  /// <summary>The member who received it.</summary>
  public Guid ToMemberId { get; set; }

  public decimal Amount { get; set; }

  public DateOnly Date { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);

  public string? Note { get; set; }

  // Sync metadata
  public Guid HouseholdId { get; set; }
  public long SyncStamp { get; set; }
  public DateTimeOffset UpdatedAtUtc { get; set; }
  public bool IsDeleted { get; set; }

  // Relationships
  [JsonIgnore]
  public Household Household { get; set; } = null!;

  [JsonIgnore]
  public Member FromMember { get; set; } = null!;

  [JsonIgnore]
  public Member ToMember { get; set; } = null!;
}
