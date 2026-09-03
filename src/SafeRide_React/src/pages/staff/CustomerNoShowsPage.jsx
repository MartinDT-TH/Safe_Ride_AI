import { useMemo, useState } from 'react';
import { AdminLayout } from '../../shared/layouts/AdminLayout';
import useFetch from '../../shared/hooks/useFetch';
import ActionFeedback from '../../shared/components/ActionFeedback/ActionFeedback';
import {
  clearCustomerBookingRestriction,
  exemptCustomerNoShow,
  getCustomerNoShowDetail,
  getCustomerNoShows,
} from '../../features/staff/noShows/staffNoShowsApi';
import './CustomerNoShowsPage.css';

const fmt = (value) => value ? new Date(value).toLocaleString('vi-VN') : '—';
const statusLabels = { VERIFIED: 'Đã xác minh', EXEMPTED: 'Đã miễn trừ', STAFF_CONFIRMED: 'Nhân sự đã xác nhận', REVERSED: 'Đã hoàn tác' };
const restrictionLabels = { NORMAL: 'Bình thường', REMINDER: 'Nhắc nhở', WARNING: 'Cảnh báo', SCHEDULE_RISK: 'Rủi ro đặt lịch', PERSISTENT_ABUSE: 'Lạm dụng lặp lại', STAFF_REVIEW: 'Cần nhân sự xem xét', TEMP_RESTRICTED: 'Tạm hạn chế' };
const statusLabel = (status) => statusLabels[String(status).toUpperCase()] || status || 'Chưa xác định';
const restrictionLabel = (level) => restrictionLabels[String(level).toUpperCase()] || level || 'Chưa có dữ liệu';
const displayName = (name, fallback) => name?.trim() || fallback;
const formatDistance = (meters) => meters == null ? '—' : `${Number(meters).toLocaleString('vi-VN')} m`;

function InfoCard({ label, children }) {
  return <div className="noshow-info-card"><span>{label}</span><strong>{children}</strong></div>;
}

function DetailValue({ label, children }) {
  return <div className="noshow-detail-value"><span>{label}</span><strong>{children || '—'}</strong></div>;
}

