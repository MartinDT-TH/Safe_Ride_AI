import { lazy, Suspense } from 'react';
import { useEffect } from 'react';
import { useAppSelector } from './app/hooks';
import { useAppDispatch } from './app/hooks';
import { setActiveSidebar } from './features/ui/uiSlice';
import {
    getCurrentManagementRole,
    getDefaultManagementSidebarId,
    isAllowedSidebarId,
    MANAGEMENT_ROLES,
} from './features/auth/managementRoles';
import LoginPage from './pages/LoginPage';
import DriversPage from './pages/DriversPage';
import './App.css';

const RevenuePage = lazy(() => import('./pages/RevenuePage'));
const TransactionsPage = lazy(() => import('./pages/TransactionsPage'));
const AdminPromotionsPage = lazy(() => import('./pages/admin/promotions/AdminPromotionsPage'));
const AdminPricingRulesPage = lazy(() => import('./pages/admin/pricing/AdminPricingRulesPage'));
const CustomersPage = lazy(() => import('./pages/CustomersPage'));
const NotificationsPage = lazy(() => import('./pages/NotificationsPage'));
const BookingsPage = lazy(() => import('./pages/BookingsPage'));
const TripsPage = lazy(() => import('./pages/TripsPage'));
const AdminReportsPage = lazy(() => import('./pages/admin/reports/AdminReportsPage'));
const AdminDriverAccountsPage = lazy(() => import('./pages/admin/drivers/AdminDriverAccountsPage'));
const AdminNotificationReviewPage = lazy(() => import('./pages/admin/notifications/AdminNotificationReviewPage'));
const StaffDriverVerificationPage = lazy(() => import('./pages/staff/StaffDriverVerificationPage'));
const StaffPaymentStatusPage = lazy(() => import('./pages/staff/StaffPaymentStatusPage'));
const StaffDriverRatingsPage = lazy(() => import('./pages/staff/StaffDriverRatingsPage'));
const StaffNotificationRequestsPage = lazy(() => import('./pages/staff/StaffNotificationRequestsPage'));
/**
 * Root component — reads auth state from Redux to decide
 * which page to show. Will be replaced by React Router later.
 */
function App() {
    const dispatch = useAppDispatch();
    const isAuthenticated = useAppSelector((state) => state.auth.isAuthenticated);
    const authUser = useAppSelector((state) => state.auth.user);
    const activeSidebarId = useAppSelector((state) => state.ui.activeSidebarId);
    const managementRole = isAuthenticated ? getCurrentManagementRole(authUser) : null;
    const defaultSidebarId = getDefaultManagementSidebarId(managementRole);
    const effectiveSidebarId = isAllowedSidebarId(managementRole, activeSidebarId)
        ? activeSidebarId
        : defaultSidebarId;
    useEffect(() => {
        if (isAuthenticated && activeSidebarId !== effectiveSidebarId) {
            dispatch(setActiveSidebar(effectiveSidebarId));
        }
    }, [activeSidebarId, dispatch, effectiveSidebarId, isAuthenticated]);
    if (!isAuthenticated) return <LoginPage />;
    if (!managementRole) return <LoginPage />;
    if (managementRole === MANAGEMENT_ROLES.staff) {
        if (effectiveSidebarId === 'bookings') {
            return <Suspense fallback={<div className="app-loading">Đang tải yêu cầu đặt xe...</div>}><BookingsPage /></Suspense>;
        }
        if (effectiveSidebarId === 'trips') {
            return <Suspense fallback={<div className="app-loading">Đang tải chuyến đi...</div>}><TripsPage /></Suspense>;
        }
        if (effectiveSidebarId === 'staff-driver-verification') {
            return <Suspense fallback={<div className="app-loading">Đang tải xác minh tài xế...</div>}><StaffDriverVerificationPage /></Suspense>;
        }
        if (effectiveSidebarId === 'staff-payments') {
            return <Suspense fallback={<div className="app-loading">Đang tải thanh toán...</div>}><StaffPaymentStatusPage /></Suspense>;
        }
        if (effectiveSidebarId === 'staff-driver-ratings') {
            return <Suspense fallback={<div className="app-loading">Đang tải đánh giá tài xế...</div>}><StaffDriverRatingsPage /></Suspense>;
        }
        if (effectiveSidebarId === 'staff-notifications') {
            return <Suspense fallback={<div className="app-loading">Đang tải yêu cầu thông báo...</div>}><StaffNotificationRequestsPage /></Suspense>;
        }
    }
    if (activeSidebarId !== effectiveSidebarId && effectiveSidebarId === 'revenue') {
        return <Suspense fallback={<div className="app-loading">Đang tải trang doanh thu...</div>}><RevenuePage /></Suspense>;
    }
    if (effectiveSidebarId === 'drivers') {
        return <Suspense fallback={<div className="app-loading">Đang tải tài khoản tài xế...</div>}><AdminDriverAccountsPage /></Suspense>;
    }
    if (effectiveSidebarId === 'notifications') {
        return <Suspense fallback={<div className="app-loading">Đang tải duyệt thông báo...</div>}><AdminNotificationReviewPage /></Suspense>;
    }
    if (activeSidebarId === 'bookings') {
        return <Suspense fallback={<div className="app-loading">Đang tải trang yêu cầu đặt xe...</div>}><BookingsPage /></Suspense>;
    }
    if (activeSidebarId === 'trips') {
        return <Suspense fallback={<div className="app-loading">Đang tải trang chuyến đi...</div>}><TripsPage /></Suspense>;
    }
    if (activeSidebarId === 'customers') {
        return <Suspense fallback={<div className="app-loading">Đang tải trang khách hàng...</div>}><CustomersPage /></Suspense>;
    }
    if (activeSidebarId === 'revenue') {
        return <Suspense fallback={<div className="app-loading">Đang tải trang doanh thu...</div>}><RevenuePage /></Suspense>;
    }
    if (activeSidebarId === 'transactions') {
        return <Suspense fallback={<div className="app-loading">Đang tải trang giao dịch...</div>}><TransactionsPage /></Suspense>;
    }
    if (activeSidebarId === 'promotions') {
        return <Suspense fallback={<div className="app-loading">Đang tải trang khuyến mãi...</div>}><AdminPromotionsPage /></Suspense>;
    }
    if (activeSidebarId === 'pricing') {
        return <Suspense fallback={<div className="app-loading">Đang tải trang cấu hình giá...</div>}><AdminPricingRulesPage /></Suspense>;
    }
    if (activeSidebarId === 'notifications') {
        return <Suspense fallback={<div className="app-loading">Đang tải trang thông báo...</div>}><NotificationsPage /></Suspense>;
    }
    if (activeSidebarId === 'reports') {
        return <Suspense fallback={<div className="app-loading">Đang tải danh sách báo cáo...</div>}><AdminReportsPage /></Suspense>;
    }
    return <DriversPage />;
}
export default App;
