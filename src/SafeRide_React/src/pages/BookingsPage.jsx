import { useMemo, useState } from 'react';
import { FontAwesomeIcon } from '@fortawesome/react-fontawesome';
import {
    faCalendarDays,
    faCar,
    faClock,
    faEye,
    faFilter,
    faLocationDot,
    faMoneyBillWave,
    faMotorcycle,
    faSearch,
    faUser,
    faUserPlus,
    faXmark,
} from '@fortawesome/free-solid-svg-icons';
import { AdminLayout } from '../shared/layouts/AdminLayout';
import useAdminSearch from '../shared/hooks/useAdminSearch';
import useFetch from '../shared/hooks/useFetch';
import Pagination from '../shared/components/Pagination/Pagination';
import StatusBadge from '../shared/components/StatusBadge/StatusBadge';
import {
    getAdminBookingsPath,
    mapAdminBookingsPage,
} from '../features/bookings/bookingsApi';
import './BookingsPage.css';

const PAGE_SIZE = 10;
const EMPTY_COUNTS = {
    total: 0,
    pending: 0,
    scheduled: 0,
    cancelledOrExpired: 0,
    nextScheduledAt: null,
    nextScheduledInMinutes: null,
};

function BookingsPage() {
    const todayValue = useMemo(() => formatDateInput(new Date()), []);
    const initialFilters = useMemo(() => ({
        status: 'all',
        fromDate: todayValue,
        toDate: todayValue,
        sortBy: 'createdAt',
        sortDirection: 'desc',
    }), [todayValue]);
    const [draftFilters, setDraftFilters] = useState(initialFilters);
    const [appliedFilters, setAppliedFilters] = useState(initialFilters);
    const [currentPage, setCurrentPage] = useState(1);
    const [selectedBooking, setSelectedBooking] = useState(null);
    const { query, setQuery } = useAdminSearch({
        placeholder: 'Tìm kiếm yêu cầu đặt xe, khách hàng, tài xế hoặc địa điểm...',
    });

    const path = useMemo(() => getAdminBookingsPath({
        page: currentPage,
        pageSize: PAGE_SIZE,
        search: query,
        status: appliedFilters.status,
        sortBy: appliedFilters.sortBy,
        sortDirection: appliedFilters.sortDirection,
        fromDate: appliedFilters.fromDate,
        toDate: appliedFilters.toDate,
    }), [appliedFilters, currentPage, query]);

    const { data, isLoading, error, refetch } = useFetch(path, {
        select: mapAdminBookingsPage,
    });

    const bookingsData = data ?? {
        items: [],
        counts: EMPTY_COUNTS,
        page: 1,
        pageSize: PAGE_SIZE,
        totalItems: 0,
        totalPages: 1,
    };

    const handleDraftChange = (name, value) => {
        setDraftFilters((current) => ({
            ...current,
            [name]: value,
        }));
    };

    const handleApplyFilters = () => {
        setCurrentPage(1);
        setAppliedFilters(normalizeDateRangeFilters(draftFilters));
    };

    const handleSortToggle = (column) => {
        const nextDirection = appliedFilters.sortBy === column
            && appliedFilters.sortDirection === 'desc'
            ? 'asc'
            : 'desc';

        const nextFilters = {
            ...draftFilters,
            sortBy: column,
            sortDirection: nextDirection,
        };

        setCurrentPage(1);
        setDraftFilters(nextFilters);
        setAppliedFilters(normalizeDateRangeFilters(nextFilters));
    };

    return (
        <AdminLayout>
            <div className="bookings-page">
                <header className="bookings-page__header">
                    <div>
                        <h1 className="page-title">Quản lý Yêu cầu Đặt xe</h1>
                        <p className="page-subtitle">
                            Theo dõi, điều phối và xử lý các yêu cầu đặt xe từ khách hàng theo thời gian thực.
                        </p>
                    </div>
                </header>

                <div className="bookings-summary">
                    <SummaryCard
                        title="Tổng yêu cầu"
                        value={bookingsData.counts.total}
                        meta={buildDateRangeLabel(appliedFilters.fromDate, appliedFilters.toDate)}
                        icon={faSearch}
                        accent="teal"
                    />
                    <SummaryCard
                        title="Đang chờ (Pending)"
                        value={bookingsData.counts.pending}
                        meta="Đang chờ xử lý"
                        icon={faClock}
                        accent="amber"
                    />
                    <SummaryCard
                        title="Đã lên lịch"
                        value={bookingsData.counts.scheduled}
                        meta={buildNextScheduledLabel(bookingsData.counts.nextScheduledInMinutes)}
                        icon={faCalendarDays}
                        accent="blue"
                    />
                    <SummaryCard
                        title="Hủy / Hết hạn"
                        value={bookingsData.counts.cancelledOrExpired}
                        meta="Cần theo dõi"
                        icon={faXmark}
                        accent="red"
                    />
                </div>

                <section className="bookings-filter">
                    <div className="bookings-filter__search">
                            <FontAwesomeIcon icon={faSearch} className="bookings-filter__search-icon" />
                            <input
                                type="text"
                                value={query}
                                placeholder="ID yêu cầu, tên khách hàng, tài xế hoặc địa điểm..."
                                onChange={(event) => {
                                    setCurrentPage(1);
                                    setQuery(event.target.value);
                                }}
                            />
                        </div>

                    <label className="bookings-filter__field">
                        <span>Trạng thái</span>
                        <select
                            value={draftFilters.status}
                            onChange={(event) => handleDraftChange('status', event.target.value)}
                        >
                            <option value="all">Tất cả trạng thái</option>
                            <option value="Searching">Đang chờ (Pending)</option>
                            <option value="PendingSchedule">Đã lên lịch</option>
                            <option value="DriverAssigned">Đã chỉ định</option>
                            <option value="Cancelled">Đã hủy</option>
                            <option value="Expired">Hết hạn</option>
                            <option value="Completed">Hoàn thành</option>
                        </select>
                    </label>

                    <label className="bookings-filter__field">
                        <span>Khoảng ngày</span>
                        <div className="bookings-date-range">
                            <FontAwesomeIcon icon={faCalendarDays} />
                            <input
                                type="date"
                                value={draftFilters.fromDate}
                                onChange={(event) => handleDraftChange('fromDate', event.target.value)}
                            />
                            <span className="bookings-date-range__separator">-</span>
                            <input
                                type="date"
                                value={draftFilters.toDate}
                                onChange={(event) => handleDraftChange('toDate', event.target.value)}
                            />
                        </div>
                    </label>

                    <button
                        type="button"
                        className="bookings-filter__apply"
                        onClick={handleApplyFilters}
                    >
                        <FontAwesomeIcon icon={faFilter} />
                        Lọc kết quả
                    </button>
                </section>

                <section className="bookings-table-panel">
                    {error && (
                        <div className="bookings-feedback bookings-feedback--error" role="alert">
                            <span>{error}</span>
                            <button type="button" onClick={refetch}>Thử lại</button>
                        </div>
                    )}

                    {isLoading && (
                        <div className="bookings-feedback">
                            Đang tải danh sách yêu cầu đặt xe...
                        </div>
                    )}

                    {!isLoading && !error && bookingsData.items.length === 0 && (
                        <div className="bookings-empty">
                            <strong>Không tìm thấy yêu cầu đặt xe phù hợp</strong>
                            <p>
                                Hãy thử thay đổi từ khóa tìm kiếm hoặc bộ lọc trạng thái để xem thêm kết quả.
                            </p>
                        </div>
                    )}

                    {bookingsData.items.length > 0 && (
                        <>
                            <div className="bookings-table-scroll">
                                <table className="bookings-table">
                                    <thead>
                                        <tr>
                                            <th>
                                                <SortButton
                                                    label="Mã Yêu Cầu"
                                                    column="bookingId"
                                                    activeColumn={appliedFilters.sortBy}
                                                    direction={appliedFilters.sortDirection}
                                                    onToggle={handleSortToggle}
                                                />
                                            </th>
                                            <th>Khách hàng</th>
                                            <th>Loại xe</th>
                                            <th>
                                                <SortButton
                                                    label="Thời gian yêu cầu"
                                                    column="createdAt"
                                                    activeColumn={appliedFilters.sortBy}
                                                    direction={appliedFilters.sortDirection}
                                                    onToggle={handleSortToggle}
                                                />
                                            </th>
                                            <th>Điểm đón / Điểm đến</th>
                                            <th>Trạng thái</th>
                                            <th className="bookings-table__actions-heading">Thao tác</th>
                                        </tr>
                                    </thead>
                                    <tbody>
                                        {bookingsData.items.map((booking) => (
                                            <tr key={booking.rawId} className="bookings-table__row">
                                                <td className="bookings-table__code-cell">
                                                    <strong>#{booking.bookingCode}</strong>
                                                    <small>{booking.bookingTypeLabel}</small>
                                                </td>
                                                <td>
                                                    <div className="bookings-person">
                                                        <div className="bookings-person__avatar">
                                                            {booking.customerInitials}
                                                        </div>
                                                        <div className="bookings-person__content">
                                                            <strong>{booking.customerName}</strong>
                                                            <span>{booking.customerPhone}</span>
                                                            {booking.driverId && (
                                                                <small>Tài xế: {booking.driverName}</small>
                                                            )}
                                                        </div>
                                                    </div>
                                                </td>
                                                <td>
                                                    <div className="bookings-vehicle">
                                                        <FontAwesomeIcon
                                                            icon={booking.vehicleType === 'Motorbike' ? faMotorcycle : faCar}
                                                            className="bookings-vehicle__icon"
                                                        />
                                                        <div className="bookings-vehicle__content">
                                                            <strong>{booking.vehicleName}</strong>
                                                            <span>{booking.vehiclePlateNumber}</span>
                                                        </div>
                                                    </div>
                                                </td>
                                                <td>
                                                    <div className="bookings-time">
                                                        <strong>{booking.createdAtLabel}</strong>
                                                        <span>{booking.createdDateLabel}</span>
                                                        {booking.scheduledAt && (
                                                            <small>Lịch: {booking.scheduledAtLabel}</small>
                                                        )}
                                                    </div>
                                                </td>
                                                <td className="bookings-route">
                                                    <div className="bookings-route__point">
                                                        <span className="bookings-route__marker bookings-route__marker--pickup" />
                                                        <p>{booking.pickupAddress}</p>
                                                    </div>
                                                    <div className="bookings-route__line" />
                                                    <div className="bookings-route__point">
                                                        <span className="bookings-route__marker bookings-route__marker--destination" />
                                                        <p>{booking.destinationAddress}</p>
                                                    </div>
                                                </td>
                                                <td>
                                                    <StatusBadge
                                                        label={booking.statusLabel}
                                                        variant={booking.statusVariant}
                                                    />
                                                </td>
                                                <td>
                                                    <div className="bookings-actions">
                                                        <ActionButton
                                                            icon={faUserPlus}
                                                            title="Điều phối tài xế chưa nằm trong phạm vi của tính năng xem yêu cầu đặt xe."
                                                            disabled
                                                        />
                                                        <ActionButton
                                                            icon={faEye}
                                                            title="Xem chi tiết yêu cầu đặt xe"
                                                            onClick={() => setSelectedBooking(booking)}
                                                        />
                                                        <ActionButton
                                                            icon={faXmark}
                                                            title="Hủy yêu cầu chưa nằm trong phạm vi của tính năng xem yêu cầu đặt xe."
                                                            disabled
                                                            danger
                                                        />
                                                    </div>
                                                </td>
                                            </tr>
                                        ))}
                                    </tbody>
                                </table>
                            </div>

                            <div className="bookings-table-panel__footer">
                                <span>
                                    Hiển thị {bookingsData.items.length} / {bookingsData.totalItems} yêu cầu
                                </span>
                                <Pagination
                                    currentPage={bookingsData.page}
                                    totalPages={bookingsData.totalPages}
                                    onPageChange={setCurrentPage}
                                />
                            </div>
                        </>
                    )}
                </section>

                <BookingDetailsModal
                    booking={selectedBooking}
                    onClose={() => setSelectedBooking(null)}
                />
            </div>
        </AdminLayout>
    );
}