function CustomerNoShowsPage() {
  const [filters, setFilters] = useState({ status: '', customerId: '', from: '', to: '' });
  const [page, setPage] = useState(1);
  const [selected, setSelected] = useState(null);
  const [busy, setBusy] = useState(false);
  const [feedback, setFeedback] = useState('');
  const path = useMemo(() => {
    const q = new URLSearchParams({ ...filters, page, pageSize: 20 });
    return `/staff/customer-no-shows?${q}`;
  }, [filters, page]);
  const list = useFetch(path);

  const open = async (id) => {
    setFeedback('');
    setSelected({ loading: true });
    try { setSelected(await getCustomerNoShowDetail(id)); } catch (error) { setFeedback(error.message); setSelected(null); }
  };

  const action = async (fn) => {
    const reason = window.prompt('Nhập lý do xử lý:');
    if (!reason?.trim()) return;
    setBusy(true); setFeedback('');
    try {
      await fn(reason.trim());
      setFeedback('Đã cập nhật xử lý.');
      await list.refetch();
      if (selected?.event?.eventId) await open(selected.event.eventId);
    } catch (error) { setFeedback(error.message); } finally { setBusy(false); }
  };

  const items = list.data?.items ?? [];
  const event = selected?.event;
  const privilege = selected?.privilege;
  const canClearRestriction = privilege && (privilege.scheduledRestrictedUntil || privilege.bookingCooldownUntil);

  return <AdminLayout>
    <header className="page-header"><h1 className="page-title">Xử lý khách vắng mặt</h1><p className="page-subtitle">Rà soát các trường hợp khách hàng không xuất hiện tại điểm đón và hỗ trợ miễn trừ khi có lý do hợp lệ.</p></header>
    <div className="noshow-filters"><input placeholder="Customer ID" value={filters.customerId} onChange={e => { setFilters({ ...filters, customerId: e.target.value }); setPage(1); }} /><select value={filters.status} onChange={e => { setFilters({ ...filters, status: e.target.value }); setPage(1); }}><option value="">Tất cả trạng thái</option><option value="VERIFIED">Đã xác minh</option><option value="EXEMPTED">Đã miễn trừ</option><option value="REVERSED">Đã hoàn tác</option></select><input type="date" value={filters.from} onChange={e => setFilters({ ...filters, from: e.target.value })} /><input type="date" value={filters.to} onChange={e => setFilters({ ...filters, to: e.target.value })} /></div>
    <ActionFeedback message={feedback || (list.error && String(list.error))} variant={list.error || feedback && feedback.startsWith('HTTP') ? 'error' : 'success'} />
    {list.isLoading ? <div className="noshow-empty">Đang tải dữ liệu...</div> : <div className="noshow-table-wrap"><table className="noshow-table"><thead><tr><th>Trạng thái</th><th>Khách hàng</th><th>Tài xế</th><th>Booking / Trip</th><th>Khoảng cách</th><th>Thời gian</th><th></th></tr></thead><tbody>{items.map(item => <tr key={item.eventId}><td><span className={`noshow-status noshow-status--${String(item.status).toLowerCase()}`}>{statusLabel(item.status)}</span></td><td>{displayName(item.customerName, 'Khách hàng')}</td><td>{displayName(item.driverName, 'Tài xế')}</td><td>#{item.bookingId} / #{item.tripId}</td><td>{formatDistance(item.arrivalDistanceMeters)}</td><td>{fmt(item.verifiedAt || item.driverReportedAt)}</td><td><button type="button" onClick={() => open(item.eventId)}>Xem chi tiết</button></td></tr>)}{!items.length && <tr><td colSpan="7" className="noshow-empty">Không có trường hợp khách vắng mặt.</td></tr>}</tbody></table></div>}
    <div className="noshow-pagination"><button disabled={page <= 1} onClick={() => setPage(page - 1)}>Trước</button><span>Trang {list.data?.page ?? page} / {list.data?.totalPages ?? 1}</span><button disabled={page >= (list.data?.totalPages ?? 1)} onClick={() => setPage(page + 1)}>Sau</button></div>
    {selected && <div className="noshow-overlay" onClick={() => setSelected(null)}><section className="noshow-modal" onClick={e => e.stopPropagation()}>{selected.loading ? <p>Đang tải chi tiết...</p> : <><div className="noshow-modal-header"><div><span className="noshow-report-badge">Mã báo cáo #{event.eventId}</span><h2>Chi tiết khách vắng mặt</h2></div><button className="noshow-close" onClick={() => setSelected(null)} aria-label="Đóng">×</button></div><section className="noshow-modal-section"><h3>Thông tin liên quan</h3><div className="noshow-summary-grid"><InfoCard label="Khách hàng">{displayName(event.customerName, 'Khách hàng')}</InfoCard><InfoCard label="Tài xế">{displayName(event.driverName, 'Tài xế')}</InfoCard><InfoCard label="Booking / Trip">#{event.bookingId} / #{event.tripId}</InfoCard><InfoCard label="Trạng thái"><span className={`noshow-status noshow-status--${String(event.status).toLowerCase()}`}>{statusLabel(event.status)}</span></InfoCard></div></section><section className="noshow-modal-section"><h3>Bằng chứng xác minh</h3><div className="noshow-evidence-grid"><DetailValue label="GPS">{event.arrivalLatitude == null || event.arrivalLongitude == null ? '—' : `${event.arrivalLatitude}, ${event.arrivalLongitude}`}</DetailValue><DetailValue label="Khoảng cách tới điểm đón">{formatDistance(event.arrivalDistanceMeters)}</DetailValue><DetailValue label="Tài xế đã đến">{fmt(event.arrivedAt)}</DetailValue><DetailValue label="Đã nhắc khách hàng">{fmt(event.reminderSentAt)}</DetailValue><DetailValue label="Đủ thời gian chờ">{fmt(event.waitSatisfiedAt)}</DetailValue><DetailValue label="Đã xác minh">{fmt(event.verifiedAt)}</DetailValue><div className="noshow-detail-value noshow-detail-value--full"><span>Lý do</span><strong>{event.reviewReason || '—'}</strong></div></div></section><section className="noshow-modal-section"><h3>Quyền đặt chuyến</h3>{privilege ? <div className="noshow-privilege-grid"><InfoCard label="Mức độ">{restrictionLabel(privilege.restrictionLevel)}</InfoCard><InfoCard label="Số lần vắng mặt">{privilege.verifiedNoShowCount}</InfoCard><InfoCard label="Tỷ lệ">{(privilege.noShowRate * 100).toFixed(1)}%</InfoCard><InfoCard label="Chuỗi liên tiếp">{privilege.consecutiveNoShowStreak}</InfoCard></div> : <p className="noshow-muted">Chưa có dữ liệu</p>}</section>{selected.supports?.length > 0 && <p className="noshow-muted">Hỗ trợ tài xế: {selected.supports.map(s => `${s.supportAmount} VND (${s.status})`).join(', ')}</p>}<div className="noshow-actions"><button disabled={busy || event.status !== 'VERIFIED'} onClick={() => action(reason => exemptCustomerNoShow(event.eventId, reason))}>{event.status === 'EXEMPTED' ? 'Đã miễn trừ' : 'Miễn trừ trường hợp này'}</button>{canClearRestriction && <button disabled={busy} className="noshow-action-secondary" onClick={() => action(reason => clearCustomerBookingRestriction(event.customerId, reason))}>Gỡ hạn chế đặt chuyến</button>}</div><small className="noshow-action-note">Thao tác này chỉ cập nhật trạng thái xử lý, không xóa lịch sử ghi nhận.</small></>}</section></div>}
  </AdminLayout>;
}

export default CustomerNoShowsPage;
