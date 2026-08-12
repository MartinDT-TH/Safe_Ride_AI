import './PromotionForm.css';

const currencyFormatter = new Intl.NumberFormat('vi-VN', {
    style: 'currency',
    currency: 'VND',
    maximumFractionDigits: 0,
});

function PromotionForm({
    mode = 'create',
    values,
    errors = {},
    isSubmitting = false,
    onChange,
    onSubmit,
    onCancel,
    submitLabel,
    submittingLabel,
    children,
}) {
    const isUpdate = mode === 'update';
    const resolvedSubmitLabel = submitLabel ?? (isUpdate ? 'Cập nhật' : 'Tạo khuyến mãi');
    const resolvedSubmittingLabel = submittingLabel
        ?? (isUpdate ? 'Đang cập nhật...' : 'Đang tạo...');

    const handleChange = (event) => {
        const { checked, name, type, value } = event.target;
        const nextValue = type === 'checkbox'
            ? checked
            : name === 'promotionCode'
                ? value.toUpperCase()
                : value;
        onChange(name, nextValue);
    };

    return (
        <form className="promotion-form-page" onSubmit={onSubmit} noValidate>
            <div className="promotion-form-page-grid">
                <section className="promotion-form-page-fields" aria-labelledby="promotion-fields-title">
                    <header>
                        <h2 id="promotion-fields-title">Thông tin khuyến mãi</h2>
                        <p>Các thông tin áp dụng trực tiếp cho chương trình ưu đãi.</p>
                    </header>

                    {errors.form && (
                        <div className="promotion-form-page-alert" role="alert">
                            {errors.form}
                        </div>
                    )}

                    <div className="promotion-form-page-field-grid">
                        <Field
                            label="Mã khuyến mãi"
                            name="promotionCode"
                            value={values.promotionCode}
                            error={errors.promotionCode}
                            onChange={handleChange}
                            placeholder="SAFE20"
                            required
                        />

                        <label className="promotion-form-page-field">
                            <span>Loại giảm giá</span>
                            <select
                                name="discountType"
                                value={values.discountType}
                                onChange={handleChange}
                            >
                                <option value="Percentage">Phần trăm (%)</option>
                                <option value="Fixed">Số tiền cố định</option>
                            </select>
                            {errors.discountType && <small>{errors.discountType}</small>}
                        </label>

                        <Field
                            label="Giá trị giảm"
                            name="discountValue"
                            type="number"
                            min="0"
                            step="0.01"
                            value={values.discountValue}
                            error={errors.discountValue}
                            onChange={handleChange}
                            required
                        />
                        <Field
                            label="Tổng lượt dùng tối đa"
                            name="maxUsageCount"
                            type="number"
                            min="1"
                            step="1"
                            value={values.maxUsageCount}
                            error={errors.maxUsageCount}
                            onChange={handleChange}
                            required
                        />
                        <Field
                            label="Giới hạn mỗi người"
                            name="usageLimitPerUser"
                            type="number"
                            min="1"
                            step="1"
                            value={values.usageLimitPerUser}
                            error={errors.usageLimitPerUser}
                            onChange={handleChange}
                            required
                        />
                        <Field
                            label="Số chuyến hoàn thành tối thiểu"
                            name="requiredCompletedTrips"
                            type="number"
                            min="0"
                            step="1"
                            value={values.requiredCompletedTrips}
                            error={errors.requiredCompletedTrips}
                            helperText="Để trống hoặc nhập 0 nếu voucher không yêu cầu số chuyến."
                            onChange={handleChange}
                        />
                        <Field
                            label="Giá trị nhỏ nhất cho chuyến"
                            name="minimumOrderValue"
                            type="number"
                            min="0"
                            step="1"
                            value={values.minimumOrderValue}
                            error={errors.minimumOrderValue}
                            onChange={handleChange}
                        />
                        <Field
                            label="Giá giảm tối đa cho chuyến"
                            name="maximumDiscountValue"
                            type="number"
                            min="0"
                            step="1"
                            value={values.maximumDiscountValue}
                            error={errors.maximumDiscountValue}
                            onChange={handleChange}
                        />
                        <Field
                            label="Ngày bắt đầu"
                            name="startDate"
                            type="datetime-local"
                            value={values.startDate}
                            error={errors.startDate}
                            onChange={handleChange}
                            required
                        />
                        <Field
                            label="Ngày kết thúc"
                            name="endDate"
                            type="datetime-local"
                            value={values.endDate}
                            error={errors.endDate}
                            onChange={handleChange}
                            required
                        />
                    </div>

                    {!isUpdate && (
                        <label className="promotion-form-page-toggle">
                            <input
                                type="checkbox"
                                name="isActive"
                                checked={values.isActive}
                                onChange={handleChange}
                            />
                            <span>
                                <strong>Kích hoạt khuyến mãi</strong>
                                <small>Khuyến mãi có thể được sử dụng trong thời gian hiệu lực.</small>
                            </span>
                        </label>
                    )}
                </section>

                <aside className="promotion-form-page-side">
                    <PromotionPreview values={values} />
                    {children}
                    <div className="promotion-form-page-actions">
                        <button
                            className="promotion-form-page-submit"
                            type="submit"
                            disabled={isSubmitting}
                        >
                            {isSubmitting ? resolvedSubmittingLabel : resolvedSubmitLabel}
                        </button>
                        <button
                            className="promotion-form-page-cancel"
                            type="button"
                            onClick={onCancel}
                            disabled={isSubmitting}
                        >
                            {isUpdate ? 'Hủy thay đổi' : 'Hủy'}
                        </button>
                    </div>
                </aside>
            </div>
        </form>
    );
}

