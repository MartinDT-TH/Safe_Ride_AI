import './StatusBadge.css';
function StatusBadge({ label, variant }) {
    return (<span className={`status-badge status-badge--${variant}`}>
      {label}
    </span>);
}
export default StatusBadge;
