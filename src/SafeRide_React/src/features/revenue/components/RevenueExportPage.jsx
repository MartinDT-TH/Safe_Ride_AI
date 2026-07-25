import { useEffect, useRef, useState } from 'react';
import { FontAwesomeIcon } from '@fortawesome/react-fontawesome';
import { faArrowLeft, faClockRotateLeft, faCloudArrowDown, faDownload, faFileCsv, faFileExcel, faSliders } from '@fortawesome/free-solid-svg-icons';
import { AdminLayout } from '../../../shared/layouts/AdminLayout';
import { apiDownload } from '../../../shared/api/apiClient';
import useAdminSearch from '../../../shared/hooks/useAdminSearch';
import RevenueDatePicker from './RevenueDatePicker';
import './RevenueExportPage.css';

const REPORTS = [{ value: 'revenue', label: 'Doanh thu (Revenue)' }];

function RevenueExportPage({ initialRange, onBack }) {
  const [form, setForm] = useState({ report: 'revenue', format: 'xlsx', from: initialRange.from, to: initialRange.to });
  const [history, setHistory] = useState([]);
  const [status, setStatus] = useState({ loading: false, error: '' });
  const urls = useRef([]);
  useAdminSearch({ placeholder: 'Tìm kiếm giao dịch, mã chuyến...' });
  useEffect(() => () => urls.current.forEach(URL.revokeObjectURL), []);

  const valid = form.from && form.to && form.from <= form.to;
  const exportReport = async () => {
    if (!valid || status.loading) return;
    setStatus({ loading: true, error: '' });
    try {
      const params = new URLSearchParams(form);
      const result = await apiDownload(`/admin/revenue/export?${params}`);
      const url = URL.createObjectURL(result.blob);
      urls.current.push(url);
      const fileName = result.fileName || `Bao_cao_doanh_thu_${form.from}_${form.to}.${form.format}`;
      setHistory((items) => [{ id: crypto.randomUUID(), fileName, format: form.format, createdAt: new Date(), size: result.blob.size, url }, ...items]);
      download(url, fileName);
      setStatus({ loading: false, error: '' });
    } catch (error) {
      setStatus({ loading: false, error: error.message || 'Không thể xuất báo cáo.' });
    }
  };

  return <AdminLayout><div className="revenue-export-page">
    <button className="export-back" type="button" onClick={onBack}><FontAwesomeIcon icon={faArrowLeft} /> Quay lại Doanh thu</button>
    <header><h1>Xuất dữ liệu Excel</h1><p>Tùy chỉnh và tải xuống các báo cáo định kỳ của SafeRide.</p></header>
    <div className="export-workspace">
      <section className="export-config card"><h2><span><FontAwesomeIcon icon={faSliders} /></span>Cấu hình báo cáo</h2>
        <div className="export-fields"><label>Loại báo cáo<select value={form.report} onChange={(e) => setForm({ ...form, report: e.target.value })}>{REPORTS.map((item) => <option key={item.value} value={item.value}>{item.label}</option>)}</select></label>
          <fieldset><legend>Định dạng tệp</legend><div className="format-options"><button className={form.format === 'xlsx' ? 'active' : ''} type="button" onClick={() => setForm({ ...form, format: 'xlsx' })}><FontAwesomeIcon icon={faFileExcel} />.XLSX</button><button className={form.format === 'csv' ? 'active' : ''} type="button" onClick={() => setForm({ ...form, format: 'csv' })}><FontAwesomeIcon icon={faFileCsv} />.CSV</button></div></fieldset>
        </div>
        <div className="date-heading">Khoảng thời gian</div><div className="export-dates"><RevenueDatePicker className="export-picker" selected={parseDate(form.from)} onChange={(date) => setForm({ ...form, from: toDateValue(date) })} selectsStart startDate={parseDate(form.from)} endDate={parseDate(form.to)} maxDate={parseDate(form.to)} /><RevenueDatePicker className="export-picker" selected={parseDate(form.to)} onChange={(date) => setForm({ ...form, to: toDateValue(date) })} selectsEnd startDate={parseDate(form.from)} endDate={parseDate(form.to)} minDate={parseDate(form.from)} /></div>
        {status.error && <p className="export-error">{status.error}</p>}<button className="start-export" disabled={!valid || status.loading} type="button" onClick={exportReport}><FontAwesomeIcon icon={faDownload} />{status.loading ? 'Đang tạo tệp...' : 'Bắt đầu xuất dữ liệu'}</button>
      </section>
      <section className="export-ready card"><span><FontAwesomeIcon icon={faCloudArrowDown} /></span><h2>{history.length ? 'Xuất dữ liệu thành công' : 'Sẵn sàng'}</h2><p>{history.length ? 'Tệp báo cáo mới nhất đã được tải xuống thiết bị của bạn.' : 'Chọn các thông số bên trái để bắt đầu tạo tệp báo cáo của bạn.'}</p></section>
    </div>
    <section className="export-history card"><div className="history-title"><h2><FontAwesomeIcon icon={faClockRotateLeft} />Lịch sử xuất báo cáo</h2><span>{history.length ? `${history.length} tệp trong phiên này` : 'Chưa có báo cáo'}</span></div>
      <div className="history-table"><div className="history-row history-head"><span>Tên tệp</span><span>Loại báo cáo</span><span>Ngày tạo</span><span>Kích thước</span><span>Trạng thái</span><span>Thao tác</span></div>
      {history.length === 0 ? <div className="history-empty">Các tệp vừa xuất sẽ xuất hiện tại đây.</div> : history.map((item) => <div className="history-row" key={item.id}><span className="file-cell"><FontAwesomeIcon icon={item.format === 'xlsx' ? faFileExcel : faFileCsv} /><span><b>{item.fileName}</b><small>{item.format === 'xlsx' ? 'EXCEL SPREADSHEET' : 'CSV FILE'}</small></span></span><span>Doanh thu</span><span>{item.createdAt.toLocaleString('vi-VN')}</span><span>{formatBytes(item.size)}</span><span><em>Hoàn tất</em></span><span><button aria-label={`Tải ${item.fileName}`} type="button" onClick={() => download(item.url, item.fileName)}><FontAwesomeIcon icon={faDownload} /></button></span></div>)}</div>
    </section>
  </div></AdminLayout>;
}

function download(url, fileName) { const anchor = document.createElement('a'); anchor.href = url; anchor.download = fileName; anchor.click(); }
function formatBytes(bytes) { if (bytes < 1024) return `${bytes} B`; if (bytes < 1048576) return `${(bytes / 1024).toFixed(1)} KB`; return `${(bytes / 1048576).toFixed(1)} MB`; }
function parseDate(value) { const [year, month, day] = value.split('-').map(Number); return new Date(year, month - 1, day); }
function toDateValue(date) { const year = date.getFullYear(); const month = String(date.getMonth() + 1).padStart(2, '0'); const day = String(date.getDate()).padStart(2, '0'); return `${year}-${month}-${day}`; }
export default RevenueExportPage;
