import { useEffect, useMemo, useState } from 'react';
import { FontAwesomeIcon } from '@fortawesome/react-fontawesome';
import { faFilter, faLock, faLockOpen, faSearch } from '@fortawesome/free-solid-svg-icons';
import { Pagination, StatusBadge } from '../../../shared/components';
import './CustomerTable.css';

const FILTERS = [
    { id: 'all', label: 'Tất cả khách hàng' },
    { id: 'active', label: 'Hoạt động' },
    { id: 'blocked', label: 'Bị khóa' },
    { id: 'premium', label: 'Premium' },
];

const STATUS_MAP = {
    active: { label: 'Hoạt động', variant: 'green' },
    blocked: { label: 'Bị khóa', variant: 'red' },
};

const PAGE_SIZE = 10;

function CustomerTable({
    customers,
    totalCustomers,
    activeFilter,
    onFilterChange,
    onToggleBlock,
    actionCustomerId,
    searchQuery,
    onSearchChange,
}) {
    const [currentPage, setCurrentPage] = useState(1);
    const totalPages = Math.max(1, Math.ceil(customers.length / PAGE_SIZE));
    const pageCustomers = useMemo(
        () => customers.slice((currentPage - 1) * PAGE_SIZE, currentPage * PAGE_SIZE),
        [customers, currentPage],
    );
    const pageStart = customers.length === 0 ? 0 : (currentPage - 1) * PAGE_SIZE + 1;
    const pageEnd = pageCustomers.length === 0 ? 0 : pageStart + pageCustomers.length - 1;
    const displayTotal = searchQuery.trim() || activeFilter !== 'all'
        ? customers.length
        : totalCustomers;

    useEffect(() => {
        setCurrentPage(1);
    }, [activeFilter, searchQuery]);

    useEffect(() => {
        if (currentPage > totalPages) {
            setCurrentPage(totalPages);
        }
    }, [currentPage, totalPages]);

    return (
        <section className="customer-table-card" id="customer-table-card">
            <div className="customer-table-toolbar">
                <div className="customer-search-box">
                    <FontAwesomeIcon icon={faSearch} className="customer-search-icon" />
                    <input
                        type="text"
                        className="customer-search-input"
                        placeholder="Tìm theo tên, email, số điện thoại hoặc ID..."
                        value={searchQuery}
                        onChange={(event) => onSearchChange?.(event.target.value)}
                    />
                </div>

                <div className="customer-filter-group">
                    {FILTERS.map((filter) => (
                        <button
                            key={filter.id}
                            type="button"
                            className={`customer-filter-btn${activeFilter === filter.id ? ' customer-filter-btn--active' : ''}`}
                            onClick={() => onFilterChange?.(filter.id)}
                        >
                            {filter.label}
                        </button>
                    ))}
                    <button type="button" className="customer-filter-icon-btn" aria-label="Bộ lọc nâng cao">
                        <FontAwesomeIcon icon={faFilter} />
                    </button>
                </div>
            </div>

            <div className="customer-table-wrapper">
                <table className="customer-table" id="customer-table">
                    <thead>
                        <tr>
                            <th className="col-name">Khách hàng</th>
                            <th className="col-contact">Liên hệ</th>
                            <th className="col-date">Ngày tham gia</th>
                            <th className="col-tier">Hạng tài khoản</th>
                            <th className="col-status">Trạng thái</th>
                            <th className="col-actions">Thao tác</th>
                        </tr>
                    </thead>
                    <tbody>
                        {pageCustomers.map((customer) => {
                            const status = STATUS_MAP[customer.status];
                            const isActionBusy = actionCustomerId === customer.id;

                            return (
                                <tr
                                    key={customer.id}
                                    className={customer.status === 'blocked' ? 'customer-row customer-row--blocked' : 'customer-row'}
                                >
                                    <td className="col-name">
                                        <div className="customer-name-cell">
                                            <div className="customer-avatar" style={{ background: customer.avatar }}>
                                                {customer.avatarUrl ? (
                                                    <img src={customer.avatarUrl} alt={customer.name} />
                                                ) : (
                                                    customer.initials
                                                )}
                                            </div>
                                            <div className="customer-meta">
                                                <span className="customer-name">{customer.name}</span>
                                                <span className="customer-code">ID: {customer.customerCode}</span>
                                            </div>
                                        </div>
                                    </td>
                                    <td className="col-contact">
                                        <div className="customer-contact-cell">
                                            <span className="customer-email">{customer.email}</span>
                                            <span className="customer-phone">{customer.phone}</span>
                                        </div>
                                    </td>
                                    <td className="col-date">{customer.joinDate}</td>
                                    <td className="col-tier">
                                        <span className={`customer-tier customer-tier--${customer.tier}`}>
                                            {customer.tierLabel}
                                        </span>
                                    </td>
                                    <td className="col-status">
                                        <StatusBadge label={status.label} variant={status.variant} />
                                    </td>
                                    <td className="col-actions">
                                        <button
                                            type="button"
                                            className={`customer-action-btn${customer.isActive ? ' customer-action-btn--danger' : ''}`}
                                            onClick={() => onToggleBlock?.(customer)}
                                            disabled={isActionBusy}
                                        >
                                            <FontAwesomeIcon icon={customer.isActive ? faLock : faLockOpen} />
                                            {customer.isActive ? 'Khóa tài khoản' : 'Mở khóa'}
                                        </button>
                                    </td>
                                </tr>
                            );
                        })}
                        {pageCustomers.length === 0 && (
                            <tr>
                                <td colSpan={6} className="customer-table-empty">
                                    Không có khách hàng phù hợp.
                                </td>
                            </tr>
                        )}
                    </tbody>
                </table>
            </div>

            <div className="customer-table-footer">
                <p className="customer-table-summary">
                    {displayTotal === 0
                        ? 'Hiển thị 0 khách hàng'
                        : `Hiển thị ${pageStart} - ${pageEnd} trong số ${displayTotal} khách hàng`}
                </p>
                <Pagination currentPage={currentPage} totalPages={totalPages} onPageChange={setCurrentPage} />
            </div>
        </section>
    );
}

export default CustomerTable;
