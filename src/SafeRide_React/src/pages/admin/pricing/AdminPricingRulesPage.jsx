import { useEffect, useMemo, useState } from 'react';
import { FontAwesomeIcon } from '@fortawesome/react-fontawesome';
import {
    faCar,
    faCircleCheck,
    faClipboardList,
    faClock,
    faDollarSign,
    faDownload,
    faFilter,
    faPen,
    faPlus,
    faTimes,
} from '@fortawesome/free-solid-svg-icons';
import { AdminLayout } from '../../../shared/layouts/AdminLayout';
import useAdminSearch from '../../../shared/hooks/useAdminSearch';
import useFetch from '../../../shared/hooks/useFetch';
import {
    createAdminPricingRule,
    formatPricingMoney,
    getAdminPricingRulesPath,
    mapAdminPricingRulesPage,
    updateAdminPricingRule,
} from '../../../features/admin/pricing/pricingRulesApi';
import {
    createPricingRuleFormValues,
    mapPricingRuleToFormValues,
    toPricingRulePayload,
    validatePricingRuleValues,
} from '../../../features/admin/pricing/pricingRuleFormValues';
import './AdminPricingRulesPage.css';

const PAGE_SIZE = 20;

function AdminPricingRulesPage() {
    const { query } = useAdminSearch({
        placeholder: 'Tìm kiếm cấu hình giá, hạng xe hoặc dịch vụ...',
    });
    const [status, setStatus] = useState('all');
    const [paging, setPaging] = useState({ page: 1, query: '', status: 'all' });
    const [panelMode, setPanelMode] = useState('create');
    const [selectedRule, setSelectedRule] = useState(null);
    const [formValues, setFormValues] = useState(() => createPricingRuleFormValues());
    const [formErrors, setFormErrors] = useState({});
    const [isPanelOpen, setIsPanelOpen] = useState(false);
    const [isSubmitting, setIsSubmitting] = useState(false);
    const [rowActionId, setRowActionId] = useState(null);
    const [successMessage, setSuccessMessage] = useState('');
    const [mutationError, setMutationError] = useState('');
    const page = paging.query === query && paging.status === status ? paging.page : 1;

    const path = useMemo(
        () => getAdminPricingRulesPath({
            page,
            pageSize: PAGE_SIZE,
            search: query,
            status,
        }),
        [page, query, status],
    );
    const {
        data,
        isLoading,
        error,
        refetch,
    } = useFetch(path, { select: mapAdminPricingRulesPage });

    const items = data?.items ?? [];
    const counts = data?.counts ?? {
        total: 0,
        active: 0,
        inactive: 0,
        mostCommonVehicleClass: null,
        lastUpdatedAtLabel: 'Chưa cập nhật',
    };
    const serviceTypes = data?.serviceTypes ?? [];
    const totalPages = data?.totalPages ?? 1;

    useEffect(() => {
        if (!successMessage) {
            return undefined;
        }

        const timeoutId = window.setTimeout(() => {
            setSuccessMessage('');
        }, 4000);

        return () => {
            window.clearTimeout(timeoutId);
        };
    }, [successMessage]);

    const openCreatePanel = () => {
        setPanelMode('create');
        setSelectedRule(null);
        setFormValues(createPricingRuleFormValues(serviceTypes[0]?.id));
        setFormErrors({});
        setSuccessMessage('');
        setMutationError('');
        setIsPanelOpen(true);
    };

    const openEditPanel = (pricingRule) => {
        setPanelMode('update');
        setSelectedRule(pricingRule);
        setFormValues(mapPricingRuleToFormValues(pricingRule));
        setFormErrors({});
        setSuccessMessage('');
        setMutationError('');
        setIsPanelOpen(true);
    };

    const closePanel = () => {
        if (isSubmitting) {
            return;
        }

        setIsPanelOpen(false);
        setSelectedRule(null);
        setFormErrors({});
    };

    const handleFormChange = (name, value) => {
        setFormValues((current) => ({ ...current, [name]: value }));
        setFormErrors((current) => ({
            ...current,
            [name]: undefined,
            form: undefined,
            unitPrice: undefined,
        }));
    };

    const handleStatusChange = (event) => {
        const nextStatus = event.target.value;
        setStatus(nextStatus);
        setPaging({ page: 1, query, status: nextStatus });
    };

    const handlePageChange = (nextPage) => {
        setPaging({ page: nextPage, query, status });
    };

    const handleSubmit = async (event) => {
        event.preventDefault();
        const validationErrors = validatePricingRuleValues(formValues, serviceTypes);
        if (Object.keys(validationErrors).length > 0) {
            setFormErrors(validationErrors);
            return;
        }

        setIsSubmitting(true);
        setMutationError('');
        setFormErrors({});

        try {
            const payload = toPricingRulePayload(formValues);
            if (panelMode === 'update' && selectedRule) {
                await updateAdminPricingRule(selectedRule.rawId, payload);
                setSuccessMessage('Cập nhật cấu hình giá thành công.');
            } else {
                await createAdminPricingRule(payload);
                setSuccessMessage('Tạo cấu hình giá thành công.');
            }

            setIsPanelOpen(false);
            setSelectedRule(null);
            refetch();
        } catch (caughtError) {
            setFormErrors({
                form: caughtError.message || 'Không thể lưu cấu hình giá.',
            });
        } finally {
            setIsSubmitting(false);
        }
    };

    const handleToggleStatus = async (pricingRule) => {
        setRowActionId(pricingRule.rawId);
        setMutationError('');
        setSuccessMessage('');

        try {
            await updateAdminPricingRule(pricingRule.rawId, {
                ...toPricingRulePayload(mapPricingRuleToFormValues(pricingRule)),
                isActive: !pricingRule.isActive,
            });
            setSuccessMessage(
                pricingRule.isActive
                    ? 'Đã tạm tắt cấu hình giá.'
                    : 'Đã kích hoạt cấu hình giá.',
            );
            refetch();
        } catch (caughtError) {
            setMutationError(caughtError.message || 'Không thể cập nhật trạng thái cấu hình giá.');
        } finally {
            setRowActionId(null);
        }
    };

    const exportCsv = () => {
        if (items.length === 0) {
            return;
        }

        const rows = [
            ['Hạng xe', 'Dịch vụ', 'Giá cơ bản', 'Giá tối thiểu', 'Giá mỗi km', 'Giá mỗi giờ', 'Trạng thái'],
            ...items.map((item) => [
                item.vehicleClass,
                item.serviceTypeName,
                item.baseFare,
                item.minFare,
                item.pricePerKm ?? '',
                item.pricePerHour ?? '',
                item.statusLabel,
            ]),
        ];
        const csv = rows.map((row) => row.map(csvCell).join(',')).join('\r\n');
        const blob = new Blob([`\uFEFF${csv}`], { type: 'text/csv;charset=utf-8' });
        const url = URL.createObjectURL(blob);
        const link = document.createElement('a');
        link.href = url;
        link.download = 'SafeRide_pricing_rules.csv';
        link.click();
        URL.revokeObjectURL(url);
    };

    return (
        <AdminLayout>
            <main className="admin-pricing-page">
                <header className="admin-pricing-page-header">
                    <div>
                        <h1>Cấu hình giá</h1>
                        <p>Quản lý các quy tắc tính giá đang được SafeRide sử dụng.</p>
                    </div>
                    <button className="admin-pricing-create-button" type="button" onClick={openCreatePanel}>
                        <FontAwesomeIcon icon={faPlus} />
                        <span>Thêm cấu hình</span>
                    </button>
                </header>

                {successMessage && (
                    <div className="admin-pricing-feedback admin-pricing-feedback--success" role="status">
                        <FontAwesomeIcon icon={faCircleCheck} />
                        <span>{successMessage}</span>
                    </div>
                )}

                {mutationError && (
                    <div className="admin-pricing-feedback admin-pricing-feedback--error" role="alert">
                        <span>{mutationError}</span>
                    </div>
                )}

                <section className="admin-pricing-summary" aria-label="Tổng quan cấu hình giá">
                    <SummaryCard label="Tổng số quy tắc" value={counts.total} icon={faClipboardList} variant="teal" />
                    <SummaryCard label="Quy tắc đang hoạt động" value={counts.active} icon={faCircleCheck} variant="green" />
                    <SummaryCard label="Hạng xe phổ biến nhất" value={counts.mostCommonVehicleClass ? `Hạng ${counts.mostCommonVehicleClass}` : '--'} icon={faCar} variant="gray" />
                    <SummaryCard label="Lần cập nhật cuối" value={counts.lastUpdatedAtLabel} icon={faClock} variant="orange" compact />
                </section>

                <section className="admin-pricing-panel" aria-label="Danh sách cấu hình giá">
                    <div className="admin-pricing-panel-header">
                        <h2>Danh sách Cấu hình Giá</h2>
                        <div className="admin-pricing-panel-actions">
                            <label className="admin-pricing-status-filter">
                                <FontAwesomeIcon icon={faFilter} />
                                <select value={status} onChange={handleStatusChange}>
                                    <option value="all">Tất cả trạng thái</option>
                                    <option value="active">Đang hoạt động</option>
                                    <option value="inactive">Tạm tắt</option>
                                </select>
                            </label>
                            <button type="button" onClick={exportCsv} disabled={items.length === 0}>
                                <FontAwesomeIcon icon={faDownload} />
                                <span>Xuất CSV</span>
                            </button>
                        </div>
                    </div>

                    {isLoading && (
                        <div className="admin-pricing-state" aria-live="polite">
                            Đang tải cấu hình giá...
                        </div>
                    )}

                    {!isLoading && error && (
                        <div className="admin-pricing-state admin-pricing-state--error" role="alert">
                            <span>{error}</span>
                            <button type="button" onClick={refetch}>Thử lại</button>
                        </div>
                    )}

                    {!isLoading && !error && items.length === 0 && (
                        <div className="admin-pricing-state" aria-live="polite">
                            Chưa có cấu hình giá phù hợp.
                        </div>
                    )}

                    {!isLoading && !error && items.length > 0 && (
                        <>
                            <PricingRulesTable
                                items={items}
                                rowActionId={rowActionId}
                                onEdit={openEditPanel}
                                onToggleStatus={handleToggleStatus}
                            />
                            <div className="admin-pricing-pagination">
                                <span>
                                    Hiển thị {items.length} trên {data?.totalItems ?? items.length} kết quả
                                </span>
                                <div>
                                    <button type="button" disabled={page <= 1} onClick={() => handlePageChange(Math.max(1, page - 1))}>
                                        Trước
                                    </button>
                                    <strong>{page}</strong>
                                    <button type="button" disabled={page >= totalPages} onClick={() => handlePageChange(Math.min(totalPages, page + 1))}>
                                        Sau
                                    </button>
                                </div>
                            </div>
                        </>
                    )}
                </section>
            </main>

            <PricingRulePanel
                isOpen={isPanelOpen}
                mode={panelMode}
                values={formValues}
                errors={formErrors}
                serviceTypes={serviceTypes}
                isSubmitting={isSubmitting}
                onChange={handleFormChange}
                onSubmit={handleSubmit}
                onClose={closePanel}
            />
        </AdminLayout>
    );
}

