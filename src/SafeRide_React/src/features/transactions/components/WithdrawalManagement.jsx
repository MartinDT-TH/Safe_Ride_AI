import { useMemo, useState } from 'react';
import { FontAwesomeIcon } from '@fortawesome/react-fontawesome';
import { faArrowLeft, faFilter, faMoneyBillWave, faReceipt, faClipboardCheck } from '@fortawesome/free-solid-svg-icons';
import { Pagination } from '../../../shared/components';
import useFetch from '../../../shared/hooks/useFetch';
import { approveWithdrawal, getWithdrawalsPath, mapWithdrawals, rejectWithdrawal } from '../transactionsApi';

const money = new Intl.NumberFormat('vi-VN');
const statusMeta = { pending: ['Chờ duyệt', 'pending'], approved: ['Đã duyệt', 'approved'], rejected: ['Từ chối', 'rejected'] };

function WithdrawalManagement({ onBack }) {
  const [page, setPage] = useState(1);
  const [status, setStatus] = useState('all');
  const [processingId, setProcessingId] = useState(null);
  const [actionError, setActionError] = useState('');
  const path = useMemo(() => getWithdrawalsPath({ page, status }), [page, status]);
  const { data, isLoading, error, refetch } = useFetch(path, { select: mapWithdrawals });

  async function process(item, action) {
    let reason;
    if (action === 'reject') {
      reason = window.prompt(`Lý do từ chối yêu cầu của ${item.driverName}:`, 'Thông tin tài khoản không hợp lệ');
      if (reason === null) return;
    } else if (!window.confirm(`Duyệt yêu cầu rút ${money.format(item.amount)}đ của ${item.driverName}?`)) return;
    setProcessingId(item.id); setActionError('');
    try {
      await (action === 'approve' ? approveWithdrawal(item.id) : rejectWithdrawal(item.id, reason));
      refetch();
    } catch (requestError) { setActionError(requestError.message); }
    finally { setProcessingId(null); }
  }

  const stats = data?.stats ?? {};
  const items = data?.items ?? [];
  return <>
    <header className="withdrawal-header">
      <button type="button" className="withdrawal-back" onClick={onBack}><FontAwesomeIcon icon={faArrowLeft} /> Lịch sử giao dịch</button>
      <h1 className="page-title">Quản lý Rút tiền</h1>
    </header>
    <section className="withdrawal-filter-bar"><button type="button" className="advanced-filter-btn" onClick={() => { setStatus(status === 'all' ? 'pending' : 'all'); setPage(1); }}><FontAwesomeIcon icon={faFilter} />{status === 'pending' ? 'Đang lọc: Chờ duyệt' : 'Bộ lọc nâng cao'}</button></section>
    {(error || actionError) && <div className="transaction-feedback transaction-feedback--error"><span>{error || actionError}</span><button type="button" onClick={refetch}>Thử lại</button></div>}
    <section className="withdrawal-stats">
      <Summary label="Tổng yêu cầu" value={stats.totalRequests ?? 0} note="+12% so với tuần trước" icon={faReceipt} variant="teal" />
      <Summary label="Đang chờ duyệt" value={stats.pendingRequests ?? 0} note="Cần xử lý gấp" icon={faClipboardCheck} variant="brown" />
      <Summary label="Tổng số tiền" value={compactMoney(stats.totalAmount)} note="Đơn vị: VNĐ" icon={faMoneyBillWave} variant="teal" />
    </section>
    <section className="withdrawal-panel">
      {isLoading && <div className="transaction-feedback">Đang tải yêu cầu rút tiền...</div>}
      <div className="transaction-table-scroll"><table className="withdrawal-table">
        <thead><tr><th>Tài xế</th><th>Thông tin Ngân<br />hàng</th><th>Số tiền</th><th>Ngày yêu cầu</th><th>Trạng thái</th><th>Hành động</th></tr></thead>
        <tbody>{items.map((item) => {
          const meta = statusMeta[item.status] ?? statusMeta.pending;
          return <tr key={item.id}>
            <td><div className="withdrawal-driver">{item.avatarUrl ? <img src={item.avatarUrl} alt="" loading="lazy" /> : <span>{item.initials}</span>}<div><strong>{item.driverName}</strong><small>DRV-{String(item.driverId).slice(0, 4).toUpperCase()}</small></div></div></td>
            <td><div className="bank-info"><strong>{item.bankName}</strong><span>{item.bankAccountNumber}</span><small>{item.bankAccountName}</small></div></td>
            <td><strong className="withdrawal-amount">{money.format(item.amount)}đ</strong></td>
            <td><span className="date-cell"><strong>{item.requestedDate}</strong><small>{item.requestedTime}</small></span></td>
            <td><span className={`withdrawal-status withdrawal-status--${meta[1]}`}>{meta[0]}</span></td>
            <td>{item.status === 'pending' ? <div className="withdrawal-actions"><button disabled={processingId === item.id} onClick={() => process(item, 'approve')} type="button">Duyệt</button><button disabled={processingId === item.id} onClick={() => process(item, 'reject')} type="button">Từ chối</button></div> : <span className="withdrawal-completed">{item.status === 'approved' ? 'Đã thực hiện' : 'Đã từ chối'}</span>}</td>
          </tr>;
        })}{!isLoading && !items.length && <tr><td colSpan="6" className="empty-transactions">Không có yêu cầu rút tiền.</td></tr>}</tbody>
      </table></div>
      <footer className="transaction-footer"><span>Hiển thị {items.length ? (data.page - 1) * data.pageSize + 1 : 0}–{Math.min((data?.page ?? 1) * (data?.pageSize ?? 10), data?.totalItems ?? 0)} trên {Number(data?.totalItems ?? 0).toLocaleString('vi-VN')} yêu cầu</span><Pagination currentPage={data?.page ?? page} totalPages={data?.totalPages ?? 1} onPageChange={setPage} /></footer>
    </section>
  </>;
}

function Summary({ label, value, note, icon, variant }) {
  return <article className="withdrawal-summary"><div><span>{label}</span><strong>{value}</strong><small className={`summary-note--${variant}`}>{note}</small></div><span className={`withdrawal-summary-icon withdrawal-summary-icon--${variant}`}><FontAwesomeIcon icon={icon} /></span></article>;
}
function compactMoney(value = 0) { const amount = Number(value); return amount >= 1_000_000 ? `${Number((amount / 1_000_000).toFixed(1))}M` : money.format(amount); }
export default WithdrawalManagement;
