import { forwardRef, useState } from 'react';
import { FontAwesomeIcon } from '@fortawesome/react-fontawesome';
import { faCalendarDays, faChevronDown, faDownload, faFilter } from '@fortawesome/free-solid-svg-icons';
import DatePicker, { registerLocale } from 'react-datepicker';
import { vi } from 'date-fns/locale/vi';
import 'react-datepicker/dist/react-datepicker.css';
import { AdminLayout } from '../shared/layouts/AdminLayout';
import { RevenueBarChart, RevenueDonut, RevenueStats } from '../features/revenue/components';
import { getRevenuePath, mapRevenue } from '../features/revenue/revenueApi';
import useFetch from '../shared/hooks/useFetch';
import './RevenuePage.css';

registerLocale('vi', vi);

function RevenuePage() {
  const [range, setRange] = useState(() => createPresetRange(30));
  const [showFilters, setShowFilters] = useState(false);
  const [mode, setMode] = useState('range');
  const [draft, setDraft] = useState(() => ({ ...createPresetRange(30), month: toLocalDate(new Date()).slice(0, 7), year: String(new Date().getFullYear()) }));
  const { data, isLoading, error, refetch } = useFetch(getRevenuePath(range.from, range.to), { select: mapRevenue });
  const revenue = data ?? { totalRevenue: 0, successfulTrips: 0, platformFee: 0, timeline: [], services: [] };
  return (
    <AdminLayout>
      <div className="revenue-page">
        <div className="revenue-toolbar">
          <h1>Quản lý Doanh thu</h1>
          <div className="revenue-toolbar-actions">
            <div className="date-filter">
              <button type="button" onClick={() => setShowFilters((current) => !current)}><FontAwesomeIcon icon={faCalendarDays} />{formatRange(range.from, range.to)}</button>
              <button type="button" onClick={() => setShowFilters((current) => !current)} aria-expanded={showFilters}><FontAwesomeIcon icon={faFilter} />Lọc nâng cao</button>
            </div>
            <button className="export-button" type="button"><FontAwesomeIcon icon={faDownload} />Xuất Excel</button>
          </div>
        </div>
        {showFilters && <RevenueFilters mode={mode} setMode={setMode} draft={draft} setDraft={setDraft} onApply={(nextRange) => { setRange(nextRange); setShowFilters(false); }} />}
        {error && <div className="revenue-feedback"><span>{error}</span><button type="button" onClick={refetch}>Thử lại</button></div>}
        {isLoading && <div className="revenue-feedback">Đang tải dữ liệu doanh thu...</div>}
        <RevenueStats revenue={revenue} />
        <div className="revenue-charts">
          <RevenueBarChart timeline={revenue.timeline} range={range} onPresetChange={(days) => setRange(createPresetRange(days))} />
          <RevenueDonut services={revenue.services} />
        </div>
      </div>
    </AdminLayout>
  );
}

function RevenueFilters({ mode, setMode, draft, setDraft, onApply }) {
  const apply = () => {
    if (mode === 'month') {
      const [year, month] = draft.month.split('-').map(Number);
      onApply({ from: `${draft.month}-01`, to: toLocalDate(new Date(year, month, 0)), label: 'month' });
      return;
    }
    if (mode === 'year') {
      onApply({ from: `${draft.year}-01-01`, to: `${draft.year}-12-31`, label: 'year' });
      return;
    }
    if (draft.from <= draft.to) onApply({ from: draft.from, to: draft.to, label: 'custom' });
  };

  return <section className="advanced-revenue-filter" aria-label="Bộ lọc thời gian">
    <div className="quick-ranges"><span>Khoảng nhanh</span>{[7, 15, 30].map((days) => <button key={days} type="button" onClick={() => onApply(createPresetRange(days))}>{days} ngày</button>)}</div>
    <div className="filter-modes">
      {[['range', 'Theo ngày'], ['month', 'Theo tháng'], ['year', 'Theo năm']].map(([value, label]) => <button className={mode === value ? 'active' : ''} key={value} type="button" onClick={() => setMode(value)}>{label}</button>)}
    </div>
    <div className="filter-fields">
      {mode === 'range' && <><label>Từ ngày<DatePicker locale="vi" selected={parseLocalDate(draft.from)} onChange={(date) => setDraft({ ...draft, from: toLocalDate(date) })} selectsStart startDate={parseLocalDate(draft.from)} endDate={parseLocalDate(draft.to)} maxDate={parseLocalDate(draft.to)} dateFormat="dd/MM/yyyy" customInput={<PickerButton />} /></label><label>Đến ngày<DatePicker locale="vi" selected={parseLocalDate(draft.to)} onChange={(date) => setDraft({ ...draft, to: toLocalDate(date) })} selectsEnd startDate={parseLocalDate(draft.from)} endDate={parseLocalDate(draft.to)} minDate={parseLocalDate(draft.from)} dateFormat="dd/MM/yyyy" customInput={<PickerButton />} /></label></>}
      {mode === 'month' && <label>Chọn tháng<DatePicker locale="vi" selected={parseLocalDate(`${draft.month}-01`)} onChange={(date) => setDraft({ ...draft, month: toLocalDate(date).slice(0, 7) })} showMonthYearPicker dateFormat="MM/yyyy" customInput={<PickerButton />} /></label>}
      {mode === 'year' && <label>Chọn năm<DatePicker locale="vi" selected={new Date(Number(draft.year), 0, 1)} onChange={(date) => setDraft({ ...draft, year: String(date.getFullYear()) })} showYearPicker dateFormat="yyyy" minDate={new Date(2020, 0, 1)} maxDate={new Date(2100, 11, 31)} customInput={<PickerButton />} /></label>}
      <button className="apply-filter" type="button" onClick={apply}>Áp dụng</button>
    </div>
  </section>;
}

const PickerButton = forwardRef(function PickerButton({ value, onClick }, ref) {
  return <button className="picker-button" type="button" onClick={onClick} ref={ref}><FontAwesomeIcon icon={faCalendarDays} /><span>{value}</span><FontAwesomeIcon icon={faChevronDown} /></button>;
});

function createPresetRange(days) {
  const to = new Date();
  const from = new Date(to);
  from.setDate(from.getDate() - days + 1);
  return { from: toLocalDate(from), to: toLocalDate(to), label: `${days}` };
}

function toLocalDate(date) {
  const year = date.getFullYear();
  const month = String(date.getMonth() + 1).padStart(2, '0');
  const day = String(date.getDate()).padStart(2, '0');
  return `${year}-${month}-${day}`;
}

function parseLocalDate(value) {
  const [year, month, day] = value.split('-').map(Number);
  return new Date(year, month - 1, day);
}

function formatRange(from, to) {
  if (!from || !to) return '30 ngày gần nhất';
  const formatter = new Intl.DateTimeFormat('vi-VN', { day: '2-digit', month: '2-digit', year: 'numeric' });
  return `${formatter.format(new Date(`${from}T00:00:00`))} - ${formatter.format(new Date(`${to}T00:00:00`))}`;
}

export default RevenuePage;
