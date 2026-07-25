import { forwardRef } from 'react';
import { FontAwesomeIcon } from '@fortawesome/react-fontawesome';
import { faCalendarDays, faChevronDown } from '@fortawesome/free-solid-svg-icons';
import DatePicker, { registerLocale } from 'react-datepicker';
import { vi } from 'date-fns/locale/vi';
import 'react-datepicker/dist/react-datepicker.css';

registerLocale('vi', vi);

/** Date picker shared by revenue filtering and report exporting. */
function RevenueDatePicker({ className = '', ...props }) {
  return <DatePicker locale="vi" dateFormat="dd/MM/yyyy" customInput={<PickerButton className={className} />} {...props} />;
}

const PickerButton = forwardRef(function PickerButton({ value, onClick, className = '' }, ref) {
  return <button className={`picker-button ${className}`.trim()} type="button" onClick={onClick} ref={ref}>
    <FontAwesomeIcon icon={faCalendarDays} /><span>{value}</span><FontAwesomeIcon icon={faChevronDown} />
  </button>;
});

export default RevenueDatePicker;
