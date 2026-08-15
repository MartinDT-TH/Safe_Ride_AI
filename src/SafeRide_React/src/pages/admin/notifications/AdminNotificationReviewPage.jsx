import { useEffect, useMemo, useState } from 'react';
import { FontAwesomeIcon } from '@fortawesome/react-fontawesome';
import { faClipboardCheck, faRotateRight } from '@fortawesome/free-solid-svg-icons';
import { AdminLayout } from '../../../shared/layouts/AdminLayout';
import useAdminSearch from '../../../shared/hooks/useAdminSearch';
import useFetch from '../../../shared/hooks/useFetch';
import ActionFeedback from '../../../shared/components/ActionFeedback/ActionFeedback';
import Pagination from '../../../shared/components/Pagination/Pagination';
import StatusBadge from '../../../shared/components/StatusBadge/StatusBadge';
import NotificationApproveDialog from '../../../features/notifications/components/NotificationApproveDialog';
import NotificationRejectDialog from '../../../features/notifications/components/NotificationRejectDialog';
import {
    approveAdminNotification,
    getAdminNotificationsPath,
    mapAdminNotificationsPage,
    rejectAdminNotification,
} from '../../../features/notifications/notificationsApi';
import '../../NotificationsPage.css';

const DEFAULT_COUNTS = {
    all: 0,
    pending: 0,
    approved: 0,
    rejected: 0,
};

function AdminNotificationReviewPage() {
    const [filters, setFilters] = useState({
        status: 'all',
        type: 'all',
        audience: 'all',
    });
    const [currentPage, setCurrentPage] = useState(1);
    const [mutationError, setMutationError] = useState(null);
    const [successMessage, setSuccessMessage] = useState(null);
    const [isMutating, setIsMutating] = useState(false);
    const [approvingNotification, setApprovingNotification] = useState(null);
    const [rejectingNotification, setRejectingNotification] = useState(null);
    const [rejectionReason, setRejectionReason] = useState('');
    const { query } = useAdminSearch({
        placeholder: 'Tìm kiếm tiêu đề, nội dung hoặc loại thông báo...',
    });

    const listPath = useMemo(() => getAdminNotificationsPath({
        page: currentPage,
        status: filters.status,
        type: filters.type,
        audience: filters.audience,
        search: query,
    }), [currentPage, filters, query]);

    const listResult = useFetch(listPath, { select: mapAdminNotificationsPage });
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

    const closeApproveDialog = () => {
        if (isMutating) {
            return;
        }

        setApprovingNotification(null);
        setMutationError(null);
    };

    const confirmApproveNotification = async () => {
        if (!approvingNotification) {
            return;
        }

        setMutationError(null);
        setIsMutating(true);
        try {
            await approveAdminNotification(approvingNotification.rawId);
            setSuccessMessage('Thông báo đã được duyệt và phát hành tới ứng dụng di động.');
            setApprovingNotification(null);
            listResult.refetch();
        }
        catch (caughtError) {
            setMutationError(caughtError instanceof Error ? caughtError.message : 'Không thể duyệt thông báo.');
        }
        finally {
            setIsMutating(false);
        }
    };

    const openRejectDialog = (notification) => {
        setMutationError(null);
        setRejectingNotification(notification);
        setRejectionReason('');
    };

    const closeRejectDialog = () => {
        if (isMutating) {
            return;
        }

        setRejectingNotification(null);
        setRejectionReason('');
        setMutationError(null);
    };

    const handleRejectNotification = async () => {
        if (!rejectingNotification) {
            return;
        }

        if (!rejectionReason.trim()) {
            setMutationError('Vui lòng nhập lý do từ chối trước khi xác nhận.');
            return;
        }

        setMutationError(null);
        setIsMutating(true);
        try {
            await rejectAdminNotification(rejectingNotification.rawId, rejectionReason.trim());
            setSuccessMessage('Thông báo đã được từ chối.');
            setRejectingNotification(null);
            setRejectionReason('');
            listResult.refetch();
        }
        catch (caughtError) {
            setMutationError(caughtError instanceof Error ? caughtError.message : 'Không thể từ chối thông báo.');
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
                        <h1 className="page-title">Duyệt Thông báo</h1>
                        <p className="page-subtitle">
                            Xem yêu cầu thông báo của nhân viên, phê duyệt để đẩy tới mobile app hoặc từ chối kèm lý do.
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
                            <strong>Chưa có yêu cầu thông báo phù hợp</strong>
                            <p>Hãy thử bộ lọc khác hoặc chờ nhân viên tạo yêu cầu mới.</p>
                        </div>
                    )}

                    {listData.items.length > 0 && (
                        <>
                            <div className="notification-review__list">
                                {listData.items.map((notification) => (
                                    <article key={notification.rawId} className="notification-review__card">
                                        <div className="notification-review__card-main">
                                            <div className="notification-review__card-top">
                                                <StatusBadge label={notification.typeLabel} variant="gray" />
                                                <span>{notification.audienceLabel}</span>
                                            </div>
                                            <h3>{notification.title}</h3>
                                            <p>{notification.content}</p>
                                            <div className="notification-review__meta">
                                                <span>Tạo bởi: <strong>{notification.createdByName}</strong></span>
                                                <span>Lúc: <strong>{notification.createdAtLabel}</strong></span>
                                            </div>
                                            <NotificationResolutionSummary notification={notification} />
                                        </div>
                                        {notification.status === 'Pending' && (
                                            <div className="notification-review__actions">
                                                <button
                                                    type="button"
                                                    className="notification-review__approve"
                                                    onClick={() => setApprovingNotification(notification)}
                                                    disabled={isMutating}
                                                >
                                                    <FontAwesomeIcon icon={faClipboardCheck} />
                                                    Duyệt
                                                </button>
                                                <button
                                                    type="button"
                                                    className="notification-review__reject"
                                                    onClick={() => openRejectDialog(notification)}
                                                    disabled={isMutating}
                                                >
                                                    Từ chối
                                                </button>
                                            </div>
                                        )}
                                    </article>
                                ))}
                            </div>

                            <div className="notifications-panel__footer">
                                <span>{listData.totalItems} yêu cầu thông báo</span>
                                <Pagination
                                    currentPage={listData.page}
                                    totalPages={listData.totalPages}
                                    onPageChange={setCurrentPage}
                                />
                            </div>
                        </>
                    )}
                </section>

                <NotificationApproveDialog
                    notification={approvingNotification}
                    errorMessage={approvingNotification ? mutationError : null}
                    isSubmitting={isMutating}
                    onClose={closeApproveDialog}
                    onConfirm={confirmApproveNotification}
                />
                <NotificationRejectDialog
                    notification={rejectingNotification}
                    reason={rejectionReason}
                    errorMessage={rejectingNotification ? mutationError : null}
                    isSubmitting={isMutating}
                    onReasonChange={setRejectionReason}
                    onClose={closeRejectDialog}
                    onConfirm={handleRejectNotification}
                />
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
            <small>Chưa phát hành tới ứng dụng</small>
        </div>
    );
}

export default AdminNotificationReviewPage;
