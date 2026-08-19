import { createAsyncThunk } from "@reduxjs/toolkit";
import apiService from "../services/apiService";

export const fetchApiResource = createAsyncThunk(
  "api/fetchResource",
  async ({ path, requestKey }, { rejectWithValue, signal }) => {
    try {
      const data = await apiService.get(path, { signal });
      return { requestKey, data };
    } catch (error) {
      if (error.name === "AbortError") throw error;
      return rejectWithValue({
        requestKey,
        message:
          error instanceof Error ? error.message : "Không thể tải dữ liệu.",
      });
    }
  },
);
