import { Provider } from 'react-redux';
import { store } from '../store';
/**
 * Global provider wrapper for the entire app.
 *
 * Wraps children with all required context providers:
 * - Redux Provider (store)
 * - (Future: Router, Theme, Toast, etc.)
 *
 * Adding a new global provider? Wrap it here so
 * main.jsx stays clean and every part of the app has access.
 */
function AppProvider({ children }) {
    return (<Provider store={store}>
      {children}
    </Provider>);
}
export default AppProvider;
