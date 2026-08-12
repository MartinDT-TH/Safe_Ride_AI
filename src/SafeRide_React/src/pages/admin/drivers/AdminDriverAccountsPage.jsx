import { useEffect, useMemo, useState } from 'react';
import { FontAwesomeIcon } from '@fortawesome/react-fontawesome';
import { faEye, faLock, faLockOpen, faStar as faStarSolid } from '@fortawesome/free-solid-svg-icons';
import { faStar as faStarRegular } from '@fortawesome/free-regular-svg-icons';
import { AdminLayout } from '../../../shared/layouts/AdminLayout';
import useAdminSearch from '../../../shared/hooks/useAdminSearch';
import useFetch from '../../../shared/hooks/useFetch';
import StatusBadge from '../../../shared/components/StatusBadge/StatusBadge';
import Pagination from '../../../shared/components/Pagination/Pagination';
import ActionFeedback from '../../../shared/components/ActionFeedback/ActionFeedback';
import AccountActionDialog from '../../../shared/components/AccountActionDialog/AccountActionDialog';
import { blockDriver, getDriversPath, mapDriverList, unlockDriver } from '../../../features/drivers/driversApi';
import '../../../pages/DriversPage.css';
import '../../../features/drivers/components/DriverTable.css';
import '../../../features/drivers/components/DriverVerificationDetail.css';

const EMPTY_COUNTS = {
    all: 0,
    active: 0,
    busy: 0,
    pendingKyc: 0,
    blocked: 0,
};

const STATUS_MAP = {
    active: { label: 'Hoạt động', variant: 'green' },
    pending_kyc: { label: 'Chờ KYC', variant: 'yellow' },
    blocked: { label: 'Bị khóa', variant: 'red' },
};

const TABS = [
    { id: 'all', label: 'Tất cả', countKey: 'all' },
    { id: 'active', label: 'Đang hoạt động', countKey: 'active' },
    { id: 'busy', label: 'Đang bận', countKey: 'busy' },
    { id: 'blocked', label: 'Bị khóa', countKey: 'blocked' },
];

const PAGE_SIZE = 10;

function AdminDriverAccountsPage() {
    const [activeTab, setActiveTab] = useState('all');
    const [selectedDriver, setSelectedDriver] = useState(null);
    const [actionDriverId, setActionDriverId] = useState(null);
    const [mutationError, setMutationError] = useState(null);
    const [successMessage, setSuccessMessage] = useState(null);
    const [pendingDriverAction, setPendingDriverAction] = useState(null);
    const { query } = useAdminSearch({
        placeholder: 'Tìm kiếm tài xế, mã tài xế, email hoặc số điện thoại...',
    });
    const driversPath = useMemo(() => getDriversPath(activeTab), [activeTab]);
    const { data, isLoading, error, refetch, setData } = useFetch(driversPath, {
        select: mapDriverList,
    });
    const safeDrivers = useMemo(() => data?.drivers ?? [], [data]);
    const visibleDrivers = useMemo(() => safeDrivers.filter((driver) => driverMatchesSearch(driver, query)), [query, safeDrivers]);
    const counts = data?.counts ?? EMPTY_COUNTS;

    useEffect(() => {
        if (!successMessage) {
            return undefined;
        }

        const timeoutId = window.setTimeout(() => setSuccessMessage(null), 4000);
        return () => window.clearTimeout(timeoutId);
    }, [successMessage]);

    const updateDriver = (nextDriver) => {
        setData({
            counts,
            drivers: driverMatchesFilter(nextDriver, activeTab)
                ? safeDrivers.map((driver) => (driver.id === nextDriver.id ? nextDriver : driver))
                : safeDrivers.filter((driver) => driver.id !== nextDriver.id),
        });
        setSelectedDriver((current) => (current?.id === nextDriver.id ? nextDriver : current));
    };

    const closeAccountActionDialog = () => {
        if (actionDriverId) {
            return;
        }

        setMutationError(null);
        setPendingDriverAction(null);
    };

    const handleConfirmDriverAction = async (payload = {}) => {
        const driver = pendingDriverAction?.driver;
        const actionMode = pendingDriverAction?.mode;
        if (!driver || !actionMode) {
            return;
        }

        setActionDriverId(driver.id);
        setMutationError(null);

        try {
            const nextDriver = actionMode === 'lock'
                ? await blockDriver(driver.id, payload.reason)
                : await unlockDriver(driver.id);
            updateDriver(nextDriver);
            refetch();
            setSuccessMessage(actionMode === 'lock'
                ? 'Đã khóa tài khoản tài xế thành công.'
                : 'Đã mở khóa tài khoản tài xế thành công.');
            setPendingDriverAction(null);
        }
        catch (caughtError) {
            setMutationError(caughtError instanceof Error ? caughtError.message : 'Không thể cập nhật tài xế.');
        }
        finally {
            setActionDriverId(null);
        }
    };

    const handleToggleBlock = (driver) => {
        setMutationError(null);
        setPendingDriverAction({
            mode: driver.isActive ? 'lock' : 'unlock',
            driver,
        });
    };

    return (
        <AdminLayout>
            {selectedDriver ? (
                <DriverAccountDetails driver={selectedDriver} onBack={() => setSelectedDriver(null)} />
            ) : (
                <>
                    <div className="page-header" id="admin-driver-accounts-page-header">
                        <h1 className="page-title">Tài khoản Tài xế</h1>
                        <p className="page-subtitle">
                            Xem tài khoản tài xế, đánh giá và thực hiện khóa hoặc mở khóa tài khoản khi cần.
                        </p>
                    </div>

                    <ActionFeedback message={successMessage} />

                    {error && (
                        <div className="drivers-feedback drivers-feedback--error">
                            <span>{error}</span>
                            <button type="button" onClick={refetch}>Thử lại</button>
                        </div>
                    )}

                    {mutationError && (
                        <div className="drivers-feedback drivers-feedback--error">
                            <span>{mutationError}</span>
                        </div>
                    )}

                    {isLoading && (
                        <div className="drivers-feedback">
                            Đang tải danh sách tài xế...
                        </div>
                    )}

                    <AdminDriverAccountTable
                        drivers={visibleDrivers}
                        counts={counts}
                        activeTab={activeTab}
                        onTabChange={setActiveTab}
                        onSelectDriver={setSelectedDriver}
                        onToggleBlock={handleToggleBlock}
                        actionDriverId={actionDriverId}
                    />

                    <AccountActionDialog
                        key={pendingDriverAction ? `${pendingDriverAction.mode}-${pendingDriverAction.driver.id}` : 'driver-account-action-closed'}
                        isOpen={Boolean(pendingDriverAction)}
                        mode={pendingDriverAction?.mode}
                        accountType="driver"
                        accountName={pendingDriverAction?.driver?.name}
                        currentReason={pendingDriverAction?.driver?.banReason}
                        isSubmitting={actionDriverId === pendingDriverAction?.driver?.id}
                        errorMessage={pendingDriverAction ? mutationError : null}
                        onClose={closeAccountActionDialog}
                        onConfirm={handleConfirmDriverAction}
                    />
                </>
            )}
        </AdminLayout>
    );
}

