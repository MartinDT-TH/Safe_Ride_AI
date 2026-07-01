import { useState } from 'react';
import { FontAwesomeIcon } from '@fortawesome/react-fontawesome';
import { faEnvelope, faLock, faEye, faEyeSlash, faArrowRight } from '@fortawesome/free-solid-svg-icons';
import { faGoogle } from '@fortawesome/free-brands-svg-icons';
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
            const response = await apiRequest('/auth/demo-login', {
                auth: false,
                method: 'POST',
                body: JSON.stringify({
                    provider: 'Google',
                    email: email || 'admin@saferide.com',
                    fullName: 'Quản trị viên',
                    role: 'Admin',
                    deviceName: 'SafeRide Admin Console',
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
    const handleGoogleLogin = () => {
        // TODO: integrate with Google OAuth
        console.log('Google login clicked');
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
        <FormInput label="Email hoặc Tên đăng nhập" inputId="login-email" type="text" placeholder="admin@saferide.com" value={email} onChange={(e) => setEmail(e.target.value)} autoComplete="username" leftIcon={<FontAwesomeIcon icon={faEnvelope}/>}/>

        <FormInput label="Mật khẩu" inputId="login-password" type={showPassword ? 'text' : 'password'} placeholder="••••••••" value={password} onChange={(e) => setPassword(e.target.value)} autoComplete="current-password" leftIcon={<FontAwesomeIcon icon={faLock}/>} rightAction={passwordToggle}/>

        <FormCheckbox label="Ghi nhớ đăng nhập" checkboxId="remember-me" checked={rememberMe} onChange={(e) => setRememberMe(e.target.checked)}/>

        {error && (<div className="login-error" role="alert">
            {error}
          </div>)}

        <Button type="submit" variant="primary" id="login-btn" disabled={isSubmitting}>
          <span>Đăng nhập</span>
          <FontAwesomeIcon icon={faArrowRight}/>
        </Button>
      </form>

      {/* Divider */}
      <div className="login-divider" id="login-divider">
        <span>HOẶC</span>
      </div>

      {/* Google login */}
      <Button type="button" variant="outline" id="google-login-btn" onClick={handleGoogleLogin}>
        <FontAwesomeIcon icon={faGoogle} style={{ color: '#4285F4' }}/>
        <span>Đăng nhập với Google</span>
      </Button>
    </>);
}
export default LoginForm;
