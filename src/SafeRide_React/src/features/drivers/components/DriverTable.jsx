import { useState } from 'react';
import { FontAwesomeIcon } from '@fortawesome/react-fontawesome';
import {
    faSlidersH,
    faCheck,
    faEye,
    faLock,
    faLockOpen,
    faStar as faStarSolid,
} from '@fortawesome/free-solid-svg-icons';
import { faStar as faStarRegular } from '@fortawesome/free-regular-svg-icons';
import { StatusBadge, Pagination } from '../../../shared/components';
import './DriverTable.css';

const STATUS_MAP = {
    active: { label: 'Hoạt động', variant: 'green' },
    pending_kyc: { label: 'Chờ KYC', variant: 'yellow' },
    blocked: { label: 'Bị khóa', variant: 'red' },
};

const TABS = [
    { id: 'all', label: 'Tất cả', countKey: 'all' },
    { id: 'active', label: 'Đang hoạt động', countKey: 'active' },
    { id: 'busy', label: 'Đang bận', countKey: 'busy' },
    { id: 'pending_kyc', label: 'Chờ duyệt KYC', countKey: 'pendingKyc' },
    { id: 'blocked', label: 'Bị khóa', countKey: 'blocked' },
];

const PAGE_SIZE = 10;

function DriverTable({
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
        <div className="driver-table-container" id="driver-table-container">
            <div className="driver-table-toolbar">
                <div className="driver-tabs" id="driver-tabs">
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
                <button type="button" className="driver-filter-btn" id="driver-filter-btn">
                    <FontAwesomeIcon icon={faSlidersH} />
                    <span>Bộ lọc nâng cao</span>
                </button>
            </div>

            <div className="driver-table-wrapper">
                <table className="driver-table" id="driver-table">
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
                            const status = STATUS_MAP[driver.status];
                            const isActionBusy = actionDriverId === driver.id;

                            return (
                                <tr key={driver.id}>
                                    <td className="col-driver">
                                        <div className="driver-cell">
                                            <div className="driver-avatar" style={{ background: driver.avatar }}>
                                                {driver.avatarUrl ? (
                                                    <img src={driver.avatarUrl} alt={driver.name} />
                                                ) : (
                                                    driver.initials
                                                )}
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
                                                    <FontAwesomeIcon
                                                        icon={faStarSolid}
                                                        className="rating-star rating-star--filled"
                                                    />
                                                    <span className="rating-value">{driver.rating}</span>
                                                </>
                                            ) : (
                                                <>
                                                    <FontAwesomeIcon
                                                        icon={faStarRegular}
                                                        className="rating-star rating-star--empty"
                                                    />
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
                                            {driver.status === 'pending_kyc' ? (
                                                <button
                                                    type="button"
                                                    className="action-link action-link--teal"
                                                    onClick={() => onSelectDriver?.(driver)}
                                                >
                                                    <FontAwesomeIcon icon={faCheck} />
                                                    Duyệt KYC
                                                </button>
                                            ) : (
                                                <button
                                                    type="button"
                                                    className="action-link action-link--teal"
                                                    onClick={() => onSelectDriver?.(driver)}
                                                >
                                                    <FontAwesomeIcon icon={faEye} />
                                                    Chi tiết
                                                </button>
                                            )}
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

export default DriverTable;
