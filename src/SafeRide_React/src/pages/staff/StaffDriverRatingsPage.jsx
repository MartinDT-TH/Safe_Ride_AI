import { useMemo, useState } from 'react';
import { FontAwesomeIcon } from '@fortawesome/react-fontawesome';
import { faStar as faStarSolid } from '@fortawesome/free-solid-svg-icons';
import { faStar as faStarRegular } from '@fortawesome/free-regular-svg-icons';
import { AdminLayout } from '../../shared/layouts/AdminLayout';
import useAdminSearch from '../../shared/hooks/useAdminSearch';
import useFetch from '../../shared/hooks/useFetch';
import Pagination from '../../shared/components/Pagination/Pagination';
import { getDriversPath, mapDriverList } from '../../features/drivers/driversApi';
import '../DriversPage.css';
import '../../features/drivers/components/DriverTable.css';

const PAGE_SIZE = 10;

function StaffDriverRatingsPage() {
    const [currentPage, setCurrentPage] = useState(1);
    const { query } = useAdminSearch({
        placeholder: 'Tìm kiếm tài xế để xem đánh giá...',
    });
    const { data, isLoading, error, refetch } = useFetch(getDriversPath('all'), {
        select: mapDriverList,
    });
    const safeDrivers = useMemo(() => data?.drivers ?? [], [data]);
    const visibleDrivers = useMemo(
        () => safeDrivers.filter((driver) => driverMatchesSearch(driver, query)),
        [query, safeDrivers],
    );
    const totalPages = Math.max(1, Math.ceil(visibleDrivers.length / PAGE_SIZE));
    const pageDrivers = visibleDrivers.slice((currentPage - 1) * PAGE_SIZE, currentPage * PAGE_SIZE);

    return (
        <AdminLayout>
            <div className="page-header" id="staff-driver-ratings-page-header">
                <h1 className="page-title">Đánh giá Tài xế</h1>
                <p className="page-subtitle">
                    Xem điểm đánh giá và số chuyến của từng tài xế để hỗ trợ việc xác minh thông tin.
                </p>
            </div>

            {error && (
                <div className="drivers-feedback drivers-feedback--error">
                    <span>{error}</span>
                    <button type="button" onClick={refetch}>Thử lại</button>
                </div>
            )}

            {isLoading && (
                <div className="drivers-feedback">
                    Đang tải đánh giá tài xế...
                </div>
            )}

            <div className="driver-table-container" id="staff-driver-ratings-table-container">
                <div className="driver-table-wrapper">
                    <table className="driver-table" id="staff-driver-ratings-table">
                        <thead>
                            <tr>
                                <th className="col-driver">Tài xế</th>
                                <th className="col-contact">Liên hệ</th>
                                <th className="col-rating">Đánh giá</th>
                                <th className="col-date">Ngày gia nhập</th>
                                <th className="col-status">Số chuyến</th>
                            </tr>
                        </thead>
                        <tbody>
                            {pageDrivers.map((driver) => (
                                <tr key={driver.id}>
                                    <td className="col-driver">
                                        <div className="driver-cell">
                                            <div className="driver-avatar" style={{ background: driver.avatar }}>
                                                {driver.avatarUrl ? <img src={driver.avatarUrl} alt={driver.name} /> : driver.initials}
                                            </div>
                                            <span className="driver-name">{driver.name}</span>
                                        </div>
                                    </td>
                                    <td className="col-contact">
                                        <div className="contact-cell">
                                            <span className="contact-email">{driver.email}</span>
                                            <span className="contact-phone">{driver.phone}</span>
                                        </div>
                                    </td>
                                    <td className="col-rating">
                                        <div className="rating-cell">
                                            {driver.rating !== null ? (
                                                <>
                                                    <FontAwesomeIcon icon={faStarSolid} className="rating-star rating-star--filled" />
                                                    <span className="rating-value">{driver.rating}</span>
                                                </>
                                            ) : (
                                                <>
                                                    <FontAwesomeIcon icon={faStarRegular} className="rating-star rating-star--empty" />
                                                    <span className="rating-value">N/A</span>
                                                </>
                                            )}
                                        </div>
                                    </td>
                                    <td className="col-date">{driver.joinDate}</td>
                                    <td className="col-status">{driver.trips} chuyến</td>
                                </tr>
                            ))}
                            {pageDrivers.length === 0 && (
                                <tr>
                                    <td colSpan={5} className="driver-table-empty">
                                        Không có tài xế phù hợp.
                                    </td>
                                </tr>
                            )}
                        </tbody>
                    </table>
                </div>

                <Pagination
                    currentPage={currentPage}
                    totalPages={totalPages}
                    onPageChange={setCurrentPage}
                />
            </div>
        </AdminLayout>
    );
}

function driverMatchesSearch(driver, query) {
    const normalizedQuery = String(query ?? '').trim().toLocaleLowerCase('vi-VN');
    if (!normalizedQuery) {
        return true;
    }

    return [
        driver.name,
        driver.email,
        driver.phone,
        driver.driverCode,
    ].some((value) => String(value ?? '').trim().toLocaleLowerCase('vi-VN').includes(normalizedQuery));
}

export default StaffDriverRatingsPage;
