using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace GroceryTracker.Domain.Import;

/// <summary>One grocery booking read from a spreadsheet export.</summary>
/// <param name="Key">
/// Stable identity for the booking (date, amount and which repeat of that pair it is). Turned into a
/// deterministic id so importing the same file twice recognises what it already brought in.
/// </param>
/// <param name="Column">Zero-based column the amount was in, which is how it is tied to a person.</param>
public sealed record ImportedBooking(DateOnly Date, decimal Amount, string Key, int SourceLine, int Column);

/// <summary>A column that held amounts, with the name from the header row if there is one.</summary>
public sealed record CsvAmountColumn(int Index, string Header, int BookingCount, decimal Total);

public sealed record SkippedRow(int Line, string Reason, string Raw);

/// <param name="DateOutliers">
/// Bookings dated far from every other booking, which is nearly always a typo such as 2015 for
/// 2025. They are still in <paramref name="Bookings"/>; this only lets the UI point them out.
/// </param>
public sealed record CsvParseResult(
  IReadOnlyList<ImportedBooking> Bookings,
  IReadOnlyList<SkippedRow> Skipped,
  IReadOnlyList<ImportedBooking> DateOutliers,
  IReadOnlyList<CsvAmountColumn> Columns);

/// <summary>
/// Reads bookings out of a CSV export where only dates and amounts matter.
/// </summary>
/// <remarks>
/// Deliberately does not depend on column names or positions. A cell that looks like a date is the
/// date, a cell that looks like an amount is an amount, and anything else is ignored — so a sheet
/// with one amount column per person, or a stray notes column, imports the same way. Every filled
/// amount cell becomes its own booking, since two people shopping on the same day is two receipts.
/// </remarks>
public static partial class GroceryCsvParser
{
  private static readonly string[] DateFormats = ["d.M.yyyy", "d.M.yy", "yyyy-MM-dd"];

  public static CsvParseResult Parse(string text)
  {
    ArgumentNullException.ThrowIfNull(text);

    text = text.TrimStart('\uFEFF');

    var bookings = new List<ImportedBooking>();
    var skipped = new List<SkippedRow>();
    var occurrences = new Dictionary<string, int>();
    var seenFirstContentRow = false;
    List<string>? header = null;

    foreach (var (line, fields) in ReadRows(text, DetectDelimiter(text)))
    {
      if (fields.All(IsBlank))
      {
        continue;
      }

      var dates = new List<DateOnly>();
      var amounts = new List<(int Column, decimal Amount)>();

      for (var column = 0; column < fields.Count; column++)
      {
        if (TryParseDate(fields[column], out var date))
        {
          dates.Add(date);
        }
        else if (TryParseAmount(fields[column], out var amount))
        {
          amounts.Add((column, amount));
        }
      }

      var isFirstContentRow = !seenFirstContentRow;
      seenFirstContentRow = true;

      if (dates.Count == 0 && amounts.Count == 0)
      {
        // The header row, e.g. "Mandy,Lukas,Datum". Anything later that is all text is worth
        // mentioning, because it could be a booking whose date or amount failed to parse.
        if (isFirstContentRow)
        {
          header = fields;
        }
        else
        {
          skipped.Add(new SkippedRow(line, "No date or amount found.", Describe(fields)));
        }

        continue;
      }

      if (dates.Count == 0)
      {
        skipped.Add(new SkippedRow(line, "Has an amount but no date.", Describe(fields)));
        continue;
      }

      if (dates.Count > 1)
      {
        skipped.Add(new SkippedRow(line, "Has more than one date.", Describe(fields)));
        continue;
      }

      if (amounts.Count == 0)
      {
        skipped.Add(new SkippedRow(line, "Has a date but no amount.", Describe(fields)));
        continue;
      }

      foreach (var (column, amount) in amounts)
      {
        if (amount == 0m)
        {
          skipped.Add(new SkippedRow(line, "Amount is zero.", Describe(fields)));
          continue;
        }

        var baseKey = string.Create(CultureInfo.InvariantCulture, $"csv|{dates[0]:yyyy-MM-dd}|{amount:0.00}");
        var occurrence = occurrences.GetValueOrDefault(baseKey);
        occurrences[baseKey] = occurrence + 1;

        bookings.Add(new ImportedBooking(dates[0], amount, $"{baseKey}|{occurrence}", line, column));
      }
    }

    return new CsvParseResult(bookings, skipped, FindDateOutliers(bookings), DescribeColumns(bookings, header));
  }

  private static List<CsvAmountColumn> DescribeColumns(IReadOnlyList<ImportedBooking> bookings, List<string>? header) =>
    bookings
      .GroupBy(b => b.Column)
      .OrderBy(g => g.Key)
      .Select(g =>
      {
        var name = header is not null && g.Key < header.Count ? header[g.Key].Trim() : string.Empty;
        return new CsvAmountColumn(
          g.Key,
          name.Length > 0 ? name : $"Column {g.Key + 1}",
          g.Count(),
          g.Sum(b => b.Amount));
      })
      .ToList();

