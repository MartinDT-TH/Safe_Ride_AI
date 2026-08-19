import { createSlice } from "@reduxjs/toolkit";
import { getAccessToken } from "../shared/api/apiClient";
import { login } from "../thunks/authThunks";

const initialState = {
  isAuthenticated: Boolean(getAccessToken()),
  user: null,
  rememberMe: false,
  status: "idle",
  error: null,
};

const authSlice = createSlice({
  name: "auth",
  initialState,
  reducers: {
    loginSuccess(state, action) {
      state.isAuthenticated = true;
      state.user = action.payload.user;
      state.rememberMe = action.payload.rememberMe;
    },
    clearAuthError(state) {
      state.error = null;
    },
    logout(state) {
      state.isAuthenticated = false;
      state.user = null;
      state.rememberMe = false;
      state.status = "idle";
      state.error = null;
    },
  },
  extraReducers: (builder) => {
    builder
      .addCase(login.pending, (state) => {
        state.status = "loading";
        state.error = null;
      })
      .addCase(login.fulfilled, (state, action) => {
        state.isAuthenticated = true;
        state.user = action.payload.user;
        state.rememberMe = action.payload.rememberMe;
        state.status = "succeeded";
      })
      .addCase(login.rejected, (state, action) => {
        state.isAuthenticated = false;
        state.user = null;
        state.status = "failed";
        state.error = action.payload ?? action.error.message;
      });
  },
});

export const { loginSuccess, logout, clearAuthError } = authSlice.actions;
export default authSlice.reducer;
