using System.Globalization;

namespace GroceryTracker.UI.Blazor.Services;

/// <summary>
/// Formats amounts without depending on the browser's culture data.
/// </summary>
/// <remarks>
/// WebAssembly builds can ship trimmed globalization data, so currency formatting is done by hand
/// rather than through <c>ToString("C")</c>, which would otherwise render differently depending on
/// how the app was published.
/// </remarks>
public static class Money
{
  private static readonly Dictionary<string, string> Symbols = new(StringComparer.OrdinalIgnoreCase)
  {
    ["EUR"] = "€",
    ["USD"] = "$",
    ["GBP"] = "£",
    ["CHF"] = "CHF",
    ["SEK"] = "kr",
    ["PLN"] = "zł",
  };

  public static string Format(decimal amount, string currencyCode, bool withDecimals = true)
  {
    var symbol = Symbol(currencyCode);
    var number = amount.ToString(withDecimals ? "N2" : "N0", CultureInfo.InvariantCulture);
    return $"{symbol} {number}";
  }

  /// <summary>Compact form for chart labels, where a full amount would not fit.</summary>
  public static string FormatCompact(decimal amount, string currencyCode)
  {
    var symbol = Symbol(currencyCode);

    return Math.Abs(amount) >= 1000m
      ? $"{symbol} {(amount / 1000m).ToString("0.#", CultureInfo.InvariantCulture)}k"
      : $"{symbol} {amount.ToString("0", CultureInfo.InvariantCulture)}";
  }

  public static string Symbol(string currencyCode) =>
    Symbols.TryGetValue(currencyCode ?? string.Empty, out var symbol) ? symbol : currencyCode ?? string.Empty;
}
