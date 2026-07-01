import { useAppSelector, useAppDispatch } from '../../../app/hooks';
import { setActiveSidebar } from '../../../features/ui/uiSlice';
import { logout } from '../../../features/auth/authSlice';
import { SIDEBAR_NAV_ITEMS, SIDEBAR_FOOTER_DEFS } from '../../constants/sidebarNav';
import { Sidebar } from '../../components/Sidebar';
import { TopHeader } from '../../components/TopHeader';
import { clearAuthTokens } from '../../api/apiClient';
import './AdminLayout.css';
/** Main admin layout: sidebar (left) + top header + content area */
function AdminLayout({ children }) {
    const dispatch = useAppDispatch();
    const activeSidebarId = useAppSelector((state) => state.ui.activeSidebarId);
    // Build sidebar items with active state from Redux + dispatch on click
    const sidebarItems = SIDEBAR_NAV_ITEMS.map((item) => ({
        ...item,
        active: item.id === activeSidebarId,
        onClick: () => dispatch(setActiveSidebar(item.id)),
    }));
    // Build footer items with logout wired to Redux
    const footerItems = SIDEBAR_FOOTER_DEFS.map((item) => ({
        ...item,
        onClick: item.id === 'logout' ? () => {
            clearAuthTokens();
            dispatch(logout());
        } : undefined,
    }));
    return (<div className="admin-layout" id="admin-layout">
      <Sidebar items={sidebarItems} footerItems={footerItems}/>
      <div className="admin-main">
        <TopHeader />
        <main className="admin-content" id="admin-content">
          {children}
        </main>
      </div>
    </div>);
}
export default AdminLayout;
