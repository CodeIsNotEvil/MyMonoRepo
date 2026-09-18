using GroceryTracker.Application.Analytics;
using GroceryTracker.Bff.Controllers;
using GroceryTracker.Domain.Analytics;
using Microsoft.AspNetCore.Mvc;

namespace GroceryTracker.Bff.Tests;

/// <summary>
/// Covers the window the controller picks when the client does not supply one, and the guard
/// against a reversed range.
/// </summary>
public class AnalyticsControllerTests
{
  private sealed class RecordingAnalyticsService : IAnalyticsService
  {
    public DateOnly From { get; private set; }

    public DateOnly To { get; private set; }

    public SpendGranularity Granularity { get; private set; }

    public Task<SpendSummary> SummariseAsync(
      DateOnly from,
      DateOnly to,
      SpendGranularity granularity,
      CancellationToken cancellationToken = default)
    {
      From = from;
      To = to;
      Granularity = granularity;
      return Task.FromResult(SpendSummary.Empty(from, to, "EUR"));
    }
  }

  [Fact]
  public async Task Default_window_is_six_months_and_starts_on_a_month_boundary()
  {
    var service = new RecordingAnalyticsService();
    var controller = new AnalyticsController(service);

    var result = await controller.GetSummary(from: null, to: null);

    Assert.IsType<OkObjectResult>(result.Result);

    var today = DateOnly.FromDateTime(DateTime.UtcNow);
    var expectedStart = today.AddMonths(-5);

    Assert.Equal(today, service.To);
    Assert.Equal(new DateOnly(expectedStart.Year, expectedStart.Month, 1), service.From);

    // Six buckets inclusive: the current month plus the five before it.
    Assert.Equal(1, service.From.Day);
    Assert.Equal(SpendGranularity.Month, service.Granularity);
  }

  [Fact]
  public async Task Explicit_window_is_passed_through_untouched()
  {
    var service = new RecordingAnalyticsService();
    var controller = new AnalyticsController(service);

    var from = new DateOnly(2026, 1, 15);
    var to = new DateOnly(2026, 2, 20);

    await controller.GetSummary(from, to, SpendGranularity.Week);

    Assert.Equal(from, service.From);
    Assert.Equal(to, service.To);
    Assert.Equal(SpendGranularity.Week, service.Granularity);
  }

  [Fact]
  public async Task A_reversed_window_is_rejected()
  {
    var controller = new AnalyticsController(new RecordingAnalyticsService());

    var result = await controller.GetSummary(
      from: new DateOnly(2026, 5, 1),
      to: new DateOnly(2026, 4, 1));

    Assert.IsType<BadRequestObjectResult>(result.Result);
  }
}
