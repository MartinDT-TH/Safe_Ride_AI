import { apiRequest } from '../../../shared/api/apiClient';

export function getAdminReportsPath({
    page = 1,
    pageSize = 10,
    search = '',
    status = 'all',
} = {}) {
    const params = new URLSearchParams({
        page: String(page),
        pageSize: String(pageSize),
    });

    if (search.trim()) {
        params.set('search', search.trim());
    }
    if (status !== 'all') {
        params.set('status', status);
    }

    return `/admin/reports?${params}`;
}

export function getAdminReport(reportId, options = {}) {
    return apiRequest(`/admin/reports/${encodeURIComponent(reportId)}`, options)
        .then(mapAdminReport);
}

export function updateAdminReportStatus(reportId, status) {
    return apiRequest(`/admin/reports/${encodeURIComponent(reportId)}/status`, {
        method: 'PUT',
        body: JSON.stringify({ status }),
    }).then(mapAdminReport);
}

export function mapAdminReportsPage(response) {
    return {
        items: (response.items ?? []).map(mapAdminReport),
        page: response.page ?? 1,
        pageSize: response.pageSize ?? 10,
        totalItems: response.totalItems ?? 0,
        totalPages: response.totalPages ?? 1,
    };
}

export function mapAdminReport(item) {
    const status = normalizeStatus(item.status);

    return {
        id: item.id,
        code: String(item.id ?? '').padStart(4, '0'),
        tripId: item.tripId ?? null,
        bookingId: item.bookingId ?? null,
        reporterUserId: item.reporterUserId,
        reporterName: item.reporterName || 'Người dùng SafeRide',
        reporterEmail: item.reporterEmail || null,
        reporterPhone: item.reporterPhone || null,
        driverId: item.driverId ?? null,
        driverName: item.driverName || null,
        driverEmail: item.driverEmail || null,
        driverPhoneNumber: item.driverPhoneNumber || null,
        subject: item.subject || 'Không có tiêu đề',
        description: item.description || 'Không có nội dung',
        status,
        statusLabel: mapStatusLabel(status),
        statusVariant: mapStatusVariant(status),
        createdAt: item.createdAt,
        createdAtLabel: formatDateTime(item.createdAt),
    };
}

function normalizeStatus(status) {
    const value = String(status ?? 'Pending').toLowerCase();
    if (value === 'resolved') return 'Resolved';
    if (value === 'rejected') return 'Rejected';
    return 'Pending';
}

function mapStatusLabel(status) {
    if (status === 'Resolved') return 'Đã giải quyết';
    if (status === 'Rejected') return 'Đã từ chối';
    return 'Chờ xử lý';
}

function mapStatusVariant(status) {
    if (status === 'Resolved') return 'green';
    if (status === 'Rejected') return 'red';
    return 'yellow';
}

function formatDateTime(value) {
    if (!value) return 'Chưa có dữ liệu';
    const date = new Date(value);
    if (Number.isNaN(date.getTime())) return value;

    return new Intl.DateTimeFormat('vi-VN', {
        day: '2-digit',
        month: '2-digit',
        year: 'numeric',
        hour: '2-digit',
        minute: '2-digit',
    }).format(date);
}
