import { useMemo, useState } from 'react';
import { FontAwesomeIcon } from '@fortawesome/react-fontawesome';
import {
    faBan,
    faCircleCheck,
    faClock,
    faMagnifyingGlass,
    faPen,
    faPlus,
    faTags,
} from '@fortawesome/free-solid-svg-icons';
import {
    getAdminPromotionsPath,
    mapAdminPromotionsPage,
} from '../../../features/admin/promotions/adminPromotionsApi';
import { AdminLayout } from '../../../shared/layouts/AdminLayout';
import useFetch from '../../../shared/hooks/useFetch';
import { Select } from '../../../shared/components/Select';
import AdminPromotionCreatePage from './AdminPromotionCreatePage';
import AdminPromotionUpdatePage from './AdminPromotionUpdatePage';
import './AdminPromotionsPage.css';

function AdminPromotionsPage() {
    const [searchInput, setSearchInput] = useState('');
    const [search, setSearch] = useState('');
    const [statusInput, setStatusInput] = useState('all');
    const [status, setStatus] = useState('all');
    const [discountTypeInput, setDiscountTypeInput] = useState('all');
    const [discountType, setDiscountType] = useState('all');
    const [view, setView] = useState('list');
    const [selectedPromotion, setSelectedPromotion] = useState(null);
    const [successMessage, setSuccessMessage] = useState('');
    const path = useMemo(
        () => getAdminPromotionsPath({
            page: 1,
            pageSize: 10,
            search,
            status,
        }),
        [search, status],
    );
    const {
        data,
        isLoading,
        error,
        refetch,
    } = useFetch(path, { select: mapAdminPromotionsPage });
    const promotions = useMemo(() => {
        const items = data?.items ?? [];
        return discountType === 'all'
            ? items
            : items.filter((promotion) => promotion.discountType === discountType);
    }, [data, discountType]);
    const counts = data?.counts ?? {
        total: 0,
        active: 0,
        inactive: 0,
        expired: 0,
    };

    const handleSearch = (event) => {
        event.preventDefault();
        setSearch(searchInput.trim());
        setStatus(statusInput);
        setDiscountType(discountTypeInput);
    };

    const returnToList = () => {
        setView('list');
        setSelectedPromotion(null);
    };

    const handlePromotionCreated = () => {
        setSuccessMessage('Tạo khuyến mãi thành công.');
        returnToList();
        refetch();
    };

    const handlePromotionUpdated = () => {
        setSuccessMessage('Cập nhật khuyến mãi thành công.');
        returnToList();
        refetch();
    };

    if (view === 'create') {
        return (
            <AdminLayout>
                <AdminPromotionCreatePage
                    onCancel={returnToList}
                    onCreated={handlePromotionCreated}
                />
            </AdminLayout>
        );
    }

    if (view === 'update' && selectedPromotion) {
        return (
            <AdminLayout>
                <AdminPromotionUpdatePage
                    promotion={selectedPromotion}
                    onCancel={returnToList}
                    onUpdated={handlePromotionUpdated}
                />
            </AdminLayout>
        );
    }

    return (
        <AdminLayout>
            <main className="admin-promotions-page">
                <header className="admin-promotions-header">
                    <div>
                        <h1>Quản lý Khuyến mãi</h1>
                        <p>Theo dõi và quản lý các chương trình ưu đãi của SafeRide.</p>
                    </div>
                    <button
                        className="admin-promotions-create-button"
                        type="button"
                        onClick={() => {
                            setSuccessMessage('');
                            setSelectedPromotion(null);
                            setView('create');
                        }}
                    >
                        <FontAwesomeIcon icon={faPlus} />
                        <span>Tạo khuyến mãi</span>
                    </button>
                </header>

                {successMessage && (
                    <div className="admin-promotions-success" role="status">
                        <FontAwesomeIcon icon={faCircleCheck} />
                        <span>{successMessage}</span>
                    </div>
                )}

                <section className="admin-promotions-summary" aria-label="Tổng quan khuyến mãi">
                    <SummaryCard label="Tổng khuyến mãi" value={counts.total} icon={faTags} variant="teal" />
                    <SummaryCard label="Đang hoạt động" value={counts.active} icon={faCircleCheck} variant="green" />
                    <SummaryCard label="Tạm tắt" value={counts.inactive} icon={faBan} variant="gray" />
                    <SummaryCard label="Hết hạn" value={counts.expired} icon={faClock} variant="red" />
                </section>

                <section className="admin-promotions-list" aria-label="Danh sách khuyến mãi">
                    <form className="admin-promotions-search" role="search" onSubmit={handleSearch}>
                        <label className="admin-promotions-search-control">
                            <FontAwesomeIcon icon={faMagnifyingGlass} />
                            <input
                                type="search"
                                value={searchInput}
                                onChange={(event) => setSearchInput(event.target.value)}
                                placeholder="Tìm kiếm mã khuyến mãi..."
                                aria-label="Tìm kiếm mã khuyến mãi"
                            />
                        </label>
                        <Select
                            value={statusInput}
                            onChange={(event) => setStatusInput(event.target.value)}
                            aria-label="Lọc theo trạng thái"
                        >
                            <option value="all">Tất cả trạng thái</option>
                            <option value="active">Đang hoạt động</option>
                            <option value="inactive">Tạm tắt</option>
                            <option value="expired">Hết hạn</option>
                            <option value="upcoming">Sắp diễn ra</option>
                        </Select>
                        <Select
                            value={discountTypeInput}
                            onChange={(event) => setDiscountTypeInput(event.target.value)}
                            aria-label="Lọc theo loại giảm"
                        >
                            <option value="all">Tất cả loại giảm</option>
                            <option value="Percentage">Phần trăm</option>
                            <option value="Fixed">Số tiền cố định</option>
                        </Select>
                        <button type="submit">Tìm kiếm</button>
                    </form>

                    {isLoading && (
                        <div className="admin-promotions-feedback" aria-live="polite">
                            Đang tải danh sách khuyến mãi...
                        </div>
                    )}

                    {!isLoading && error && (
                        <div className="admin-promotions-feedback admin-promotions-feedback--error" role="alert">
                            <span>Không thể tải danh sách khuyến mãi.</span>
                            <button type="button" onClick={refetch}>Thử lại</button>
                        </div>
                    )}

                    {!isLoading && !error && promotions.length === 0 && (
                        <div className="admin-promotions-feedback" aria-live="polite">
                            Chưa có khuyến mãi phù hợp.
                        </div>
                    )}

                    {!isLoading && !error && promotions.length > 0 && (
                        <PromotionsTable
                            promotions={promotions}
                            onEdit={(promotion) => {
                                setSuccessMessage('');
                                setSelectedPromotion(promotion);
                                setView('update');
                            }}
                        />
                    )}
                </section>
            </main>
        </AdminLayout>
    );
}

