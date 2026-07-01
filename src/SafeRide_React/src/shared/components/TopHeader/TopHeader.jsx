import { FontAwesomeIcon } from '@fortawesome/react-fontawesome';
import { faSearch, faStar, faBell, faThLarge, faUser } from '@fortawesome/free-solid-svg-icons';
import './TopHeader.css';
function TopHeader() {
    return (<header className="top-header" id="top-header">
      {/* Search */}
      <div className="header-search" id="header-search">
        <FontAwesomeIcon icon={faSearch} className="header-search-icon"/>
        <input id="header-search-input" type="text" className="header-search-input" placeholder="Tìm kiếm tài xế, chuyến đi hoặc người dùng..."/>
      </div>

      {/* Actions */}
      <div className="header-actions">
        {/* Emergency alert */}
        <button className="header-alert-btn" id="header-alert-btn" type="button">
          <FontAwesomeIcon icon={faStar}/>
          <span>Cảnh báo khẩn cấp</span>
        </button>

        {/* Notification */}
        <button className="header-icon-btn" id="header-notifications" type="button" aria-label="Thông báo">
          <FontAwesomeIcon icon={faBell}/>
        </button>

        {/* Grid / Apps */}
        <button className="header-icon-btn" id="header-apps" type="button" aria-label="Ứng dụng">
          <FontAwesomeIcon icon={faThLarge}/>
        </button>

        {/* Divider */}
        <div className="header-divider"></div>

        {/* User */}
        <div className="header-user" id="header-user">
          <div className="header-user-info">
            <span className="header-user-name">Quản trị viên</span>
            <span className="header-user-role">Quản trị cao cấp</span>
          </div>
          <div className="header-user-avatar">
            <FontAwesomeIcon icon={faUser}/>
          </div>
        </div>
      </div>
    </header>);
}
export default TopHeader;
