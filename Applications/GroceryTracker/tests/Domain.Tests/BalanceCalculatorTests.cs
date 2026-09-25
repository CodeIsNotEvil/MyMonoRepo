using CINE.GroceryTracker.Domain.Balance;
using CINE.GroceryTracker.Domain.Model;

namespace CINE.GroceryTracker.Domain.Tests;

public class BalanceCalculatorTests
{
  private readonly Guid _household = Guid.NewGuid();
  private readonly Guid _store = Guid.NewGuid();

  private Member Person(string name, bool deleted = false) =>
    new() { DisplayName = name, HouseholdId = _household, IsDeleted = deleted };

  private ShoppingTrip Trip(decimal amount, Member? paidBy, bool deleted = false) => new()
  {
    TotalAmount = amount,
    PaidByMemberId = paidBy?.Id,
    StoreId = _store,
    HouseholdId = _household,
    PurchasedOn = new DateOnly(2026, 3, 1),
    IsDeleted = deleted,
  };

  private Settlement Transfer(Member from, Member to, decimal amount, bool deleted = false) => new()
  {
    FromMemberId = from.Id,
    ToMemberId = to.Id,
    Amount = amount,
    HouseholdId = _household,
    IsDeleted = deleted,
  };

  private static MemberBalance Of(BalanceSummary summary, Member member) =>
    summary.Members.Single(m => m.MemberId == member.Id);

  [Fact]
  public void Whoever_paid_more_is_owed_half_of_the_difference()
  {
    var anna = Person("Anna");
    var ben = Person("Ben");

    var summary = BalanceCalculator.Calculate([Trip(100m, anna), Trip(40m, ben)], [anna, ben], []);

    Assert.Equal(140m, summary.TotalAssigned);
    Assert.Equal(70m, Of(summary, anna).FairShare);
    Assert.Equal(30m, Of(summary, anna).Balance);
    Assert.Equal(-30m, Of(summary, ben).Balance);

    var transfer = Assert.Single(summary.Transfers);
    Assert.Equal(("Ben", "Anna", 30m), (transfer.FromName, transfer.ToName, transfer.Amount));
  }

  [Fact]
  public void A_recorded_transfer_settles_the_difference()
  {
    var anna = Person("Anna");
    var ben = Person("Ben");

    var summary = BalanceCalculator.Calculate(
      [Trip(100m, anna), Trip(40m, ben)], [anna, ben], [Transfer(ben, anna, 30m)]);

    Assert.True(summary.IsSettled);
    Assert.Equal(0m, Of(summary, anna).Balance);
    Assert.Equal(0m, Of(summary, ben).Balance);
    Assert.Equal(30m, Of(summary, ben).TransfersSent);
    Assert.Equal(30m, Of(summary, anna).TransfersReceived);
  }

  [Fact]
  public void A_partial_transfer_leaves_the_remainder_to_pay()
  {
    var anna = Person("Anna");
    var ben = Person("Ben");

    var summary = BalanceCalculator.Calculate(
      [Trip(100m, anna), Trip(40m, ben)], [anna, ben], [Transfer(ben, anna, 10m)]);

    Assert.Equal(20m, Assert.Single(summary.Transfers).Amount);
  }

  [Fact]
  public void Paying_back_too_much_flips_the_direction()
  {
    var anna = Person("Anna");
    var ben = Person("Ben");

    var summary = BalanceCalculator.Calculate(
      [Trip(100m, anna), Trip(40m, ben)], [anna, ben], [Transfer(ben, anna, 50m)]);

    var transfer = Assert.Single(summary.Transfers);
    Assert.Equal(("Anna", "Ben", 20m), (transfer.FromName, transfer.ToName, transfer.Amount));
  }

  [Fact]
  public void Everyone_paid_the_same_means_nobody_owes_anything()
  {
    var anna = Person("Anna");
    var ben = Person("Ben");

    var summary = BalanceCalculator.Calculate([Trip(50m, anna), Trip(50m, ben)], [anna, ben], []);

    Assert.True(summary.IsSettled);
    Assert.All(summary.Members, m => Assert.Equal(0m, m.Balance));
  }

