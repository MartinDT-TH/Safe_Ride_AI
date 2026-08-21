import { createSlice } from "@reduxjs/toolkit";

const initialState = {
  activeSidebarId: "revenue",
  sidebarCollapsed: false,
  headerSearchQuery: "",
  headerSearchPlaceholder: "Tìm kiếm tài xế, chuyến đi hoặc người dùng...",
};

const uiSlice = createSlice({
  name: "ui",
  initialState,
  reducers: {
    setActiveSidebar(state, action) {
      state.activeSidebarId = action.payload;
    },
    toggleSidebar(state) {
      state.sidebarCollapsed = !state.sidebarCollapsed;
    },
    setHeaderSearchQuery(state, action) {
      state.headerSearchQuery = action.payload;
    },
    setHeaderSearchPlaceholder(state, action) {
      state.headerSearchPlaceholder = action.payload;
    },
    resetHeaderSearch(state) {
      state.headerSearchQuery = "";
      state.headerSearchPlaceholder = initialState.headerSearchPlaceholder;
    },
  },
});

export const {
  setActiveSidebar,
  toggleSidebar,
  setHeaderSearchQuery,
  setHeaderSearchPlaceholder,
  resetHeaderSearch,
} = uiSlice.actions;

export default uiSlice.reducer;
