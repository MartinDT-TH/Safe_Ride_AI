import { FontAwesomeIcon } from '@fortawesome/react-fontawesome';
import { faUsers, faBroadcastTower, faFileAlt } from '@fortawesome/free-solid-svg-icons';
import { StatCard } from '../../../shared/components';
import './DriverStats.css';
function DriverStats({ totalDrivers, activeDrivers, pendingKycDrivers, }) {
    return (<div className="driver-stats" id="driver-stats">
      <StatCard icon={<FontAwesomeIcon icon={faUsers} style={{ color: '#1a8a7d' }}/>} variant="teal" label="TỔNG TÀI XẾ" value={totalDrivers.toLocaleString('vi-VN')} badge="Toàn hệ thống"/>
      <StatCard icon={<FontAwesomeIcon icon={faBroadcastTower} style={{ color: '#16a34a' }}/>} variant="green" label="TRỰC TUYẾN" value={activeDrivers.toLocaleString('vi-VN')} badge="Đang hoạt động"/>
      <StatCard icon={<FontAwesomeIcon icon={faFileAlt} style={{ color: '#ea580c' }}/>} variant="orange" label="CHỜ DUYỆT KYC" value={pendingKycDrivers.toLocaleString('vi-VN')} badge="Hồ sơ cần xử lý"/>
    </div>);
}
export default DriverStats;
