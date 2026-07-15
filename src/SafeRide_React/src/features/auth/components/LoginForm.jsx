import { useState } from 'react';
import { FontAwesomeIcon } from '@fortawesome/react-fontawesome';
import { faEnvelope, faLock, faEye, faEyeSlash, faArrowRight } from '@fortawesome/free-solid-svg-icons';
import { useAppDispatch } from '../../../app/hooks';
import { loginSuccess } from '../authSlice';
import { apiRequest, saveAuthTokens } from '../../../shared/api/apiClient';
import { FormInput, FormCheckbox, Button } from '../../../shared/components';
import './LoginForm.css';
function LoginForm() {
    const dispatch = useAppDispatch();
    const [email, setEmail] = useState('');
    const [password, setPassword] = useState('');
    const [showPassword, setShowPassword] = useState(false);
    const [rememberMe, setRememberMe] = useState(false);
    const [error, setError] = useState(null);
    const [isSubmitting, setIsSubmitting] = useState(false);
    const handleSubmit = async (e) => {
        e.preventDefault();
        setError(null);
        setIsSubmitting(true);
        try {
            const response = await apiRequest('/admin/auth/login', {
                auth: false,
                method: 'POST',
                body: JSON.stringify({
                    email,
                    password,
                    deviceName: 'SafeRide Admin Web',
                }),
            });
            saveAuthTokens(response.accessToken, response.refreshToken);
            dispatch(loginSuccess({
                user: {
                    name: response.fullName,
                    role: response.roles.includes('Admin') ? 'Quản trị cao cấp' : response.roles[0] ?? 'Quản trị viên',
                    email: response.email ?? email ?? 'admin@saferide.com',
                },
                rememberMe,
            }));
        }
        catch (caughtError) {
            setError(caughtError instanceof Error ? caughtError.message : 'Không thể đăng nhập.');
        }
        finally {
            setIsSubmitting(false);
        }
    };
    const passwordToggle = (<button type="button" className="password-toggle-btn" id="toggle-password" onClick={() => setShowPassword(!showPassword)} aria-label={showPassword ? 'Ẩn mật khẩu' : 'Hiện mật khẩu'}>
      <FontAwesomeIcon icon={showPassword ? faEyeSlash : faEye}/>
    </button>);
    return (<>
      {/* Avatar */}
      <div className="login-avatar" id="login-avatar">
        <FontAwesomeIcon icon={faLock} size="lg"/>
      </div>

      {/* Heading */}
      <h1 className="login-title" id="login-title">Đăng nhập Quản trị viên</h1>
      <p className="login-subtitle" id="login-subtitle">
        Vui lòng đăng nhập để quản lý hệ thống SafeRide.
      </p>

      {/* Form */}
      <form className="login-form" id="login-form" onSubmit={handleSubmit}>
        <FormInput label="Email quản trị" inputId="login-email" type="email" placeholder="admin@saferide.com" value={email} onChange={(e) => setEmail(e.target.value)} autoComplete="username" required leftIcon={<FontAwesomeIcon icon={faEnvelope}/>}/>

        <FormInput label="Mật khẩu" inputId="login-password" type={showPassword ? 'text' : 'password'} placeholder="••••••••" value={password} onChange={(e) => setPassword(e.target.value)} autoComplete="current-password" required leftIcon={<FontAwesomeIcon icon={faLock}/>} rightAction={passwordToggle}/>

        <FormCheckbox label="Ghi nhớ đăng nhập" checkboxId="remember-me" checked={rememberMe} onChange={(e) => setRememberMe(e.target.checked)}/>

        {error && (<div className="login-error" role="alert">
            {error}
          </div>)}

        <Button type="submit" variant="primary" id="login-btn" disabled={isSubmitting}>
          <span>Đăng nhập</span>
          <FontAwesomeIcon icon={faArrowRight}/>
        </Button>
      </form>

    </>);
}
export default LoginForm;
