import { useMemo, useState } from 'react';
import { FontAwesomeIcon } from '@fortawesome/react-fontawesome';
import { faCheck, faEye, faStar as faStarSolid } from '@fortawesome/free-solid-svg-icons';
import { faStar as faStarRegular } from '@fortawesome/free-regular-svg-icons';
import { AdminLayout } from '../../shared/layouts/AdminLayout';
import useAdminSearch from '../../shared/hooks/useAdminSearch';
import useFetch from '../../shared/hooks/useFetch';
import StatusBadge from '../../shared/components/StatusBadge/StatusBadge';
import Pagination from '../../shared/components/Pagination/Pagination';
import { DriverVerificationDetail } from '../../features/drivers/components';
import { getDriversPath, mapDriverList, reviewDriverKyc } from '../../features/drivers/driversApi';
import '../DriversPage.css';
import '../../features/drivers/components/DriverTable.css';

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
    { id: 'pending_kyc', label: 'Chờ duyệt KYC', countKey: 'pendingKyc' },
    { id: 'active', label: 'Đã xác minh', countKey: 'active' },
];

const PAGE_SIZE = 10;

function StaffDriverVerificationPage() {
    const [activeTab, setActiveTab] = useState('pending_kyc');
    const [selectedDriver, setSelectedDriver] = useState(null);
    const [actionDriverId, setActionDriverId] = useState(null);
    const [mutationError, setMutationError] = useState(null);
    const { query } = useAdminSearch({
        placeholder: 'Tìm kiếm tài xế, hồ sơ KYC, email hoặc số điện thoại...',
    });
    const driversPath = useMemo(() => getDriversPath(activeTab), [activeTab]);
    const { data, isLoading, error, refetch, setData } = useFetch(driversPath, {
        select: mapDriverList,
    });
    const safeDrivers = useMemo(() => data?.drivers ?? [], [data]);
    const visibleDrivers = useMemo(() => safeDrivers.filter((driver) => driverMatchesSearch(driver, query)), [query, safeDrivers]);
    const counts = data?.counts ?? EMPTY_COUNTS;

    const updateDriver = (nextDriver) => {
        setData({
            counts,
            drivers: driverMatchesFilter(nextDriver, activeTab)
                ? safeDrivers.map((driver) => (driver.id === nextDriver.id ? nextDriver : driver))
                : safeDrivers.filter((driver) => driver.id !== nextDriver.id),
        });
        setSelectedDriver((current) => (current?.id === nextDriver.id ? nextDriver : current));
    };

    const handleReviewKyc = async (driver, status, rejectionReason) => {
        setActionDriverId(driver.id);
        setMutationError(null);
        try {
            const nextDriver = await reviewDriverKyc(driver.id, status, rejectionReason);
            updateDriver(nextDriver);
            refetch();
        }
        catch (caughtError) {
            setMutationError(caughtError instanceof Error ? caughtError.message : 'Không thể duyệt hồ sơ KYC.');
        }
        finally {
            setActionDriverId(null);
        }
    };

    return (
        <AdminLayout>
            {selectedDriver ? (
                <DriverVerificationDetail
                    driver={selectedDriver}
                    onBack={() => setSelectedDriver(null)}
                    onReviewKyc={handleReviewKyc}
                    actionDriverId={actionDriverId}
                />
            ) : (
                <>
                    <div className="page-header" id="staff-driver-verification-page-header">
                        <h1 className="page-title">Xác minh thông tin Tài xế</h1>
                        <p className="page-subtitle">
                            Kiểm tra hồ sơ KYC, giấy phép và trạng thái đánh giá của tài xế trước khi phê duyệt.
                        </p>
                    </div>

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
                            Đang tải danh sách hồ sơ tài xế...
                        </div>
                    )}

                    <StaffDriverVerificationTable
                        drivers={visibleDrivers}
                        counts={counts}
                        activeTab={activeTab}
                        onTabChange={setActiveTab}
                        onSelectDriver={setSelectedDriver}
                    />
                </>
            )}
        </AdminLayout>
    );
}

function StaffDriverVerificationTable({
    drivers,
    counts,
    activeTab,
    onTabChange,
    onSelectDriver,
}) {
    const [currentPage, setCurrentPage] = useState(1);
    const totalPages = Math.max(1, Math.ceil(drivers.length / PAGE_SIZE));
    const pageDrivers = drivers.slice((currentPage - 1) * PAGE_SIZE, currentPage * PAGE_SIZE);

    const handleTabChange = (tabId) => {
        onTabChange(tabId);
        setCurrentPage(1);
    };

    return (
        <div className="driver-table-container" id="staff-driver-verification-table-container">
            <div className="driver-table-toolbar">
                <div className="driver-tabs" id="staff-driver-verification-tabs">
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
                <table className="driver-table" id="staff-driver-verification-table">
                    <thead>
                        <tr>
                            <th className="col-driver">Tài xế</th>
                            <th className="col-contact">Liên hệ</th>
                            <th className="col-rating">Đánh giá</th>
                            <th className="col-date">Ngày nộp</th>
                            <th className="col-status">Trạng thái</th>
                            <th className="col-actions">Thao tác</th>
                        </tr>
                    </thead>
                    <tbody>
                        {pageDrivers.map((driver) => {
                            const status = STATUS_MAP[driver.status] ?? STATUS_MAP.active;

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
                                                <FontAwesomeIcon icon={driver.status === 'pending_kyc' ? faCheck : faEye} />
                                                {driver.status === 'pending_kyc' ? 'Duyệt KYC' : 'Chi tiết'}
                                            </button>
                                        </div>
                                    </td>
                                </tr>
                            );
                        })}
                        {pageDrivers.length === 0 && (
                            <tr>
                                <td colSpan={6} className="driver-table-empty">
                                    Không có hồ sơ tài xế phù hợp.
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

export default StaffDriverVerificationPage;
