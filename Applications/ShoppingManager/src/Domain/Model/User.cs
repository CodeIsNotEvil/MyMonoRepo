namespace ShoppingManager.Domain.Model;

public sealed class User {
  public Guid Id { get; init; } = Guid.NewGuid();

  public string Name { get; set; } = string.Empty;
  public string Email { get; set; } = string.Empty;

  // Relationships
  public ICollection<Payment> Payments { get; init; } = new List<Payment>();

}
