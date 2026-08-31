import '../../../../l10n/generated/app_localizations.dart';

String accidentStatusLabel(AppLocalizations l10n, String value) =>
    switch (value) {
      'REPORTED' => l10n.riskStatusReported,
      'EVIDENCE_COLLECTION' => l10n.riskStatusEvidenceCollection,
      'UNDER_REVIEW' => l10n.riskStatusUnderReview,
      'LIABILITY_PENDING' => l10n.riskStatusLiabilityPending,
      'SETTLEMENT' => l10n.riskStatusSettlement,
      'CLOSED' => l10n.riskStatusClosed,
      'REJECTED' => l10n.riskStatusRejected,
      _ => l10n.statusPending,
    };

String accidentCategoryLabel(AppLocalizations l10n, String value) =>
    switch (value) {
      'DRIVER_INJURY' => l10n.riskCategoryDriverInjury,
      'CUSTOMER_VEHICLE_DAMAGE' => l10n.riskCategoryCustomerVehicleDamage,
      'THIRD_PARTY_DAMAGE' => l10n.riskCategoryThirdPartyDamage,
      'MULTIPLE' => l10n.riskCategoryMultiple,
      _ => l10n.riskCategoryMultiple,
    };

String driverFaultLevelLabel(AppLocalizations l10n, String value) =>
    switch (value) {
      'NO_FAULT' => l10n.riskFaultNoFault,
      'ORDINARY_NEGLIGENCE' => l10n.riskFaultOrdinary,
      'GROSS_NEGLIGENCE' => l10n.riskFaultGross,
      'INTENTIONAL_MISCONDUCT' => l10n.riskFaultIntentional,
      _ => l10n.statusPending,
    };

String assessmentStatusLabel(AppLocalizations l10n, String value) =>
    switch (value) {
      'DRAFT' => l10n.riskAssessmentDraft,
      'PENDING_CONFIRMATION' => l10n.riskAssessmentPendingConfirmation,
      'CONFIRMED' => l10n.riskAssessmentConfirmed,
      'DISPUTED' => l10n.riskAssessmentDisputed,
      _ => l10n.statusPending,
    };

String claimStatusLabel(AppLocalizations l10n, String? value) =>
    switch (value) {
      'DRAFT' => l10n.riskAssessmentDraft,
      'UNDER_REVIEW' => l10n.riskStatusUnderReview,
      'APPROVED' => l10n.riskClaimApproved,
      'PENDING_FUNDING' => l10n.riskClaimPendingFunding,
      'FUNDED' => l10n.riskClaimFunded,
      'RECOVERY_IN_PROGRESS' => l10n.riskClaimRecovery,
      'SETTLED' => l10n.riskClaimSettled,
      'CLOSED' => l10n.riskStatusClosed,
      'REJECTED' => l10n.riskStatusRejected,
      _ => l10n.statusPending,
    };

String driverLiabilityStatusLabel(AppLocalizations l10n, String value) =>
    switch (value) {
      'CONFIRMED' => l10n.riskAssessmentConfirmed,
      'DISPUTED' => l10n.riskAssessmentDisputed,
      'PARTIALLY_PAID' => l10n.riskLiabilityPartiallyPaid,
      'PAID' => l10n.riskLiabilityPaid,
      'WAIVED' => l10n.riskLiabilityWaived,
      _ => l10n.statusPending,
    };

String participantLabel(AppLocalizations l10n, String value) => switch (value) {
  'DRIVER' => l10n.riskRoleDriver,
  'CUSTOMER' => l10n.riskRoleCustomer,
  'THIRD_PARTY' => l10n.riskRoleThirdParty,
  'VEHICLE' => l10n.riskRoleVehicle,
  'OBJECTIVE' => l10n.riskRoleObjective,
  _ => l10n.statusPending,
};

String safetyReasonLabel(AppLocalizations l10n, String value) =>
    switch (value) {
      'BRAKE_FAILURE' => l10n.brakeResponse,
      'LIGHT_FAILURE' => l10n.frontRearLights,
      'TIRE_FAILURE' => l10n.visibleTires,
      'STEERING_FAILURE' => l10n.otherVehicleFault,
      'ENGINE_FAILURE' => l10n.dashboardWarning,
      'ELECTRICAL_FAILURE' => l10n.turnSignals,
      'DISTRACTING' => l10n.riskReasonDistracting,
      'VIOLENT' => l10n.riskReasonViolent,
      'INTERFERING_WITH_VEHICLE' => l10n.riskReasonInterferingVehicle,
      'UNSAFE_REQUEST' => l10n.riskReasonUnsafeRequest,
      _ => l10n.riskReasonOther,
    };
