import { FontAwesomeIcon } from '@fortawesome/react-fontawesome';
import { faWallet, faReceipt, faCircleExclamation, faEllipsis } from '@fortawesome/free-solid-svg-icons';
import { StatCard } from '../../../shared/components';

const ICONS = {
  revenue: faWallet,
  success: faReceipt,
  failed: faCircleExclamation,
  withdrawal: faEllipsis,
};

function TransactionStats({ stats = {}, onWithdrawalClick }) {
  const transactionStats = [
    { id: 'revenue', label: 'Tổng doanh thu', value: `${Number(stats.totalRevenue ?? 0).toLocaleString('vi-VN')}đ`, badge: growth(stats.revenueGrowthPercent), variant: 'teal' },
    { id: 'success', label: 'Giao dịch thành công', value: Number(stats.successfulTransactions ?? 0).toLocaleString('vi-VN'), badge: growth(stats.successGrowthPercent), variant: 'green' },
    { id: 'failed', label: 'Giao dịch thất bại', value: Number(stats.failedTransactions ?? 0).toLocaleString('vi-VN'), badge: growth(stats.failedGrowthPercent), variant: 'red' },
    { id: 'withdrawal', label: 'Yêu cầu rút tiền', value: Number(stats.pendingWithdrawals ?? 0).toLocaleString('vi-VN'), variant: 'orange' },
  ];
  return (
    <section className="transaction-stats" aria-label="Tổng quan giao dịch">
      {transactionStats.map((stat) => {
        const card = <StatCard {...stat} icon={<FontAwesomeIcon icon={ICONS[stat.id]} />} />;
        return stat.id === 'withdrawal'
          ? <button key={stat.id} type="button" className="withdrawal-card-trigger" onClick={onWithdrawalClick}>{card}</button>
          : <div key={stat.id} className="transaction-stat-item">{card}</div>;
      })}
    </section>
  );
}

function growth(value) {
  if (value == null) return 'Mới';
  return `${value >= 0 ? '+' : ''}${value}%${value >= 0 ? '↑' : '↓'}`;
}

export default TransactionStats;
