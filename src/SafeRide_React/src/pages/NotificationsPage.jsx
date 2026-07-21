import { useEffect, useMemo, useState } from 'react';
import { FontAwesomeIcon } from '@fortawesome/react-fontawesome';
import {
    faArrowLeft,
    faClipboardCheck,
    faPaperPlane,
    faPlus,
    faRotateRight,
} from '@fortawesome/free-solid-svg-icons';
import { AdminLayout } from '../shared/layouts/AdminLayout';
import useAdminSearch from '../shared/hooks/useAdminSearch';
import useFetch from '../shared/hooks/useFetch';
import ActionFeedback from '../shared/components/ActionFeedback/ActionFeedback';
import Pagination from '../shared/components/Pagination/Pagination';
import StatusBadge from '../shared/components/StatusBadge/StatusBadge';
import NotificationApproveDialog from '../features/notifications/components/NotificationApproveDialog';
import NotificationRejectDialog from '../features/notifications/components/NotificationRejectDialog';
import {
    approveAdminNotification,
    createAdminNotification,
    getAdminNotificationsPath,
    mapAdminNotificationsPage,
    rejectAdminNotification,
} from '../features/notifications/notificationsApi';
import './NotificationsPage.css';

const DEFAULT_COUNTS = {
    all: 0,
    pending: 0,
    approved: 0,
    rejected: 0,
};

const TYPE_OPTIONS = [
    { value: 'all', label: 'Tất cả loại' },
    { value: 'Promotion', label: 'Khuyến mãi' },
    { value: 'System Update', label: 'Cập nhật hệ thống' },
    { value: 'Warning', label: 'Cảnh báo' },
];

const AUDIENCE_FILTER_OPTIONS = [
    { value: 'all', label: 'Tất cả đối tượng' },
    { value: 'Both', label: 'Tất cả người dùng' },
    { value: 'Driver', label: 'Tài xế' },
    { value: 'Customer', label: 'Khách hàng' },
];

const SEND_AUDIENCE_OPTIONS = [
    { value: 'Both', label: 'Tất cả người dùng', icon: '🌐' },
    { value: 'Driver', label: 'Tài xế', icon: '🚕' },
    { value: 'Customer', label: 'Khách hàng', icon: '👤' },
];

const SEND_TYPE_OPTIONS = [
    { value: 'Promotion', label: 'Khuyến mãi (Marketing & Ưu đãi)' },
    { value: 'System Update', label: 'Cập nhật hệ thống (Bảo trì & Tính năng)' },
    { value: 'Warning', label: 'Cảnh báo (An toàn & Khẩn cấp)' },
];

