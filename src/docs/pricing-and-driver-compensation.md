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
finalized progress is monotonic:

```text
CandidateProgress = Project(CurrentLocation, Booking.RoutePolyline)
PlannedRouteProgress = max(PreviousPlannedRouteProgress, CandidateProgress)
GrossFare = max(
    RoundVnd(Booking.EstimatedFare * PlannedRouteProgress),
    AcceptedMinimumServiceFare)
```

The distance actually driven and an active/rerouted polyline are not early-stop
pricing authorities. The finalized component allocation uses the same shared
calculation used by settlement; the resulting persisted settlement always keeps
`GrossFare = FareComponent + LongDistanceComponent`.

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

Important unresolved case: SafeRide still permits QR prepayment before a trip
starts. If a later customer-requested stop reduces `GrossFare`, current code does
not reconcile a successful prepayment amount against the finalized
`CustomerPayable`. A business-approved normal-trip refund/reconciliation policy
is required before that path is capstone-ready.

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
The offer snapshot does not, by itself, define the payable event. Current
settlement pays it only for `NORMAL_COMPLETION`; eligibility for early stop,
failed start, cancellation, and no-show still requires an approved business
decision.

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
| `CUSTOMER_REQUESTED_STOP` | Original-route progress with `AcceptedMinimumServiceFare` floor. |
| `DRIVER_UNABLE_TO_CONTINUE` | Current client-selectable path produces zero fare; evidence/authorized reconciliation is not yet implemented. |
| `STARTED_BY_MISTAKE` | Current client-selectable path produces zero fare; controlled authorization/rollback is not yet implemented. |
| `SYSTEM_ERROR` | Rejected with reconciliation-required conflict; no staff reconciliation endpoint is currently present. |
| `VEHICLE_SAFETY_ISSUE` | Must use the Safety/Risk Protection termination workflow. |
| `SAFETY_TERMINATION` | Delegates to the existing Safety/Risk Protection reconciliation subsystem. |

`TripEndReason` is the detailed end-reason taxonomy. The broader
`TripTerminationCategory` remains only as the standard-versus-safety lifecycle
classification used by Risk Protection compatibility.

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
