import { FontAwesomeIcon } from '@fortawesome/react-fontawesome';
import { faCar } from '@fortawesome/free-solid-svg-icons';
import './Sidebar.css';
function Sidebar({ items, footerItems = [] }) {
    return (<aside className="sidebar" id="sidebar">
      {/* Logo */}
      <div className="sidebar-logo" id="sidebar-logo">
        <div className="sidebar-logo-icon">
          <FontAwesomeIcon icon={faCar} size="lg"/>
        </div>
        <div className="sidebar-logo-text">
          <span className="sidebar-logo-title">SafeRide</span>
          <span className="sidebar-logo-sub">Admin Console</span>
        </div>
      </div>

      {/* Main nav */}
      <nav className="sidebar-nav" id="sidebar-nav">
        <ul className="sidebar-menu">
          {items.map((item) => (<li key={item.id}>
              <button id={`nav-${item.id}`} className={`sidebar-item${item.active ? ' sidebar-item--active' : ''}`} onClick={item.onClick} type="button">
                <span className="sidebar-item-icon">{item.icon}</span>
                <span className="sidebar-item-label">{item.label}</span>
              </button>
            </li>))}
        </ul>
      </nav>

      {/* Footer */}
      {footerItems.length > 0 && (<div className="sidebar-footer" id="sidebar-footer">
          {footerItems.map((item) => (<button key={item.id} id={`nav-${item.id}`} className={`sidebar-item sidebar-item--footer${item.variant === 'danger' ? ' sidebar-item--danger' : ''}`} onClick={item.onClick} type="button">
              <span className="sidebar-item-icon">{item.icon}</span>
              <span className="sidebar-item-label">{item.label}</span>
            </button>))}
        </div>)}
    </aside>);
}
export default Sidebar;
