export function createEmptyPromotionValues() {
    const startDate = new Date();
    const endDate = new Date(startDate);
    endDate.setDate(endDate.getDate() + 1);

    return {
        promotionCode: '',
        discountType: 'Percentage',
        discountValue: '',
        startDate: toDateTimeLocal(startDate),
        endDate: toDateTimeLocal(endDate),
        maxUsageCount: 100,
        usageLimitPerUser: 1,
        minimumOrderValue: 0,
        maximumDiscountValue: 0,
        isActive: true,
    };
}

export function mapPromotionToFormValues(promotion = {}) {
    return {
        promotionCode: promotion.promotionCode ?? '',
        discountType: promotion.discountType ?? 'Percentage',
        discountValue: promotion.discountValue ?? '',
        startDate: toDateTimeLocal(promotion.startDate),
        endDate: toDateTimeLocal(promotion.endDate),
        maxUsageCount: promotion.maxUsageCount ?? 1,
        usageLimitPerUser: promotion.usageLimitPerUser ?? 1,
        minimumOrderValue: promotion.minimumOrderValue ?? 0,
        maximumDiscountValue: promotion.maximumDiscountValue ?? 0,
        isActive: promotion.isActive ?? true,
    };
}

export function validatePromotionValues(values, { currentUsageCount = 0 } = {}) {
    const errors = {};
    const discountValue = Number(values.discountValue);
    const maxUsageCount = Number(values.maxUsageCount);
    const usageLimitPerUser = Number(values.usageLimitPerUser);
    const minimumOrderValue = Number(values.minimumOrderValue);
    const maximumDiscountValue = Number(values.maximumDiscountValue);
    const startTime = new Date(values.startDate).getTime();
    const endTime = new Date(values.endDate).getTime();

    if (!values.promotionCode.trim()) {
        errors.promotionCode = 'Vui lòng nhập mã khuyến mãi.';
    }
    if (!Number.isFinite(discountValue) || discountValue <= 0) {
        errors.discountValue = 'Giá trị giảm phải lớn hơn 0.';
    } else if (values.discountType === 'Percentage' && discountValue > 100) {
        errors.discountValue = 'Phần trăm giảm không được vượt quá 100.';
    }
    if (!Number.isInteger(maxUsageCount) || maxUsageCount <= 0) {
        errors.maxUsageCount = 'Tổng lượt dùng tối đa phải là số nguyên lớn hơn 0.';
    } else if (maxUsageCount < currentUsageCount) {
        errors.maxUsageCount = 'Số lượt sử dụng tối đa không được nhỏ hơn số lượt đã sử dụng.';
    }
    if (!Number.isInteger(usageLimitPerUser) || usageLimitPerUser <= 0) {
        errors.usageLimitPerUser = 'Giới hạn mỗi người phải là số nguyên lớn hơn 0.';
    }
    if (!Number.isFinite(minimumOrderValue) || minimumOrderValue < 0) {
        errors.minimumOrderValue = 'Giá trị nhỏ nhất cho chuyến không được âm.';
    }
    if (!Number.isFinite(maximumDiscountValue) || maximumDiscountValue < 0) {
        errors.maximumDiscountValue = 'Giá giảm tối đa cho chuyến không được âm.';
    }
    if (!values.startDate || Number.isNaN(startTime)) {
        errors.startDate = 'Vui lòng chọn ngày bắt đầu.';
    }
    if (!values.endDate || Number.isNaN(endTime)) {
        errors.endDate = 'Vui lòng chọn ngày kết thúc.';
    } else if (!Number.isNaN(startTime) && endTime <= startTime) {
        errors.endDate = 'Ngày kết thúc phải lớn hơn ngày bắt đầu.';
    }

    return errors;
}

export function toPromotionPayload(values) {
    return {
        promotionCode: values.promotionCode.trim().toUpperCase(),
        discountType: values.discountType,
        discountValue: Number(values.discountValue),
        startDate: new Date(values.startDate).toISOString(),
        endDate: new Date(values.endDate).toISOString(),
        maxUsageCount: Number(values.maxUsageCount),
        minimumOrderValue: Number(values.minimumOrderValue),
        maximumDiscountValue: Number(values.maximumDiscountValue),
        usageLimitPerUser: Number(values.usageLimitPerUser),
        isActive: Boolean(values.isActive),
    };
}

function toDateTimeLocal(value) {
    const date = value instanceof Date ? value : new Date(value);
    if (Number.isNaN(date.getTime())) return '';

    const offset = date.getTimezoneOffset() * 60_000;
    return new Date(date.getTime() - offset).toISOString().slice(0, 16);
}
