import { createSlice } from "@reduxjs/toolkit";
import { fetchApiResource } from "../thunks/apiThunks";

const initialResourceState = {
  data: null,
  status: "idle",
  error: null,
  requestId: null,
};

const apiSlice = createSlice({
  name: "api",
  initialState: { resources: {} },
  reducers: {
    updateApiResource(state, action) {
      const { requestKey, data } = action.payload;
      const resource = state.resources[requestKey] ?? {
        ...initialResourceState,
      };
      resource.data = data;
      resource.error = null;
      state.resources[requestKey] = resource;
    },
    removeApiResource(state, action) {
      delete state.resources[action.payload];
    },
    resetApiResources(state) {
      state.resources = {};
    },
  },
  extraReducers: (builder) => {
    builder
      .addCase(fetchApiResource.pending, (state, action) => {
        const { requestKey } = action.meta.arg;
        const resource = state.resources[requestKey] ?? {
          ...initialResourceState,
        };
        resource.status = "loading";
        resource.error = null;
        resource.requestId = action.meta.requestId;
        state.resources[requestKey] = resource;
      })
      .addCase(fetchApiResource.fulfilled, (state, action) => {
        const { requestKey, data } = action.payload;
        const resource = state.resources[requestKey];
        if (!resource || resource.requestId !== action.meta.requestId) return;
        resource.data = data;
        resource.status = "succeeded";
        resource.error = null;
        resource.requestId = null;
      })
      .addCase(fetchApiResource.rejected, (state, action) => {
        const requestKey =
          action.payload?.requestKey ?? action.meta.arg.requestKey;
        const resource = state.resources[requestKey];
        if (!resource || resource.requestId !== action.meta.requestId) return;
        resource.status = action.meta.aborted ? "idle" : "failed";
        resource.error = action.meta.aborted
          ? null
          : (action.payload?.message ?? action.error.message);
        resource.requestId = null;
      });
  },
});

export const { updateApiResource, removeApiResource, resetApiResources } =
  apiSlice.actions;

export const selectApiResource = (state, requestKey) =>
  state.api.resources[requestKey] ?? initialResourceState;

export default apiSlice.reducer;
