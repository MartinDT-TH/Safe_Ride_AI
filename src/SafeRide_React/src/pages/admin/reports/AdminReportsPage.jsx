import { useEffect, useMemo, useState } from 'react';
import { FontAwesomeIcon } from '@fortawesome/react-fontawesome';
import {
    faEye,
    faFileCircleExclamation,
    faSearch,
    faXmark,
} from '@fortawesome/free-solid-svg-icons';
import { AdminLayout } from '../../../shared/layouts/AdminLayout';
import useAdminSearch from '../../../shared/hooks/useAdminSearch';
import useFetch from '../../../shared/hooks/useFetch';
import Pagination from '../../../shared/components/Pagination/Pagination';
import StatusBadge from '../../../shared/components/StatusBadge/StatusBadge';
import { Select } from '../../../shared/components/Select';
import {
    getAdminReport,
    getAdminReportsPath,
    mapAdminReportsPage,
    updateAdminReportStatus,
} from '../../../features/admin/reports/adminReportsApi';
import { createAdminReportsConnection } from '../../../features/admin/reports/adminReportsRealtime';
import './AdminReportsPage.css';

const PAGE_SIZE = 10;
const STATUS_OPTIONS = [
    { value: 'Pending', label: 'Chờ xử lý' },
    { value: 'Resolved', label: 'Đã giải quyết' },
    { value: 'Rejected', label: 'Đã từ chối' },
];
const STATUS_ACTIONS = [
    { value: 'Pending', label: 'Chờ xử lý' },
    { value: 'Resolved', label: 'Đã xử lý' },
    { value: 'Rejected', label: 'Từ chối' },
];

