# SafeRide Pricing and Driver Compensation

This document describes the V1 pricing, trip-finalization, matching, settlement,
and compatibility flow implemented by the current source. The server is the
financial authority. Client applications select only permitted workflow inputs;
they do not calculate or choose financial outcomes.

## Distance booking pricing

`FareEstimationService` calculates the accepted booking snapshot from the route
estimate and the pricing/surge rule effective at the authoritative evaluation
time:

```text
RawFare = AcceptedBaseFare + EstimatedDistanceKm * AcceptedPricePerKm
NormalFare = max(AcceptedMinimumServiceFare, RawFare)
SurgedFare = max(AcceptedMinimumServiceFare, RawFare * AcceptedSurgeMultiplier)
LongDistanceComponent = max(0, EstimatedDistanceKm - AcceptedLongDistanceThresholdKm)
                        * AcceptedLongDistanceRatePerKm
EstimatedFare = RoundedSurgedFare + RoundedLongDistanceComponent
```

The minimum service floor and long-distance component are not themselves
multiplied by surge. `Booking.EstimatedFare` and its accepted pricing components
are immutable after the V1 booking is accepted. Pricing-rule and surge-rule IDs
remain audit references, not later repricing authorities.

## Normal completion

`TripStatusService` finalizes a V1 distance trip against the original booking
destination. When the final server-side location is within the configured
`DriverCompensation:DestinationReachedThresholdMeters` tolerance, currently
250 m, `TripFareFinalizationService` sets:

```text
GrossFare = Booking.EstimatedFare
```

Actual distance, duration, and route remain operational/audit evidence. They do
not increase the normal-completion customer fare, even after a detour.

## Customer-requested early stop

After the trip is `IN_PROGRESS`, realtime tracking projects the current
server-side location onto the immutable `Booking.RoutePolyline`. The cached and
finalized progress is monotonic. Pricing clamps that authoritative value to the
inclusive range `[0, 1]` and applies an inclusive 50% threshold:

```text
CandidateProgress = Project(CurrentLocation, Booking.RoutePolyline)
PlannedRouteProgress = max(PreviousPlannedRouteProgress, CandidateProgress)
Progress = Clamp(PlannedRouteProgress, 0, 1)

if Progress < 0.50:
    GrossFare = max(
        RoundVnd(Booking.EstimatedFare * Progress),
        AcceptedMinimumServiceFare)
else:
    GrossFare = Booking.EstimatedFare
```

Therefore `0.499999` remains progress-priced (subject to whole-VND rounding and
the minimum-service floor), while `0.50`, `0.75`, and `1.00` charge the full
locked booking fare. The distance actually driven and an active/rerouted
polyline are not early-stop pricing authorities.

The finalized component allocation uses the same shared calculation used by
settlement and always keeps `GrossFare = FareComponent + LongDistanceComponent`:

```text
if Progress < 0.50:
    LongDistanceComponent = RoundVnd(
        AcceptedLongDistanceComponent * Progress)
    FareComponent = GrossFare - LongDistanceComponent
else:
    FareComponent = Booking.EstimatedFare - AcceptedLongDistanceComponent
    LongDistanceComponent = AcceptedLongDistanceComponent
```

At or above 50%, the full accepted long-distance component is restored from the
immutable booking snapshot; it is not recomputed from current driver
compensation or pricing configuration.

## Customer settlement and promotion

The accepted `BookingPromotion.DiscountAmount` is the promotion authority. A
percentage is not recomputed from the finalized fare:

```text
AppliedPromotionDiscount = min(GrossFare, max(0, SnapshotPromotionDiscount))
CustomerPayable = GrossFare - AppliedPromotionDiscount
```

Payment after finalization reads the durable settlement customer payable. The
lifecycle remains:

```text
IN_PROGRESS
  -> WAITING_PAYMENT
  -> WAITING_RETURN_CONFIRM
  -> RETURN_CONFIRMED
  -> COMPLETED
```

Promotion usage is incremented only on the first transition to `COMPLETED`.

SafeRide permits PayOS QR prepayment before a trip starts. A pending provider
payment is not treated as money received: after trip end the lifecycle remains
at `WAITING_PAYMENT`. A successful payment is authoritative only after the
signed PayOS callback (or the explicit demo-only confirmation path) records it.
After fare finalization, the payment reconciliation service compares the total
successful payment amount with the durable `CustomerPayable`:

- underpayment leaves the trip at `WAITING_PAYMENT` with the remainder payable;
- exact payment permits `WAITING_RETURN_CONFIRM`;
- overpayment persists a refund obligation before lifecycle advance, and Staff
  confirms the manual refund with evidence through the existing refund flow.

The current QR flow does not ask the customer to upload a transfer screenshot
or ask Staff to verify payment proof. Staff participates only in the existing
manual-refund and exceptional-reconciliation paths; the PayOS callback is the
ordinary QR payment authority.

## Long pickup

Redis GEO provides candidate discovery and coarse ordering only. Before an offer
is created, the server obtains the driving route from the driver's current
location to the booking pickup and snapshots both `PickupDistanceKm` and:

```text
LongPickupCompensation = RoundVnd(
    max(0, PickupDistanceKm - LongPickupThresholdKm) * LongPickupRatePerKm)
```

Pickup compensation does not alter `Booking.EstimatedFare`, `GrossFare`, or
`CustomerPayable`. It is non-commissionable and platform-funded when payable.
The accepted offer snapshot does not, by itself, define the payable event.
`Trip.StartedAt` is the durable earning boundary: once the trip has reached
`IN_PROGRESS`, an otherwise-valid customer-requested early stop preserves the
accepted long-pickup compensation. A trip that never starts does not earn it.

