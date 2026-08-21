import { configureStore } from '@reduxjs/toolkit';
import authReducer from '../slices/authSlice';
import uiReducer from '../slices/uiSlice';
import apiReducer from '../slices/apiSlice';
/**
 * Central Redux store for the entire SafeRide Admin app.
 *
 * All feature slices are registered here.
 */
export const store = configureStore({
    reducer: {
        auth: authReducer,
        ui: uiReducer,
        api: apiReducer,
    },
});
