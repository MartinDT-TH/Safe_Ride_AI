import { useMemo, useRef, useState } from 'react';
import { Bar } from 'react-chartjs-2';
import { BarElement, CategoryScale, Chart as ChartJS, LinearScale, Tooltip } from 'chart.js';
import { FontAwesomeIcon } from '@fortawesome/react-fontawesome';
import { faCheck, faChevronDown } from '@fortawesome/free-solid-svg-icons';

ChartJS.register(BarElement, CategoryScale, LinearScale, Tooltip);

const money = new Intl.NumberFormat('vi-VN', { style: 'currency', currency: 'VND', maximumFractionDigits: 0 });

function RevenueBarChart({ timeline, range, onPresetChange }) {
  const chartRef = useRef(null);
  const [periodOpen, setPeriodOpen] = useState(false);
  const visible = useMemo(() => {
    if (timeline.length <= 45) return timeline;
    const step = Math.ceil(timeline.length / 31);
    return timeline.filter((_, index) => index % step === 0);
  }, [timeline]);
  const data = useMemo(() => ({
    labels: visible.map((item) => formatDate(item.date)),
    datasets: [{
      data: visible.map((item) => item.revenue),
      backgroundColor: '#c9e0e3',
      hoverBackgroundColor: '#008995',
      borderRadius: { topLeft: 4, topRight: 4 },
      borderSkipped: false,
      barPercentage: 1,
      categoryPercentage: 1,
    }],
  }), [visible]);

  const replay = () => {
    const chart = chartRef.current;
    if (!chart) return;
    chart.reset();
    chart.update();
  };

  return <section className="revenue-panel revenue-history">
    <div className="revenue-panel-heading"><h2>Doanh thu theo thời gian</h2><div className="period-dropdown">
      <button className="period-trigger" type="button" onClick={() => setPeriodOpen((current) => !current)} aria-expanded={periodOpen} aria-haspopup="listbox"><span>{periodLabel(range.label)}</span><FontAwesomeIcon icon={faChevronDown} /></button>
      {periodOpen && <div className="period-menu" role="listbox">{[7, 15, 30].map((days) => <button key={days} className={range.label === String(days) ? 'active' : ''} type="button" role="option" aria-selected={range.label === String(days)} onClick={() => { onPresetChange(days); setPeriodOpen(false); }}><span>{days} ngày gần nhất</span>{range.label === String(days) && <FontAwesomeIcon icon={faCheck} />}</button>)}</div>}
    </div></div>
    <div className="chartjs-bar" onClick={replay} role="button" tabIndex="0" onKeyDown={(event) => event.key === 'Enter' && replay()} aria-label="Phát lại hiệu ứng biểu đồ doanh thu">
      <Bar ref={chartRef} data={data} options={barOptions} />
    </div>
  </section>;
}

function periodLabel(value) {
  return ['7', '15', '30'].includes(value) ? `${value} ngày gần nhất` : 'Khoảng tùy chọn';
}

const barOptions = {
  responsive: true,
  maintainAspectRatio: false,
  animation: { duration: 900, easing: 'easeOutQuart' },
  animations: { y: { from: (context) => context.chart.scales.y.getPixelForValue(0), duration: 900 } },
  interaction: { intersect: false, mode: 'index' },
  plugins: {
    legend: { display: false },
    tooltip: { callbacks: { label: (context) => `Doanh thu: ${money.format(context.raw)}` } },
  },
  scales: {
    x: { grid: { display: false }, border: { display: false }, ticks: { color: '#888', font: { size: 11, weight: 700 }, maxTicksLimit: 4, maxRotation: 0 } },
    y: { display: false, beginAtZero: true },
  },
};

function formatDate(value) {
  return new Intl.DateTimeFormat('vi-VN', { day: '2-digit', month: 'short' }).format(new Date(`${value}T00:00:00`));
}

export default RevenueBarChart;
