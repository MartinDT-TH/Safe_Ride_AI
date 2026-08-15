import { useEffect, useMemo, useState } from 'react';
import { FontAwesomeIcon } from '@fortawesome/react-fontawesome';
import { faPaperPlane, faRotateRight } from '@fortawesome/free-solid-svg-icons';
import { AdminLayout } from '../../shared/layouts/AdminLayout';
import useAdminSearch from '../../shared/hooks/useAdminSearch';
import useFetch from '../../shared/hooks/useFetch';
import ActionFeedback from '../../shared/components/ActionFeedback/ActionFeedback';
import Pagination from '../../shared/components/Pagination/Pagination';
import StatusBadge from '../../shared/components/StatusBadge/StatusBadge';
import {
    createStaffNotificationRequest,
    getStaffNotificationRequestsPath,
    mapStaffNotificationRequestsPage,
} from '../../features/staff/notifications/staffNotificationsApi';
import '../NotificationsPage.css';

const DEFAULT_COUNTS = {
    all: 0,
    pending: 0,
    approved: 0,
    rejected: 0,
};

function StaffNotificationRequestsPage() {
    const [filters, setFilters] = useState({
        status: 'all',
        type: 'all',
        audience: 'all',
    });
    const [currentPage, setCurrentPage] = useState(1);
    const [mutationError, setMutationError] = useState(null);
    const [successMessage, setSuccessMessage] = useState(null);
    const [isMutating, setIsMutating] = useState(false);
    const [sendForm, setSendForm] = useState({
        targetAudience: 'Both',
        notificationType: 'System Update',
        title: '',
        content: '',
    });
    const { query } = useAdminSearch({
        placeholder: 'Tìm kiếm yêu cầu thông báo của bạn...',
    });

    const listPath = useMemo(() => getStaffNotificationRequestsPath({
        page: currentPage,
        status: filters.status,
        type: filters.type,
        audience: filters.audience,
        search: query,
    }), [currentPage, filters, query]);

    const listResult = useFetch(listPath, { select: mapStaffNotificationRequestsPage });
    const listData = listResult.data ?? {
        items: [],
        counts: DEFAULT_COUNTS,
        page: 1,
        totalPages: 1,
        totalItems: 0,
    };

    useEffect(() => {
        if (!successMessage) {
            return undefined;
        }

        const timeoutId = window.setTimeout(() => setSuccessMessage(null), 4000);
        return () => window.clearTimeout(timeoutId);
    }, [successMessage]);

    const handleFilterChange = (name, value) => {
        setFilters((current) => ({
            ...current,
            [name]: value,
        }));
        setCurrentPage(1);
    };

    const handleSendFieldChange = (name, value) => {
        setSendForm((current) => ({
            ...current,
            [name]: value,
        }));
    };

    const handleSubmitNotification = async () => {
        setMutationError(null);

        if (!sendForm.title.trim() || !sendForm.content.trim()) {
            setMutationError('Vui lòng nhập đầy đủ tiêu đề và nội dung thông báo.');
            return;
        }

        setIsMutating(true);
        try {
            await createStaffNotificationRequest({
                targetAudience: sendForm.targetAudience,
                notificationType: sendForm.notificationType,
                title: sendForm.title.trim(),
                content: sendForm.content.trim(),
            });
            setSendForm({
                targetAudience: 'Both',
                notificationType: 'System Update',
                title: '',
                content: '',
            });
            setSuccessMessage('Yêu cầu thông báo đã được lưu với trạng thái chờ duyệt.');
            listResult.refetch();
        }
        catch (caughtError) {
            setMutationError(caughtError instanceof Error ? caughtError.message : 'Không thể tạo yêu cầu thông báo.');
        }
        finally {
            setIsMutating(false);
        }
    };

    return (
        <AdminLayout>
            <div className="notifications-page">
                <header className="notifications-page__header">
                    <div>
                        <h1 className="page-title">Yêu cầu Thông báo</h1>
                        <p className="page-subtitle">
                            Tạo yêu cầu thông báo để quản trị viên xem xét. Nhân viên không thể phát hành trực tiếp tới mobile app.
                        </p>
                    </div>
                    <div className="notifications-page__header-actions">
                        <button type="button" className="notifications-page__ghost-btn" onClick={listResult.refetch}>
                            <FontAwesomeIcon icon={faRotateRight} />
                            Làm mới
                        </button>
                    </div>
                </header>

                <ActionFeedback message={successMessage} />

                {mutationError && (
                    <div className="notifications-feedback notifications-feedback--error" role="alert">
                        <span>{mutationError}</span>
                    </div>
                )}

                <section className="notification-send">
                    <div className="notification-send__grid">
                        <div className="notification-send__card">
                            <section>
                                <label className="notification-send__label">Đối tượng nhận tin</label>
                                <div className="notification-send__audience-grid">
                                    {[
                                        { value: 'Both', label: 'Tất cả người dùng' },
                                        { value: 'Driver', label: 'Tài xế' },
                                        { value: 'Customer', label: 'Khách hàng' },
                                    ].map((option) => (
                                        <button
                                            key={option.value}
                                            type="button"
                                            className={`notification-send__audience-card${sendForm.targetAudience === option.value ? ' notification-send__audience-card--active' : ''}`}
                                            onClick={() => handleSendFieldChange('targetAudience', option.value)}
                                        >
                                            <strong>{option.label}</strong>
                                        </button>
                                    ))}
                                </div>
                            </section>

                            <section>
                                <label className="notification-send__label" htmlFor="staff-notification-type">
                                    Loại thông báo
                                </label>
                                <select
                                    id="staff-notification-type"
                                    className="notification-send__select"
                                    value={sendForm.notificationType}
                                    onChange={(event) => handleSendFieldChange('notificationType', event.target.value)}
                                >
                                    <option value="Promotion">Khuyến mãi</option>
                                    <option value="System Update">Cập nhật hệ thống</option>
                                    <option value="Warning">Cảnh báo</option>
                                </select>
                            </section>

                            <section className="notification-send__fields">
                                <label className="notification-send__label" htmlFor="staff-notification-title">
                                    Tiêu đề thông báo
                                </label>
                                <input
                                    id="staff-notification-title"
                                    className="notification-send__input"
                                    type="text"
                                    value={sendForm.title}
                                    maxLength="40"
                                    placeholder="Ví dụ: Lịch bảo trì định kỳ"
                                    onChange={(event) => handleSendFieldChange('title', event.target.value)}
                                />
                                <div className="notification-send__count">{sendForm.title.length}/40</div>

                                <label className="notification-send__label" htmlFor="staff-notification-content">
                                    Nội dung tin nhắn
                                </label>
                                <textarea
                                    id="staff-notification-content"
                                    className="notification-send__textarea"
                                    value={sendForm.content}
                                    maxLength="140"
                                    rows="4"
                                    placeholder="Mô tả ngắn gọn nội dung cần thông báo..."
                                    onChange={(event) => handleSendFieldChange('content', event.target.value)}
                                />
                                <div className="notification-send__count">{sendForm.content.length}/140</div>
                            </section>

                            <button
                                type="button"
                                className="notifications-page__primary-btn notifications-page__primary-btn--full"
                                onClick={handleSubmitNotification}
                                disabled={isMutating}
                            >
                                <FontAwesomeIcon icon={faPaperPlane} />
                                {isMutating ? 'Đang lưu...' : 'Gửi yêu cầu'}
                            </button>
                        </div>

                        <aside className="notification-send__preview notification-send__preview--empty" aria-hidden="true" />
                    </div>
                </section>

                <div className="notifications-summary">
                    <SummaryCard label="Tất cả yêu cầu" value={listData.counts.all} />
                    <SummaryCard label="Đang chờ duyệt" value={listData.counts.pending} accent="amber" />
                    <SummaryCard label="Đã duyệt" value={listData.counts.approved} accent="green" />
                    <SummaryCard label="Đã từ chối" value={listData.counts.rejected} accent="red" />
                </div>

                <section className="notifications-panel">
                    <div className="notifications-filters">
                        <label className="notifications-field">
                            <span>Trạng thái</span>
                            <select value={filters.status} onChange={(event) => handleFilterChange('status', event.target.value)}>
                                <option value="all">Tất cả</option>
                                <option value="Pending">Đang chờ</option>
                                <option value="Approved">Đã duyệt</option>
                                <option value="Rejected">Đã từ chối</option>
                            </select>
                        </label>
                        <label className="notifications-field">
                            <span>Loại thông báo</span>
                            <select value={filters.type} onChange={(event) => handleFilterChange('type', event.target.value)}>
                                <option value="all">Tất cả loại</option>
                                <option value="Promotion">Khuyến mãi</option>
                                <option value="System Update">Cập nhật hệ thống</option>
                                <option value="Warning">Cảnh báo</option>
                            </select>
                        </label>
                        <label className="notifications-field">
                            <span>Đối tượng nhận</span>
                            <select value={filters.audience} onChange={(event) => handleFilterChange('audience', event.target.value)}>
                                <option value="all">Tất cả đối tượng</option>
                                <option value="Both">Tất cả người dùng</option>
                                <option value="Driver">Tài xế</option>
                                <option value="Customer">Khách hàng</option>
                            </select>
                        </label>
                    </div>

                    {listResult.error && (
                        <div className="notifications-feedback notifications-feedback--error">
                            <span>{listResult.error}</span>
                            <button type="button" onClick={listResult.refetch}>Thử lại</button>
                        </div>
                    )}

                    {listResult.isLoading && (
                        <div className="notifications-feedback">
                            Đang tải yêu cầu thông báo...
                        </div>
                    )}

                    {!listResult.isLoading && !listResult.error && listData.items.length === 0 && (
                        <div className="notifications-empty">
                            <strong>Bạn chưa có yêu cầu thông báo phù hợp</strong>
                            <p>Yêu cầu mới tạo sẽ xuất hiện ở đây với trạng thái chờ duyệt.</p>
                        </div>
                    )}

                    {listData.items.length > 0 && (
                        <>
                            <div className="notifications-table-scroll">
                                <table className="notifications-table">
                                    <thead>
                                        <tr>
                                            <th>Thông báo</th>
                                            <th>Loại</th>
                                            <th>Đối tượng</th>
                                            <th>Thời gian</th>
                                            <th>Trạng thái</th>
                                            <th>Kết quả</th>
                                        </tr>
                                    </thead>
                                    <tbody>
                                        {listData.items.map((notification) => (
                                            <tr key={notification.rawId}>
                                                <td>
                                                    <div className="notifications-table__message">
                                                        <strong>{notification.title}</strong>
                                                        <p>{notification.content}</p>
                                                    </div>
                                                </td>
                                                <td>{notification.typeLabel}</td>
                                                <td>{notification.audienceLabel}</td>
                                                <td>{notification.createdAtLabel}</td>
                                                <td>
                                                    <StatusBadge label={notification.statusLabel} variant={notification.statusVariant} />
                                                </td>
                                                <td>
                                                    <NotificationResolutionSummary notification={notification} />
                                                </td>
                                            </tr>
                                        ))}
                                    </tbody>
                                </table>
                            </div>

                            <div className="notifications-panel__footer">
                                <span>Hiển thị {listData.items.length} / {listData.totalItems} yêu cầu</span>
                                <Pagination
                                    currentPage={listData.page}
                                    totalPages={listData.totalPages}
                                    onPageChange={setCurrentPage}
                                />
                            </div>
                        </>
                    )}
                </section>
            </div>
        </AdminLayout>
    );
}

function SummaryCard({ label, value, accent = 'teal' }) {
    return (
        <div className={`notifications-summary__card notifications-summary__card--${accent}`}>
            <span>{label}</span>
            <strong>{value}</strong>
        </div>
    );
}

function NotificationResolutionSummary({ notification }) {
    if (notification.status === 'Approved') {
        return (
            <div className="notifications-table__resolution">
                <strong>{notification.approvedByName ?? 'Đã duyệt'}</strong>
                <small>{notification.approvedAtLabel}</small>
            </div>
        );
    }

    if (notification.status === 'Rejected') {
        return (
            <div className="notifications-table__resolution">
                <strong>{notification.rejectedByName ?? 'Đã từ chối'}</strong>
                <small>{notification.rejectedAtLabel}</small>
                {notification.rejectedReason && <p>{notification.rejectedReason}</p>}
            </div>
        );
    }

    return (
        <div className="notifications-table__resolution">
            <strong>Chờ quản trị viên xử lý</strong>
            <small>Chưa phát hành tới mobile app</small>
        </div>
    );
}

export default StaffNotificationRequestsPage;