## Driver settlement

`TripFinancialSettlementService` creates one durable snapshot per trip and
`TripCommissionCalculator` applies the versioned Risk Protection policy:

```text
GrossFare = FareComponent + LongDistanceComponent
CommissionBase = FareComponent
GrossPlatformCommission = RoundVnd(FareComponent * PlatformCommissionRate)
DriverFareEarning = FareComponent - GrossPlatformCommission
LongDistanceEarning = LongDistanceComponent
DriverPayout = DriverFareEarning + LongDistanceEarning + LongPickupCompensation
```

Long-distance earning and long-pickup compensation are both non-commissionable.
Platform-funded promotion changes customer payable and platform net commission;
it does not reduce the driver's earned components. QR and cash wallet effects
use the stored `DriverPayout` and durable settlement-effect identities to prevent
duplicate credits/debits on ordinary retries.

## Platform and Risk Fund

Risk contribution uses the existing versioned `RiskProtectionPolicyVersion` and
existing `RiskFundLedgerService`. It is platform-side accounting derived from
eligible non-negative net platform commission. It does not reduce
`DriverPayout`. Long-pickup compensation is not a Risk Fund claim payout.
Accident liability, claim funding, and claim recovery remain separate from
ordinary trip earning settlement, and driver liability is not automatically
deducted from the driver wallet.

## Trip end reasons

| Reason | Current financial direction |
|---|---|
| `NORMAL_COMPLETION` | Locked V1 `Booking.EstimatedFare` after destination validation; hourly bookings use their locked duration-based estimate. |
| `CUSTOMER_REQUESTED_STOP` | Below 50% original-route progress: deterministic progress fare with `AcceptedMinimumServiceFare` floor. At or above 50% (inclusive): full locked V1 `Booking.EstimatedFare` and full accepted fare components. Ends directly in `WAITING_PAYMENT` without Staff approval. |
| `DRIVER_UNABLE_TO_CONTINUE` | Deterministic zero-fare operational end; goes directly to `WAITING_PAYMENT`, forces the driver offline, and does not require Staff approval. |
| `STARTED_BY_MISTAKE` | Operationally ends immediately in `WAITING_PAYMENT`, but leaves fare fields null and creates a pending Staff reconciliation request. Payment and settlement stay blocked until Staff approves the existing zero-fare rule. |
| `SYSTEM_ERROR` | Rejected with reconciliation-required conflict; no staff reconciliation endpoint is currently present. |
| `VEHICLE_SAFETY_ISSUE` | Must use the Safety/Risk Protection termination workflow. |
| `SAFETY_TERMINATION` | Delegates to the existing Safety/Risk Protection reconciliation subsystem. |

`TripEndReason` is the detailed end-reason taxonomy. The broader
`TripTerminationCategory` remains only as the standard-versus-safety lifecycle
classification used by Risk Protection compatibility.

Trip lifecycle, fare authority, exceptional reconciliation, and driver
availability are separate concerns. `CanContinueWorking` is an availability
choice on the end-trip request, not a substitute trip-end reason. When false,
the server persists `DriverWorkStatus.Offline` and removes the driver from Redis
online/status/location and GEO matching state. That offline choice survives the
later payment, return-confirmation, and `COMPLETED` transitions. When true, the
driver remains `Busy` until normal completion releases them back to `Online`.
`DRIVER_UNABLE_TO_CONTINUE` remains an explicit end reason: the driver client
sends that reason and forces `CanContinueWorking = false`, so ending near the
pickup does not accidentally use `NORMAL_COMPLETION` destination validation.
Choosing Offline after another valid end reason changes availability only; it
does not rewrite the financial end reason.

A pending exceptional reconciliation never keeps the trip `IN_PROGRESS`.
Operational timestamps and tracking evidence are captured at the request time,
while the authoritative fare remains unset. A rejected request also remains
financially unresolved: payment code refuses to fall back to an estimate or
advance settlement while the ended trip has no finalized fare. The driver may
submit a new reconciliation request without reactivating the trip.

## Driver opt-in and maximum-distance rules

`AcceptLongPickupTrips` and `AcceptLongDistanceTrips` are independent and
default to false for existing drivers. Distance bookings above the accepted
long-distance opt-in threshold require the long-distance preference. Routed
pickup distance above the configured pickup opt-in threshold requires the
long-pickup preference. Matching and assignment recheck eligibility.

Distance bookings above `MaximumTripDistanceKm` are rejected and defensively
ignored by matching. Hourly bookings do not use long-distance component,
long-distance opt-in, or maximum-distance rules; long pickup can still apply.

## Legacy V0 compatibility

Bookings with `PricingSnapshotVersion` null or zero stay on the isolated legacy
actual-distance compatibility calculator. The system does not fabricate a V1
snapshot from today's mutable pricing or surge rules, and historical terminal
bookings are not repriced or backfilled. `Trip.ActualFare` remains the compatible
database field name; for V1 it means finalized gross fare before promotion.

## Rounding and scope boundary

Distances, progress, rates, and multipliers retain decimal precision during
calculation. Each persisted monetary component is rounded to whole VND with
`MidpointRounding.AwayFromZero`, and totals are composed from the rounded
components.

Zone, remote-area, out-of-zone, return-home, intercity, customer long-pickup
surcharge, destination-change pricing, and quote-token architecture are
intentionally outside the approved scope.
