export function getTransactionsPath({ status, method, date, page, pageSize = 10 }) {
  const params = new URLSearchParams({ page: String(page), pageSize: String(pageSize) });
  if (status !== 'all') params.set('status', status);
  if (method !== 'all') params.set('method', method);
  if (date) params.set('date', date);
  return `/admin/transactions?${params}`;
}

export function mapTransactions(response) {
  return {
    stats: response.stats ?? {},
    items: (response.items ?? []).map((item) => ({
      id: `TXN-${item.id}`,
      tripId: `SR-${item.tripId}`,
      initials: initialsOf(item.customerName),
      customer: item.customerName,
      phone: item.maskedPhone,
      amount: item.amount,
      method: item.method === 'CASH' ? 'Tiền mặt' : 'QR',
      methodValue: item.method,
      date: new Intl.DateTimeFormat('vi-VN').format(new Date(item.performedAt)),
      time: new Intl.DateTimeFormat('vi-VN', { hour: '2-digit', minute: '2-digit' }).format(new Date(item.performedAt)),
      status: item.status.toLowerCase(),
    })),
    page: response.page ?? 1,
    pageSize: response.pageSize ?? 10,
    totalItems: response.totalItems ?? 0,
    totalPages: response.totalPages ?? 1,
  };
}

function initialsOf(name = '') {
  const words = name.trim().split(/\s+/).filter(Boolean);
  return words.length ? `${words[0][0]}${words.at(-1)[0]}`.toUpperCase() : 'SR';
}

export function getWithdrawalsPath({ page, status = 'all', pageSize = 10 }) {
  const params = new URLSearchParams({ page: String(page), pageSize: String(pageSize) });
  if (status !== 'all') params.set('status', status);
  return `/admin/transactions/withdrawals?${params}`;
}

export function mapWithdrawals(response) {
  return {
    ...response,
    stats: response.stats ?? {},
    items: (response.items ?? []).map((item) => ({
      ...item,
      initials: initialsOf(item.driverName),
      status: item.status.toLowerCase(),
      requestedDate: new Intl.DateTimeFormat('vi-VN').format(new Date(item.createdAt)),
      requestedTime: new Intl.DateTimeFormat('vi-VN', { hour: '2-digit', minute: '2-digit' }).format(new Date(item.createdAt)),
    })),
  };
}

export const approveWithdrawal = (id) => apiRequest(`/admin/transactions/withdrawals/${id}/approve`, { method: 'POST' });
export const rejectWithdrawal = (id, reason) => apiRequest(`/admin/transactions/withdrawals/${id}/reject`, { method: 'POST', body: JSON.stringify({ reason }) });
import { apiRequest } from '../../shared/api/apiClient';
