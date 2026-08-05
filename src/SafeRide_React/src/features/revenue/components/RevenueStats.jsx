import { FontAwesomeIcon } from '@fortawesome/react-fontawesome';
import { faArrowTrendUp, faCircleDollarToSlot, faRoute, faBuildingColumns } from '@fortawesome/free-solid-svg-icons';
import { StatCard } from '../../../shared/components';

const money = new Intl.NumberFormat('vi-VN', { style: 'currency', currency: 'VND', maximumFractionDigits: 0 });
const number = new Intl.NumberFormat('vi-VN');

function RevenueStats({ revenue }) {
  const stats = [
    { label: 'Tổng doanh thu', value: money.format(revenue.totalRevenue), badge: formatGrowth(revenue.revenueGrowthPercent), variant: 'teal', icon: faCircleDollarToSlot },
    { label: 'Chuyến đi thành công', value: number.format(revenue.successfulTrips), badge: formatGrowth(revenue.tripsGrowthPercent), variant: 'neutral', icon: faRoute },
    { label: 'Phí dịch vụ (Platform)', value: money.format(revenue.platformFee), variant: 'brown', icon: faBuildingColumns },
  ];
  return (
    <section className="revenue-stats" aria-label="Tổng quan doanh thu">
      {stats.map((item) => (
        <StatCard
          key={item.label}
          {...item}
          icon={<FontAwesomeIcon icon={item.icon} />}
          badge={item.badge ? <><FontAwesomeIcon icon={faArrowTrendUp} /> {item.badge}</> : null}
        />
      ))}
    </section>
  );
}

function formatGrowth(value) {
  if (value == null) return null;
  return `${value >= 0 ? '+' : ''}${number.format(value)}%`;
}

export default RevenueStats;
