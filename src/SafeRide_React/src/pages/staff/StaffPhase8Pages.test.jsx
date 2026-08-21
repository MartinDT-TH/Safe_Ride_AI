import { render, screen } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';

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
  beforeEach(() => {
    vi.clearAllMocks();
    useFetch.mockReturnValue(emptyResult);
  });

  it('renders the accident operations queue and filters', () => {
    render(<StaffAccidentsPage />);
    expect(screen.getByRole('heading', { name: /hàng đợi tai nạn/i })).toBeInTheDocument();
    expect(screen.getByLabelText(/trip id/i)).toBeInTheDocument();
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
