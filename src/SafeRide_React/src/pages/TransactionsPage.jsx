import { useMemo, useState } from 'react';
import { AdminLayout } from '../shared/layouts/AdminLayout';
import { TransactionStats, TransactionTable, WithdrawalManagement } from '../features/transactions/components';
import useFetch from '../shared/hooks/useFetch';
import { getTransactionsPath, mapTransactions } from '../features/transactions/transactionsApi';
import './TransactionsPage.css';

function TransactionsPage() {
  const [filters, setFilters] = useState({ status: 'all', method: 'all', date: '' });
  const [currentPage, setCurrentPage] = useState(1);
  const [view, setView] = useState('transactions');

  const path = useMemo(() => getTransactionsPath({ ...filters, page: currentPage }), [filters, currentPage]);
  const { data, isLoading, error, refetch } = useFetch(path, { select: mapTransactions });

  const handleFilterChange = (name, value) => {
    setFilters((current) => ({ ...current, [name]: value }));
    setCurrentPage(1);
  };

  return (
    <AdminLayout>
      {view === 'withdrawals' ? <WithdrawalManagement onBack={() => setView('transactions')} /> : <>
      <header className="page-header transaction-page-header">
        <h1 className="page-title">Lịch sử Giao dịch</h1>
        <p className="page-subtitle">Quản lý và theo dõi toàn bộ dòng tiền của hệ thống SafeRide</p>
      </header>
      {error && <div className="transaction-feedback transaction-feedback--error"><span>{error}</span><button type="button" onClick={refetch}>Thử lại</button></div>}
      {isLoading && <div className="transaction-feedback">Đang tải dữ liệu giao dịch...</div>}
      <TransactionStats stats={data?.stats} onWithdrawalClick={() => setView('withdrawals')} />
      <TransactionTable transactions={data?.items ?? []} filters={filters} onFilterChange={handleFilterChange} currentPage={data?.page ?? currentPage} totalPages={data?.totalPages ?? 1} totalItems={data?.totalItems ?? 0} onPageChange={setCurrentPage} />
      </>}
    </AdminLayout>
  );
}

export default TransactionsPage;
