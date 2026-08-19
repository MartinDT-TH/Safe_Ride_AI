import { forwardRef } from 'react';
import { FontAwesomeIcon } from '@fortawesome/react-fontawesome';
import { faCalendarDays, faChevronDown } from '@fortawesome/free-solid-svg-icons';
import ReactDatePicker, { registerLocale } from 'react-datepicker';
import { vi } from 'date-fns/locale/vi';
import 'react-datepicker/dist/react-datepicker.css';
import './DatePicker.css';

registerLocale('vi', vi);

/** Shared Vietnamese date picker with an optional custom input. */
function DatePicker({ className = '', customInput, ...props }) {
  return (
    <ReactDatePicker
      locale="vi"
      dateFormat="dd/MM/yyyy"
      customInput={customInput ?? <DatePickerButton className={className} />}
      {...props}
    />
  );
}

const DatePickerButton = forwardRef(function DatePickerButton(
  { value, onClick, className = '', placeholder },
  ref,
) {
  return (
    <button
      className={`picker-button ${className}`.trim()}
      type="button"
      onClick={onClick}
      ref={ref}
    >
      <FontAwesomeIcon icon={faCalendarDays} />
      <span>{value || placeholder}</span>
      <FontAwesomeIcon icon={faChevronDown} />
    </button>
  );
});

export default DatePicker;
