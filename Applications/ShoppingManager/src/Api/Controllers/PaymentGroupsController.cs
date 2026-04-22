using Microsoft.AspNetCore.Mvc;
using ShoppingManager.Application;
using ShoppingManager.Domain.Model;

namespace ShoppingManager.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PaymentGroupsController : ControllerBase
{
    private readonly IPaymentGroupService _paymentGroupService;

    public PaymentGroupsController(IPaymentGroupService paymentGroupService)
    {
        _paymentGroupService = paymentGroupService;
    }

    /// <summary>
    /// Create a new payment group
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<PaymentGroup>> CreatePaymentGroup([FromBody] CreatePaymentGroupRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        try
        {
            var paymentGroup = await _paymentGroupService.CreatePaymentGroupAsync(request.Name);
            return CreatedAtAction(nameof(GetPaymentGroup), new { id = paymentGroup.Id }, paymentGroup);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred while creating the payment group.", error = ex.Message });
        }
    }

    /// <summary>
    /// Get a payment group by ID
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<PaymentGroup>> GetPaymentGroup(Guid id)
    {
        try
        {
            var paymentGroup = await _paymentGroupService.GetPaymentGroupAsync(id);
            return Ok(paymentGroup);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred while retrieving the payment group.", error = ex.Message });
        }
    }

    /// <summary>
    /// Get all payment groups
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<PaymentGroup>>> GetAllPaymentGroups()
    {
        try
        {
            var paymentGroups = await _paymentGroupService.GetAllPaymentGroupsAsync();
            return Ok(paymentGroups);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred while retrieving payment groups.", error = ex.Message });
        }
    }

    /// <summary>
    /// Add a user to a payment group
    /// </summary>
    [HttpPost("{id:guid}/users")]
    public async Task<IActionResult> AddUserToGroup(Guid id, [FromBody] AddUserToGroupRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        try
        {
            await _paymentGroupService.AddUserToGroupAsync(id, request.UserId);
            return NoContent();
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred while adding the user to the group.", error = ex.Message });
        }
    }

    /// <summary>
    /// Delete a payment group
    /// </summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeletePaymentGroup(Guid id)
    {
        try
        {
            await _paymentGroupService.DeletePaymentGroupAsync(id);
            return NoContent();
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred while deleting the payment group.", error = ex.Message });
        }
    }
}

public class CreatePaymentGroupRequest
{
    public string Name { get; set; } = string.Empty;
}

public class AddUserToGroupRequest
{
    public Guid UserId { get; set; }
}
