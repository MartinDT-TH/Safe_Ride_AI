import { useAppSelector } from './app/hooks';
import LoginPage from './pages/LoginPage';
import DriversPage from './pages/DriversPage';
/**
 * Root component — reads auth state from Redux to decide
 * which page to show. Will be replaced by React Router later.
 */
function App() {
    const isAuthenticated = useAppSelector((state) => state.auth.isAuthenticated);
    return isAuthenticated ? <DriversPage /> : <LoginPage />;
}
export default App;
