import { useEffect } from "react";
import { FontAwesomeIcon } from "@fortawesome/react-fontawesome";
import {
  faSearch,
  faStar,
  faBell,
  faThLarge,
  faUser,
} from "@fortawesome/free-solid-svg-icons";
import { useAppDispatch, useAppSelector } from "../../../app/hooks";
import { setHeaderSearchQuery } from "../../../slices/uiSlice";
import {
  getCurrentManagementRole,
  MANAGEMENT_ROLES,
} from "../../../features/auth/managementRoles";
import "./TopHeader.css";
function TopHeader({ sosAlertCount = 0, onSOSAlertsClick }) {
  const dispatch = useAppDispatch();
  const searchQuery = useAppSelector((state) => state.ui.headerSearchQuery);
  const searchPlaceholder = useAppSelector(
    (state) => state.ui.headerSearchPlaceholder,
  );
  const authUser = useAppSelector((state) => state.auth.user);
  const managementRole = getCurrentManagementRole(authUser);
  const userRoleLabel =
    managementRole === MANAGEMENT_ROLES.staff
      ? "Nhân viên"
      : "Quản trị cao cấp";
  const userNameLabel =
    managementRole === MANAGEMENT_ROLES.staff ? "Staff" : "Quản trị viên";
  useEffect(() => {
    const input = document.getElementById("header-search-input");
    if (!(input instanceof HTMLInputElement)) {
      return undefined;
    }
    input.placeholder = searchPlaceholder;
    if (input.value !== searchQuery) {
      input.value = searchQuery;
    }
    const handleInput = (event) => {
      dispatch(setHeaderSearchQuery(event.target.value));
    };
    input.addEventListener("input", handleInput);
    return () => {
      input.removeEventListener("input", handleInput);
    };
  }, [dispatch, searchPlaceholder, searchQuery]);
  return (
    <header className="top-header" id="top-header">
      {/* Search */}
      <div className="header-search" id="header-search">
        <FontAwesomeIcon icon={faSearch} className="header-search-icon" />
        <input
          id="header-search-input"
          type="text"
          className="header-search-input"
          placeholder="Tìm kiếm tài xế, chuyến đi hoặc người dùng..."
        />
      </div>

      {/* Actions */}
      <div className="header-actions">
        {/* Emergency alert */}
        <button
          className="header-alert-btn"
          id="header-alert-btn"
          type="button"
          onClick={onSOSAlertsClick}
        >
          <FontAwesomeIcon icon={faStar} />
          <span>Cảnh báo khẩn cấp</span>
          {sosAlertCount > 0 && (
            <span className="header-alert-count">{sosAlertCount}</span>
          )}
        </button>

        {/* Notification */}
        <button
          className="header-icon-btn"
          id="header-notifications"
          type="button"
          aria-label="Thông báo"
        >
          <FontAwesomeIcon icon={faBell} />
        </button>

        {/* Grid / Apps */}
        <button
          className="header-icon-btn"
          id="header-apps"
          type="button"
          aria-label="Ứng dụng"
        >
          <FontAwesomeIcon icon={faThLarge} />
        </button>

        {/* Divider */}
        <div className="header-divider"></div>

        {/* User */}
        <div className="header-user" id="header-user">
          <div className="header-user-info">
            <span className="header-user-name">
              {authUser?.name ?? userNameLabel}
            </span>
            <span className="header-user-role">
              {authUser?.role ?? userRoleLabel}
            </span>
          </div>
          <div className="header-user-avatar">
            <FontAwesomeIcon icon={faUser} />
          </div>
        </div>
      </div>
    </header>
  );
}
export default TopHeader;
