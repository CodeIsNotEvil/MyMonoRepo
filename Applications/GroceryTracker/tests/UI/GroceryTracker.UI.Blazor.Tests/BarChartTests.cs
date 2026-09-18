using Bunit;
using GroceryTracker.UI.Blazor.Shared;

namespace GroceryTracker.UI.Blazor.Tests;

/// <summary>
/// The chart is hand-rolled SVG, so these cover the geometry that would otherwise only be caught
/// by looking at it: bars anchored to the baseline, scaled against the largest value, and never
/// widened into a solid block when there is only one period.
/// </summary>
public class BarChartTests : BunitContext
{
  private static BarChart.Bar Bar(string label, decimal value) => new(label, value, $"€ {value:0}");

  [Fact]
  public void Renders_one_path_per_point()
  {
    var component = Render<BarChart>(p => p.Add(c => c.Points,
      [Bar("Jan 26", 100m), Bar("Feb 26", 50m), Bar("Mar 26", 75m)]));

    Assert.Equal(3, component.FindAll("path.chart-bar").Count);
  }

  [Fact]
  public void The_largest_value_produces_the_tallest_bar()
  {
    var component = Render<BarChart>(p => p.Add(c => c.Points,
      [Bar("Jan 26", 25m), Bar("Feb 26", 100m)]));

    var heights = component.FindAll("path.chart-bar")
      .Select(path => BarHeight(path.GetAttribute("d")!))
      .ToList();

    Assert.True(heights[1] > heights[0], "the 100 bar should be taller than the 25 bar");

    // Heights are proportional to value, so a quarter of the spend is a quarter of the bar.
    Assert.InRange(heights[0] / heights[1], 0.2, 0.3);
  }

  [Fact]
  public void A_single_point_does_not_stretch_into_a_full_width_block()
  {
    var component = Render<BarChart>(p => p.Add(c => c.Points, [Bar("Sep 26", 220m)]));

    var width = BarWidth(component.Find("path.chart-bar").GetAttribute("d")!);

    // The viewBox is 240 wide; an uncapped lone bar would span all of it and read as a filled panel.
    Assert.True(width <= 40, $"a lone bar should stay narrow, was {width}");
  }

  [Fact]
  public void Every_bar_is_labelled_with_its_value()
  {
    var component = Render<BarChart>(p => p.Add(c => c.Points,
      [Bar("Jan 26", 100m), Bar("Feb 26", 50m)]));

    var markup = component.Markup;

    // Values are spelled out, not encoded by height alone; this is also what keeps paler palette
    // colours legible for readers who cannot rely on the fill.
    Assert.Contains("€ 100", markup);
    Assert.Contains("€ 50", markup);
    Assert.Contains("Jan 26", markup);
  }

  [Fact]
  public void An_empty_series_says_so_instead_of_drawing_an_empty_axis()
  {
    var component = Render<BarChart>(p => p.Add(c => c.Points, []));

    Assert.Empty(component.FindAll("path.chart-bar"));
    Assert.Contains("Nothing to chart", component.Markup);
  }

  [Fact]
  public void A_zero_value_draws_no_bar_at_all()
  {
    var component = Render<BarChart>(p => p.Add(c => c.Points,
      [Bar("Jan 26", 0m), Bar("Feb 26", 80m)]));

    var paths = component.FindAll("path.chart-bar");
    Assert.Equal(string.Empty, paths[0].GetAttribute("d"));
    Assert.NotEqual(string.Empty, paths[1].GetAttribute("d"));
  }

  /// <summary>Reads the bar height back out of the "M x bottom L x top …" path.</summary>
  private static double BarHeight(string path)
  {
    var numbers = Numbers(path);
    return numbers[1] - numbers[3];
  }

  private static double BarWidth(string path)
  {
    var numbers = Numbers(path);
    // The path ends with the bottom-right corner, so the span from the opening x is the bar width.
    return numbers[^2] - numbers[0];
  }

  private static double[] Numbers(string path) => path
    .Split([' ', ','], StringSplitOptions.RemoveEmptyEntries)
    .Where(token => double.TryParse(token, System.Globalization.NumberStyles.Any,
      System.Globalization.CultureInfo.InvariantCulture, out _))
    .Select(token => double.Parse(token, System.Globalization.CultureInfo.InvariantCulture))
    .ToArray();
}
