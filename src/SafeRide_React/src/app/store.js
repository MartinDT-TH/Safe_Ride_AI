import { configureStore } from '@reduxjs/toolkit';
import authReducer from '../features/auth/authSlice';
import uiReducer from '../features/ui/uiSlice';
/**
 * Central Redux store for the entire SafeRide Admin app.
 *
 * All feature slices are registered here.
 */
export const store = configureStore({
    reducer: {
        auth: authReducer,
        ui: uiReducer,
    },
});
