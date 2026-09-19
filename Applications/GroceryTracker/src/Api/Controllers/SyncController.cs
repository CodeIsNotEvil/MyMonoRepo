using GroceryTracker.Application.Sync;
using GroceryTracker.Contracts.Sync;
using Microsoft.AspNetCore.Mvc;

namespace GroceryTracker.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SyncController : ControllerBase
{
  private readonly ISyncService _syncService;
  private readonly ILogger<SyncController> _logger;

  public SyncController(ISyncService syncService, ILogger<SyncController> logger)
  {
    _syncService = syncService;
    _logger = logger;
  }

  /// <summary>
  /// Pushes a device's queued changes and returns everything it has not seen yet.
  /// </summary>
  [HttpPost]
  [ProducesResponseType<SyncResponse>(StatusCodes.Status200OK)]
  public async Task<ActionResult<SyncResponse>> Synchronise(
    [FromBody] SyncRequest request,
    CancellationToken cancellationToken)
  {
    if (request.Cursor < 0)
    {
      return BadRequest(new ProblemDetails { Title = "Cursor must not be negative." });
    }

    var response = await _syncService.SynchroniseAsync(request, cancellationToken);

    if (response.Conflicts.Count > 0)
    {
      _logger.LogInformation(
        "Sync resolved {ConflictCount} conflict(s) for {OperationCount} operation(s).",
        response.Conflicts.Count,
        request.Operations.Count);
    }

    return Ok(response);
  }
}
