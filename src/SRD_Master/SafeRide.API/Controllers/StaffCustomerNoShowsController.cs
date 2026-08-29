using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SafeRide.Application.Common.Interfaces;
using SafeRide.Application.Features.StaffNoShowReviews;
using SafeRide.Domain.Enums;

namespace SafeRide.API.Controllers;

[ApiController]
[Authorize(Roles = "Staff,Admin")]
[Route("api/staff")]
public sealed class StaffCustomerNoShowsController : ControllerBase
{
    private readonly IStaffNoShowReviewService _service;
    public StaffCustomerNoShowsController(IStaffNoShowReviewService service) => _service = service;

    [HttpGet("customer-no-shows")]
    public Task<CustomerNoShowReviewList> List(Guid? customerId, CustomerBehaviorEventStatus? status, DateTime? from, DateTime? to, int page = 1, int pageSize = 20, CancellationToken ct = default)
        => _service.ListAsync(new(customerId, status, from, to, page, pageSize), ct);

    [HttpGet("customer-no-shows/{eventId:long}")]
    public Task<CustomerNoShowReviewDetail> Get(long eventId, CancellationToken ct) => _service.GetAsync(eventId, ct);

    [HttpPost("customer-no-shows/{eventId:long}/exempt")]
    public Task<CustomerNoShowReviewDetail> Exempt(long eventId, ExemptCustomerNoShowRequest request, CancellationToken ct)
        => _service.ExemptAsync(eventId, CurrentUserId(), request.Reason, ct);

    [HttpPost("customers/{customerId:guid}/booking-privileges/clear-restriction")]
    public Task<CustomerBookingPrivilegeSummary> Clear(Guid customerId, ClearCustomerBookingRestrictionRequest request, CancellationToken ct)
        => _service.ClearRestrictionsAsync(customerId, CurrentUserId(), request.Reason, ct);

    [HttpGet("customers/{customerId:guid}/booking-privileges")]
    public Task<CustomerBookingPrivilegeSummary> Privilege(Guid customerId, CancellationToken ct) => _service.GetPrivilegeAsync(customerId, ct);

    private Guid CurrentUserId() => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : throw new UnauthorizedAccessException();
}
