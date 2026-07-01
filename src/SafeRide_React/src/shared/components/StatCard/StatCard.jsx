import './StatCard.css';
function StatCard({ icon, variant, label, value, badge }) {
    return (<div className={`stat-card stat-card--${variant}`}>
      <div className="stat-card-top">
        <div className={`stat-card-icon stat-card-icon--${variant}`}>
          {icon}
        </div>
        {badge && (<span className={`stat-card-badge stat-card-badge--${variant}`}>
            {badge}
          </span>)}
      </div>
      <span className="stat-card-label">{label}</span>
      <span className="stat-card-value">{value}</span>
    </div>);
}
export default StatCard;
