import { useMemo, useState } from 'react';
import { AdminLayout } from '../../shared/layouts/AdminLayout';
import useFetch from '../../shared/hooks/useFetch';
import { TransactionTable } from '../../features/transactions/components';
import {
  getStaffPaymentStatusesPath,
  mapStaffPaymentStatuses,
} from '../../features/staff/payments/staffPaymentsApi';
import {
  buildQueryPath,
  confirmManualRefund,
  createIdempotencyKey,
  formatVnd,
  staffRefundsPath,
} from '../../features/riskProtection/riskProtectionApi';
import '../TransactionsPage.css';
import '../admin/risk-fund/AdminRiskFundPage.css';

function StaffPaymentStatusPage() {
  const [filters, setFilters] = useState({ status: 'all', method: 'all', fromDate: '', toDate: '' });
  const [currentPage, setCurrentPage] = useState(1);
  const path = useMemo(
    () => getStaffPaymentStatusesPath({ ...filters, page: currentPage }),
    [filters, currentPage],
  );
  const { data, isLoading, error, refetch } = useFetch(path, { select: mapStaffPaymentStatuses });
  const [refundStatus, setRefundStatus] = useState('REFUND_PENDING');
  const refunds = useFetch(buildQueryPath(staffRefundsPath, { status: refundStatus }));
  const [refundDrafts, setRefundDrafts] = useState({});
  const [refundBusyId, setRefundBusyId] = useState(null);
  const [refundFeedback, setRefundFeedback] = useState('');

  const handleFilterChange = (name, value) => {
    setFilters((current) => ({ ...current, [name]: value }));
    setCurrentPage(1);
  };

  const updateRefundDraft = (refundId, field, value) => {
    setRefundDrafts((current) => ({
      ...current,
      [refundId]: { ...current[refundId], [field]: value },
    }));
  };

  const submitRefund = async (item) => {
    const draft = refundDrafts[item.refundId] ?? {};
    if (!draft.paymentReference?.trim() || !draft.evidenceUrl?.trim()) return;
    setRefundBusyId(item.refundId);
    setRefundFeedback('');
    try {
      await confirmManualRefund(item.refundId, {
        paymentReference: draft.paymentReference.trim(),
        evidenceUrl: draft.evidenceUrl.trim(),
        idempotencyKey: draft.idempotencyKey ?? createIdempotencyKey(`refund-${item.refundId}`),
        rowVersion: item.rowVersion,
      });
      setRefundFeedback(`Đã xác nhận hoàn tiền cho chuyến #${item.tripId}.`);
      refunds.refetch();
    } catch (caught) {
      setRefundFeedback(caught.status === 409
        ? 'Dữ liệu hoàn tiền đã thay đổi. Danh sách đang được tải lại.'
        : caught.message);
      refunds.refetch();
    } finally {
      setRefundBusyId(null);
    }
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
      <section className="risk-card" style={{ marginTop: 24 }}>
        <div className="risk-card__title">
          <div><h2>Hàng đợi hoàn tiền thủ công</h2><p>Chỉ xác nhận sau khi đã kiểm tra mã giao dịch và bằng chứng.</p></div>
          <select value={refundStatus} onChange={(event) => setRefundStatus(event.target.value)}>
            <option value="REFUND_PENDING">Chờ hoàn tiền</option>
            <option value="REFUNDED">Đã hoàn tiền</option>
          </select>
        </div>
        {refundFeedback && <div className="transaction-feedback">{refundFeedback}</div>}
        {refunds.error && <div className="transaction-feedback transaction-feedback--error"><span>{refunds.error}</span><button type="button" onClick={refunds.refetch}>Thử lại</button></div>}
        {refunds.isLoading ? <div className="transaction-feedback">Đang tải hàng đợi hoàn tiền...</div> : (
          <div className="risk-table-wrap"><table className="risk-table"><thead><tr><th>Trip</th><th>Số tiền</th><th>Trạng thái</th><th>Mã giao dịch</th><th>Bằng chứng</th><th></th></tr></thead><tbody>
            {(Array.isArray(refunds.data) ? refunds.data : []).map((item) => {
              const draft = refundDrafts[item.refundId] ?? {};
              return <tr key={item.refundId}><td>#{item.tripId}</td><td>{formatVnd(item.amount)}</td><td>{item.status}</td><td><input disabled={item.status === 'REFUNDED'} value={draft.paymentReference ?? item.paymentReference ?? ''} onChange={(event) => updateRefundDraft(item.refundId, 'paymentReference', event.target.value)} /></td><td><input disabled={item.status === 'REFUNDED'} type="url" placeholder="https://..." value={draft.evidenceUrl ?? item.evidenceUrl ?? ''} onChange={(event) => updateRefundDraft(item.refundId, 'evidenceUrl', event.target.value)} /></td><td>{item.status === 'REFUND_PENDING' && <button type="button" disabled={refundBusyId === item.refundId || !draft.paymentReference?.trim() || !draft.evidenceUrl?.trim()} onClick={() => submitRefund(item)}>Xác nhận</button>}</td></tr>;
            })}
            {!refunds.isLoading && (!Array.isArray(refunds.data) || refunds.data.length === 0) && <tr><td colSpan="6">Không có nghĩa vụ hoàn tiền phù hợp.</td></tr>}
          </tbody></table></div>
        )}
      </section>
    </AdminLayout>
  );
}

export default StaffPaymentStatusPage;
