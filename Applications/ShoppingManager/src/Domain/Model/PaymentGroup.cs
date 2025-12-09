namespace ShoppingManager.Domain.Model;

public sealed class PaymentGroup {
  public Guid Id { get; init; } = Guid.NewGuid();

  public string Name { get; set; } = string.Empty;

  // Relationships
  public ICollection<User> Users { get; set; } = new List<User>();
  public ICollection<Payment> Payments { get; set; } = new List<Payment>();

}