function AdminReportsPage() {
    const [statusFilter, setStatusFilter] = useState('all');
    const [currentPage, setCurrentPage] = useState(1);
    const [selectedReport, setSelectedReport] = useState(null);
    const [successMessage, setSuccessMessage] = useState('');
    const [realtimeMessage, setRealtimeMessage] = useState('');
    const [isRealtimeConnected, setIsRealtimeConnected] = useState(true);
    const { query, setQuery } = useAdminSearch({
        placeholder: 'Tìm kiếm tiêu đề, nội dung hoặc người gửi báo cáo...',
    });

    const listPath = useMemo(() => getAdminReportsPath({
        page: currentPage,
        pageSize: PAGE_SIZE,
        status: statusFilter,
        search: query,
    }), [currentPage, query, statusFilter]);

    const { data, isLoading, error, refetch } = useFetch(listPath, {
        select: mapAdminReportsPage,
    });
    const reportPage = data ?? {
        items: [],
        page: 1,
        pageSize: PAGE_SIZE,
        totalItems: 0,
        totalPages: 1,
    };

    useEffect(() => {
        let active = true;
        const connection = createAdminReportsConnection({
            onReportCreated: () => {
                if (!active) return;
                setCurrentPage(1);
                setRealtimeMessage('Có báo cáo mới vừa được gửi');
                refetch();
            },
            onConnectionChanged: (connected) => {
                if (active) setIsRealtimeConnected(connected);
            },
        });

        connection.start();
        return () => {
            active = false;
            connection.stop();
        };
    }, [refetch]);

    const handleSearchChange = (event) => {
        setCurrentPage(1);
        setQuery(event.target.value);
    };

    const handleStatusFilterChange = (event) => {
        setCurrentPage(1);
        setStatusFilter(event.target.value);
    };

    const handleUpdated = (updatedReport) => {
        setSelectedReport(updatedReport);
        setSuccessMessage('Cập nhật trạng thái thành công');
        refetch();
    };

    return (
        <AdminLayout>
            <div className="admin-reports-page">
                <header className="admin-reports-header">
                    <div>
                        <h1 className="page-title">Danh sách báo cáo</h1>
                        <p className="page-subtitle">
                            Theo dõi khiếu nại sau chuyến đi và cập nhật trạng thái xử lý.
                        </p>
                    </div>
                    <div className="admin-reports-header__count">
                        <FontAwesomeIcon icon={faFileCircleExclamation} />
                        <span>{reportPage.totalItems} báo cáo</span>
                    </div>
                </header>

                {successMessage && (
                    <div className="admin-reports-feedback admin-reports-feedback--success" role="status">
                        {successMessage}
                    </div>
                )}

                <section className="admin-reports-filters" aria-label="Bộ lọc báo cáo">
                    <label className="admin-reports-search">
                        <span className="sr-only">Tìm kiếm báo cáo</span>
                        <FontAwesomeIcon icon={faSearch} />
                        <input
                            type="search"
                            value={query}
                            placeholder="Tìm theo tiêu đề, nội dung hoặc người gửi"
                            onChange={handleSearchChange}
                        />
                    </label>
                    <label className="admin-reports-filter-field">
                        <span>Trạng thái</span>
                        <Select value={statusFilter} onChange={handleStatusFilterChange}>
                            <option value="all">Tất cả trạng thái</option>
                            {STATUS_OPTIONS.map((option) => (
                                <option key={option.value} value={option.value}>{option.label}</option>
                            ))}
                        </Select>
                    </label>
                </section>

                <section className="admin-reports-panel">
                    {realtimeMessage && (
                        <div className="admin-reports-feedback admin-reports-feedback--realtime" role="status">
                            <span>{realtimeMessage}</span>
                            <button type="button" onClick={() => setRealtimeMessage('')}>Đã xem</button>
                        </div>
                    )}
                    {!isRealtimeConnected && (
                        <div className="admin-reports-feedback admin-reports-feedback--realtime-error" role="status">
                            Không thể kết nối cập nhật báo cáo realtime
                        </div>
                    )}
                    {error && (
                        <div className="admin-reports-feedback admin-reports-feedback--error" role="alert">
                            <span>Không thể tải danh sách báo cáo</span>
                            <button type="button" onClick={refetch}>Thử lại</button>
                        </div>
                    )}

                    {isLoading && (
                        <div className="admin-reports-feedback">Đang tải danh sách báo cáo...</div>
                    )}

                    {!isLoading && !error && reportPage.items.length === 0 && (
                        <div className="admin-reports-empty">
                            <strong>Không tìm thấy báo cáo phù hợp</strong>
                            <p>Hãy thử thay đổi từ khóa hoặc trạng thái lọc.</p>
                        </div>
                    )}

                    {reportPage.items.length > 0 && (
                        <>
                            <div className="admin-reports-table-scroll">
                                <table className="admin-reports-table">
                                    <thead>
                                        <tr>
                                            <th>Mã báo cáo</th>
                                            <th>Người gửi</th>
                                            <th>Chuyến đi / Booking</th>
                                            <th>Tiêu đề</th>
                                            <th>Trạng thái</th>
                                            <th>Ngày gửi</th>
                                            <th className="admin-reports-table__action-heading">Thao tác</th>
                                        </tr>
                                    </thead>
                                    <tbody>
                                        {reportPage.items.map((report) => (
                                            <tr key={report.id}>
                                                <td className="admin-reports-table__code">#{report.code}</td>
                                                <td>
                                                    <div className="admin-reports-person">
                                                        <strong>{report.reporterName}</strong>
                                                        <small>{report.reporterEmail ?? report.reporterPhone ?? 'Không có liên hệ'}</small>
                                                    </div>
                                                </td>
                                                <td>
                                                    <div className="admin-reports-trip">
                                                        <span>Trip #{report.tripId ?? '—'}</span>
                                                        <small>Booking #{report.bookingId ?? '—'}</small>
                                                    </div>
                                                </td>
                                                <td className="admin-reports-table__subject">{report.subject}</td>
                                                <td>
                                                    <StatusBadge label={report.statusLabel} variant={report.statusVariant} />
                                                </td>
                                                <td>{report.createdAtLabel}</td>
                                                <td>
                                                    <button
                                                        type="button"
                                                        className="admin-reports-view-button"
                                                        onClick={() => {
                                                            setSuccessMessage('');
                                                            setSelectedReport(report);
                                                        }}
                                                    >
                                                        <FontAwesomeIcon icon={faEye} />
                                                        Xem / Xử lý
                                                    </button>
                                                </td>
                                            </tr>
                                        ))}
                                    </tbody>
                                </table>
                            </div>
                            <footer className="admin-reports-panel__footer">
                                <span>Hiển thị {reportPage.items.length} / {reportPage.totalItems} báo cáo</span>
                                <Pagination
                                    currentPage={reportPage.page}
                                    totalPages={reportPage.totalPages}
                                    onPageChange={setCurrentPage}
                                />
                            </footer>
                        </>
                    )}
                </section>

                {selectedReport && (
                    <ReportDetailsModal
                        key={selectedReport.id}
                        report={selectedReport}
                        onClose={() => setSelectedReport(null)}
                        onUpdated={handleUpdated}
                    />
                )}
            </div>
        </AdminLayout>
    );
}

