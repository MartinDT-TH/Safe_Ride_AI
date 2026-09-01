import { cleanup, fireEvent, render, screen } from '@testing-library/react';
import { afterEach, describe, expect, it, vi } from 'vitest';

import {
  CustomerInsuranceInput,
  SystemInsuranceCard,
} from './StaffAccidentsPage';
import {
  initialCustomerInsurance,
  shouldShowSystemInsurance,
} from '../../features/riskProtection/riskProtectionPresentation';

afterEach(cleanup);

const pendingClaim = {
  status: 'UNDER_REVIEW',
  insuranceStatus: 'PENDING',
  eligibleDamageAmount: 10_000_000,
  customerGrossExposure: 3_000_000,
  customerInsuranceAppliedAmount: 0,
  remainingLossAfterCustomerInsurance: 10_000_000,
  systemInsuranceCoveredExposureRemaining: 10_000_000,
  systemInsuranceCoverageLimitSnapshot: 8_000_000,
  maximumApprovableInsuranceAmount: 8_000_000,
  recommendedInsuranceApprovalAmount: 8_000_000,
  systemInsuranceApprovedAmount: 0,
  systemInsuranceProvider: 'MockInsuranceProvider',
  insuranceReference: 'MOCK-1',
  systemInsuranceEvaluationReason: 'AVAILABLE',
};

const initialReview = { mode: 'recommended', approvedAmount: '', reason: '' };

function renderSystemCard(claim = pendingClaim, overrides = {}) {
  const props = {
    claim,
    insurance: initialReview,
    setInsurance: vi.fn(),
    onReview: vi.fn(),
    onRefresh: vi.fn(),
    ...overrides,
  };
  const view = render(<SystemInsuranceCard {...props} />);
  return { ...props, ...view };
}

describe('Staff Accident insurance workflow', () => {
  it('uses one optional Customer insurance amount defaulted to zero without policy toggles', () => {
    render(<CustomerInsuranceInput claim={pendingClaim} value={initialCustomerInsurance} onChange={vi.fn()} />);

    expect(screen.getByLabelText('Khoản bảo hiểm riêng đã xác nhận chi trả')).toHaveValue(0);
    expect(screen.queryByRole('button', { name: 'Có sử dụng' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Không sử dụng' })).not.toBeInTheDocument();
    expect(screen.queryByText(/PHYSICAL_DAMAGE|VERIFIED|chính sách bảo hiểm/i)).not.toBeInTheDocument();
  });

  it('allows the amount to exceed Customer gross but caps the HTML input at EligibleDamage', () => {
    const onChange = vi.fn();
    render(<CustomerInsuranceInput claim={pendingClaim} value={initialCustomerInsurance} onChange={onChange} />);
    const input = screen.getByLabelText('Khoản bảo hiểm riêng đã xác nhận chi trả');

    fireEvent.change(input, { target: { value: '6000000' } });

    expect(onChange).toHaveBeenCalledWith(expect.objectContaining({ appliedAmount: '6000000' }));
    expect(input).toHaveAttribute('max', '10000000');
  });

  it.each([0, 6_000_000])('renders SafeRide System Insurance independently when Customer insurance is %s', (customerAmount) => {
    const claim = { ...pendingClaim, customerInsuranceAppliedAmount: customerAmount };
    expect(shouldShowSystemInsurance(claim)).toBe(true);
    renderSystemCard(claim);

    expect(screen.getByRole('region', { name: 'Bảo hiểm hệ thống SafeRide' })).toBeInTheDocument();
    expect(screen.getByText('MockInsuranceProvider')).toBeInTheDocument();
  });

  it('still renders when Customer residual is zero and Driver covered exposure remains', () => {
    const claim = {
      ...pendingClaim,
      customerInsuranceAppliedAmount: 3_000_000,
      customerExposureAfterOwnInsurance: 0,
      systemInsuranceCoveredExposureRemaining: 4_000_000,
      maximumApprovableInsuranceAmount: 4_000_000,
      recommendedInsuranceApprovalAmount: 4_000_000,
    };
    renderSystemCard(claim);

    expect(screen.getAllByText(/4\.000\.000/).length).toBeGreaterThan(0);
    expect(screen.getByRole('button', { name: 'Phê duyệt mức đề xuất' })).toBeInTheDocument();
  });

  it('renders the server reason and no review actions when maximum is zero', () => {
    renderSystemCard({
      ...pendingClaim,
      status: 'APPROVED',
      insuranceStatus: 'NOT_SUBMITTED',
      maximumApprovableInsuranceAmount: 0,
      recommendedInsuranceApprovalAmount: 0,
      systemInsuranceEvaluationReason: 'NO_REMAINING_COVERED_EXPOSURE',
    });

    expect(screen.getByText('Bảo hiểm hệ thống hiện không có khoản có thể duyệt cho hồ sơ này.')).toBeInTheDocument();
    expect(screen.getByText(/Không còn phần thiệt hại thuộc phạm vi Customer\/Driver/)).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Phê duyệt mức đề xuất' })).not.toBeInTheDocument();
  });

  it('supports recommended approval and constrains a lower approval to the server maximum', () => {
    const onReview = vi.fn();
    const recommended = renderSystemCard(pendingClaim, { onReview });
    fireEvent.click(screen.getByRole('button', { name: 'Phê duyệt mức đề xuất' }));
    expect(recommended.onReview).toHaveBeenCalledWith(expect.anything(), true, 'recommended');
    recommended.unmount();

    const { unmount } = render(<SystemInsuranceCard
      claim={pendingClaim}
      insurance={{ mode: 'lower', approvedAmount: '', reason: '' }}
      setInsurance={vi.fn()}
      onReview={vi.fn()}
      onRefresh={vi.fn()}
    />);
    const lowerInput = screen.getByLabelText('Số tiền duyệt thấp hơn');
    expect(lowerInput).toHaveAttribute('max', '8000000');
    expect(screen.getByLabelText('Lý do (bắt buộc)')).toBeRequired();
    unmount();
  });

  it('requires a reason field for rejection mode', () => {
    renderSystemCard(pendingClaim, {
      insurance: { mode: 'reject', approvedAmount: '', reason: '' },
    });

    expect(screen.getByLabelText('Lý do (bắt buộc)')).toBeRequired();
    expect(screen.getByRole('button', { name: 'Xác nhận từ chối' })).toBeInTheDocument();
  });
});
