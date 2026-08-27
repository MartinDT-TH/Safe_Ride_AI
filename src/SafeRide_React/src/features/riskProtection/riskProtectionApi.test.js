import { beforeEach, describe, expect, it, vi } from 'vitest';

const { apiRequest, apiDownload } = vi.hoisted(() => ({
  apiRequest: vi.fn(),
  apiDownload: vi.fn(),
}));

vi.mock('../../shared/api/apiClient', () => ({ apiRequest, apiDownload }));

import {
  buildQueryPath,
  confirmManualRefund,
  exportRiskFundTransactions,
  fundClaim,
  isEntireRiskFundRequestPermanent,
  reconcilePartyCauses,
  saveLiabilityAssessment,
} from './riskProtectionApi';

describe('risk protection API contracts', () => {
  beforeEach(() => vi.clearAllMocks());

  it('builds deterministic filtered query strings', () => {
    expect(buildQueryPath('/staff/accidents', {
      status: 'UNDER_REVIEW', category: '', tripId: 42,
    })).toBe('/staff/accidents?status=UNDER_REVIEW&tripId=42');
  });

  it('sends rowversion for liability updates', async () => {
    apiRequest.mockResolvedValue({ id: 9 });
    await saveLiabilityAssessment(7, { rowVersion: 'AQID', driverFaultPercentage: 20 });
    expect(apiRequest).toHaveBeenCalledWith('/staff/accidents/7/liability-assessment', {
      method: 'PUT',
      body: JSON.stringify({ rowVersion: 'AQID', driverFaultPercentage: 20 }),
    });
  });

  it('uses an idempotency header for claim funding', async () => {
    await fundClaim(8, 'fund-8', 'AQID');
    expect(apiRequest).toHaveBeenCalledWith('/staff/claims/8/approve-funding', {
      method: 'POST', headers: { 'Idempotency-Key': 'fund-8' },
      body: JSON.stringify({ rowVersion: 'AQID' }),
    });
  });

  it('does not hydrate a mixed advance as an entirely permanent fund loss', () => {
    expect(isEntireRiskFundRequestPermanent({
      riskFundAdvanceAmount: 3000000,
      riskFundPermanentLossAmount: 1000000,
    })).toBe(false);
    expect(isEntireRiskFundRequestPermanent({
      riskFundAdvanceAmount: 0,
      riskFundPermanentLossAmount: 1000000,
    })).toBe(true);
  });

  it('preserves multiple causes instead of inventing a replacement allocation', () => {
    const causes = [
      { rootCause: 'CUSTOMER_INTERFERENCE', responsibleParty: 'CUSTOMER', percentage: 30 },
      { rootCause: 'UNKNOWN', responsibleParty: 'CUSTOMER', percentage: 20 },
      { rootCause: 'ROAD_CONDITION', responsibleParty: 'OBJECTIVE', percentage: 50 },
    ];

    expect(reconcilePartyCauses(causes, 'CUSTOMER', 60, 'CUSTOMER_INTERFERENCE')).toEqual(causes);
    expect(reconcilePartyCauses(causes, 'OBJECTIVE', 40, 'UNKNOWN')).toEqual([
      causes[0], causes[1], { ...causes[2], percentage: 40 },
    ]);
    expect(reconcilePartyCauses(causes, 'DRIVER', 10, 'DRIVER_ERROR')).toEqual([
      ...causes,
      { rootCause: 'DRIVER_ERROR', responsibleParty: 'DRIVER', percentage: 10 },
    ]);
    expect(reconcilePartyCauses(causes, 'CUSTOMER', 0, 'CUSTOMER_INTERFERENCE')).toEqual([
      causes[2],
    ]);
  });

  it('confirms manual refunds through the staff endpoint', async () => {
    const payload = { paymentReference: 'RF-1', evidenceUrl: 'https://e.test/1', idempotencyKey: 'refund-1', rowVersion: 'AQID' };
    await confirmManualRefund(3, payload);
    expect(apiRequest).toHaveBeenCalledWith('/staff/payments/refunds/3/confirm', {
      method: 'POST', body: JSON.stringify(payload),
    });
  });

  it('exports the exact filtered ledger path', async () => {
    await exportRiskFundTransactions({ type: 'CLAIM_ADVANCE', fromUtc: '2026-08-01T00:00:00Z' });
    expect(apiDownload).toHaveBeenCalledWith('/admin/risk-fund/transactions/export?type=CLAIM_ADVANCE&fromUtc=2026-08-01T00%3A00%3A00Z');
  });
});