function ReportDetailsModal({ report, onClose, onUpdated }) {
    const [detail, setDetail] = useState(report);
    const [isLoading, setIsLoading] = useState(true);
    const [isUpdating, setIsUpdating] = useState(false);
    const [loadError, setLoadError] = useState('');
    const [updateError, setUpdateError] = useState('');
    const [updateSuccess, setUpdateSuccess] = useState('');

    useEffect(() => {
        const controller = new AbortController();
        let active = true;
        getAdminReport(report.id, { signal: controller.signal })
            .then((response) => {
                if (!active) return;
                setDetail(response);
            })
            .catch((caughtError) => {
                if (active && caughtError.name !== 'AbortError') {
                    setLoadError('Không thể tải chi tiết báo cáo');
                }
            })
            .finally(() => {
                if (active) setIsLoading(false);
            });

        return () => {
            active = false;
            controller.abort();
        };
    }, [report.id]);

    const handleStatusChange = async (status) => {
        if (isUpdating || status === detail.status) return;

        setUpdateError('');
        setUpdateSuccess('');
        setIsUpdating(true);
        try {
            const updatedReport = await updateAdminReportStatus(report.id, status);
            setDetail(updatedReport);
            setUpdateSuccess('Cập nhật trạng thái thành công');
            onUpdated(updatedReport);
        }
        catch {
            setUpdateError('Không thể cập nhật trạng thái báo cáo');
        }
        finally {
            setIsUpdating(false);
        }
    };

    return (
        <div className="admin-report-modal-backdrop" onClick={onClose} role="presentation">
            <div
                className="admin-report-modal"
                role="dialog"
                aria-modal="true"
                aria-labelledby="admin-report-modal-title"
                onClick={(event) => event.stopPropagation()}
            >
                <header className="admin-report-modal__header">
                    <div className="admin-report-modal__title">
                        <span>Báo cáo #{detail.code}</span>
                        <span aria-hidden="true">·</span>
                        <h2 id="admin-report-modal-title">Chi tiết báo cáo</h2>
                        {!isLoading && !loadError && (
                            <StatusBadge label={detail.statusLabel} variant={detail.statusVariant} />
                        )}
                    </div>
                    <button type="button" onClick={onClose} title="Đóng" aria-label="Đóng">
                        <FontAwesomeIcon icon={faXmark} />
                    </button>
                </header>

                {isLoading && <div className="admin-report-modal__message">Đang tải chi tiết báo cáo...</div>}
                {loadError && <div className="admin-report-modal__message admin-report-modal__message--error">{loadError}</div>}

                {!isLoading && !loadError && (
                    <>
                        <div className="admin-report-modal__columns">
                            <section className="admin-report-detail-section admin-report-summary">
                                <h3>Thông tin người gửi</h3>
                                <dl>
                                    <div><dt>Người gửi</dt><dd>{detail.reporterName}</dd></div>
                                    <div><dt>Email</dt><dd>{detail.reporterEmail ?? 'Chưa có dữ liệu'}</dd></div>
                                    <div><dt>SĐT</dt><dd>{detail.reporterPhone ?? 'Chưa có dữ liệu'}</dd></div>
                                </dl>
                                <h3 className="admin-report-subsection-title">Thông tin chuyến đi</h3>
                                <dl>
                                    <div>
                                        <dt>Chuyến đi</dt>
                                        <dd>Trip #{detail.tripId ?? '—'} <span aria-hidden="true">·</span> Booking #{detail.bookingId ?? '—'}</dd>
                                    </div>
                                    <div><dt>Ngày gửi</dt><dd>{detail.createdAtLabel}</dd></div>
                                </dl>
                            </section>

                            <section className="admin-report-detail-section admin-report-driver-panel">
                                <h3>Thông tin tài xế</h3>
                                {detail.driverName || detail.driverEmail || detail.driverPhoneNumber ? (
                                    <dl className="admin-report-driver">
                                        <div><dt>Tài xế</dt><dd>{detail.driverName ?? 'Chưa có tên tài xế'}</dd></div>
                                        <div><dt>Email</dt><dd>{detail.driverEmail ?? 'Chưa có email'}</dd></div>
                                        <div><dt>SĐT</dt><dd>{detail.driverPhoneNumber ?? 'Chưa có SĐT'}</dd></div>
                                    </dl>
                                ) : (
                                    <p className="admin-report-driver-empty">Chưa có thông tin tài xế</p>
                                )}

                                <div className="admin-report-status-control">
                                    <div className="admin-report-status-control__heading">
                                        <h3>Cập nhật trạng thái</h3>
                                        <span>Hiện tại: {detail.statusLabel}</span>
                                    </div>
                                    <div className="admin-report-status-buttons" role="group" aria-label="Cập nhật trạng thái báo cáo">
                                        {STATUS_ACTIONS.map((option) => {
                                            const isActive = option.value === detail.status;
                                            return (
                                                <button
                                                    key={option.value}
                                                    type="button"
                                                    className={isActive ? 'is-active' : ''}
                                                    aria-pressed={isActive}
                                                    disabled={isUpdating}
                                                    onClick={() => handleStatusChange(option.value)}
                                                >
                                                    {option.label}
                                                </button>
                                            );
                                        })}
                                    </div>
                                    {isUpdating && <small className="admin-report-update-state">Đang cập nhật...</small>}
                                    {updateError && <small className="admin-report-update-state admin-report-update-state--error">{updateError}</small>}
                                    {updateSuccess && <small className="admin-report-update-state admin-report-update-state--success">{updateSuccess}</small>}
                                </div>
                            </section>
                        </div>

                        <section className="admin-report-content">
                            <h3>Nội dung báo cáo</h3>
                            <div>
                                <span>Tiêu đề</span>
                                <strong>{detail.subject}</strong>
                            </div>
                            <div>
                                <span>Nội dung</span>
                                <p>{detail.description}</p>
                            </div>
                        </section>

                    </>
                )}
            </div>
        </div>
    );
}

export default AdminReportsPage;
