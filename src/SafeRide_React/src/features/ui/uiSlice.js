import { createSlice } from '@reduxjs/toolkit';
const initialState = {
    activeSidebarId: 'revenue',
    sidebarCollapsed: false,
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
    },
});
export const { setActiveSidebar, toggleSidebar } = uiSlice.actions;
export default uiSlice.reducer;