function SummaryCard({ label, value, icon, variant, compact = false }) {
    return (
        <article className={`admin-pricing-summary-card${compact ? ' admin-pricing-summary-card--compact' : ''}`}>
            <div>
                <span>{label}</span>
                <strong>{value}</strong>
            </div>
            <span className={`admin-pricing-summary-icon admin-pricing-summary-icon--${variant}`}>
                <FontAwesomeIcon icon={icon} />
            </span>
        </article>
    );
}

function PricingRulesTable({ items, rowActionId, onEdit, onToggleStatus }) {
    return (
        <div className="admin-pricing-table-scroll">
            <table className="admin-pricing-table">
                <thead>
                    <tr>
                        <th>Hạng xe</th>
                        <th>Dịch vụ</th>
                        <th>Giá cơ bản</th>
                        <th>Giá tối thiểu</th>
                        <th>Giá mỗi km</th>
                        <th>Giá mỗi giờ</th>
                        <th>Trạng thái</th>
                        <th>Thao tác</th>
                    </tr>
                </thead>
                <tbody>
                    {items.map((item) => (
                        <tr key={item.rawId}>
                            <td><strong>{item.vehicleClassLabel}</strong></td>
                            <td>{item.serviceTypeLabel}</td>
                            <td className="admin-pricing-money">{formatPricingMoney(item.baseFare)}</td>
                            <td>{formatPricingMoney(item.minFare)}</td>
                            <td>{item.pricePerKm === null ? '--' : formatPricingMoney(item.pricePerKm)}</td>
                            <td>{item.pricePerHour === null ? '--' : formatPricingMoney(item.pricePerHour)}</td>
                            <td>
                                <button
                                    className={`admin-pricing-switch${item.isActive ? ' admin-pricing-switch--active' : ''}`}
                                    type="button"
                                    role="switch"
                                    aria-checked={item.isActive}
                                    disabled={rowActionId === item.rawId}
                                    onClick={() => onToggleStatus(item)}
                                >
                                    <span />
                                </button>
                            </td>
                            <td>
                                <button
                                    className="admin-pricing-edit-button"
                                    type="button"
                                    onClick={() => onEdit(item)}
                                    title={`Chỉnh sửa ${item.vehicleClassLabel} - ${item.serviceTypeLabel}`}
                                >
                                    <FontAwesomeIcon icon={faPen} />
                                    <span>Sửa</span>
                                </button>
                            </td>
                        </tr>
                    ))}
                </tbody>
            </table>
        </div>
    );
}

