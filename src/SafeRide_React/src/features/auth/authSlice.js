import { createSlice } from '@reduxjs/toolkit';
import { getAccessToken } from '../../shared/api/apiClient';
const initialState = {
    isAuthenticated: Boolean(getAccessToken()),
    user: null,
    rememberMe: false,
};
const authSlice = createSlice({
    name: 'auth',
    initialState,
    reducers: {
        /** Set authenticated user after successful login */
        loginSuccess(state, action) {
            state.isAuthenticated = true;
            state.user = action.payload.user;
            state.rememberMe = action.payload.rememberMe;
        },
        /** Clear auth state on logout */
        logout(state) {
            state.isAuthenticated = false;
            state.user = null;
            state.rememberMe = false;
        },
    },
});
export const { loginSuccess, logout } = authSlice.actions;
export default authSlice.reducer;
