import './FormInput.css';
function FormInput({ label, inputId, leftIcon, rightAction, ...inputProps }) {
    return (<div className="form-group">
      <label className="form-label" htmlFor={inputId}>
        {label}
      </label>
      <div className="input-wrapper">
        {leftIcon && (<span className="input-icon input-icon--left">{leftIcon}</span>)}
        <input id={inputId} className="form-input" {...inputProps}/>
        {rightAction && (<span className="input-icon input-icon--right">{rightAction}</span>)}
      </div>
    </div>);
}
export default FormInput;