function NotificationsPage() {
    const [view, setView] = useState('list');
    const [filters, setFilters] = useState({
        status: 'all',
        type: 'all',
        audience: 'all',
    });
    const [currentPage, setCurrentPage] = useState(1);
    const [reviewPage, setReviewPage] = useState(1);
    const [mutationError, setMutationError] = useState(null);
    const [successMessage, setSuccessMessage] = useState(null);
    const [isMutating, setIsMutating] = useState(false);
    const [approvingNotification, setApprovingNotification] = useState(null);
    const [rejectingNotification, setRejectingNotification] = useState(null);
    const [rejectionReason, setRejectionReason] = useState('');
    const [sendForm, setSendForm] = useState({
        targetAudience: 'Both',
        notificationType: 'System Update',
        title: '',
        content: '',
    });

    const { query } = useAdminSearch({
        placeholder: view === 'send'
            ? 'Tìm kiếm tài xế, chuyến đi hoặc người dùng...'
            : 'Tìm kiếm tiêu đề, nội dung hoặc loại thông báo...',
    });

    const listPath = useMemo(() => getAdminNotificationsPath({
        page: currentPage,
        status: filters.status,
        type: filters.type,
        audience: filters.audience,
        search: query,
    }), [currentPage, filters, query]);

    const reviewPath = useMemo(() => getAdminNotificationsPath({
        page: reviewPage,
        status: 'Pending',
        search: query,
    }), [reviewPage, query]);

    const listResult = useFetch(listPath, { select: mapAdminNotificationsPage });
    const reviewResult = useFetch(reviewPath, { select: mapAdminNotificationsPage });

    const listData = listResult.data ?? {
        items: [],
        counts: DEFAULT_COUNTS,
        page: 1,
        totalPages: 1,
        totalItems: 0,
    };
    const reviewData = reviewResult.data ?? {
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

        const timeoutId = window.setTimeout(() => {
            setSuccessMessage(null);
        }, 4000);

        return () => window.clearTimeout(timeoutId);
    }, [successMessage]);

    useEffect(() => {
        setCurrentPage(1);
    }, [filters.status, filters.type, filters.audience, query]);

    useEffect(() => {
        setReviewPage(1);
    }, [query]);

    const openSendView = () => {
        setMutationError(null);
        setView('send');
    };

    const openReviewView = () => {
        setMutationError(null);
        setView('review');
    };

    const handleRefresh = () => {
        if (view === 'review') {
            reviewResult.refetch();
            return;
        }

        listResult.refetch();
    };

    const handleFilterChange = (name, value) => {
        setFilters((current) => ({
            ...current,
            [name]: value,
        }));
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
            await createAdminNotification({
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
            setSuccessMessage('Thông báo đã được lưu với trạng thái chờ duyệt.');
            setView('list');
            listResult.refetch();
            reviewResult.refetch();
        }
        catch (caughtError) {
            setMutationError(caughtError instanceof Error ? caughtError.message : 'Không thể gửi thông báo.');
        }
        finally {
            setIsMutating(false);
        }
    };

    const handleApproveNotification = async (notification) => {
        return openApproveDialog(notification);
        const confirmed = window.confirm(`Duyệt thông báo "${notification.title}"?`);
        if (!confirmed) {
            return;
        }

        setMutationError(null);
        setIsMutating(true);
        try {
            await approveAdminNotification(notification.rawId);
            setSuccessMessage('Thông báo đã được duyệt và phát hành tới người dùng phù hợp.');
            listResult.refetch();
            reviewResult.refetch();
        }
        catch (caughtError) {
            setMutationError(caughtError instanceof Error ? caughtError.message : 'Không thể duyệt thông báo.');
        }
        finally {
            setIsMutating(false);
        }
    };

    const openApproveDialog = (notification) => {
        setMutationError(null);
        setApprovingNotification(notification);
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
            setSuccessMessage('ThÃ´ng bÃ¡o Ä‘Ã£ Ä‘Æ°á»£c duyá»‡t vÃ  phÃ¡t hÃ nh tá»›i ngÆ°á»i dÃ¹ng phÃ¹ há»£p.');
            setApprovingNotification(null);
            listResult.refetch();
            reviewResult.refetch();
        }
        catch (caughtError) {
            setMutationError(caughtError instanceof Error ? caughtError.message : 'KhÃ´ng thá»ƒ duyá»‡t thÃ´ng bÃ¡o.');
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
            await rejectAdminNotification(
                rejectingNotification.rawId,
                rejectionReason.trim(),
            );
            setSuccessMessage('Thông báo đã được từ chối.');
            setRejectingNotification(null);
            setRejectionReason('');
            listResult.refetch();
            reviewResult.refetch();
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
                {view === 'list' && (
                    <>
                        <header className="notifications-page__header">
                            <div>
                                <h1 className="page-title">Quản lý Thông báo</h1>
                                <p className="page-subtitle">
                                    Theo dõi toàn bộ thông báo hệ thống, trạng thái duyệt và lịch sử phát hành.
                                </p>
                            </div>
                            <div className="notifications-page__header-actions">
                                <button type="button" className="notifications-page__ghost-btn" onClick={handleRefresh}>
                                    <FontAwesomeIcon icon={faRotateRight} />
                                    Làm mới
                                </button>
                                <button type="button" className="notifications-page__ghost-btn" onClick={openReviewView}>
                                    <FontAwesomeIcon icon={faClipboardCheck} />
                                    Duyệt chờ xử lý
                                </button>
                                <button type="button" className="notifications-page__primary-btn" onClick={openSendView}>
                                    <FontAwesomeIcon icon={faPlus} />
                                    Gửi thông báo
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
                            <SummaryCard label="Tất cả thông báo" value={listData.counts.all} />
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
                                        {TYPE_OPTIONS.map((option) => (
                                            <option key={option.value} value={option.value}>{option.label}</option>
                                        ))}
                                    </select>
                                </label>
                                <label className="notifications-field">
                                    <span>Đối tượng nhận</span>
                                    <select value={filters.audience} onChange={(event) => handleFilterChange('audience', event.target.value)}>
                                        {AUDIENCE_FILTER_OPTIONS.map((option) => (
                                            <option key={option.value} value={option.value}>{option.label}</option>
                                        ))}
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
                                    Đang tải danh sách thông báo...
                                </div>
                            )}

                            {!listResult.isLoading && !listResult.error && listData.items.length === 0 && (
                                <div className="notifications-empty">
                                    <strong>Chưa có thông báo phù hợp</strong>
                                    <p>Hãy thử bộ lọc khác hoặc tạo một thông báo mới để bắt đầu.</p>
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
                                                    <th>Người tạo</th>
                                                    <th>Thời gian</th>
                                                    <th>Trạng thái</th>
                                                    <th>Xử lý</th>
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
                                                        <td>{notification.createdByName}</td>
                                                        <td>{notification.createdAtLabel}</td>
                                                        <td>
                                                            <StatusBadge
                                                                label={notification.statusLabel}
                                                                variant={notification.statusVariant}
                                                            />
                                                        </td>
                                                        <td>
                                                            <NotificationResolutionSummary notification={notification} />
                                                            {notification.status === 'Pending' && (
                                                                <button
                                                                    type="button"
                                                                    className="notifications-page__inline-btn"
                                                                    onClick={openReviewView}
                                                                >
                                                                    Mở duyệt
                                                                </button>
                                                            )}
                                                        </td>
                                                    </tr>
                                                ))}
                                            </tbody>
                                        </table>
                                    </div>

                                    <div className="notifications-panel__footer">
                                        <span>Hiển thị {listData.items.length} / {listData.totalItems} thông báo</span>
                                        <Pagination
                                            currentPage={listData.page}
                                            totalPages={listData.totalPages}
                                            onPageChange={setCurrentPage}
                                        />
                                    </div>
                                </>
                            )}
                        </section>
                    </>
                )}

                {view === 'send' && (
                    <section className="notification-send">
                        <div className="notification-subview__header">
                            <button type="button" className="notification-subview__back" onClick={() => setView('list')}>
                                <FontAwesomeIcon icon={faArrowLeft} />
                                Quay lại danh sách
                            </button>
                            <button type="button" className="notifications-page__ghost-btn" onClick={openReviewView}>
                                <FontAwesomeIcon icon={faClipboardCheck} />
                                Duyệt chờ xử lý
                            </button>
                        </div>

                        <header className="notification-send__intro">
                            <h1 className="page-title">Gửi thông báo hệ thống</h1>
                            <p className="page-subtitle">
                                Tạo và lưu thông báo vào hàng chờ để quản trị viên xét duyệt trước khi phát hành.
                            </p>
                        </header>

                        <ActionFeedback message={successMessage} />

                        {mutationError && (
                            <div className="notifications-feedback notifications-feedback--error" role="alert">
                                <span>{mutationError}</span>
                            </div>
                        )}

                        <div className="notification-send__grid">
                            <div className="notification-send__card">
                                <section>
                                    <label className="notification-send__label">Đối tượng nhận tin</label>
                                    <div className="notification-send__audience-grid">
                                        {SEND_AUDIENCE_OPTIONS.map((option) => (
                                            <button
                                                key={option.value}
                                                type="button"
                                                className={`notification-send__audience-card${sendForm.targetAudience === option.value ? ' notification-send__audience-card--active' : ''}`}
                                                onClick={() => handleSendFieldChange('targetAudience', option.value)}
                                            >
                                                <span className="notification-send__audience-icon">{option.icon}</span>
                                                <strong>{option.label}</strong>
                                            </button>
                                        ))}
                                    </div>
                                </section>

                                <section>
                                    <label className="notification-send__label" htmlFor="notification-type">
                                        Loại thông báo
                                    </label>
                                    <select
                                        id="notification-type"
                                        className="notification-send__select"
                                        value={sendForm.notificationType}
                                        onChange={(event) => handleSendFieldChange('notificationType', event.target.value)}
                                    >
                                        {SEND_TYPE_OPTIONS.map((option) => (
                                            <option key={option.value} value={option.value}>{option.label}</option>
                                        ))}
                                    </select>
                                </section>

                                <section className="notification-send__fields">
                                    <label className="notification-send__label" htmlFor="notification-title">
                                        Tiêu đề thông báo
                                    </label>
                                    <input
                                        id="notification-title"
                                        className="notification-send__input"
                                        type="text"
                                        value={sendForm.title}
                                        maxLength="40"
                                        placeholder="Ví dụ: Lịch bảo trì định kỳ"
                                        onChange={(event) => handleSendFieldChange('title', event.target.value)}
                                    />
                                    <div className="notification-send__count">{sendForm.title.length}/40</div>

                                    <label className="notification-send__label" htmlFor="notification-content">
                                        Nội dung tin nhắn
                                    </label>
                                    <textarea
                                        id="notification-content"
                                        className="notification-send__textarea"
                                        value={sendForm.content}
                                        maxLength="140"
                                        rows="4"
                                        placeholder="Mô tả ngắn gọn về bản cập nhật hoặc khuyến mãi..."
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
                                    {isMutating ? 'Đang lưu...' : 'Gửi thông báo ngay'}
                                </button>
                            </div>

                            <aside className="notification-send__preview notification-send__preview--empty" aria-hidden="true" />
                        </div>
                    </section>
                )}

                {view === 'review' && (
                    <section className="notification-review">
                        <div className="notification-subview__header">
                            <button type="button" className="notification-subview__back" onClick={() => setView('list')}>
                                <FontAwesomeIcon icon={faArrowLeft} />
                                Quay lại danh sách
                            </button>
                            <button type="button" className="notifications-page__primary-btn" onClick={openSendView}>
                                <FontAwesomeIcon icon={faPaperPlane} />
                                Tạo thông báo mới
                            </button>
                        </div>

                        <header className="notifications-page__header">
                            <div>
                                <h1 className="page-title">Duyệt thông báo chờ xử lý</h1>
                                <p className="page-subtitle">
                                    Chỉ các thông báo được duyệt mới được phát hành tới ứng dụng di động.
                                </p>
                            </div>
                            <div className="notifications-page__header-actions">
                                <button type="button" className="notifications-page__ghost-btn" onClick={() => reviewResult.refetch()}>
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

                        <section className="notifications-panel">
                            {reviewResult.error && (
                                <div className="notifications-feedback notifications-feedback--error">
                                    <span>{reviewResult.error}</span>
                                    <button type="button" onClick={reviewResult.refetch}>Thử lại</button>
                                </div>
                            )}

                            {reviewResult.isLoading && (
                                <div className="notifications-feedback">
                                    Đang tải thông báo chờ duyệt...
                                </div>
                            )}

                            {!reviewResult.isLoading && !reviewResult.error && reviewData.items.length === 0 && (
                                <div className="notifications-empty">
                                    <strong>Không còn thông báo nào chờ duyệt</strong>
                                    <p>Mọi thông báo hiện tại đã được xử lý hoặc chưa có yêu cầu mới.</p>
                                </div>
                            )}

                            <div className="notification-review__list">
                                {reviewData.items.map((notification) => (
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
                                        </div>
                                        <div className="notification-review__actions">
                                            <button
                                                type="button"
                                                className="notification-review__approve"
                                                onClick={() => openApproveDialog(notification)}
                                                disabled={isMutating}
                                            >
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
                                    </article>
                                ))}
                            </div>

                            {reviewData.items.length > 0 && (
                                <div className="notifications-panel__footer">
                                    <span>{reviewData.totalItems} thông báo chờ duyệt</span>
                                    <Pagination
                                        currentPage={reviewData.page}
                                        totalPages={reviewData.totalPages}
                                        onPageChange={setReviewPage}
                                    />
                                </div>
                            )}
                        </section>
                    </section>
                )}

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

export default NotificationsPage;