function SummaryCard({ label, value, icon, variant }) {
    return (
        <article className="admin-promotions-summary-card">
            <div>
                <span>{label}</span>
                <strong>{value}</strong>
            </div>
            <span className={`admin-promotions-summary-icon admin-promotions-summary-icon--${variant}`}>
                <FontAwesomeIcon icon={icon} />
            </span>
        </article>
    );
}

function PromotionsTable({ promotions, onEdit }) {
    return (
        <div className="admin-promotions-table-scroll">
            <table className="admin-promotions-table">
                <thead>
                    <tr>
                        <th>Mã khuyến mãi</th>
                        <th>Loại giảm</th>
                        <th>Giá trị</th>
                        <th>Điều kiện áp dụng</th>
                        <th>Lượt sử dụng</th>
                        <th>Thời gian hiệu lực</th>
                        <th>Trạng thái</th>
                        <th className="admin-promotions-action-column">Thao tác</th>
                    </tr>
                </thead>
                <tbody>
                    {promotions.map((promotion) => (
                        <tr key={promotion.rawId ?? promotion.promotionCode}>
                            <td><strong>{promotion.promotionCode}</strong></td>
                            <td>{promotion.discountTypeLabel}</td>
                            <td>{promotion.discountValueLabel}</td>
                            <td>
                                <span className="admin-promotions-cell-details">
                                    <span>Tối thiểu <strong>{promotion.minimumOrderValueLabel}</strong></span>
                                    <span>Giảm tối đa <strong>{promotion.maximumDiscountValueLabel}</strong></span>
                                    <span>{promotion.requiredCompletedTripsLabel}</span>
                                </span>
                            </td>
                            <td>
                                <span className="admin-promotions-cell-details">
                                    <span>Đã dùng <strong>{promotion.currentUsageCount} / {promotion.maxUsageCount}</strong></span>
                                    <span>Mỗi người <strong>{promotion.usageLimitPerUser}</strong></span>
                                </span>
                            </td>
                            <td>
                                <span className="admin-promotions-date-range">
                                    <span>{promotion.startDateLabel}</span>
                                    <span>{promotion.endDateLabel}</span>
                                </span>
                            </td>
                            <td>
                                <span className={`admin-promotions-status admin-promotions-status--${promotion.statusVariant}`}>
                                    {promotion.statusLabel}
                                </span>
                            </td>
                            <td className="admin-promotions-action-column">
                                <button
                                    className="admin-promotions-edit-button"
                                    type="button"
                                    onClick={() => onEdit(promotion)}
                                    title={`Chỉnh sửa ${promotion.promotionCode}`}
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

export default AdminPromotionsPage;
