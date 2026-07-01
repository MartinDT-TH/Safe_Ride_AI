import { AuthLayout } from '../shared/layouts/AuthLayout';
import { LoginForm } from '../features/auth/components';
/** Login page - composes AuthLayout + LoginForm */
function LoginPage() {
    return (<AuthLayout>
      <LoginForm />
    </AuthLayout>);
}
export default LoginPage;
