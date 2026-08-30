# SafeRide Insurance and Risk Protection Guide

This document is the repository source of truth for the Risk Protection workflow
implemented by the current source. The backend owns liability, policy-snapshot,
claim, funding, recovery, reconciliation, and ledger decisions. Flutter and React
collect permitted inputs and present server results; they do not recalculate the
financial outcome.

This guide supersedes conclusions from older user-guide copies. In particular:

- `CUSTOMER_INTOXICATION` is not an `AccidentRootCause`. Intoxication may be
  context or evidence, but customer fault requires causal conduct.
- customer-owned insurance is optional and external. Its confirmed claim
  contribution is recorded at accident settlement time; it is not the authority
  that enables SafeRide System Insurance.
- only SafeRide System Insurance is simulated by `MockInsuranceProvider`.
- accident evidence is camera-first on mobile. Pre-trip and safety-termination
  evidence also make the camera primary and offer Gallery as a secondary path.

## Overview

SafeRide Vehicle Protection is included by default for eligible trips when the
effective Risk Protection policy enables it. There are no Basic, Standard, or
Premium protection tiers in this capstone.

Risk Protection combines four separate concerns:

1. an immutable trip coverage and policy snapshot;
2. evidence-based responsibility assessment;
3. a server-calculated claim and funding/recovery workflow;
4. an immutable internal Risk Fund ledger.

Responsibility does not itself identify the payment source. A party can be at
fault while the Risk Fund temporarily advances money, and a later recovery can
replenish the fund.

## Roles

### Customer

- may keep legacy vehicle insurance records under Profile, My Vehicles,
  Insurance, but no policy registration or verification is required for Risk
  Protection settlement;
- reports an accident from an active trip;
- captures accident evidence with the rear camera;
- reads the localized case summary, responsibility result, and protection
  outcome;
- can request review of a confirmed assessment when evidence exists.

### Driver

- performs the pre-trip safety check before starting an eligible trip;
- reports structured unsafe-customer or vehicle-issue reasons plus a human note;
- reports accidents and captures evidence;
- reads the same participant-safe accident summary;
- opens My liabilities from Profile to see confirmed amounts and recovery history.

### Staff

The React sidebar entry is **Tai nạn & Trách nhiệm**. The page is **Hồ sơ sự cố
& bảo vệ** and presents five stages:

1. **Bằng chứng & thông tin sự cố**
2. **Nguyên nhân**
3. **Phân bổ trách nhiệm**
4. **Nhập thiệt hại**
5. **Rà soát & thực hiện**

Staff chooses evidence-supported causes and responsibility. The five buckets
must total 100 percent. In step 4 Staff enters total/eligible damage and, when
available, the confirmed contribution from the Customer's external insurer.
The server derives both insurance allocation and the Risk Fund split. Funding,
recovery, write-off, and closure remain server-validated and audited.

### Admin

The React sidebar entry is **Risk Fund**. The **Quỹ rủi ro SafeRide** page has:

- **Tổng quan** for balance, contributions, advances, permanent support,
  recoveries, outstanding exposure, and pending cases;
- **Sổ cái** for readable transaction history, filters, references, and CSV
  export;
- **Cài đặt chính sách** for the current effective version and immutable history.

Opening balance, audited adjustment, and creation of a policy version are under
**Thao tác quản trị nâng cao & có kiểm toán** and require explicit confirmation.

## Normal trip flow

1. The assigned Driver completes the pre-trip safety checklist.
2. The latest check must be `PASS`. It is a reasonable visible safety check, not
   a mechanical inspection or warranty that hidden defects do not exist.
3. On the `ARRIVED -> IN_PROGRESS` transition, the server creates at most one
   `TripProtectionCoverage` for the trip.
4. Coverage references the immutable effective Risk Protection policy version.
   Its `MockInsuranceCoverageLimit` is the historical SafeRide System Insurance
   limit. A legacy Customer `PHYSICAL_DAMAGE` snapshot may also be retained for
   compatibility, but it is not settlement authority.
5. Normal trip lifecycle and fare settlement continue under the ordinary trip
   rules.
6. An eligible completed trip creates at most one Risk Fund contribution from
   non-negative platform-side net commission. It is not a hidden Driver deduction.

The coverage snapshot and policy version remain authoritative even if a current
policy or vehicle-insurance record changes later.

## Accident and claim flow

```text
Report accident
  -> capture/upload evidence
  -> Staff confirms cause and responsibility
  -> record optional confirmed Customer external-insurance contribution
  -> server calculates remaining Customer/Driver exposure
  -> SafeRide System Insurance (MockInsuranceProvider) review
  -> Risk Fund funding
  -> recoveries or audited write-off
  -> reconciliation
  -> close claim and accident
```

