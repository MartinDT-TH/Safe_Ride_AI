import { apiRequest } from '../../../shared/api/apiClient';

export function getStaffNotificationRequestsPath({
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

    if (search.trim()) params.set('search', search.trim());
    if (status !== 'all') params.set('status', status);
    if (type !== 'all') params.set('type', type);
    if (audience !== 'all') params.set('audience', audience);

    return `/staff/notifications?${params}`;
}

export function mapStaffNotificationRequestsPage(response) {
    return {
        items: (response.items ?? []).map(mapStaffNotificationRequest),
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

export function createStaffNotificationRequest(payload) {
    return apiRequest('/staff/notifications', {
        method: 'POST',
        body: JSON.stringify(payload),
    }).then(mapStaffNotificationRequest);
}

function mapStaffNotificationRequest(item) {
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
    if (!value) return null;
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
