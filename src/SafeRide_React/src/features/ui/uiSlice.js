import { createSlice } from '@reduxjs/toolkit';
const initialState = {
    activeSidebarId: 'revenue',
    sidebarCollapsed: false,
    headerSearchQuery: '',
    headerSearchPlaceholder: 'Tìm kiếm tài xế, chuyến đi hoặc người dùng...',
};
const uiSlice = createSlice({
    name: 'ui',
    initialState,
    reducers: {
        /** Change the active sidebar navigation item */
        setActiveSidebar(state, action) {
            state.activeSidebarId = action.payload;
        },
        /** Toggle sidebar collapsed state */
        toggleSidebar(state) {
            state.sidebarCollapsed = !state.sidebarCollapsed;
        },
        /** Sync the top-header search box with the active admin page */
        setHeaderSearchQuery(state, action) {
            state.headerSearchQuery = action.payload;
        },
        /** Allow each admin page to tailor the shared search placeholder */
        setHeaderSearchPlaceholder(state, action) {
            state.headerSearchPlaceholder = action.payload;
        },
        /** Reset shared header search state when changing admin pages */
        resetHeaderSearch(state) {
            state.headerSearchQuery = '';
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