function SummaryCard({ title, value, meta, icon, accent }) {
    return (
        <article className={`bookings-summary__card bookings-summary__card--${accent}`}>
            <div className="bookings-summary__top">
                <div className="bookings-summary__icon">
                    <FontAwesomeIcon icon={icon} />
                </div>
                <span>{meta}</span>
            </div>
            <p>{title}</p>
            <strong>{value}</strong>
        </article>
    );
}

function SortButton({ label, column, activeColumn, direction, onToggle }) {
    const indicator = activeColumn !== column
        ? '↕'
        : direction === 'asc'
            ? '↑'
            : '↓';

    return (
        <button
            type="button"
            className={`bookings-sort${activeColumn === column ? ' bookings-sort--active' : ''}`}
            onClick={() => onToggle(column)}
        >
            <span>{label}</span>
            <small>{indicator}</small>
        </button>
    );
}

function ActionButton({ icon, title, onClick, disabled = false, danger = false }) {
    return (
        <button
            type="button"
            className={`bookings-actions__button${danger ? ' bookings-actions__button--danger' : ''}`}
            title={title}
            onClick={onClick}
            disabled={disabled}
        >
            <FontAwesomeIcon icon={icon} />
        </button>
    );
}

function BookingDetailsModal({ booking, onClose }) {
    if (!booking) {
        return null;
    }

    return (
        <div className="booking-modal-backdrop" onClick={onClose} role="presentation">
            <div
                className="booking-modal"
                onClick={(event) => event.stopPropagation()}
                role="dialog"
                aria-modal="true"
                aria-labelledby="booking-modal-title"
            >
                <div className="booking-modal__header">
                    <div>
                        <span className="booking-modal__eyebrow">Yêu cầu #{booking.bookingCode}</span>
                        <h2 id="booking-modal-title">Chi tiết yêu cầu đặt xe</h2>
                        <p>
                            Theo dõi đầy đủ thông tin khách hàng, tài xế, lộ trình và thanh toán của yêu cầu này.
                        </p>
                    </div>
                    <button type="button" className="booking-modal__close" onClick={onClose}>
                        <FontAwesomeIcon icon={faXmark} />
                    </button>
                </div>

                <div className="booking-modal__status">
                    <StatusBadge label={booking.statusLabel} variant={booking.statusVariant} />
                </div>

                <div className="booking-modal__summary">
                    <SummaryItem icon={faUser} label="Khách hàng" value={booking.customerName} detail={booking.customerPhone} />
                    <SummaryItem icon={faUserPlus} label="Tài xế" value={booking.driverName} detail={booking.driverPhone} />
                    <SummaryItem icon={faMoneyBillWave} label="Giá ước tính" value={booking.estimatedFareLabel} detail={booking.paymentMethodLabel} />
                    <SummaryItem icon={faCalendarDays} label="Đặt lịch" value={booking.scheduledAtLabel} detail={booking.bookingTypeLabel} />
                </div>

                <div className="booking-modal__grid">
                    <DetailField label="Mã yêu cầu" value={`#${booking.bookingCode}`} />
                    <DetailField label="Loại yêu cầu" value={booking.bookingTypeLabel} />
                    <DetailField label="Ngày tạo" value={booking.createdAtLabel} />
                    <DetailField label="Cập nhật gần nhất" value={booking.updatedAtLabel} />
                    <DetailField label="Khách hàng" value={booking.customerName} />
                    <DetailField label="Số điện thoại khách hàng" value={booking.customerPhone} />
                    <DetailField label="Tài xế đã chỉ định" value={booking.driverName} />
                    <DetailField label="Số điện thoại tài xế" value={booking.driverPhone} />
                    <DetailField label="Loại xe" value={booking.vehicleName} />
                    <DetailField label="Biển số / Màu xe" value={`${booking.vehiclePlateNumber} • ${booking.vehicleColor}`} />
                    <DetailField label="Dịch vụ" value={booking.serviceName} />
                    <DetailField label="Thanh toán" value={`${booking.paymentMethodLabel} • ${booking.paymentStatusLabel}`} />
                </div>

                <div className="booking-modal__locations">
                    <LocationCard
                        title="Điểm đón"
                        icon={faLocationDot}
                        value={booking.pickupAddress}
                        variant="pickup"
                    />
                    <LocationCard
                        title="Điểm đến"
                        icon={faLocationDot}
                        value={booking.destinationAddress}
                        variant="destination"
                    />
                </div>
            </div>
        </div>
    );
}

