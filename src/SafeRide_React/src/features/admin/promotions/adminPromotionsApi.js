import { apiRequest } from '../../../shared/api/apiClient';

const currencyFormatter = new Intl.NumberFormat('vi-VN', {
    style: 'currency',
    currency: 'VND',
    maximumFractionDigits: 0,
});

const numberFormatter = new Intl.NumberFormat('vi-VN', {
    maximumFractionDigits: 2,
});

const dateFormatter = new Intl.DateTimeFormat('vi-VN', {
    timeZone: 'Asia/Ho_Chi_Minh',
});

export function getAdminPromotionsPath({
    page = 1,
    pageSize = 10,
    search = '',
    status = 'all',
} = {}) {
    const params = new URLSearchParams({
        page: String(page),
        pageSize: String(pageSize),
    });
    const normalizedSearch = search.trim();

    if (normalizedSearch) {
        params.set('search', normalizedSearch);
    }
    if (status !== 'all') {
        params.set('status', status);
    }

    return `/admin/promotions?${params}`;
}

export function mapAdminPromotionsPage(response = {}) {
    const sourceItems = read(response, 'items', 'Items') ?? [];
    const items = Array.isArray(sourceItems)
        ? sourceItems.map(mapPromotion)
        : [];
    const sourceCounts = read(response, 'counts', 'Counts') ?? {};
    const derivedCounts = countStatuses(items);
    const page = toNumber(read(response, 'page', 'Page'), 1);
    const pageSize = toNumber(read(response, 'pageSize', 'PageSize'), 10);
    const totalItems = toNumber(
        read(response, 'totalItems', 'TotalItems'),
        items.length,
    );

    return {
        items,
        counts: {
            total: toNumber(
                read(sourceCounts, 'total', 'Total'),
                totalItems,
            ),
            active: toNumber(
                read(sourceCounts, 'active', 'Active'),
                derivedCounts.active,
            ),
            inactive: toNumber(
                read(sourceCounts, 'inactive', 'Inactive'),
                derivedCounts.inactive,
            ),
            expired: toNumber(
                read(sourceCounts, 'expired', 'Expired'),
                derivedCounts.expired,
            ),
        },
        page,
        pageSize,
        totalItems,
        totalPages: toNumber(
            read(response, 'totalPages', 'TotalPages'),
            Math.max(1, Math.ceil(totalItems / Math.max(1, pageSize))),
        ),
    };
}

export function createAdminPromotion(payload) {
    return apiRequest('/admin/promotions', {
        method: 'POST',
        body: JSON.stringify({ ...payload }),
    });
}

export function updateAdminPromotion(promotionId, payload) {
    return apiRequest(`/admin/promotions/${encodeURIComponent(promotionId)}`, {
        method: 'PUT',
        body: JSON.stringify({ ...payload }),
    });
}

function mapPromotion(promotion) {
    const discountType = normalizeDiscountType(
        read(promotion, 'discountType', 'DiscountType'),
    );
    const discountValue = toNumber(
        read(promotion, 'discountValue', 'DiscountValue'),
    );
    const minimumOrderValue = toNumber(
        read(promotion, 'minimumOrderValue', 'MinimumOrderValue'),
    );
    const maximumDiscountValue = toNumber(
        read(promotion, 'maximumDiscountValue', 'MaximumDiscountValue'),
    );
    const requiredCompletedTrips = toNumber(
        read(promotion, 'requiredCompletedTrips', 'RequiredCompletedTrips'),
    );
    const startDate = read(promotion, 'startDate', 'StartDate') ?? null;
    const endDate = read(promotion, 'endDate', 'EndDate') ?? null;
    const isActive = toBoolean(
        read(promotion, 'isActive', 'IsActive'),
        true,
    );
    const status = getPromotionStatus({ isActive, startDate, endDate });

    return {
        rawId: read(promotion, 'id', 'Id')
            ?? read(promotion, 'promotionId', 'PromotionId')
            ?? null,
        promotionCode: read(promotion, 'promotionCode', 'PromotionCode') ?? '',
        discountType,
        discountTypeLabel: discountType === 'Percentage'
            ? 'Phần trăm'
            : discountType === 'Fixed'
                ? 'Số tiền cố định'
                : 'Chưa xác định',
        discountValue,
        discountValueLabel: discountType === 'Percentage'
            ? `${numberFormatter.format(discountValue)}%`
            : currencyFormatter.format(discountValue),
        minimumOrderValue,
        minimumOrderValueLabel: currencyFormatter.format(minimumOrderValue),
        maximumDiscountValue,
        maximumDiscountValueLabel: currencyFormatter.format(maximumDiscountValue),
        requiredCompletedTrips,
        requiredCompletedTripsLabel: requiredCompletedTrips > 0
            ? `Yêu cầu: Hoàn thành ${requiredCompletedTrips} chuyến`
            : 'Không yêu cầu số chuyến',
        maxUsageCount: toNumber(
            read(promotion, 'maxUsageCount', 'MaxUsageCount'),
        ),
        currentUsageCount: toNumber(
            read(promotion, 'currentUsageCount', 'CurrentUsageCount'),
        ),
        usageLimitPerUser: toNumber(
            read(promotion, 'usageLimitPerUser', 'UsageLimitPerUser'),
        ),
        startDate,
        endDate,
        startDateLabel: formatDate(startDate),
        endDateLabel: formatDate(endDate),
        isActive,
        statusLabel: status.label,
        statusVariant: status.variant,
    };
}

function getPromotionStatus({ isActive, startDate, endDate }) {
    const now = Date.now();
    const startTime = toTimestamp(startDate);
    const endTime = toTimestamp(endDate);

    if (!isActive) {
        return { label: 'Tạm tắt', variant: 'gray' };
    }
    if (endTime !== null && endTime < now) {
        return { label: 'Hết hạn', variant: 'red' };
    }
    if (startTime !== null && startTime > now) {
        return { label: 'Sắp diễn ra', variant: 'yellow' };
    }
    return { label: 'Đang hoạt động', variant: 'green' };
}

function countStatuses(items) {
    return items.reduce(
        (counts, item) => {
            if (item.statusVariant === 'green') counts.active += 1;
            if (item.statusVariant === 'gray') counts.inactive += 1;
            if (item.statusVariant === 'red') counts.expired += 1;
            return counts;
        },
        { active: 0, inactive: 0, expired: 0 },
    );
}

function normalizeDiscountType(value) {
    if (value === 0 || value === '0') return 'Percentage';
    if (value === 1 || value === '1') return 'Fixed';
    if (typeof value !== 'string') return '';
    if (value.toLowerCase() === 'percentage') return 'Percentage';
    if (value.toLowerCase() === 'fixed') return 'Fixed';
    return value;
}

function formatDate(value) {
    const timestamp = toTimestamp(value);
    return timestamp === null ? 'Chưa cập nhật' : dateFormatter.format(timestamp);
}

function toTimestamp(value) {
    if (!value) return null;
    const timestamp = new Date(value).getTime();
    return Number.isNaN(timestamp) ? null : timestamp;
}

function toNumber(value, fallback = 0) {
    const number = Number(value);
    return value === null || value === undefined || Number.isNaN(number)
        ? fallback
        : number;
}

function toBoolean(value, fallback) {
    if (typeof value === 'boolean') return value;
    if (value === 'true' || value === 1 || value === '1') return true;
    if (value === 'false' || value === 0 || value === '0') return false;
    return fallback;
}

function read(source, camelCaseKey, pascalCaseKey) {
    if (!source || typeof source !== 'object') return undefined;
    return source[camelCaseKey] ?? source[pascalCaseKey];
}
