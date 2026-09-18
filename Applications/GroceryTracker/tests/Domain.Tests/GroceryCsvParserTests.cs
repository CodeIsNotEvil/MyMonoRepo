using GroceryTracker.Domain.Analytics;
using GroceryTracker.Domain.Import;
using GroceryTracker.Domain.Model;

namespace GroceryTracker.Domain.Tests;

public class GroceryCsvParserTests
{
  // Shaped like the real spreadsheet export: one amount column per person, a date column with a
  // trailing space in its header, and blank separator rows.
  private const string SheetExport =
    "Anna,Ben,Datum \n" +
    "\"81,50 €\",,04.04.2024\n" +
    ",\"39,40 €\",08.04.2024\n" +
    ",,\n" +
    "\"52,00 €\",,16.04.2024\n" +
    "\"72,34 €\",\"33,07 €\",26.07.2025\n" +
    ",\"-9,50 €\",17.08.2024\n";

  [Fact]
  public void Reads_amounts_and_dates_from_a_per_person_column_sheet()
  {
    var result = GroceryCsvParser.Parse(SheetExport);

    Assert.Empty(result.Skipped);
    Assert.Equal(
      [
        (new DateOnly(2024, 4, 4), 81.50m),
        (new DateOnly(2024, 4, 8), 39.40m),
        (new DateOnly(2024, 4, 16), 52.00m),
        (new DateOnly(2025, 7, 26), 72.34m),
        (new DateOnly(2025, 7, 26), 33.07m),
        (new DateOnly(2024, 8, 17), -9.50m),
      ],
      result.Bookings.Select(b => (b.Date, b.Amount)));
  }

  [Fact]
  public void A_row_with_an_amount_in_both_columns_is_two_bookings()
  {
    var result = GroceryCsvParser.Parse("A,B,Datum\n\"72,34 €\",\"33,07 €\",26.07.2025\n");

    Assert.Equal(2, result.Bookings.Count);
    Assert.Equal(105.41m, result.Bookings.Sum(b => b.Amount));
  }

  [Fact]
  public void Blank_rows_and_the_header_are_silently_ignored()
  {
    var result = GroceryCsvParser.Parse("Anna,Ben,Datum\n,,\n\n,,\n\"5,00 €\",,01.01.2025\n");

    Assert.Single(result.Bookings);
    Assert.Empty(result.Skipped);
  }

  [Fact]
  public void Does_not_depend_on_column_names_or_order()
  {
    var result = GroceryCsvParser.Parse("Wann,Wieviel,Notiz\n01.02.2025,\"12,30 €\",Wocheneinkauf\n");

    var booking = Assert.Single(result.Bookings);
    Assert.Equal(new DateOnly(2025, 2, 1), booking.Date);
    Assert.Equal(12.30m, booking.Amount);
  }

  [Fact]
  public void Semicolon_delimited_exports_work_without_quoting()
  {
    var result = GroceryCsvParser.Parse("Anna;Ben;Datum\n81,50 €;;04.04.2024\n;39,40 €;08.04.2024\n");

    Assert.Equal([81.50m, 39.40m], result.Bookings.Select(b => b.Amount));
  }

  [Fact]
  public void Handles_a_byte_order_mark_and_windows_line_endings()
  {
    var result = GroceryCsvParser.Parse("\uFEFFAnna,Ben,Datum\r\n\"1,00 €\",,02.01.2025\r\n\"2,00 €\",,03.01.2025\r\n");

    Assert.Equal(2, result.Bookings.Count);
    Assert.Empty(result.Skipped);
  }

  [Theory]
  [InlineData("81,50 €", 81.50)]
  [InlineData("81,5", 81.5)]
  [InlineData("81", 81)]
  [InlineData("1.234,56 €", 1234.56)]
  [InlineData("1,234.56", 1234.56)]
  [InlineData("12.50", 12.50)]
  [InlineData("-9,50 €", -9.50)]
  [InlineData("€ 4,20", 4.20)]
  [InlineData("4,20 EUR", 4.20)]
  [InlineData("4,20\u00A0€", 4.20)]
  [InlineData("4,20 \uFFFD", 4.20)]
  [InlineData("1.234", 1234)]
  [InlineData("1 234,50 €", 1234.50)]
  public void Parses_common_amount_notations(string cell, double expected)
  {
    Assert.True(GroceryCsvParser.TryParseAmount(cell, out var amount));
    Assert.Equal((decimal)expected, amount);
  }

  [Theory]
  [InlineData("")]
  [InlineData("   ")]
  [InlineData("Mandy")]
  [InlineData("abc12")]
  [InlineData("1,2,3")]
  [InlineData("€")]
  public void Rejects_cells_that_are_not_amounts(string cell)
  {
    Assert.False(GroceryCsvParser.TryParseAmount(cell, out _));
  }

  [Theory]
  [InlineData("04.04.2024", 2024, 4, 4)]
  [InlineData("4.4.2024", 2024, 4, 4)]
  [InlineData("04.04.24", 2024, 4, 4)]
  [InlineData("2024-04-04", 2024, 4, 4)]
  [InlineData(" 04.04.2024 ", 2024, 4, 4)]
  public void Parses_common_date_notations(string cell, int year, int month, int day)
  {
    Assert.True(GroceryCsvParser.TryParseDate(cell, out var date));
    Assert.Equal(new DateOnly(year, month, day), date);
  }

  [Theory]
  [InlineData("31.02.2024")]
  [InlineData("81,50 €")]
  [InlineData("13.13.2024")]
  [InlineData("")]
  public void Rejects_cells_that_are_not_dates(string cell)
  {
    Assert.False(GroceryCsvParser.TryParseDate(cell, out _));
  }

