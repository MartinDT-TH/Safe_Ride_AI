import { useMemo, useRef } from 'react';
import { Doughnut } from 'react-chartjs-2';
import { ArcElement, Chart as ChartJS, Tooltip } from 'chart.js';

ChartJS.register(ArcElement, Tooltip);

const centerLabel = {
  id: 'centerLabel',
  afterDraw(chart) {
    const { ctx, chartArea } = chart;
    if (!chartArea) return;
    const x = (chartArea.left + chartArea.right) / 2;
    const y = (chartArea.top + chartArea.bottom) / 2;
    ctx.save();
    ctx.textAlign = 'center';
    ctx.fillStyle = '#171717';
    ctx.font = '700 22px Inter, sans-serif';
    ctx.fillText('100%', x, y - 2);
    ctx.fillStyle = '#7d7d7d';
    ctx.font = '700 11px Inter, sans-serif';
    ctx.fillText('Thị phần', x, y + 18);
    ctx.restore();
  },
};

function RevenueDonut({ services }) {
  const chartRef = useRef(null);
  const visibleServices = services.slice(0, 6);
  const data = useMemo(() => ({
    labels: visibleServices.map((item) => item.serviceName),
    datasets: [{
      data: visibleServices.length ? visibleServices.map((item) => item.percentage) : [100],
      backgroundColor: visibleServices.length ? ['#008995', '#dce7e9', '#70b8be', '#a8d2d6', '#006d76', '#eaf1f2'] : ['#dce7e9'],
      hoverOffset: 5,
      borderWidth: 0,
    }],
  }), [visibleServices]);

  const replay = () => {
    const chart = chartRef.current;
    if (!chart) return;
    chart.reset();
    chart.update();
  };

  return <section className="revenue-panel revenue-services">
    <h2>Doanh thu theo dịch vụ</h2>
    <div className="chartjs-donut" onClick={replay} role="button" tabIndex="0" onKeyDown={(event) => event.key === 'Enter' && replay()} aria-label="Phát lại hiệu ứng biểu đồ dịch vụ">
      <Doughnut ref={chartRef} data={data} plugins={[centerLabel]} options={donutOptions} />
    </div>
    <div className="service-legend">
      {visibleServices.length ? visibleServices.slice(0, 2).map((item, index) => <div key={item.serviceName}><span className={`legend-dot${index === 0 ? ' legend-dot--teal' : ''}`} /><b>{item.serviceName}</b><strong>{item.percentage}%</strong></div>) : <div><span className="legend-dot" /><b>Chưa có dữ liệu</b><strong>0%</strong></div>}
    </div>
  </section>;
}

const donutOptions = {
  responsive: true,
  maintainAspectRatio: false,
  cutout: '78%',
  rotation: -90,
  animation: { animateRotate: true, animateScale: false, duration: 1100, easing: 'easeOutQuart' },
  plugins: {
    legend: { display: false },
    tooltip: { callbacks: { label: (context) => `${context.label}: ${context.raw}%` } },
  },
};

export default RevenueDonut;
