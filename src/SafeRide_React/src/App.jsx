import { lazy, Suspense } from 'react';
import { useAppSelector } from './app/hooks';
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
/**
 * Root component — reads auth state from Redux to decide
 * which page to show. Will be replaced by React Router later.
 */
function App() {
    const isAuthenticated = useAppSelector((state) => state.auth.isAuthenticated);
    const activeSidebarId = useAppSelector((state) => state.ui.activeSidebarId);
    if (!isAuthenticated) return <LoginPage />;
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
