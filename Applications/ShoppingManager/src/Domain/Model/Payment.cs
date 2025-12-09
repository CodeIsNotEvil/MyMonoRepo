namespace ShoppingManager.Domain.Model;

public sealed class Payment {
  public Guid Id { get; init; } = Guid.NewGuid();

  public DateTime Date { get; set; } = DateTime.UtcNow;
  public decimal Amount { get; set; }

  // Relationships
  public Guid UserId { get; init; }
  public User User { get; init; } = null!;

}
