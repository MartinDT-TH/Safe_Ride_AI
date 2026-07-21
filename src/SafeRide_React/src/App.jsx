import { lazy, Suspense } from 'react';
import { useAppSelector } from './app/hooks';
import LoginPage from './pages/LoginPage';
import DriversPage from './pages/DriversPage';
import './App.css';

const RevenuePage = lazy(() => import('./pages/RevenuePage'));
const TransactionsPage = lazy(() => import('./pages/TransactionsPage'));
const CustomersPage = lazy(() => import('./pages/CustomersPage'));
const NotificationsPage = lazy(() => import('./pages/NotificationsPage'));
/**
 * Root component — reads auth state from Redux to decide
 * which page to show. Will be replaced by React Router later.
 */
function App() {
    const isAuthenticated = useAppSelector((state) => state.auth.isAuthenticated);
    const activeSidebarId = useAppSelector((state) => state.ui.activeSidebarId);
    if (!isAuthenticated) return <LoginPage />;
    if (activeSidebarId === 'customers') {
        return <Suspense fallback={<div className="app-loading">Đang tải trang khách hàng...</div>}><CustomersPage /></Suspense>;
    }
    if (activeSidebarId === 'revenue') {
        return <Suspense fallback={<div className="app-loading">Đang tải trang doanh thu...</div>}><RevenuePage /></Suspense>;
    }
    if (activeSidebarId === 'transactions') {
        return <Suspense fallback={<div className="app-loading">Đang tải trang giao dịch...</div>}><TransactionsPage /></Suspense>;
    }
    if (activeSidebarId === 'notifications') {
        return <Suspense fallback={<div className="app-loading">Đang tải trang thông báo...</div>}><NotificationsPage /></Suspense>;
    }
    return <DriversPage />;
}
export default App;
