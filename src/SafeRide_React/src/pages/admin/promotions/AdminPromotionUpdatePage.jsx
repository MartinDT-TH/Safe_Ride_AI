import { useState } from 'react';
import { FontAwesomeIcon } from '@fortawesome/react-fontawesome';
import { faArrowLeft } from '@fortawesome/free-solid-svg-icons';
import { updateAdminPromotion } from '../../../features/admin/promotions/adminPromotionsApi';
import PromotionForm from '../../../features/admin/promotions/components/PromotionForm';
import {
    mapPromotionToFormValues,
    toPromotionPayload,
    validatePromotionValues,
} from '../../../features/admin/promotions/promotionFormValues';

function AdminPromotionUpdatePage({ promotion, onCancel, onUpdated }) {
    const [values, setValues] = useState(() => mapPromotionToFormValues(promotion));
    const [errors, setErrors] = useState({});
    const [isSubmitting, setIsSubmitting] = useState(false);
    const currentUsageCount = Number(promotion.currentUsageCount ?? 0);

    const handleChange = (name, value) => {
        setValues((current) => ({ ...current, [name]: value }));
        setErrors((current) => ({
            ...current,
            [name]: undefined,
            form: undefined,
        }));
    };

    const handleSubmit = async (event) => {
        event.preventDefault();
        const validationErrors = validatePromotionValues(values, {
            currentUsageCount,
        });

        if (Object.keys(validationErrors).length > 0) {
            setErrors(validationErrors);
            return;
        }

        setIsSubmitting(true);
        setErrors({});

        try {
            await updateAdminPromotion(promotion.rawId, toPromotionPayload(values));
            onUpdated();
        } catch (error) {
            setErrors({
                form: error.status === 404
                    ? 'Không tìm thấy khuyến mãi.'
                    : error.message || 'Không thể cập nhật khuyến mãi.',
            });
        } finally {
            setIsSubmitting(false);
        }
    };

    return (
        <main className="admin-promotion-editor-page">
            <header className="admin-promotion-editor-header">
                <button
                    className="admin-promotion-editor-back"
                    type="button"
                    onClick={onCancel}
                >
                    <FontAwesomeIcon icon={faArrowLeft} />
                    <span>Quay lại</span>
                </button>
                <div>
                    <h1>Cập nhật khuyến mãi</h1>
                    <p>Chỉnh sửa thông tin chiến dịch giảm giá hiện tại.</p>
                </div>
            </header>

            <PromotionForm
                mode="update"
                values={values}
                errors={errors}
                isSubmitting={isSubmitting}
                onChange={handleChange}
                onSubmit={handleSubmit}
                onCancel={onCancel}
                submitLabel="Cập nhật"
                submittingLabel="Đang cập nhật..."
            >
                <PromotionStatusCard
                    values={values}
                    currentUsageCount={currentUsageCount}
                    onToggle={() => handleChange('isActive', !values.isActive)}
                />
            </PromotionForm>
        </main>
    );
}

function PromotionStatusCard({ values, currentUsageCount, onToggle }) {
    const status = getDerivedStatus(values);
    const maxUsageCount = Number(values.maxUsageCount);
    const progressMax = Number.isFinite(maxUsageCount) && maxUsageCount > 0
        ? maxUsageCount
        : 1;

    return (
        <section className="promotion-form-status-card" aria-label="Trạng thái khuyến mãi">
            <span className="promotion-form-status-eyebrow">Trạng thái</span>
            <div className="promotion-form-status-control">
                <span>
                    <strong>{values.isActive ? 'Đang bật' : 'Đang tắt'}</strong>
                    <small>Cho phép áp dụng khuyến mãi</small>
                </span>
                <button
                    className="promotion-form-status-switch"
                    type="button"
                    role="switch"
                    aria-checked={values.isActive}
                    aria-label={values.isActive ? 'Tắt khuyến mãi' : 'Bật khuyến mãi'}
                    onClick={onToggle}
                />
            </div>
            <span className={`promotion-form-status-derived promotion-form-status-derived--${status.variant}`}>
                {status.label}
            </span>
            <div className="promotion-form-usage">
                <div>
                    <span>Lượt đã sử dụng</span>
                    <strong>{currentUsageCount} / {values.maxUsageCount}</strong>
                </div>
                <progress
                    value={Math.min(Math.max(currentUsageCount, 0), progressMax)}
                    max={progressMax}
                />
            </div>
        </section>
    );
}

function getDerivedStatus(values) {
    const now = Date.now();
    const startTime = new Date(values.startDate).getTime();
    const endTime = new Date(values.endDate).getTime();

    if (!values.isActive) return { label: 'Tạm tắt', variant: 'gray' };
    if (!Number.isNaN(endTime) && endTime < now) {
        return { label: 'Hết hạn', variant: 'red' };
    }
    if (!Number.isNaN(startTime) && startTime > now) {
        return { label: 'Sắp diễn ra', variant: 'yellow' };
    }
    return { label: 'Đang hoạt động', variant: 'green' };
}

export default AdminPromotionUpdatePage;