function Field({ label, name, error, helperText, ...inputProps }) {
    const errorId = `${name}-error`;
    const helperId = `${name}-helper`;

    return (
        <label className="promotion-form-page-field">
            <span>{label}</span>
            <input
                name={name}
                aria-invalid={Boolean(error)}
                aria-describedby={error ? errorId : helperText ? helperId : undefined}
                {...inputProps}
            />
            {error && <small id={errorId}>{error}</small>}
            {!error && helperText && <small id={helperId}>{helperText}</small>}
        </label>
    );
}

function PromotionPreview({ values }) {
    const discountValue = Number(values.discountValue);
    const minimumOrderValue = Number(values.minimumOrderValue);
    const maximumDiscountValue = Number(values.maximumDiscountValue);
    const requiredCompletedTrips = Number(values.requiredCompletedTrips);
    const discountLabel = Number.isFinite(discountValue) && discountValue > 0
        ? values.discountType === 'Percentage'
            ? `${discountValue}%`
            : currencyFormatter.format(discountValue)
        : '--';

    return (
        <section className="promotion-form-preview" aria-label="Xem trước khuyến mãi">
            <span className="promotion-form-preview-label">Xem trước</span>
            <strong className="promotion-form-preview-code">
                {values.promotionCode.trim() || 'MÃ KHUYẾN MÃI'}
            </strong>
            <div className="promotion-form-preview-discount">{discountLabel}</div>
            <dl>
                <div>
                    <dt>Chuyến tối thiểu</dt>
                    <dd>{formatCurrency(minimumOrderValue)}</dd>
                </div>
                {maximumDiscountValue > 0 && (
                    <div>
                        <dt>Giảm tối đa</dt>
                        <dd>{formatCurrency(maximumDiscountValue)}</dd>
                    </div>
                )}
                <div>
                    <dt>Điều kiện chuyến</dt>
                    <dd>
                        {Number.isInteger(requiredCompletedTrips) && requiredCompletedTrips > 0
                            ? `${requiredCompletedTrips} chuyến hoàn thành`
                            : 'Không yêu cầu'}
                    </dd>
                </div>
            </dl>
        </section>
    );
}

function formatCurrency(value) {
    return Number.isFinite(value) && value >= 0
        ? currencyFormatter.format(value)
        : '--';
}

export default PromotionForm;