Accident evidence accepts validated JPEG, PNG, or WebP images from the mobile UI.
The backend evidence contract also supports PDF for appropriate API clients.
Filename, MIME type, size, magic bytes, and scanner policy are validated on the
server. Participant UI does not expose internal concurrency metadata or detailed
accounting reconciliation fields.

## Responsibility model

An assessment has five coexisting buckets:

- Driver
- Customer
- Third Party
- Vehicle
- Objective

Their sum must be exactly 100 percent. Cause percentages must also total 100 and
match the corresponding responsibility bucket. Examples of valid causal values
include `DRIVER_ERROR`, `CUSTOMER_INTERFERENCE`, `THIRD_PARTY_ERROR`,
`VEHICLE_MECHANICAL_FAILURE`, `VEHICLE_PRE_EXISTING_DEFECT`, road, weather, and
force-majeure causes.

Important distinctions:

- fault percentage is not payment percentage;
- vehicle fault does not automatically create Customer fault;
- Customer intoxication alone does not create Customer fault;
- Driver fault is zero if and only if the Driver fault level is `NO_FAULT`;
- a hidden pre-existing defect can be assigned to Vehicle with
  `NEITHER_COULD_REASONABLY_KNOW`;
- a defect known and concealed by the Customer can be assigned to Customer with
  `CUSTOMER_KNEW`, when supported by evidence.

## Customer-owned vehicle insurance

`MANDATORY_TPL` is mandatory third-party liability insurance. It is not treated
as coverage for damage to the customer's own vehicle.

Legacy `VehicleInsurancePolicy`, `PHYSICAL_DAMAGE`, and `MANDATORY_TPL` APIs are
retained because profile/vehicle functionality may still use them. They are not
required to activate Risk Protection and are not the authority for claim
settlement. No destructive cleanup is part of this phase.

Customer insurance is optional and external to SafeRide. Staff records only the
confirmed financial result relevant to the claim:

- `CustomerInsuranceAppliedAmount` (authoritative amount);
- optional `CustomerInsuranceReference`;
- server timestamp `CustomerInsuranceConfirmedAtUtc`;
- optional `CustomerInsuranceNote`.

The amount must be non-negative and cannot exceed `CustomerGrossExposure`.
SafeRide does not mock the Customer insurer, manage its approval lifecycle, or
redistribute any excess contribution to Driver or another fault category.

## Insurance-first settlement waterfall

The server applies this exact order:

1. validate and round `EligibleDamageAmount` within the documented total damage;
2. allocate gross exposure by confirmed Driver, Customer, Third Party, Vehicle,
   and Objective percentages using deterministic whole-VND rounding;
3. apply `CustomerInsuranceAppliedAmount` only to `CustomerGrossExposure`;
4. calculate the remaining Customer + Driver participant exposure;
5. cap SafeRide System Insurance by that participant exposure and the immutable
   `MockInsuranceCoverageLimit` from the trip's policy version;
6. allocate an approved System Insurance amount proportionally across remaining
   Customer and Driver exposure on the server;
7. apply the snapshotted Driver rate/cap only to Driver exposure remaining after
   both insurance layers;
8. derive the recoverable Risk Fund advance from Driver, Customer, Third Party,
   and, for reimbursement-to-fund claims, insurer recovery capacity;
9. classify the remaining protected funding as permanent SafeRide support.

Mandatory example: for VND 10,000,000 eligible damage at Customer 70% / Driver
30%, gross exposure is 7,000,000 / 3,000,000. Customer insurance of 6,000,000
leaves 1,000,000 / 3,000,000. SafeRide System Insurance of 2,000,000 is allocated
500,000 / 1,500,000, leaving 500,000 / 1,500,000. At a 50% Driver rate, personal
Driver liability is 750,000. Customer insurance + System Insurance + remaining
party/Risk Fund handling reconciles to 10,000,000 without double compensation.

`TripProtectionCoverage.ProtectionLimit` caps only SafeRide/Risk Fund funding.
It does not reduce Customer or Third Party economic responsibility. A provider
recovery reimburses an earlier Risk Fund advance and never increases claimant
compensation. Direct insurance plus Risk Fund claimant funding is checked against
the same eligible loss. A funded or recovery-started claim cannot be recalculated.

## Mock insurance lifecycle and Staff review

