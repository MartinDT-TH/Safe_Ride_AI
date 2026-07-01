import './Button.css';
function Button({ variant = 'primary', children, className = '', ...buttonProps }) {
    return (<button className={`btn btn--${variant} ${className}`.trim()} {...buttonProps}>
      {children}
    </button>);
}
export default Button;