function AdminDriverAccountTable({
    drivers,
    counts,
    activeTab,
    onTabChange,
    onSelectDriver,
    onToggleBlock,
    actionDriverId,
}) {
    const [currentPage, setCurrentPage] = useState(1);
    const totalPages = Math.max(1, Math.ceil(drivers.length / PAGE_SIZE));
    const pageDrivers = drivers.slice((currentPage - 1) * PAGE_SIZE, currentPage * PAGE_SIZE);

    const handleTabChange = (tabId) => {
        onTabChange(tabId);
        setCurrentPage(1);
    };

    return (
        <div className="driver-table-container" id="admin-driver-account-table-container">
            <div className="driver-table-toolbar">
                <div className="driver-tabs" id="admin-driver-account-tabs">
                    {TABS.map((tab) => (
                        <button
                            key={tab.id}
                            type="button"
                            className={`driver-tab${activeTab === tab.id ? ' driver-tab--active' : ''}`}
                            onClick={() => handleTabChange(tab.id)}
                        >
                            {tab.label}
                            <span className="driver-tab-count">{counts[tab.countKey]}</span>
                        </button>
                    ))}
                </div>
            </div>

            <div className="driver-table-wrapper">
                <table className="driver-table" id="admin-driver-account-table">
                    <thead>
                        <tr>
                            <th className="col-driver">Tài xế</th>
                            <th className="col-contact">Liên hệ</th>
                            <th className="col-rating">Đánh giá</th>
                            <th className="col-date">Ngày gia nhập</th>
                            <th className="col-status">Trạng thái</th>
                            <th className="col-actions">Thao tác</th>
                        </tr>
                    </thead>
                    <tbody>
                        {pageDrivers.map((driver) => {
                            const status = STATUS_MAP[driver.status] ?? STATUS_MAP.active;
                            const isActionBusy = actionDriverId === driver.id;

                            return (
                                <tr key={driver.id}>
                                    <td className="col-driver">
                                        <div className="driver-cell">
                                            <div className="driver-avatar" style={{ background: driver.avatar }}>
                                                {driver.avatarUrl ? <img src={driver.avatarUrl} alt={driver.name} /> : driver.initials}
                                            </div>
                                            <span className="driver-name">{driver.name}</span>
                                        </div>
                                    </td>

                                    <td className="col-contact">
                                        <div className="contact-cell">
                                            <span className="contact-email">{driver.email}</span>
                                            <span className="contact-phone">{driver.phone}</span>
                                        </div>
                                    </td>

                                    <td className="col-rating">
                                        <div className="rating-cell">
                                            {driver.rating !== null ? (
                                                <>
                                                    <FontAwesomeIcon icon={faStarSolid} className="rating-star rating-star--filled" />
                                                    <span className="rating-value">{driver.rating}</span>
                                                </>
                                            ) : (
                                                <>
                                                    <FontAwesomeIcon icon={faStarRegular} className="rating-star rating-star--empty" />
                                                    <span className="rating-value">N/A</span>
                                                </>
                                            )}
                                            <span className="rating-trips">{driver.trips} chuyến</span>
                                        </div>
                                    </td>

                                    <td className="col-date">{driver.joinDate}</td>

                                    <td className="col-status">
                                        <StatusBadge label={status.label} variant={status.variant} />
                                    </td>

                                    <td className="col-actions">
                                        <div className="actions-cell">
                                            <button
                                                type="button"
                                                className="action-link action-link--teal"
                                                onClick={() => onSelectDriver?.(driver)}
                                            >
                                                <FontAwesomeIcon icon={faEye} />
                                                Chi tiết
                                            </button>
                                            <button
                                                type="button"
                                                className={`action-link action-link--button ${driver.isActive ? 'action-link--red' : 'action-link--teal'}`}
                                                onClick={() => onToggleBlock?.(driver)}
                                                disabled={isActionBusy}
                                            >
                                                <FontAwesomeIcon icon={driver.isActive ? faLock : faLockOpen} />
                                                {driver.isActive ? 'Khóa tài khoản' : 'Mở khóa'}
                                            </button>
                                        </div>
                                    </td>
                                </tr>
                            );
                        })}
                        {pageDrivers.length === 0 && (
                            <tr>
                                <td colSpan={6} className="driver-table-empty">
                                    Không có tài xế phù hợp.
                                </td>
                            </tr>
                        )}
                    </tbody>
                </table>
            </div>

            <Pagination
                currentPage={currentPage}
                totalPages={totalPages}
                onPageChange={setCurrentPage}
            />
        </div>
    );
}

