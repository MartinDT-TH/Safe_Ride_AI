import { FontAwesomeIcon } from '@fortawesome/react-fontawesome';
import { faIdCardAlt, faUsers, faClipboardList, faRoute, faExchangeAlt, faTags, faDollarSign, faChartBar, faBell, faChartPie, faQuestionCircle, faSignOutAlt, } from '@fortawesome/free-solid-svg-icons';
/** Raw sidebar nav definitions (without active state — that comes from Redux) */
export const SIDEBAR_NAV_ITEMS = [
    { id: 'drivers', label: 'Tài xế', icon: <FontAwesomeIcon icon={faIdCardAlt}/> },
    { id: 'customers', label: 'Khách hàng', icon: <FontAwesomeIcon icon={faUsers}/> },
    { id: 'bookings', label: 'Đặt Chuyến', icon: <FontAwesomeIcon icon={faClipboardList}/> },
    { id: 'trips', label: 'Chuyến đi', icon: <FontAwesomeIcon icon={faRoute}/> },
    { id: 'transactions', label: 'Giao dịch', icon: <FontAwesomeIcon icon={faExchangeAlt}/> },
    { id: 'promotions', label: 'Khuyến mãi', icon: <FontAwesomeIcon icon={faTags}/> },
    { id: 'pricing', label: 'Cấu hình Giá', icon: <FontAwesomeIcon icon={faDollarSign}/> },
    { id: 'revenue', label: 'Doanh thu', icon: <FontAwesomeIcon icon={faChartBar}/> },
    { id: 'notifications', label: 'Thông báo', icon: <FontAwesomeIcon icon={faBell}/> },
    { id: 'reports', label: 'Báo Cáo', icon: <FontAwesomeIcon icon={faChartPie}/> },
];
export const SIDEBAR_FOOTER_DEFS = [
    { id: 'support', label: 'Hỗ trợ', icon: <FontAwesomeIcon icon={faQuestionCircle}/> },
    { id: 'logout', label: 'Đăng xuất', icon: <FontAwesomeIcon icon={faSignOutAlt}/>, variant: 'danger' },
];