  [Fact]
  public void A_date_is_never_mistaken_for_an_amount()
  {
    // "04.04.2024" would otherwise be a tempting thousands-separated number.
    var result = GroceryCsvParser.Parse("x,y\n04.04.2024,\"5,00 €\"\n");

    var booking = Assert.Single(result.Bookings);
    Assert.Equal(5.00m, booking.Amount);
  }

  [Fact]
  public void Rows_that_cannot_be_read_are_reported_with_their_line_number_not_dropped_silently()
  {
    var result = GroceryCsvParser.Parse(
      "Anna,Ben,Datum\n" +
      "\"5,00 €\",,01.01.2025\n" +
      "\"6,00 €\",,\n" +
      ",,03.01.2025\n" +
      "\"0,00 €\",,04.01.2025\n" +
      "\"7,00 €\",,05.01.2025,06.01.2025\n" +
      "storniert,,\n");

    Assert.Single(result.Bookings);

    Assert.Equal(
      [
        (3, "Has an amount but no date."),
        (4, "Has a date but no amount."),
        (5, "Amount is zero."),
        (6, "Has more than one date."),
        (7, "No date or amount found."),
      ],
      result.Skipped.Select(s => (s.Line, s.Reason)));
  }

  [Fact]
  public void Line_numbers_account_for_newlines_inside_quoted_fields()
  {
    var result = GroceryCsvParser.Parse("A,B\n\"note\nwith break\",x\n\"5,00 €\",\n");

    var skipped = Assert.Single(result.Skipped, s => s.Reason == "Has an amount but no date.");
    Assert.Equal(4, skipped.Line);
  }

  [Fact]
  public void Identical_bookings_on_the_same_day_stay_distinct()
  {
    var result = GroceryCsvParser.Parse("A,B\n\"30,00 €\",,18.05.2024\n\"30,00 €\",,18.05.2024\n");

    Assert.Equal(2, result.Bookings.Count);
    Assert.Equal(2, result.Bookings.Select(b => b.Key).Distinct().Count());
  }

  [Fact]
  public void Keys_are_stable_across_parses_and_do_not_depend_on_other_rows()
  {
    var first = GroceryCsvParser.Parse(SheetExport);
    var again = GroceryCsvParser.Parse(SheetExport);

    // A re-import of the same file, or of a longer export of the same sheet, must recognise the
    // rows it already has.
    Assert.Equal(first.Bookings.Select(b => b.Key), again.Bookings.Select(b => b.Key));

    var longer = GroceryCsvParser.Parse(SheetExport + "\"9,99 €\",,01.09.2026\n");
    Assert.Equal(first.Bookings.Select(b => b.Key), longer.Bookings.Take(first.Bookings.Count).Select(b => b.Key));
  }

  [Fact]
  public void A_date_isolated_from_all_the_others_is_flagged_as_a_likely_typo()
  {
    // The middle row is 2015 where every neighbour is 2025 — the shape of a real typo in the sheet.
    var result = GroceryCsvParser.Parse(
      "A,B\n" +
      "\"1,00 €\",,07.11.2025\n" +
      "\"2,00 €\",,14.11.2015\n" +
      "\"3,00 €\",,22.11.2025\n" +
      "\"4,00 €\",,05.12.2025\n");

    var outlier = Assert.Single(result.DateOutliers);
    Assert.Equal(new DateOnly(2015, 11, 14), outlier.Date);
    Assert.Equal(4, result.Bookings.Count);
  }

  [Fact]
  public void Ordinary_gaps_between_shopping_trips_are_not_flagged()
  {
    var result = GroceryCsvParser.Parse(SheetExport);

    Assert.Empty(result.DateOutliers);
  }

  [Fact]
  public void Too_few_bookings_to_judge_flags_nothing()
  {
    var result = GroceryCsvParser.Parse("A,B\n\"1,00 €\",,07.11.2025\n\"2,00 €\",,14.11.2015\n");

    Assert.Empty(result.DateOutliers);
  }

  [Fact]
  public void Empty_input_yields_nothing()
  {
    var result = GroceryCsvParser.Parse(string.Empty);

    Assert.Empty(result.Bookings);
    Assert.Empty(result.Skipped);
  }

  [Fact]
  public void Deterministic_ids_repeat_and_differ_by_household_and_name()
  {
    var household = Guid.NewGuid();

    Assert.Equal(DeterministicGuid.Create(household, "a"), DeterministicGuid.Create(household, "a"));
    Assert.NotEqual(DeterministicGuid.Create(household, "a"), DeterministicGuid.Create(household, "b"));
    Assert.NotEqual(DeterministicGuid.Create(household, "a"), DeterministicGuid.Create(Guid.NewGuid(), "a"));
  }

  [Fact]
  public void A_refund_keeps_the_category_slices_adding_up_to_the_headline_total()
  {
    var household = Guid.NewGuid();
    var store = new Store { Name = "Imported", HouseholdId = household };

    ShoppingTrip Trip(string date, decimal total) => new()
    {
      PurchasedOn = DateOnly.Parse(date),
      TotalAmount = total,
      StoreId = store.Id,
      HouseholdId = household,
    };

    var summary = SpendAnalyzer.Summarise(
      [Trip("2026-03-02", 50m), Trip("2026-03-05", -9.50m)],
      [store],
      [],
      new DateOnly(2026, 3, 1),
      new DateOnly(2026, 3, 31));

    Assert.Equal(40.50m, summary.TotalSpend);

    var slice = Assert.Single(summary.ByCategory);
    Assert.Equal(40.50m, slice.Amount);
  }
}
