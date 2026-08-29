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
    private readonly IDriverWalletTopUpService _walletTopUpService;

    public PaymentsController(IPaymentService paymentService, IDriverWalletTopUpService walletTopUpService)
    {
        _paymentService = paymentService;
        _walletTopUpService = walletTopUpService;
    }

    [Authorize]
    [HttpPost("driver/wallet/top-ups")]
    [ProducesResponseType<WalletTopUpResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult<WalletTopUpResult>> CreateWalletTopUp(
        [FromBody] CreateWalletTopUpRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var driverId)) return Unauthorized();
        return Ok(await _walletTopUpService.CreateAsync(driverId, request.Amount, request.ReturnUrl, request.CancelUrl, cancellationToken));
    }

    [Authorize]
    [HttpGet("driver/wallet/top-ups/{topUpId:long}")]
    [ProducesResponseType<WalletTopUpResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult<WalletTopUpResult>> GetWalletTopUpStatus(long topUpId, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var driverId)) return Unauthorized();
        return Ok(await _walletTopUpService.GetStatusAsync(driverId, topUpId, cancellationToken));
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
    [HttpPost("driver/trips/{tripId:long}/start")]
    [AllowTripContinuation(TripContinuationOperation.TripPayment)]
    [ProducesResponseType<PaymentStatusResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PaymentStatusResult>> StartDriverPayment(
        long tripId,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var driverId))
        {
            return Unauthorized();
        }

        var result = await _paymentService.StartDriverPaymentAsync(
            driverId,
            tripId,
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
        if (await _walletTopUpService.TryHandleWebhookAsync(request, cancellationToken))
            return Ok(new { success = true });
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
public sealed record CreateWalletTopUpRequest(decimal Amount, string? ReturnUrl, string? CancelUrl);