The mock provider represents only SafeRide System Insurance. It exposes the
server-calculated `MaximumApprovableInsuranceAmount` and the same value as the
recommended approval. The maximum is bounded by remaining Customer/Driver
exposure after Customer insurance and by the immutable System Insurance limit in
the trip's Risk Protection policy version. Customer `PHYSICAL_DAMAGE` snapshots
do not enable or cap it. Staff cannot submit a maximum of their own.

The lifecycle is `NOT_SUBMITTED` -> `PENDING` -> `APPROVED` or `REJECTED`.
Auto-approved submissions are final and cannot be reviewed again. While pending,
Staff can approve the recommendation, approve a positive lower amount only with a
reason, or reject with a reason. The provider-issued reference is retained for
the lifecycle; Staff does not replace it with a free-form reference.

Payment destination is inferred for normal requests: pending review uses direct
claimant payment, while an insurer recovery can reimburse only an actually
funded Risk Fund exposure. Every calculate, submit, status, approve, and reject
operation is recorded in the provider audit payload with the maximum, result,
reason when applicable, reference, actor, and timestamp. No document binary is
stored in that audit.

For this capstone simulation, an `APPROVED` direct-to-claimant Mock Insurance
amount is treated as the simulated System Insurance payment in settlement. If
Risk Fund already advanced that amount, the later cash receipt is instead an
`INSURANCE_RECOVERY` credit and is never counted as a second claimant payment.

## Risk Fund accounting

The Risk Fund is SafeRide's internal fund. Its ledger is append-only and separates:

- `CONTRIBUTION`: platform-side contribution from an eligible completed trip;
- `CLAIM_ADVANCE`: temporary claim funding expected to be recoverable;
- `CLAIM_PAYOUT`: final/permanent support;
- Driver, Customer, Third Party, and Insurance recoveries;
- audited opening balance and adjustments.

An advance is not automatically a permanent SafeRide loss. Recovery can come
from Driver, Customer, Third Party, or Insurance. The same idempotency key and
payload replay the existing result; conflicting reuse is rejected. Insufficient
balance creates no partial debit or negative balance and leaves the claim waiting
for funding.

Writing off an unrecoverable advance creates an immutable reconciliation record.
It does not debit the Risk Fund a second time. A claim closes only after funding,
insurance, recoveries/write-off, disputes, and actual fund exposure reconcile.

## Mock Insurance Provider

The Mock Insurance Provider is the capstone SafeRide System Insurance simulation.
It demonstrates
submission, pending review, approval/rejection, limits, payment destination, and
immutable provider audit records.

It does **not** mean that:

- SafeRide is a licensed insurer;
- a real external insurance policy has been issued;
- a real insurer API is connected;
- the simulation constitutes legal insurance coverage.

The abstraction is intended to allow a future provider implementation without
redesigning the core claim workflow.

## Driver liability

The server derives Driver gross exposure from confirmed fault, allocates
SafeRide System Insurance after Customer insurance, then applies the rate and cap
snapshotted by the trip policy to the Driver's remaining exposure:

- `NO_FAULT`: no Driver liability;
- `ORDINARY_NEGLIGENCE`: configured ordinary-negligence rate and cap;
- `GROSS_NEGLIGENCE`: configured gross-negligence rate and cap;
- `INTENTIONAL_MISCONDUCT`: up to Driver-attributable residual damage.

Driver liability never silently deducts `DriverWallet`. Staff records money only
after it is actually received, with payer/payment references and trusted evidence.

## Six verified demo scenarios

### A. Driver ordinary negligence

Set Driver responsibility to 100 percent, cause to Driver error, and level to
**Sơ suất thông thường**. With 50,000,000 VND eligible damage and the regression
snapshot's 20 percent rate/2,000,000 VND cap, confirmed Driver liability is
2,000,000 VND. The remaining requested Risk Fund amount is classified separately;
DriverWallet remains unchanged.

Automated regression:
`BusinessScenarioA_OrdinaryDriverNegligence_AppliesSnapshotRateAndCapWithoutWalletDeduction`.

### B. Customer directly interferes