  [Fact]
  public void One_person_paying_for_three_is_owed_by_the_other_two()
  {
    var anna = Person("Anna");
    var ben = Person("Ben");
    var cara = Person("Cara");

    var summary = BalanceCalculator.Calculate([Trip(90m, anna)], [anna, ben, cara], []);

    Assert.Equal(60m, Of(summary, anna).Balance);
    Assert.Equal(2, summary.Transfers.Count);
    Assert.All(summary.Transfers, t => Assert.Equal(("Anna", 30m), (t.ToName, t.Amount)));
  }

  [Fact]
  public void Settling_three_people_needs_at_most_two_transfers_and_clears_every_balance()
  {
    var anna = Person("Anna");
    var ben = Person("Ben");
    var cara = Person("Cara");

    // Total 120, share 40: Anna +50, Ben -20, Cara -30.
    var summary = BalanceCalculator.Calculate(
      [Trip(90m, anna), Trip(20m, ben), Trip(10m, cara)], [anna, ben, cara], []);

    Assert.Equal(2, summary.Transfers.Count);

    var net = summary.Members.ToDictionary(m => m.MemberId, m => m.Balance);
    foreach (var t in summary.Transfers)
    {
      net[t.FromMemberId] += t.Amount;
      net[t.ToMemberId] -= t.Amount;
    }

    Assert.All(net.Values, v => Assert.Equal(0m, v));
  }

  [Fact]
  public void Trips_nobody_paid_for_are_left_out_and_reported()
  {
    var anna = Person("Anna");
    var ben = Person("Ben");

    var summary = BalanceCalculator.Calculate(
      [Trip(100m, anna), Trip(40m, ben), Trip(25m, null), Trip(5m, null)], [anna, ben], []);

    Assert.Equal(140m, summary.TotalAssigned);
    Assert.Equal(30m, summary.UnassignedTotal);
    Assert.Equal(2, summary.UnassignedCount);
    Assert.Equal(30m, Of(summary, anna).Balance);
  }

  [Fact]
  public void A_refund_lowers_what_the_payer_is_credited_with()
  {
    var anna = Person("Anna");
    var ben = Person("Ben");

    var summary = BalanceCalculator.Calculate(
      [Trip(100m, anna), Trip(-10m, anna), Trip(40m, ben)], [anna, ben], []);

    Assert.Equal(90m, Of(summary, anna).Paid);
    Assert.Equal(25m, Of(summary, anna).Balance);
  }

  [Fact]
  public void Deleted_trips_members_and_transfers_are_ignored()
  {
    var anna = Person("Anna");
    var ben = Person("Ben");
    var gone = Person("Gone", deleted: true);

    var summary = BalanceCalculator.Calculate(
      [Trip(100m, anna), Trip(40m, ben), Trip(999m, anna, deleted: true), Trip(50m, gone)],
      [anna, ben, gone],
      [Transfer(ben, anna, 500m, deleted: true), Transfer(gone, anna, 500m)]);

    Assert.Equal(2, summary.Members.Count);
    Assert.Equal(140m, summary.TotalAssigned);

    // What a removed person paid is not silently redistributed; it shows up as unassigned instead.
    Assert.Equal(50m, summary.UnassignedTotal);
    Assert.Equal(30m, Of(summary, anna).Balance);
  }

  [Fact]
  public void With_no_members_everything_is_unassigned()
  {
    var summary = BalanceCalculator.Calculate([Trip(10m, null), Trip(5m, null)], [], []);

    Assert.Empty(summary.Members);
    Assert.True(summary.IsSettled);
    Assert.Equal(15m, summary.UnassignedTotal);
  }

