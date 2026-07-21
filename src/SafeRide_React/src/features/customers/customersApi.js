import { apiRequest } from '../../shared/api/apiClient';

export function getCustomers() {
    return apiRequest(getCustomersPath()).then(mapCustomerList);
}

export function getCustomersPath() {
    return '/admin/customers';
}

export function mapCustomerList(response) {
    return {
        customers: response.customers.map(mapCustomer),
        counts: response.counts,
    };
}

export function blockCustomer(customerId, reason) {
    return apiRequest(`/admin/customers/${customerId}/block`, {
        method: 'PATCH',
        body: JSON.stringify({ reason }),
    }).then(mapCustomer);
}

export function unlockCustomer(customerId) {
    return apiRequest(`/admin/customers/${customerId}/unlock`, {
        method: 'PATCH',
    }).then(mapCustomer);
}

function mapCustomer(customer) {
    return {
        id: customer.id,
        customerCode: customer.customerCode,
        name: customer.name,
        email: customer.email ?? 'Chưa cập nhật',
        phone: customer.phone ?? 'Chưa cập nhật',
        avatar: avatarColorFromId(customer.id),
        avatarUrl: customer.avatarUrl,
        initials: getInitials(customer.name),
        joinDate: formatShortDate(customer.createdAt),
        status: customer.status,
        isActive: customer.isActive,
        banReason: customer.banReason,
        tier: customer.tier ?? 'standard',
        tierLabel: mapTier(customer.tier),
    };
}

function mapTier(tier) {
    return tier === 'premium' ? 'Premium' : 'Thường';
}

function formatShortDate(value) {
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

function getInitials(name) {
    const words = (name ?? '').trim().split(/\s+/).filter(Boolean);
    if (words.length === 0) {
        return 'KH';
    }

    return words
        .slice(-2)
        .map((word) => word[0])
        .join('')
        .toLocaleUpperCase('vi-VN');
}

function avatarColorFromId(id) {
    const colors = ['#1a8a7d', '#7c6e5a', '#4a5a6c', '#5a6c3a', '#7c3aed'];
    const index = id.split('').reduce((sum, char) => sum + char.charCodeAt(0), 0) % colors.length;
    return colors[index];
}
