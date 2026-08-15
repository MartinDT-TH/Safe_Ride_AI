import { apiRequest } from '../../../shared/api/apiClient';

export function getActiveAdminSOSAlerts(options = {}) {
    return apiRequest('/admin/sos-alerts?status=Active&page=1&pageSize=10', options)
        .then((response) => ({
            ...response,
            items: (response.items ?? []).map(mapAdminSOSAlert),
        }));
}

export function getAdminSOSAlert(sosAlertId, options = {}) {
    return apiRequest(`/admin/sos-alerts/${encodeURIComponent(sosAlertId)}`, options)
        .then(mapAdminSOSAlert);
}

export function mapAdminSOSAlert(alert) {
    return {
        sosAlertId: alert.sosAlertId,
        tripId: alert.tripId ?? null,
        bookingId: alert.bookingId ?? null,
        customerId: alert.customerId ?? null,
        customerName: alert.customerName || 'Khách hàng SafeRide',
        customerPhoneNumber: alert.customerPhoneNumber || null,
        driverId: alert.driverId ?? null,
        driverName: alert.driverName || null,
        driverPhoneNumber: alert.driverPhoneNumber || null,
        latitude: toCoordinate(alert.latitude),
        longitude: toCoordinate(alert.longitude),
        message: alert.message || null,
        createdAt: alert.createdAt,
        createdAtLabel: formatDateTime(alert.createdAt),
    };
}

function toCoordinate(value) {
    const coordinate = Number(value);
    return Number.isFinite(coordinate) ? coordinate : null;
}

function formatDateTime(value) {
    if (!value) return 'Chưa có dữ liệu';
    const date = new Date(value);
    if (Number.isNaN(date.getTime())) return value;

    return new Intl.DateTimeFormat('vi-VN', {
        timeZone: 'Asia/Ho_Chi_Minh',
        hour: '2-digit',
        minute: '2-digit',
        day: '2-digit',
        month: '2-digit',
        year: 'numeric',
    }).format(date);
}
