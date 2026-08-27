import { apiDownload, apiRequest } from '../../shared/api/apiClient';

export const riskFundDashboardPath = '/admin/risk-fund';
export const riskFundTransactionsPath = '/admin/risk-fund/transactions';
export const riskFundExportPath = '/admin/risk-fund/transactions/export';
export const riskProtectionConfigurationPath = '/admin/risk-protection/configuration';
export const riskProtectionVersionsPath = '/admin/risk-protection/configuration/versions';
export const staffAccidentsPath = '/staff/accidents';
export const staffRefundsPath = '/staff/payments/refunds';

export function buildQueryPath(path, filters = {}) {
  const query = new URLSearchParams();
  Object.entries(filters).forEach(([key, value]) => {
    if (value !== '' && value !== null && value !== undefined) query.set(key, value);
  });
  const suffix = query.toString();
  return suffix ? `${path}?${suffix}` : path;
}

export function getAccident(accidentId) {
  return apiRequest(`/accidents/${accidentId}`);
}

export function saveLiabilityAssessment(accidentId, payload) {
  return apiRequest(`/staff/accidents/${accidentId}/liability-assessment`, {
    method: 'PUT', body: JSON.stringify(payload),
  });
}

export function createOpeningBalance(payload) {
  return apiRequest('/admin/risk-fund/opening-balance', {
    method: 'POST', body: JSON.stringify(payload),
  });
}

export function createRiskFundAdjustment(payload) {
  return apiRequest('/admin/risk-fund/adjustments', {
    method: 'POST', body: JSON.stringify(payload),
  });
}

export function exportRiskFundTransactions(filters) {
  return apiDownload(buildQueryPath(riskFundExportPath, filters));
}

export function createRiskProtectionConfiguration(payload) {
  return apiRequest(riskProtectionConfigurationPath, {
    method: 'PUT', body: JSON.stringify(payload),
  });
}

export function confirmLiabilityAssessment(accidentId, payload) {
  return apiRequest(`/staff/accidents/${accidentId}/liability-assessment/confirm`, {
    method: 'POST', body: JSON.stringify(payload),
  });
}

export function calculateClaim(claimId, payload) {
  return apiRequest(`/staff/claims/${claimId}/calculate`, {
    method: 'POST', body: JSON.stringify(payload),
  });
}

export function reviewMockInsurance(claimId, approve, payload) {
  return apiRequest(`/staff/claims/${claimId}/mock-insurance/${approve ? 'approve' : 'reject'}`, {
    method: 'POST', body: JSON.stringify(payload),
  });
}

export function getMockInsuranceAudits(claimId) {
  return apiRequest(`/staff/claims/${claimId}/mock-insurance/audits`);
}

export function fundClaim(claimId, idempotencyKey, rowVersion) {
  return apiRequest(`/staff/claims/${claimId}/approve-funding`, {
    method: 'POST', headers: { 'Idempotency-Key': idempotencyKey },
    body: JSON.stringify({ rowVersion }),
  });
}

export function recordClaimRecovery(claimId, payload) {
  const body = new FormData();
  ['sourceType', 'payerReference', 'amount', 'paymentReference', 'rowVersion'].forEach((key) => body.set(key, payload[key]));
  body.set('evidence', payload.evidence);
  return apiRequest(`/staff/claims/${claimId}/recoveries`, {
    method: 'POST', headers: { 'Idempotency-Key': payload.idempotencyKey }, body,
  });
}

export function writeOffClaimAdvance(claimId, payload) {
  const body = new FormData();
  ['amount', 'reason', 'rowVersion'].forEach((key) => body.set(key, payload[key]));
  body.set('evidence', payload.evidence);
  return apiRequest(`/staff/claims/${claimId}/write-offs`, {
    method: 'POST', headers: { 'Idempotency-Key': payload.idempotencyKey }, body,
  });
}

export function closeClaim(claimId, rowVersion) {
  return apiRequest(`/staff/claims/${claimId}/close`, {
    method: 'POST', body: JSON.stringify({ rowVersion }),
  });
}

export function confirmManualRefund(refundId, payload) {
  return apiRequest(`/staff/payments/refunds/${refundId}/confirm`, {
    method: 'POST', body: JSON.stringify(payload),
  });
}

export function formatVnd(value) {
  return new Intl.NumberFormat('vi-VN', {
    style: 'currency', currency: 'VND', maximumFractionDigits: 0,
  }).format(Number(value ?? 0));
}

export function createIdempotencyKey(prefix) {
  return `${prefix}-${globalThis.crypto?.randomUUID?.() ?? Date.now()}`;
}