function PricingRulePanel({
    isOpen,
    mode,
    values,
    errors,
    serviceTypes,
    isSubmitting,
    onChange,
    onSubmit,
    onClose,
}) {
    const isUpdate = mode === 'update';
    const submitLabel = isSubmitting
        ? 'Đang lưu...'
        : isUpdate
            ? 'Lưu thay đổi'
            : 'Lưu cấu hình';

    const handleInputChange = (event) => {
        const { checked, name, type, value } = event.target;
        onChange(name, type === 'checkbox' ? checked : value);
    };

    return (
        <>
            <aside className={`admin-pricing-side-panel${isOpen ? ' admin-pricing-side-panel--open' : ''}`} aria-hidden={!isOpen}>
                <div className="admin-pricing-side-panel-header">
                    <div>
                        <h3>{isUpdate ? 'Chỉnh sửa Cấu hình Giá' : 'Cấu hình Giá mới'}</h3>
                        <p>Thiết lập tham số vận hành</p>
                    </div>
                    <button type="button" onClick={onClose} aria-label="Đóng">
                        <FontAwesomeIcon icon={faTimes} />
                    </button>
                </div>

                <form className="admin-pricing-form" onSubmit={onSubmit} noValidate>
                    {errors.form && (
                        <div className="admin-pricing-form-alert" role="alert">
                            {errors.form}
                        </div>
                    )}

                    <div className="admin-pricing-form-section">
                        <label>
                            <span>Hạng xe</span>
                            <select name="vehicleClass" value={values.vehicleClass} onChange={handleInputChange}>
                                <option value="A1">Hạng A1</option>
                                <option value="A">Hạng A</option>
                                <option value="B">Hạng B</option>
                            </select>
                            {errors.vehicleClass && <small>{errors.vehicleClass}</small>}
                        </label>

                        <label>
                            <span>Dịch vụ</span>
                            <select name="serviceTypeId" value={values.serviceTypeId} onChange={handleInputChange}>
                                <option value="">Chọn dịch vụ</option>
                                {serviceTypes.map((serviceType) => (
                                    <option key={serviceType.id} value={serviceType.id}>
                                        {serviceType.serviceLabel}
                                    </option>
                                ))}
                            </select>
                            {errors.serviceTypeId && <small>{errors.serviceTypeId}</small>}
                        </label>
                    </div>

                    <div className="admin-pricing-form-grid">
                        <NumberField label="Giá cơ bản (đ)" name="baseFare" value={values.baseFare} error={errors.baseFare} onChange={handleInputChange} required />
                        <NumberField label="Giá tối thiểu (đ)" name="minFare" value={values.minFare} error={errors.minFare} onChange={handleInputChange} required />
                        <NumberField label="Giá mỗi km (đ)" name="pricePerKm" value={values.pricePerKm} error={errors.pricePerKm} onChange={handleInputChange} />
                        <NumberField label="Giá mỗi giờ (đ)" name="pricePerHour" value={values.pricePerHour} error={errors.pricePerHour} onChange={handleInputChange} />
                    </div>

                    {errors.unitPrice && (
                        <div className="admin-pricing-form-alert" role="alert">
                            {errors.unitPrice}
                        </div>
                    )}

                    <label className="admin-pricing-form-toggle">
                        <span>
                            <strong>Kích hoạt quy tắc</strong>
                            <small>Cho phép áp dụng giá này ngay lập tức</small>
                        </span>
                        <input
                            type="checkbox"
                            name="isActive"
                            checked={values.isActive}
                            onChange={handleInputChange}
                        />
                    </label>

                    <div className="admin-pricing-form-actions">
                        <button type="button" onClick={onClose} disabled={isSubmitting}>
                            Hủy bỏ
                        </button>
                        <button type="submit" disabled={isSubmitting}>
                            <FontAwesomeIcon icon={faDollarSign} />
                            <span>{submitLabel}</span>
                        </button>
                    </div>
                </form>
            </aside>
            {isOpen && <button className="admin-pricing-overlay" type="button" aria-label="Đóng bảng cấu hình" onClick={onClose} />}
        </>
    );
}

function NumberField({ label, name, value, error, onChange, required = false }) {
    return (
        <label>
            <span>{label}</span>
            <input
                name={name}
                type="number"
                min="0"
                step="0.01"
                value={value}
                required={required}
                aria-invalid={Boolean(error)}
                onChange={onChange}
            />
            {error && <small>{error}</small>}
        </label>
    );
}

function csvCell(value) {
    return `"${String(value ?? '').replaceAll('"', '""')}"`;
}

export default AdminPricingRulesPage;
