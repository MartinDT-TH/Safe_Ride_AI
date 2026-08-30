import { beforeEach, describe, expect, it, vi } from 'vitest';
import { ApiError, apiRequest } from './apiClient';

describe('API ProblemDetails errors', () => {
  beforeEach(() => vi.restoreAllMocks());

  it('preserves status, backend code, detail, and trace id', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(new Response(JSON.stringify({
      status: 409,
      code: 'risk_protection.recovery_payer_mismatch',
      detail: 'Payer không khớp với tài xế.',
      traceId: 'trace-123',
    }), { status: 409, headers: { 'Content-Type': 'application/problem+json' } })));

    await expect(apiRequest('/staff/claims/1/recoveries')).rejects.toMatchObject({
      status: 409,
      code: 'risk_protection.recovery_payer_mismatch',
      message: 'Payer không khớp với tài xế.',
      detail: 'Payer không khớp với tài xế.',
      traceId: 'trace-123',
    });
  });

  it('remains backwards compatible with the two-argument constructor', () => {
    expect(new ApiError('failed', 400)).toMatchObject({
      message: 'failed', status: 400, detail: 'failed',
    });
  });
});
