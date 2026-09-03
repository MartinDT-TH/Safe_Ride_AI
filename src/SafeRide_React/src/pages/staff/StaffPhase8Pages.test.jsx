import { cleanup, fireEvent, render, screen } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

const { useFetch } = vi.hoisted(() => ({ useFetch: vi.fn() }));
vi.mock('../../shared/hooks/useFetch', () => ({ default: useFetch }));
vi.mock('../../shared/layouts/AdminLayout', () => ({
  AdminLayout: ({ children }) => <main>{children}</main>,
}));
vi.mock('../../features/transactions/components', () => ({
  TransactionTable: () => <div>payment-status-table</div>,
}));

import StaffAccidentsPage from './StaffAccidentsPage';
import StaffPaymentStatusPage from './StaffPaymentStatusPage';

const emptyResult = { data: [], isLoading: false, error: null, refetch: vi.fn(), setData: vi.fn() };

describe('Phase 8 Staff pages', () => {
  afterEach(cleanup);

  beforeEach(() => {
    vi.clearAllMocks();
    useFetch.mockReturnValue(emptyResult);
  });

  it('renders the accident operations queue and filters', () => {
    render(<StaffAccidentsPage />);
    expect(screen.getByRole('heading', { name: /hàng đợi tai nạn/i })).toBeInTheDocument();
    expect(screen.getByLabelText(/trip id/i)).toBeInTheDocument();
  });

  it('paginates the accident queue and resets its page controls correctly', () => {
    const accidents = Array.from({ length: 11 }, (_, index) => ({
      id: index + 1,
      tripId: 2001 + index,
      category: 'DRIVER_INJURY',
      status: 'REPORTED',
      occurredAtUtc: '2026-09-03T10:00:00Z',
      claimId: null,
    }));
    useFetch.mockReturnValue({ ...emptyResult, data: accidents });

    render(<StaffAccidentsPage />);

    expect(screen.queryByText('#2011')).not.toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: 'Trang sau' }));
    expect(screen.getByText('#2011')).toBeInTheDocument();
    expect(screen.getByText(/11–11/)).toBeInTheDocument();
  });

  it('renders the manual refund queue alongside payment status', () => {
    useFetch.mockImplementation((path) => path.includes('/payments/status')
      ? { ...emptyResult, data: { items: [], page: 1, totalPages: 1, totalItems: 0 } }
      : emptyResult);
    render(<StaffPaymentStatusPage />);
    expect(screen.getByRole('heading', { name: /hàng đợi hoàn tiền thủ công/i })).toBeInTheDocument();
    expect(screen.getByText('payment-status-table')).toBeInTheDocument();
  });
});
