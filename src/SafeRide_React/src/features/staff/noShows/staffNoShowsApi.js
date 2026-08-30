import { apiRequest } from '../../../shared/api/apiClient';

export function getCustomerNoShows(params = {}) {
  const query = new URLSearchParams(Object.entries(params).filter(([, value]) => value !== '' && value != null));
  return apiRequest(`/staff/customer-no-shows?${query}`);
}
export const getCustomerNoShowDetail = (eventId) => apiRequest(`/staff/customer-no-shows/${eventId}`);
export const exemptCustomerNoShow = (eventId, reason) => apiRequest(`/staff/customer-no-shows/${eventId}/exempt`, { method: 'POST', body: JSON.stringify({ reason }) });
export const getCustomerBookingPrivileges = (customerId) => apiRequest(`/staff/customers/${customerId}/booking-privileges`);
export const clearCustomerBookingRestriction = (customerId, reason) => apiRequest(`/staff/customers/${customerId}/booking-privileges/clear-restriction`, { method: 'POST', body: JSON.stringify({ reason }) });
