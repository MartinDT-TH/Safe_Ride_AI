import { render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

import {
  buildSettlementRecommendation,
  confirmRiskAction,
  responsibilityTotal,
  riskProtectionLabel,
  SettlementRecommendation,
} from './riskProtectionPresentation';

describe('risk protection presentation', () => {
  it('maps critical domain values without changing their API representation', () => {
    expect(riskProtectionLabel('ORDINARY_NEGLIGENCE')).toBe('Sơ suất thông thường');
    expect(riskProtectionLabel('CUSTOMER_INTERFERENCE')).toBe('Khách hàng can thiệp việc điều khiển xe');
    expect(riskProtectionLabel('REIMBURSE_RISK_FUND')).toBe('Hoàn lại Quỹ rủi ro');
    expect(riskProtectionLabel('CUSTOMER_INTOXICATION')).not.toBe('Lỗi khách hàng');
  });

  it('reports the exact responsibility total', () => {
    expect(responsibilityTotal({
      driverFaultPercentage: 30,
      customerFaultPercentage: 0,
      thirdPartyFaultPercentage: 70,
      vehicleFailurePercentage: 0,
      objectiveCausePercentage: 0,
    })).toBe(100);
  });

  it('renders only server-returned settlement fields with friendly labels', () => {
    const claim = {
      status: 'PENDING_FUNDING', eligibleDamageAmount: 80000000,
      insuranceApprovedAmount: 50000000, driverLiabilityAmount: 3000000,
      customerLiabilityAmount: 0, thirdPartyLiabilityAmount: 10000000,
      riskFundAdvanceAmount: 12000000, riskFundPermanentLossAmount: 5000000,
    };
    expect(buildSettlementRecommendation(claim)).toHaveLength(7);
    render(<SettlementRecommendation claim={claim} />);
    expect(screen.getByTestId('server-settlement-recommendation')).toHaveTextContent('Đề xuất từ máy chủ');
    expect(screen.getByText('Chờ cấp kinh phí')).toBeInTheDocument();
    expect(screen.queryByText('PENDING_FUNDING')).not.toBeInTheDocument();
    expect(screen.getByText('Trách nhiệm bên thứ ba có thể thu hồi')).toBeInTheDocument();
  });

  it('requires an explicit confirmation callback for dangerous actions', () => {
    const confirmAction = vi.fn(() => true);
    expect(confirmRiskAction('Xác nhận ghi giảm quỹ?', confirmAction)).toBe(true);
    expect(confirmAction).toHaveBeenCalledOnce();
  });
});
