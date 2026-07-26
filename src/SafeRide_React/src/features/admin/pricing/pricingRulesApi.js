import { apiRequest } from '../../../shared/api/apiClient';

const currencyFormatter = new Intl.NumberFormat('vi-VN', {
    style: 'currency',
    currency: 'VND',
    maximumFractionDigits: 0,
});

const dateTimeFormatter = new Intl.DateTimeFormat('vi-VN', {
    day: '2-digit',
    month: '2-digit',
    year: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
});

export function getAdminPricingRulesPath({
    page = 1,
    pageSize = 20,
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

    return `/admin/pricing-rules?${params}`;
}

export function mapAdminPricingRulesPage(response = {}) {
    const sourceItems = read(response, 'items', 'Items') ?? [];
    const items = Array.isArray(sourceItems)
        ? sourceItems.map(mapPricingRule)
        : [];
    const sourceServiceTypes = read(response, 'serviceTypes', 'ServiceTypes') ?? [];
    const serviceTypes = Array.isArray(sourceServiceTypes)
        ? sourceServiceTypes.map(mapServiceType)
        : [];
    const sourceCounts = read(response, 'counts', 'Counts') ?? {};
    const page = toNumber(read(response, 'page', 'Page'), 1);
    const pageSize = toNumber(read(response, 'pageSize', 'PageSize'), 20);
    const totalItems = toNumber(
        read(response, 'totalItems', 'TotalItems'),
        items.length,
    );

    return {
        items,
        serviceTypes,
        counts: {
            total: toNumber(read(sourceCounts, 'total', 'Total')),
            active: toNumber(read(sourceCounts, 'active', 'Active')),
            inactive: toNumber(read(sourceCounts, 'inactive', 'Inactive')),
            mostCommonVehicleClass: read(
                sourceCounts,
                'mostCommonVehicleClass',
                'MostCommonVehicleClass',
            ) ?? null,
            lastUpdatedAt: read(sourceCounts, 'lastUpdatedAt', 'LastUpdatedAt') ?? null,
            lastUpdatedAtLabel: formatDateTime(
                read(sourceCounts, 'lastUpdatedAt', 'LastUpdatedAt'),
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

export function createAdminPricingRule(payload) {
    return apiRequest('/admin/pricing-rules', {
        method: 'POST',
        body: JSON.stringify({ ...payload }),
    });
}

export function updateAdminPricingRule(pricingRuleId, payload) {
    return apiRequest(`/admin/pricing-rules/${encodeURIComponent(pricingRuleId)}`, {
        method: 'PUT',
        body: JSON.stringify({ ...payload }),
    });
}

export function formatPricingMoney(value) {
    const amount = Number(value);
    return Number.isFinite(amount) ? currencyFormatter.format(amount) : '--';
}

function mapPricingRule(pricingRule) {
    const vehicleClass = String(
        read(pricingRule, 'vehicleClass', 'VehicleClass') ?? '',
    );
    const isActive = toBoolean(
        read(pricingRule, 'isActive', 'IsActive'),
        true,
    );
    const updatedAt = read(pricingRule, 'updatedAt', 'UpdatedAt')
        ?? read(pricingRule, 'createdAt', 'CreatedAt')
        ?? null;

    return {
        rawId: read(pricingRule, 'id', 'Id'),
        vehicleClass,
        vehicleClassLabel: formatVehicleClass(vehicleClass),
        serviceTypeId: toNumber(read(pricingRule, 'serviceTypeId', 'ServiceTypeId')),
        serviceTypeName: read(pricingRule, 'serviceTypeName', 'ServiceTypeName') ?? '',
        serviceTypeLabel: formatServiceType(
            read(pricingRule, 'serviceTypeName', 'ServiceTypeName'),
        ),
        baseFare: toNumber(read(pricingRule, 'baseFare', 'BaseFare')),
        minFare: toNumber(read(pricingRule, 'minFare', 'MinFare')),
        pricePerKm: toNullableNumber(read(pricingRule, 'pricePerKm', 'PricePerKm')),
        pricePerHour: toNullableNumber(read(pricingRule, 'pricePerHour', 'PricePerHour')),
        isActive,
        statusLabel: isActive ? 'Đang hoạt động' : 'Tạm tắt',
        statusVariant: isActive ? 'green' : 'gray',
        createdAt: read(pricingRule, 'createdAt', 'CreatedAt') ?? null,
        updatedAt,
        updatedAtLabel: formatDateTime(updatedAt),
    };
}

function mapServiceType(serviceType) {
    const serviceName = read(serviceType, 'serviceName', 'ServiceName') ?? '';
    return {
        id: toNumber(read(serviceType, 'id', 'Id')),
        serviceName,
        serviceLabel: formatServiceType(serviceName),
    };
}

function formatVehicleClass(vehicleClass) {
    return vehicleClass ? `Hạng ${vehicleClass}` : 'Chưa cập nhật';
}

function formatServiceType(serviceName) {
    if (serviceName === 'PerTrip') return 'Theo chuyến';
    if (serviceName === 'Hourly') return 'Theo giờ';
    return serviceName || 'Chưa cập nhật';
}

function formatDateTime(value) {
    const timestamp = toTimestamp(value);
    return timestamp === null
        ? 'Chưa cập nhật'
        : dateTimeFormatter.format(timestamp);
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

function toNullableNumber(value) {
    if (value === null || value === undefined || value === '') {
        return null;
    }

    const number = Number(value);
    return Number.isNaN(number) ? null : number;
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
