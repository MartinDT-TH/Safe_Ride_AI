import './AuthLayout.css';
/** Full-screen centered layout used for auth pages (login, register, forgot password, etc.) */
function AuthLayout({ children }) {
    return (<div className="auth-layout" id="auth-layout">
      <div className="auth-card" id="auth-card">
        {children}
      </div>
    </div>);
}
export default AuthLayout;
