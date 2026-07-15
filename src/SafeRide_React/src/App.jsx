import { lazy, Suspense } from 'react';
import { useAppSelector } from './app/hooks';
import LoginPage from './pages/LoginPage';
import DriversPage from './pages/DriversPage';
import './App.css';

const RevenuePage = lazy(() => import('./pages/RevenuePage'));
/**
 * Root component — reads auth state from Redux to decide
 * which page to show. Will be replaced by React Router later.
 */
function App() {
    const isAuthenticated = useAppSelector((state) => state.auth.isAuthenticated);
    const activeSidebarId = useAppSelector((state) => state.ui.activeSidebarId);
    if (!isAuthenticated) return <LoginPage />;
    return activeSidebarId === 'revenue'
        ? <Suspense fallback={<div className="app-loading">Đang tải trang doanh thu...</div>}><RevenuePage /></Suspense>
        : <DriversPage />;
}
export default App;
