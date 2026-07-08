using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SafeRide.API.Authorization;
using SafeRide.Application.Common.Interfaces;
using SafeRide.Application.Features.Auth;

namespace SafeRide.API.Controllers;

[ApiController]
[Route("api/payments")]
public sealed class PaymentsController : ControllerBase
{
    private readonly IPaymentService _paymentService;

    public PaymentsController(IPaymentService paymentService)
    {
        _paymentService = paymentService;
    }

    [Authorize]
    [HttpPost("trips/{tripId:long}/qr")]
    [AllowTripContinuation(TripContinuationOperation.TripPayment)]
    [ProducesResponseType<QrPaymentResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult<QrPaymentResult>> CreateQrPayment(
        long tripId,
        [FromBody] CreateQrPaymentRequest? request,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var customerId))
        {
            return Unauthorized();
        }

        var result = await _paymentService.CreateQrPaymentAsync(
            customerId,
            tripId,
            request?.ReturnUrl,
            request?.CancelUrl,
            cancellationToken);
        return Ok(result);
    }

    [Authorize]
    [HttpPost("driver/trips/{tripId:long}/qr")]
    [AllowTripContinuation(TripContinuationOperation.TripPayment)]
    [ProducesResponseType<QrPaymentResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult<QrPaymentResult>> CreateDriverQrPayment(
        long tripId,
        [FromBody] CreateQrPaymentRequest? request,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var driverId))
        {
            return Unauthorized();
        }

        var result = await _paymentService.CreateDriverQrPaymentAsync(
            driverId,
            tripId,
            request?.ReturnUrl,
            request?.CancelUrl,
            cancellationToken);
        return Ok(result);
    }

    [Authorize]
    [HttpPost("driver/trips/{tripId:long}/cash")]
    [AllowTripContinuation(TripContinuationOperation.TripPayment)]
    [ProducesResponseType<PaymentStatusResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PaymentStatusResult>> ConfirmCashPayment(
        long tripId,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var driverId))
        {
            return Unauthorized();
        }

        var result = await _paymentService.ConfirmCashPaymentAsync(
            driverId,
            tripId,
            cancellationToken);
        return Ok(result);
    }

    [Authorize]
    [HttpGet("trips/{tripId:long}/status")]
    [AllowTripContinuation(TripContinuationOperation.TripPayment)]
    [ProducesResponseType<PaymentStatusResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PaymentStatusResult>> GetPaymentStatus(
        long tripId,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var customerId))
        {
            return Unauthorized();
        }

        var result = await _paymentService.GetTripPaymentStatusAsync(
            customerId,
            tripId,
            cancellationToken);
        return Ok(result);
    }

    [Authorize]
    [HttpGet("driver/trips/{tripId:long}/status")]
    [AllowTripContinuation(TripContinuationOperation.TripPayment)]
    [ProducesResponseType<PaymentStatusResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PaymentStatusResult>> GetDriverPaymentStatus(
        long tripId,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var driverId))
        {
            return Unauthorized();
        }

        var result = await _paymentService.GetDriverTripPaymentStatusAsync(
            driverId,
            tripId,
            cancellationToken);
        return Ok(result);
    }

    [AllowAnonymous]
    [HttpPost("payos/webhook")]
    public async Task<IActionResult> PayOsWebhook(
        [FromBody] PayOsWebhookRequest request,
        CancellationToken cancellationToken)
    {
        await _paymentService.HandlePayOsWebhookAsync(request, cancellationToken);
        return Ok(new { success = true });
    }

    [AllowAnonymous]
    [HttpPost("demo/qr/webhook")]
    [ProducesResponseType<PaymentStatusResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PaymentStatusResult>> DemoQrWebhook(
        [FromBody] DemoQrPaymentWebhookRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _paymentService.ConfirmDemoQrPaymentAsync(
            request,
            cancellationToken);
        return Ok(result);
    }

    private bool TryGetUserId(out Guid userId)
    {
        return Guid.TryParse(
            User.FindFirstValue(ClaimTypes.NameIdentifier),
            out userId);
    }
}

public sealed record CreateQrPaymentRequest(string? ReturnUrl, string? CancelUrl);