  /// <summary>
  /// Flags bookings more than <paramref name="maxGapDays"/> away from both neighbours in date order.
  /// Groceries are bought far more often than yearly, so an isolated date is almost certainly a typo.
  /// </summary>
  internal static IReadOnlyList<ImportedBooking> FindDateOutliers(
    IReadOnlyList<ImportedBooking> bookings,
    int maxGapDays = 365)
  {
    if (bookings.Count < 3)
    {
      return [];
    }

    var ordered = bookings.OrderBy(b => b.Date).ToList();
    var outliers = new List<ImportedBooking>();

    for (var i = 0; i < ordered.Count; i++)
    {
      var gapToPrevious = i > 0 ? ordered[i].Date.DayNumber - ordered[i - 1].Date.DayNumber : int.MaxValue;
      var gapToNext = i < ordered.Count - 1 ? ordered[i + 1].Date.DayNumber - ordered[i].Date.DayNumber : int.MaxValue;

      if (Math.Min(gapToPrevious, gapToNext) > maxGapDays)
      {
        outliers.Add(ordered[i]);
      }
    }

    return outliers;
  }

  internal static bool TryParseDate(string cell, out DateOnly date) =>
    DateOnly.TryParseExact(cell.Trim(), DateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out date);

  /// <summary>
  /// Parses "81,50 €", "1.234,56", "1,234.56", "-9,50 €" and the like.
  /// </summary>
  /// <remarks>
  /// German notation is tried first (comma decimal, dot thousands), because that is what a
  /// spreadsheet in this locale exports. It only ever changes the answer for a lone "1,234", which
  /// is read as 1.234 rather than 1234.
  /// </remarks>
  internal static bool TryParseAmount(string cell, out decimal amount)
  {
    amount = 0m;

    var cleaned = StripCurrency(cell);
    if (cleaned.Length == 0)
    {
      return false;
    }

    string normalised;
    if (GermanAmount().IsMatch(cleaned))
    {
      normalised = cleaned.Replace(".", string.Empty).Replace(',', '.');
    }
    else if (EnglishAmount().IsMatch(cleaned))
    {
      normalised = cleaned.Replace(",", string.Empty);
    }
    else
    {
      return false;
    }

    if (!decimal.TryParse(normalised, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
      CultureInfo.InvariantCulture, out var parsed))
    {
      return false;
    }

    amount = decimal.Round(parsed, 2, MidpointRounding.AwayFromZero);
    return true;
  }

  private static string StripCurrency(string cell)
  {
    var builder = new StringBuilder(cell.Length);

    // U+FFFD is what a Windows-1252 euro sign turns into when a file is decoded as UTF-8.
    foreach (var c in cell)
    {
      if (char.IsWhiteSpace(c) || c is '€' or '$' or '£' or '\uFFFD')
      {
        continue;
      }

      builder.Append(c);
    }

    return builder.ToString().Replace("EUR", string.Empty, StringComparison.OrdinalIgnoreCase);
  }

  [GeneratedRegex(@"^[+-]?(\d{1,3}(\.\d{3})+|\d+)(,\d+)?$", RegexOptions.CultureInvariant)]
  private static partial Regex GermanAmount();

  [GeneratedRegex(@"^[+-]?(\d{1,3}(,\d{3})+|\d+)(\.\d+)?$", RegexOptions.CultureInvariant)]
  private static partial Regex EnglishAmount();

  private static bool IsBlank(string field) => string.IsNullOrWhiteSpace(field);

  private static string Describe(List<string> fields) => string.Join(" | ", fields.Select(f => f.Trim()));

  /// <summary>
  /// Picks whichever of ';' and ',' is used more in the header line. German spreadsheet exports
  /// often use semicolons because the comma is already the decimal separator.
  /// </summary>
  private static char DetectDelimiter(string text)
  {
    var commas = 0;
    var semicolons = 0;
    var inQuotes = false;

    foreach (var c in text)
    {
      if (c == '"')
      {
        inQuotes = !inQuotes;
      }
      else if (!inQuotes)
      {
        if (c is '\n' or '\r')
        {
          if (commas + semicolons > 0)
          {
            break;
          }
        }
        else if (c == ',')
        {
          commas++;
        }
        else if (c == ';')
        {
          semicolons++;
        }
      }
    }

    return semicolons > commas ? ';' : ',';
  }

  /// <summary>Minimal RFC 4180 reader: quoted fields, doubled quotes, embedded newlines, CRLF.</summary>
  private static List<(int Line, List<string> Fields)> ReadRows(string text, char delimiter)
  {
    var rows = new List<(int, List<string>)>();
    var fields = new List<string>();
    var field = new StringBuilder();
    var inQuotes = false;
    var line = 1;
    var rowLine = 1;

    void EndRow()
    {
      fields.Add(field.ToString());
      field.Clear();
      rows.Add((rowLine, fields));
      fields = [];
      line++;
      rowLine = line;
    }

    for (var i = 0; i < text.Length; i++)
    {
      var c = text[i];

      if (inQuotes)
      {
        if (c == '"')
        {
          if (i + 1 < text.Length && text[i + 1] == '"')
          {
            field.Append('"');
            i++;
          }
          else
          {
            inQuotes = false;
          }
        }
        else
        {
          if (c == '\n')
          {
            line++;
          }

          field.Append(c);
        }
      }
      else if (c == '"' && field.Length == 0)
      {
        inQuotes = true;
      }
      else if (c == delimiter)
      {
        fields.Add(field.ToString());
        field.Clear();
      }
      else if (c == '\r')
      {
        if (i + 1 < text.Length && text[i + 1] == '\n')
        {
          i++;
        }

        EndRow();
      }
      else if (c == '\n')
      {
        EndRow();
      }
      else
      {
        field.Append(c);
      }
    }

    if (field.Length > 0 || fields.Count > 0)
    {
      EndRow();
    }

    return rows;
  }
}
