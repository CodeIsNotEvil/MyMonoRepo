using Microsoft.AspNetCore.Mvc;
using ShoppingManager.Application;
using ShoppingManager.Domain.Model;

namespace ShoppingManager.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PaymentsController : ControllerBase
{
    private readonly IPaymentService _paymentService;

    public PaymentsController(IPaymentService paymentService)
    {
        _paymentService = paymentService;
    }

    /// <summary>
    /// Create a new payment
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<Payment>> CreatePayment([FromBody] CreatePaymentRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        try
        {
            var payment = await _paymentService.CreatePaymentAsync(request.Amount, request.UserId, request.Date);
            return CreatedAtAction(nameof(GetPayment), new { id = payment.Id }, payment);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred while creating the payment.", error = ex.Message });
        }
    }

    /// <summary>
    /// Get a payment by ID
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<Payment>> GetPayment(Guid id)
    {
        try
        {
            var payment = await _paymentService.GetPaymentAsync(id);
            return Ok(payment);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred while retrieving the payment.", error = ex.Message });
        }
    }

    /// <summary>
    /// Get all payments for a user
    /// </summary>
    [HttpGet("user/{userId:guid}")]
    public async Task<ActionResult<IEnumerable<Payment>>> GetUserPayments(Guid userId)
    {
        try
        {
            var payments = await _paymentService.GetUserPaymentsAsync(userId);
            return Ok(payments);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred while retrieving payments.", error = ex.Message });
        }
    }
}

public class CreatePaymentRequest
{
    public decimal Amount { get; set; }
    public Guid UserId { get; set; }
    public DateTime? Date { get; set; }
}
