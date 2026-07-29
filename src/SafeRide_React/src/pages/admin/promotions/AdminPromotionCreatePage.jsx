import { useState } from 'react';
import { FontAwesomeIcon } from '@fortawesome/react-fontawesome';
import { faArrowLeft } from '@fortawesome/free-solid-svg-icons';
import { createAdminPromotion } from '../../../features/admin/promotions/adminPromotionsApi';
import PromotionForm from '../../../features/admin/promotions/components/PromotionForm';
import {
    createEmptyPromotionValues,
    toPromotionPayload,
    validatePromotionValues,
} from '../../../features/admin/promotions/promotionFormValues';

function AdminPromotionCreatePage({ onCancel, onCreated }) {
    const [values, setValues] = useState(createEmptyPromotionValues);
    const [errors, setErrors] = useState({});
    const [isSubmitting, setIsSubmitting] = useState(false);

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
        const validationErrors = validatePromotionValues(values);

        if (Object.keys(validationErrors).length > 0) {
            setErrors(validationErrors);
            return;
        }

        setIsSubmitting(true);
        setErrors({});

        try {
            await createAdminPromotion(toPromotionPayload(values));
            onCreated();
        } catch (error) {
            setErrors({
                form: error.message || 'Không thể tạo khuyến mãi.',
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
                    <h1>Tạo khuyến mãi mới</h1>
                    <p>Thiết lập các chương trình ưu đãi cho khách hàng và tài xế SafeRide.</p>
                </div>
            </header>

            <PromotionForm
                mode="create"
                values={values}
                errors={errors}
                isSubmitting={isSubmitting}
                onChange={handleChange}
                onSubmit={handleSubmit}
                onCancel={onCancel}
                submitLabel="Tạo khuyến mãi"
                submittingLabel="Đang tạo..."
            />
        </main>
    );
}

export default AdminPromotionCreatePage;
