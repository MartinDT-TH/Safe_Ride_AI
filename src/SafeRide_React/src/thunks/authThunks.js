import { createAsyncThunk } from "@reduxjs/toolkit";
import authService from "../services/authService";

export const login = createAsyncThunk(
  "auth/login",
  async ({ email, password, rememberMe }, { rejectWithValue }) => {
    try {
      const response = await authService.login({ email, password });
      const isStaff = response.roles.includes("Staff");

      return {
        rememberMe,
        user: {
          name: response.fullName,
          email: response.email ?? email ?? "admin@saferide.com",
          roles: response.roles,
          role: isStaff ? "Nhân viên" : "Quản trị cao cấp",
        },
      };
    } catch (error) {
      return rejectWithValue(
        error instanceof Error ? error.message : "Không thể đăng nhập.",
      );
    }
  },
);