  [Fact]
  public void An_uneven_split_of_cents_still_gives_whole_cent_transfers()
  {
    var anna = Person("Anna");
    var ben = Person("Ben");
    var cara = Person("Cara");

    var summary = BalanceCalculator.Calculate([Trip(100m, anna)], [anna, ben, cara], []);

    Assert.Equal(2, summary.Transfers.Count);
    Assert.All(summary.Transfers, t => Assert.Equal(33.33m, t.Amount));
  }

  [Fact]
  public void The_same_books_always_give_the_same_suggestions()
  {
    var anna = Person("Anna");
    var ben = Person("Ben");
    var cara = Person("Cara");
    var trips = new[] { Trip(60m, anna), Trip(0m, ben), Trip(0m, cara) };

    var first = BalanceCalculator.Calculate(trips, [cara, ben, anna], []);
    var second = BalanceCalculator.Calculate(trips, [anna, cara, ben], []);

    Assert.Equal(
      first.Transfers.Select(t => (t.FromName, t.ToName, t.Amount)),
      second.Transfers.Select(t => (t.FromName, t.ToName, t.Amount)));
  }

  private static Member Weighted(Member member, decimal weight)
  {
    member.ShareWeight = weight;
    return member;
  }

  [Fact]
  public void Shares_follow_the_weights_instead_of_splitting_evenly()
  {
    var anna = Weighted(Person("Anna"), 2m);
    var ben = Weighted(Person("Ben"), 1m);

    // Anna carries two thirds of 90 = 60 and paid 30, so she owes 30; Ben carries 30 and paid 60.
    var summary = BalanceCalculator.Calculate([Trip(30m, anna), Trip(60m, ben)], [anna, ben], []);

    Assert.Equal(60m, Of(summary, anna).FairShare);
    Assert.Equal(66.67m, Of(summary, anna).SharePercent);
    Assert.Equal(-30m, Of(summary, anna).Balance);
    Assert.Equal(30m, Of(summary, ben).Balance);

    var transfer = Assert.Single(summary.Transfers);
    Assert.Equal(("Anna", "Ben", 30m), (transfer.FromName, transfer.ToName, transfer.Amount));
  }

  [Fact]
  public void Weights_only_matter_relative_to_each_other()
  {
    var anna = Weighted(Person("Anna"), 60m);
    var ben = Weighted(Person("Ben"), 40m);

    var summary = BalanceCalculator.Calculate([Trip(100m, anna)], [anna, ben], []);

    Assert.Equal(60m, Of(summary, anna).FairShare);
    Assert.Equal(40m, Of(summary, ben).FairShare);
    Assert.Equal(-40m, Of(summary, ben).Balance);
  }

  [Fact]
  public void A_zero_weight_carries_nothing()
  {
    var anna = Person("Anna");
    var ben = Weighted(Person("Ben"), 0m);

    var summary = BalanceCalculator.Calculate([Trip(50m, anna)], [anna, ben], []);

    Assert.Equal(0m, Of(summary, ben).FairShare);
    Assert.True(summary.IsSettled);
  }

  [Fact]
  public void All_zero_weights_fall_back_to_an_even_split()
  {
    var anna = Weighted(Person("Anna"), 0m);
    var ben = Weighted(Person("Ben"), 0m);

    var summary = BalanceCalculator.Calculate([Trip(100m, anna)], [anna, ben], []);

    Assert.Equal(50m, Of(summary, anna).FairShare);
    Assert.Equal(-50m, Of(summary, ben).Balance);
  }

  [Fact]
  public void Settling_a_weighted_split_brings_everyone_to_zero()
  {
    var anna = Weighted(Person("Anna"), 3m);
    var ben = Weighted(Person("Ben"), 1m);

    var summary = BalanceCalculator.Calculate(
      [Trip(100m, anna)], [anna, ben], [Transfer(ben, anna, 25m)]);

    Assert.True(summary.IsSettled);
    Assert.Equal(0m, Of(summary, anna).Balance);
    Assert.Equal(0m, Of(summary, ben).Balance);
  }
}