Set Customer to 100 percent with **Khách hàng can thiệp việc điều khiển xe** and
Driver to zero/**Không có lỗi**. A Risk Fund advance creates Customer-recoverable
exposure. Recording the actual Customer payment creates one `CUSTOMER_RECOVERY`
credit; Driver liability and DriverWallet remain unchanged.

Automated regression:
`BusinessScenarioB_CustomerInterference_CanBeRecoveredFromCustomerWithoutDriverLiability`.

### C. Customer intoxicated but passive

Treat intoxication only as context/evidence. For a third-party collision, set
Third Party to 100 percent and both Customer and Driver to zero. There is no
`CUSTOMER_INTOXICATION` root cause to select.

Automated regression:
`BusinessScenarioC_PassiveCustomerIntoxication_CannotCreateFaultAndThirdPartyCanBeFullyResponsible`.

### D. Third party 100 percent at fault

Set Third Party to 100 percent with **Lỗi từ bên thứ ba**. A Risk Fund advance is
recoverable rather than automatically permanent. Recording and retrying the same
third-party recovery replenishes the fund exactly once.

Automated regression:
`BusinessScenarioD_ThirdPartyFault_RiskFundAdvanceIsRecoverableAndReplenishedExactlyOnce`.

### E. Hidden brake failure

Represent the latent pre-existing defect as Vehicle responsibility with
`VEHICLE_PRE_EXISTING_DEFECT` and **Không bên nào có thể phát hiện hợp lý**.
Driver and Customer remain zero. With no recoverable human/third-party obligation,
the tested Risk Fund request is final support rather than an artificial recovery.

Automated regression:
`BusinessScenarioE_LatentBrakeDefect_AssignsVehicleWithoutInventingHumanFault`.

### F. Customer knowingly concealed a defect

Assign `VEHICLE_PRE_EXISTING_DEFECT` to Customer with `CUSTOMER_KNEW`, supported
by evidence. Driver remains zero/**Không có lỗi**. The Customer obligation can
support a recoverable Risk Fund advance without blaming the Driver.

Automated regression:
`BusinessScenarioF_ConcealedKnownDefect_AssignsCustomerWithoutBlamingDriver`.

## Staff and Admin UI guide

For Staff:

1. Open **Tai nạn & Trách nhiệm** and select **Mở hồ sơ**.
2. Review evidence and police reference in step 1.
3. Select friendly cause, awareness, and Driver fault labels in step 2.
4. Enter the five responsibility buckets in step 3. Both displayed totals must
   be 100 percent before **Xác nhận trách nhiệm** is enabled.
5. In step 4 enter documented total and eligible damage. Record the optional
   confirmed Customer external-insurance amount/reference/note; do not approve or
   reject that external insurer. Then choose **Yêu cầu máy chủ tính đề xuất**.
   The adjacent waterfall is authoritative.
6. In step 5 review causes, responsibilities, insurance, liabilities, recoveries,
   and Risk Fund exposure. Use **Cấp kinh phí / thử lại cấp kinh phí** only when
   enabled.
7. Open **Thao tác kế toán nâng cao & kiểm toán** only for SafeRide System
   Insurance (`MockInsuranceProvider`) review,
   actual recovery, audited write-off, or closure. Confirm the impact prompt.

For Admin:

1. Open **Risk Fund**.
2. Use **Tổng quan** for balance and operational counts.
3. Use **Sổ cái** for immutable transaction evidence and Trip/Claim references.
4. Use **Cài đặt chính sách** to show the effective version and history.
5. Use the advanced/audited section only for initial balance, documented
   correction, or a new future-effective policy version. Historical versions are
   never edited.

## Troubleshooting

- **Pre-trip pass required**: complete every visible checklist item and submit a
  passing check before starting the trip. A failed attempt remains in audit history.
- **Invalid responsibility request**: verify both totals are 100 percent, each
  cause matches its party bucket, and Driver level matches the Driver percentage.
- **Awareness mismatch**: `CUSTOMER_KNEW`, `DRIVER_KNEW`, `BOTH_KNEW`, or
  `NEITHER_COULD_REASONABLY_KNOW` must match who owns the pre-existing-defect cause.
- **Concurrency conflict**: another Staff operator changed the case. The UI
  reloads the latest case; review it before retrying.
- **Waiting for funding**: the Risk Fund has insufficient balance for an
  all-or-nothing debit. No partial or negative transaction was created. Add only
  an authorized audited funding source, then retry.
- **Recovery rejected**: funding must have happened, evidence is required, and
  recovery cannot exceed payer obligation or actual outstanding fund exposure.
- **Claim cannot close**: unresolved recovery, write-off, insurance reimbursement,
  dispute, or ledger exposure remains. Reconcile the material amount first.
- **SafeRide System Insurance maximum is zero**: check the snapshotted Risk
  Protection policy's `MockInsuranceCoverageLimit` and whether any Customer or
  Driver participant exposure remains. Customer policy registration is not a
  prerequisite.

See [the capstone demo checklist](risk-protection-demo-checklist.md) for the
operator-ready sequence.
