using CINE.GroceryTracker.Domain.Model;

namespace CINE.GroceryTracker.Domain.Balance;

/// <param name="Balance">
/// Positive: the rest of the household owes this person. Negative: this person owes the rest.
/// </param>
public sealed record MemberBalance(
  Guid MemberId,
  string Name,
  decimal Paid,
  decimal ShareWeight,
  decimal SharePercent,
  decimal FairShare,
  decimal TransfersSent,
  decimal TransfersReceived,
  decimal Balance);

public sealed record SuggestedTransfer(
  Guid FromMemberId,
  string FromName,
  Guid ToMemberId,
  string ToName,
  decimal Amount);

/// <param name="UnassignedTotal">
/// Spend on trips nobody is recorded as having paid for. It is left out of the balance rather than
/// guessed at, and reported so it can be fixed.
/// </param>
public sealed record BalanceSummary(
  IReadOnlyList<MemberBalance> Members,
  IReadOnlyList<SuggestedTransfer> Transfers,
  decimal TotalAssigned,
  decimal UnassignedTotal,
  int UnassignedCount)
{
  public bool IsSettled => Transfers.Count == 0;

  public static BalanceSummary Empty { get; } = new([], [], 0m, 0m, 0);
}

/// <summary>
/// Works out who owes whom for shared grocery spending, and the fewest transfers that settle it.
/// </summary>
/// <remarks>
/// Every household member carries a share of everything that was paid for: equal by default, or in
/// proportion to their <see cref="Member.ShareWeight"/>. A member's balance
/// is what they paid, minus their share, plus transfers they sent, minus transfers they received —
/// so recording a settlement moves the balance towards zero, and a balance of zero means square.
/// Pure and free of EF Core and HTTP, so the API and the offline browser compute identical numbers.
/// </remarks>
public static class BalanceCalculator
{
  /// <summary>Smaller than a cent: leftovers from rounding that are not worth a transfer.</summary>
  private const decimal Cent = 0.01m;

  public static BalanceSummary Calculate(
    IEnumerable<ShoppingTrip> trips,
    IEnumerable<Member> members,
    IEnumerable<Settlement> settlements)
  {
    ArgumentNullException.ThrowIfNull(trips);
    ArgumentNullException.ThrowIfNull(members);
    ArgumentNullException.ThrowIfNull(settlements);

    var people = members
      .Where(m => !m.IsDeleted)
      .OrderBy(m => m.DisplayName, StringComparer.CurrentCultureIgnoreCase)
      .ToList();

    var liveTrips = trips.Where(t => !t.IsDeleted).ToList();
    var knownIds = people.Select(p => p.Id).ToHashSet();

    var assigned = liveTrips.Where(t => t.PaidByMemberId is { } id && knownIds.Contains(id)).ToList();
    var unassigned = liveTrips.Except(assigned).ToList();

    var totalAssigned = assigned.Sum(t => t.TotalAmount);
    var unassignedTotal = unassigned.Sum(t => t.TotalAmount);

    if (people.Count == 0)
    {
      return new BalanceSummary([], [], 0m, unassignedTotal, unassigned.Count);
    }

    var weights = people.ToDictionary(p => p.Id, p => Math.Max(0m, p.ShareWeight));
    var weightSum = weights.Values.Sum();

    // Nobody is left with a share of nothing: if every weight is zero, fall back to an even split.
    if (weightSum <= 0m)
    {
      weights = people.ToDictionary(p => p.Id, _ => 1m);
      weightSum = people.Count;
    }

    var liveSettlements = settlements
      .Where(s => !s.IsDeleted && knownIds.Contains(s.FromMemberId) && knownIds.Contains(s.ToMemberId))
      .ToList();

    var balances = people
      .Select(person =>
      {
        var paid = assigned.Where(t => t.PaidByMemberId == person.Id).Sum(t => t.TotalAmount);
        var sent = liveSettlements.Where(s => s.FromMemberId == person.Id).Sum(s => s.Amount);
        var received = liveSettlements.Where(s => s.ToMemberId == person.Id).Sum(s => s.Amount);
        var fraction = weights[person.Id] / weightSum;
        var share = totalAssigned * fraction;

        return new MemberBalance(
          person.Id,
          person.DisplayName,
          Round(paid),
          weights[person.Id],
          Round(fraction * 100m),
          Round(share),
          Round(sent),
          Round(received),
          Round(paid - share + sent - received));
      })
      .ToList();

    return new BalanceSummary(
      balances,
      SuggestTransfers(balances),
      Round(totalAssigned),
      Round(unassignedTotal),
      unassigned.Count);
  }

  /// <summary>
  /// Repeatedly pairs the person who owes most with the person owed most. Every step fully settles at
  /// least one of the two, so n people never need more than n - 1 transfers.
  /// </summary>
  private static List<SuggestedTransfer> SuggestTransfers(IReadOnlyList<MemberBalance> balances)
  {
    var owed = balances.Where(b => b.Balance >= Cent)
      .Select(b => (Member: b, Left: b.Balance)).ToList();
    var owing = balances.Where(b => b.Balance <= -Cent)
      .Select(b => (Member: b, Left: -b.Balance)).ToList();

    var transfers = new List<SuggestedTransfer>();

    while (owed.Count > 0 && owing.Count > 0)
    {
      // Ties are broken by name so the same books always give the same suggestion.
      var creditor = owed.OrderByDescending(o => o.Left).ThenBy(o => o.Member.Name, StringComparer.Ordinal).First();
      var debtor = owing.OrderByDescending(o => o.Left).ThenBy(o => o.Member.Name, StringComparer.Ordinal).First();

      var amount = Math.Min(creditor.Left, debtor.Left);

      transfers.Add(new SuggestedTransfer(
        debtor.Member.MemberId,
        debtor.Member.Name,
        creditor.Member.MemberId,
        creditor.Member.Name,
        amount));

      owed.Remove(creditor);
      owing.Remove(debtor);

      if (creditor.Left - amount >= Cent)
      {
        owed.Add((creditor.Member, creditor.Left - amount));
      }

      if (debtor.Left - amount >= Cent)
      {
        owing.Add((debtor.Member, debtor.Left - amount));
      }
    }

    return transfers;
  }

  private static decimal Round(decimal value) => decimal.Round(value, 2, MidpointRounding.AwayFromZero);
}
