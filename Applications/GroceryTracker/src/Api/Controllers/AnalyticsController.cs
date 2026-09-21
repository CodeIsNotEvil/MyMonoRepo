using GroceryTracker.Application.Analytics;
using GroceryTracker.Domain.Analytics;
using Microsoft.AspNetCore.Mvc;

namespace GroceryTracker.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AnalyticsController : ControllerBase
{
  private readonly IAnalyticsService _analyticsService;

  public AnalyticsController(IAnalyticsService analyticsService)
  {
    _analyticsService = analyticsService;
  }

  /// <summary>
  /// Spend summary for a window. The client computes the same shape from its cache when offline.
  /// </summary>
  [HttpGet("summary")]
  [ProducesResponseType<SpendSummary>(StatusCodes.Status200OK)]
  public async Task<ActionResult<SpendSummary>> GetSummary(
    [FromQuery] DateOnly? from,
    [FromQuery] DateOnly? to,
    [FromQuery] SpendGranularity granularity = SpendGranularity.Month,
    CancellationToken cancellationToken = default)
  {
    var today = DateOnly.FromDateTime(DateTime.UtcNow);
    var resolvedTo = to ?? today;

    // Default window is the current month plus the five before it, starting on a month boundary so
    // the monthly series does not open with a half bar.
    var defaultStart = resolvedTo.AddMonths(-5);
    var resolvedFrom = from ?? new DateOnly(defaultStart.Year, defaultStart.Month, 1);

    if (resolvedTo < resolvedFrom)
    {
      return BadRequest(new ProblemDetails { Title = "'to' must not be earlier than 'from'." });
    }

    var summary = await _analyticsService.SummariseAsync(
      resolvedFrom,
      resolvedTo,
      granularity,
      cancellationToken);

    return Ok(summary);
  }
}
