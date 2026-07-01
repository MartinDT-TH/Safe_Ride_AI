import { FontAwesomeIcon } from '@fortawesome/react-fontawesome';
import { faCheck } from '@fortawesome/free-solid-svg-icons';
import './FormCheckbox.css';
function FormCheckbox({ label, checkboxId, ...inputProps }) {
    return (<div className="form-checkbox-group">
      <label className="checkbox-label" htmlFor={checkboxId}>
        <input id={checkboxId} type="checkbox" className="checkbox-input" {...inputProps}/>
        <span className="checkbox-custom">
          <FontAwesomeIcon icon={faCheck}/>
        </span>
        <span className="checkbox-text">{label}</span>
      </label>
    </div>);
}
export default FormCheckbox;
