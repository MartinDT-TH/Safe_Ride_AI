import { useMemo, useState } from 'react';
import { AdminLayout } from '../../shared/layouts/AdminLayout';
import useFetch from '../../shared/hooks/useFetch';
import { TransactionTable } from '../../features/transactions/components';
import {
  getStaffPaymentStatusesPath,
  mapStaffPaymentStatuses,
} from '../../features/staff/payments/staffPaymentsApi';
import '../TransactionsPage.css';

function StaffPaymentStatusPage() {
  const [filters, setFilters] = useState({ status: 'all', method: 'all', date: '' });
  const [currentPage, setCurrentPage] = useState(1);
  const path = useMemo(
    () => getStaffPaymentStatusesPath({ ...filters, page: currentPage }),
    [filters, currentPage],
  );
  const { data, isLoading, error, refetch } = useFetch(path, { select: mapStaffPaymentStatuses });

  const handleFilterChange = (name, value) => {
    setFilters((current) => ({ ...current, [name]: value }));
    setCurrentPage(1);
  };

  return (
    <AdminLayout>
      <header className="page-header transaction-page-header">
        <h1 className="page-title">Trạng thái Thanh toán</h1>
        <p className="page-subtitle">Theo dõi trạng thái thanh toán của các chuyến đi trong hệ thống SafeRide.</p>
      </header>
      {error && <div className="transaction-feedback transaction-feedback--error"><span>{error}</span><button type="button" onClick={refetch}>Thử lại</button></div>}
      {isLoading && <div className="transaction-feedback">Đang tải trạng thái thanh toán...</div>}
      <TransactionTable
        transactions={data?.items ?? []}
        filters={filters}
        onFilterChange={handleFilterChange}
        currentPage={data?.page ?? currentPage}
        totalPages={data?.totalPages ?? 1}
        totalItems={data?.totalItems ?? 0}
        onPageChange={setCurrentPage}
      />
    </AdminLayout>
  );
}

export default StaffPaymentStatusPage;
