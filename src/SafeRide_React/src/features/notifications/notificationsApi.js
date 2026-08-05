import { apiRequest } from '../../shared/api/apiClient';

export function getAdminNotificationsPath({
    page = 1,
    pageSize = 10,
    search = '',
    status = 'all',
    type = 'all',
    audience = 'all',
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
    if (type !== 'all') {
        params.set('type', type);
    }
    if (audience !== 'all') {
        params.set('audience', audience);
    }

    return `/admin/notifications?${params}`;
}

export function mapAdminNotificationsPage(response) {
    return {
        items: (response.items ?? []).map(mapAdminNotification),
        counts: response.counts ?? {
            all: 0,
            pending: 0,
            approved: 0,
            rejected: 0,
        },
        page: response.page ?? 1,
        pageSize: response.pageSize ?? 10,
        totalItems: response.totalItems ?? 0,
        totalPages: response.totalPages ?? 1,
    };
}

export function createAdminNotification(payload) {
    return apiRequest('/admin/notifications', {
        method: 'POST',
        body: JSON.stringify(payload),
    }).then(mapAdminNotification);
}

export function approveAdminNotification(notificationId) {
    return apiRequest(`/admin/notifications/${notificationId}/approve`, {
        method: 'POST',
    }).then(mapAdminNotification);
}

export function rejectAdminNotification(notificationId, rejectionReason) {
    return apiRequest(`/admin/notifications/${notificationId}/reject`, {
        method: 'POST',
        body: JSON.stringify({ rejectionReason }),
    }).then(mapAdminNotification);
}

function mapAdminNotification(item) {
    const status = String(item.status ?? 'Pending');

    return {
        rawId: item.id,
        title: item.title,
        content: item.content,
        type: item.notificationType,
        typeLabel: mapTypeLabel(item.notificationType),
        audience: item.targetAudience,
        audienceLabel: mapAudienceLabel(item.targetAudience),
        status,
        statusLabel: mapStatusLabel(status),
        statusVariant: mapStatusVariant(status),
        createdBy: item.createdBy,
        createdByName: item.createdByName,
        createdAt: item.createdAt,
        createdAtLabel: formatDateTime(item.createdAt),
        approvedBy: item.approvedBy,
        approvedByName: item.approvedByName,
        approvedAt: item.approvedAt,
        approvedAtLabel: formatDateTime(item.approvedAt),
        rejectedBy: item.rejectedBy,
        rejectedByName: item.rejectedByName,
        rejectedAt: item.rejectedAt,
        rejectedAtLabel: formatDateTime(item.rejectedAt),
        rejectedReason: item.rejectedReason,
    };
}

function formatDateTime(value) {
    if (!value) {
        return null;
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

function mapAudienceLabel(audience) {
    return audience === 'Customer'
        ? 'Khách hàng'
        : audience === 'Driver'
            ? 'Tài xế'
            : 'Tất cả người dùng';
}

function mapTypeLabel(type) {
    return type === 'Promotion'
        ? 'Khuyến mãi'
        : type === 'Warning'
            ? 'Cảnh báo'
            : 'Cập nhật hệ thống';
}

function mapStatusLabel(status) {
    return status === 'Approved'
        ? 'Đã duyệt'
        : status === 'Rejected'
            ? 'Đã từ chối'
            : 'Đang chờ';
}

function mapStatusVariant(status) {
    return status === 'Approved'
        ? 'green'
        : status === 'Rejected'
            ? 'red'
            : 'yellow';
}