function SummaryItem({ icon, label, value, detail }) {
    return (
        <div className="booking-modal__summary-item">
            <div className="booking-modal__summary-icon">
                <FontAwesomeIcon icon={icon} />
            </div>
            <div>
                <span>{label}</span>
                <strong>{value}</strong>
                <small>{detail}</small>
            </div>
        </div>
    );
}

function DetailField({ label, value }) {
    return (
        <div className="booking-modal__field">
            <span>{label}</span>
            <strong>{value}</strong>
        </div>
    );
}

function LocationCard({ title, icon, value, variant }) {
    return (
        <article className={`booking-modal__location booking-modal__location--${variant}`}>
            <div className="booking-modal__location-title">
                <FontAwesomeIcon icon={icon} />
                <strong>{title}</strong>
            </div>
            <p>{value}</p>
        </article>
    );
}

function normalizeDateRangeFilters(filters) {
    if (filters.fromDate && filters.toDate && filters.fromDate > filters.toDate) {
        return {
            ...filters,
            fromDate: filters.toDate,
            toDate: filters.fromDate,
        };
    }

    return filters;
}

function formatDateInput(date) {
    const year = date.getFullYear();
    const month = `${date.getMonth() + 1}`.padStart(2, '0');
    const day = `${date.getDate()}`.padStart(2, '0');
    return `${year}-${month}-${day}`;
}

function buildDateRangeLabel(fromDate, toDate) {
    if (!fromDate && !toDate) {
        return 'Toàn bộ thời gian';
    }

    if (fromDate && toDate && fromDate === toDate) {
        return formatDateValue(fromDate);
    }

    return `${formatDateValue(fromDate)} - ${formatDateValue(toDate)}`;
}

function buildNextScheduledLabel(nextScheduledInMinutes) {
    if (nextScheduledInMinutes === null || nextScheduledInMinutes === undefined) {
        return 'Chưa có lịch gần';
    }

    if (nextScheduledInMinutes === 0) {
        return 'Đến hạn ngay';
    }

    return `Tiếp theo: ${nextScheduledInMinutes}p tới`;
}

function formatDateValue(value) {
    if (!value) {
        return 'Chưa chọn';
    }

    const [year, month, day] = value.split('-');
    return `${day}/${month}/${year}`;
}

export default BookingsPage;
