import { forwardRef } from 'react';
import { FontAwesomeIcon } from '@fortawesome/react-fontawesome';
import { faArrowUpRightFromSquare, faBuildingColumns, faCalendarDays, faChevronDown, faEllipsisVertical, faFilter, faMoneyBill } from '@fortawesome/free-solid-svg-icons';
import { Pagination, StatusBadge } from '../../../shared/components';
import { DatePicker } from '../../../shared/components/DatePicker';
import { Select } from '../../../shared/components/Select';
import { STATUS_META } from '../transactionConstants';

const moneyFormatter = new Intl.NumberFormat('vi-VN');

function TransactionTable({ transactions, filters, onFilterChange, currentPage, totalPages, totalItems, onPageChange }) {
  const usesDateRange = Object.hasOwn(filters, 'fromDate') || Object.hasOwn(filters, 'toDate');
  const rangeStart = parseDateValue(filters.fromDate);
  const rangeEnd = parseDateValue(filters.toDate);

  const handleDateRangeChange = ([start, end]) => {
    onFilterChange('fromDate', formatDateValue(start));
    onFilterChange('toDate', formatDateValue(end));
  };

  return (
    <section className="transaction-panel">
      <div className="transaction-filters">
        <label className="transaction-field">
          <span>Trạng thái</span>
          <Select value={filters.status} onChange={(event) => onFilterChange('status', event.target.value)}>
            <option value="all">Tất cả trạng thái</option>
            <option value="success">Thành công</option>
            <option value="pending">Đang xử lý</option>
            <option value="failed">Thất bại</option>
          </Select>
        </label>
        <label className="transaction-field">
          <span>Khoảng ngày</span>
          {usesDateRange ? <DatePicker
            locale="vi"
            dateFormat="dd/MM/yyyy"
            selectsRange
            startDate={rangeStart}
            endDate={rangeEnd}
            onChange={handleDateRangeChange}
            isClearable
            placeholderText="Chọn ngày bắt đầu – kết thúc"
            customInput={<TransactionDateButton />}
          /> : <DatePicker
            locale="vi"
            dateFormat="dd/MM/yyyy"
            selected={parseDateValue(filters.date)}
            onChange={(date) => onFilterChange('date', formatDateValue(date))}
            isClearable
            placeholderText="Chọn ngày"
            customInput={<TransactionDateButton />}
          />}
        </label>
        <label className="transaction-field">
          <span>Phương thức</span>
          <Select value={filters.method} onChange={(event) => onFilterChange('method', event.target.value)}>
            <option value="all">Tất cả phương thức</option>
            <option value="QR">QR / PayOS</option>
            <option value="CASH">Tiền mặt</option>
          </Select>
        </label>
        <button type="button" className="advanced-filter-btn">
          <FontAwesomeIcon icon={faFilter} />
          Bộ lọc nâng cao
        </button>
      </div>

      <div className="transaction-table-scroll">
        <table className="transaction-table">
          <thead><tr><th>Mã GD</th><th>Mã Chuyến</th><th>Khách hàng</th><th>Số tiền</th><th>Phương thức</th><th>Ngày thực hiện</th><th>Trạng thái</th><th aria-label="Thao tác" /></tr></thead>
          <tbody>
            {transactions.map((transaction, index) => {
              const status = STATUS_META[transaction.status];
              return (
                <tr key={transaction.id}>
                  <td><strong className="transaction-code">#{transaction.id}</strong></td>
                  <td><button type="button" className="trip-link">#{transaction.tripId}<FontAwesomeIcon icon={faArrowUpRightFromSquare} /></button></td>
                  <td><div className="customer-cell"><span className={`customer-avatar customer-avatar--${index % 4}`}>{transaction.initials}</span><span><strong>{transaction.customer}</strong><small>{transaction.phone}</small></span></div></td>
                  <td><strong className="amount-cell">{moneyFormatter.format(transaction.amount)}đ</strong></td>
                  <td><span className={`payment-method payment-method--${transaction.methodValue === 'QR' ? 'online' : 'cash'}`}><FontAwesomeIcon icon={transaction.methodValue === 'QR' ? faBuildingColumns : faMoneyBill} />{transaction.method}</span></td>
                  <td><span className="date-cell"><strong>{transaction.time}</strong><small>{transaction.date}</small></span></td>
                  <td><StatusBadge label={status.label} variant={status.variant} /></td>
                  <td><button type="button" className="row-action" aria-label={`Thao tác ${transaction.id}`}><FontAwesomeIcon icon={faEllipsisVertical} /></button></td>
                </tr>
              );
            })}
            {transactions.length === 0 && <tr><td colSpan="8" className="empty-transactions">Không có giao dịch phù hợp.</td></tr>}
          </tbody>
        </table>
      </div>

      <footer className="transaction-footer">
        <span>Hiển thị {transactions.length ? (currentPage - 1) * 10 + 1 : 0}–{Math.min(currentPage * 10, totalItems)} trong số {totalItems.toLocaleString('vi-VN')} giao dịch</span>
        <Pagination currentPage={currentPage} totalPages={totalPages} onPageChange={onPageChange} />
      </footer>
    </section>
  );
}

const TransactionDateButton = forwardRef(function TransactionDateButton(
  { value, onClick, placeholder },
  ref,
) {
  return (
    <button ref={ref} type="button" className="transaction-date-picker" onClick={onClick}>
      <FontAwesomeIcon icon={faCalendarDays} />
      <span>{value || placeholder || 'Chọn ngày'}</span>
      <FontAwesomeIcon icon={faChevronDown} />
    </button>
  );
});

function parseDateValue(value) {
  if (!value) return null;
  const [year, month, day] = value.split('-').map(Number);
  const date = new Date(year, month - 1, day);
  return Number.isNaN(date.getTime()) ? null : date;
}

function formatDateValue(date) {
  if (!date) return '';
  const year = date.getFullYear();
  const month = String(date.getMonth() + 1).padStart(2, '0');
  const day = String(date.getDate()).padStart(2, '0');
  return `${year}-${month}-${day}`;
}

export default TransactionTable;