function DriverAccountDetails({ driver, onBack }) {
    const status = STATUS_MAP[driver.status] ?? STATUS_MAP.active;

    return (
        <section className="driver-verification" id="admin-driver-account-details">
            <button type="button" className="verification-back-btn" onClick={onBack}>
                <FontAwesomeIcon icon={faEye} />
                Quay lại
            </button>

            <div className="verification-title-row">
                <h1 className="verification-title">Chi tiết tài khoản Tài xế</h1>
                <StatusBadge label={status.label} variant={status.variant} />
            </div>

            <div className="verification-grid">
                <div className="verification-side">
                    <article className="driver-profile-card">
                        <div className="profile-photo-ring">
                            <div className={`profile-photo${driver.avatarUrl ? ' profile-photo--image' : ''}`} style={{ backgroundColor: driver.avatar }}>
                                {driver.avatarUrl ? <img src={driver.avatarUrl} alt={driver.name} /> : <span>{driver.initials}</span>}
                            </div>
                        </div>

                        <h2>{driver.name}</h2>
                        <p>DRIVER_ID: {driver.driverCode}</p>

                        <dl className="profile-meta-list">
                            <div>
                                <dt>Số điện thoại</dt>
                                <dd>{driver.phone}</dd>
                            </div>
                            <div>
                                <dt>Email</dt>
                                <dd title={driver.email}>{driver.email}</dd>
                            </div>
                            <div>
                                <dt>Ngày đăng ký</dt>
                                <dd>{driver.registeredDate}</dd>
                            </div>
                            <div>
                                <dt>Thành phố</dt>
                                <dd>{driver.city}</dd>
                            </div>
                        </dl>
                    </article>
                </div>

                <article className="verification-doc-card">
                    <dl className="document-info-panel">
                        <InfoItem label="Đánh giá trung bình" value={driver.rating ?? 'N/A'} />
                        <InfoItem label="Tổng số chuyến" value={driver.trips} />
                        <InfoItem label="Trạng thái làm việc" value={driver.workStatus} />
                        <InfoItem label="Trạng thái tài khoản" value={driver.isActive ? 'Đang hoạt động' : 'Bị khóa'} />
                        <InfoItem label="Lý do khóa" value={driver.banReason ?? 'Không có'} wide />
                    </dl>
                </article>
            </div>
        </section>
    );
}

function InfoItem({ label, value, wide = false }) {
    return (
        <div className={wide ? 'document-info-panel__wide' : undefined}>
            <dt>{label}</dt>
            <dd>{value}</dd>
        </div>
    );
}

function driverMatchesSearch(driver, query) {
    const normalizedQuery = normalizeSearchQuery(query);
    if (!normalizedQuery) {
        return true;
    }
    const digitQuery = normalizeDigits(query);
    const searchableValues = [
        driver.name,
        driver.email,
        driver.phone,
        driver.id,
        driver.driverCode,
    ];
    return searchableValues
        .map(normalizeSearchQuery)
        .some((value) => value.includes(normalizedQuery))
        || (digitQuery.length > 0 && normalizeDigits(driver.phone).includes(digitQuery));
}

function driverMatchesFilter(driver, filter) {
    if (filter === 'all') {
        return true;
    }
    if (filter === 'busy') {
        return driver.workStatus === 'Busy';
    }
    return driver.status === filter;
}

function normalizeSearchQuery(value) {
    return String(value ?? '').trim().toLocaleLowerCase('vi-VN');
}

function normalizeDigits(value) {
    return String(value ?? '').replace(/\D/g, '');
}

export default AdminDriverAccountsPage;
