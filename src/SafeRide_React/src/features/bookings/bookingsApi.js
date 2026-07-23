import { apiRequest } from '../../shared/api/apiClient';

const currencyFormatter = new Intl.NumberFormat('vi-VN', {
    style: 'currency',
    currency: 'VND',
    maximumFractionDigits: 0,
});

export function getAdminBookingsPath({
    page = 1,
    pageSize = 10,
    search = '',
    status = 'all',
    sortBy = 'createdAt',
    sortDirection = 'desc',
    fromDate = '',
    toDate = '',
} = {}) {
    const params = new URLSearchParams({
        page: String(page),
        pageSize: String(pageSize),
        sortBy,
        sortDirection,
    });

    if (search.trim()) {
        params.set('search', search.trim());
    }

    if (status !== 'all') {
        params.set('status', status);
    }

    if (fromDate) {
        params.set('fromDate', fromDate);
    }

    if (toDate) {
        params.set('toDate', toDate);
    }

    return `/admin/bookings?${params.toString()}`;
}

export function getAdminBookings(filters) {
    return apiRequest(getAdminBookingsPath(filters)).then(mapAdminBookingsPage);
}

export function mapAdminBookingsPage(response) {
    return {
        items: (response.items ?? []).map(mapAdminBooking),
        counts: response.counts ?? {
            total: 0,
            pending: 0,
            scheduled: 0,
            cancelledOrExpired: 0,
            nextScheduledAt: null,
            nextScheduledInMinutes: null,
        },
        page: response.page ?? 1,
        pageSize: response.pageSize ?? 10,
        totalItems: response.totalItems ?? 0,
        totalPages: response.totalPages ?? 1,
    };
}

function mapAdminBooking(item) {
    const bookingStatus = String(item.bookingStatus ?? 'Searching');
    const paymentMethod = item.paymentMethod ? String(item.paymentMethod) : null;
    const paymentStatus = item.paymentStatus ? String(item.paymentStatus) : null;
    const vehicleType = String(item.vehicleType ?? 'Car');

    return {
        rawId: item.id,
        bookingCode: item.bookingCode ?? `SR-${item.id}`,
        customerId: item.customerId,
        customerName: item.customerName ?? 'Khách hàng',
        customerPhone: item.customerPhone ?? 'Chưa cập nhật',
        customerAvatarUrl: item.customerAvatarUrl,
        customerInitials: getInitials(item.customerName ?? 'Khách hàng'),
        driverId: item.driverId,
        driverName: item.driverName ?? 'Chưa chỉ định',
        driverPhone: item.driverPhone ?? 'Chưa chỉ định',
        driverAvatarUrl: item.driverAvatarUrl,
        driverInitials: item.driverName ? getInitials(item.driverName) : 'TX',
        pickupAddress: item.pickupAddress,
        destinationAddress: item.destinationAddress ?? 'Chưa cập nhật',
        vehicleName: item.vehicleName ?? 'Chưa cập nhật',
        vehiclePlateNumber: item.vehiclePlateNumber ?? 'Chưa cập nhật',
        vehicleColor: item.vehicleColor ?? 'Chưa cập nhật',
        vehicleType,
        serviceName: item.serviceName ?? 'Chưa cập nhật',
        bookingType: String(item.bookingType ?? 'Now'),
        bookingTypeLabel: mapBookingTypeLabel(item.bookingType),
        bookingStatus,
        statusLabel: mapBookingStatusLabel(bookingStatus),
        statusVariant: mapBookingStatusVariant(bookingStatus),
        estimatedFare: item.estimatedFare ?? 0,
        estimatedFareLabel: currencyFormatter.format(item.estimatedFare ?? 0),
        paymentMethod,
        paymentMethodLabel: mapPaymentMethodLabel(paymentMethod),
        paymentStatus,
        paymentStatusLabel: mapPaymentStatusLabel(paymentStatus),
        scheduledAt: item.scheduledAt,
        scheduledAtLabel: formatDateTime(item.scheduledAt),
        createdAt: item.createdAt,
        createdAtLabel: formatDateTime(item.createdAt),
        createdDateLabel: formatDate(item.createdAt),
        updatedAt: item.updatedAt,
        updatedAtLabel: formatDateTime(item.updatedAt),
    };
}

function formatDateTime(value) {
    if (!value) {
        return 'Chưa cập nhật';
    }

    const date = new Date(value);
    if (Number.isNaN(date.getTime())) {
        return value;
    }

    return new Intl.DateTimeFormat('vi-VN', {
        day: '2-digit',
        month: '2-digit',
        year: 'numeric',
        hour: '2-digit',
        minute: '2-digit',
    }).format(date);
}

function formatDate(value) {
    if (!value) {
        return 'Chưa cập nhật';
    }

    const date = new Date(value);
    if (Number.isNaN(date.getTime())) {
        return value;
    }

    return new Intl.DateTimeFormat('vi-VN', {
        day: '2-digit',
        month: '2-digit',
        year: 'numeric',
    }).format(date);
}

function mapBookingTypeLabel(type) {
    return String(type) === 'Scheduled'
        ? 'Đặt lịch'
        : 'Đặt ngay';
}

function mapBookingStatusLabel(status) {
    return status === 'PendingSchedule'
        ? 'Đã lên lịch'
        : status === 'DriverAssigned'
            ? 'Đã chỉ định'
            : status === 'Cancelled'
                ? 'Đã hủy'
                : status === 'Expired'
                    ? 'Hết hạn'
                    : status === 'Completed'
                        ? 'Hoàn thành'
                        : 'Đang chờ';
}

function mapBookingStatusVariant(status) {
    return status === 'DriverAssigned' || status === 'Completed'
        ? 'green'
        : status === 'Cancelled' || status === 'Expired'
            ? 'red'
            : status === 'PendingSchedule'
                ? 'gray'
                : 'yellow';
}

function mapPaymentMethodLabel(method) {
    return method === 'QR'
        ? 'QR Code'
        : method === 'CASH'
            ? 'Tiền mặt'
            : 'Chưa phát sinh';
}

function mapPaymentStatusLabel(status) {
    return status === 'Success'
        ? 'Đã thanh toán'
        : status === 'Pending'
            ? 'Chờ thanh toán'
            : status === 'Failed'
                ? 'Thanh toán lỗi'
                : status === 'Cancelled'
                    ? 'Đã hủy thanh toán'
                    : status === 'Refunded'
                        ? 'Đã hoàn tiền'
                        : 'Chưa phát sinh';
}

function getInitials(name) {
    const words = String(name ?? '').trim().split(/\s+/).filter(Boolean);
    if (words.length === 0) {
        return 'SR';
    }

    return words
        .slice(-2)
        .map((word) => word[0])
        .join('')
        .toLocaleUpperCase('vi-VN');
}
