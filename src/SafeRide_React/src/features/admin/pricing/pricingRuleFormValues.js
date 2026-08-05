const VEHICLE_CLASSES = ['A1', 'A', 'B'];

export function createPricingRuleFormValues(defaultServiceTypeId = '') {
    return {
        vehicleClass: 'A1',
        serviceTypeId: defaultServiceTypeId ? String(defaultServiceTypeId) : '',
        baseFare: '',
        minFare: '',
        pricePerKm: '',
        pricePerHour: '',
        isActive: true,
    };
}

export function mapPricingRuleToFormValues(pricingRule) {
    return {
        vehicleClass: pricingRule.vehicleClass || 'A1',
        serviceTypeId: String(pricingRule.serviceTypeId ?? ''),
        baseFare: formatInputNumber(pricingRule.baseFare),
        minFare: formatInputNumber(pricingRule.minFare),
        pricePerKm: formatInputNumber(pricingRule.pricePerKm),
        pricePerHour: formatInputNumber(pricingRule.pricePerHour),
        isActive: Boolean(pricingRule.isActive),
    };
}

export function toPricingRulePayload(values) {
    return {
        vehicleClass: values.vehicleClass,
        serviceTypeId: Number(values.serviceTypeId),
        baseFare: Number(values.baseFare),
        minFare: Number(values.minFare),
        pricePerKm: toNullableNumber(values.pricePerKm),
        pricePerHour: toNullableNumber(values.pricePerHour),
        isActive: Boolean(values.isActive),
    };
}

export function validatePricingRuleValues(values, serviceTypes = []) {
    const errors = {};
    const serviceTypeExists = serviceTypes.some(
        (serviceType) => String(serviceType.id) === String(values.serviceTypeId),
    );

    if (!VEHICLE_CLASSES.includes(values.vehicleClass)) {
        errors.vehicleClass = 'Vui lòng chọn hạng xe hợp lệ.';
    }

    if (!values.serviceTypeId || !serviceTypeExists) {
        errors.serviceTypeId = 'Vui lòng chọn dịch vụ tính giá.';
    }

    validateRequiredMoney(values.baseFare, 'baseFare', 'Vui lòng nhập giá cơ bản.', errors);
    validateRequiredMoney(values.minFare, 'minFare', 'Vui lòng nhập giá tối thiểu.', errors);
    validateOptionalMoney(values.pricePerKm, 'pricePerKm', errors);
    validateOptionalMoney(values.pricePerHour, 'pricePerHour', errors);

    const hasPricePerKm = hasNumericInput(values.pricePerKm);
    const hasPricePerHour = hasNumericInput(values.pricePerHour);
    if (hasPricePerKm === hasPricePerHour) {
        errors.unitPrice = 'Vui lòng nhập đúng một loại giá theo km hoặc theo giờ.';
    }

    return errors;
}

function validateRequiredMoney(value, fieldName, requiredMessage, errors) {
    if (!hasNumericInput(value)) {
        errors[fieldName] = requiredMessage;
        return;
    }

    validateOptionalMoney(value, fieldName, errors);
}

function validateOptionalMoney(value, fieldName, errors) {
    if (!hasNumericInput(value)) {
        return;
    }

    const number = Number(value);
    if (!Number.isFinite(number)) {
        errors[fieldName] = 'Giá trị phải là số hợp lệ.';
        return;
    }

    if (number < 0) {
        errors[fieldName] = 'Giá trị không được nhỏ hơn 0.';
    }
}

function hasNumericInput(value) {
    return value !== null && value !== undefined && String(value).trim() !== '';
}

function toNullableNumber(value) {
    return hasNumericInput(value) ? Number(value) : null;
}

function formatInputNumber(value) {
    if (value === null || value === undefined || value === '') {
        return '';
    }

    return String(value);
}
