import { apiRequest } from '../../../shared/api/apiClient';

export function getStaffPaymentStatusesPath({ status, method, date, page, pageSize = 10 }) {
  const params = new URLSearchParams({ page: String(page), pageSize: String(pageSize) });
  if (status !== 'all') params.set('status', status);
  if (method !== 'all') params.set('method', method);
  if (date) params.set('date', date);
  return `/staff/payments?${params}`;
}

export function getStaffPaymentStatuses(filters) {
  return apiRequest(getStaffPaymentStatusesPath(filters)).then(mapStaffPaymentStatuses);
}

export function mapStaffPaymentStatuses(response) {
  return {
    counts: response.counts ?? {},
    items: (response.items ?? []).map((item) => ({
      id: `PAY-${item.id}`,
      tripId: `SR-${item.tripId}`,
      bookingId: `SR-${item.bookingId}`,
      initials: initialsOf(item.customerName),
      customer: item.customerName,
      phone: item.maskedPhone,
      amount: item.amount,
      method: item.method === 'CASH' ? 'Tiền mặt' : 'QR',
      methodValue: item.method,
      date: new Intl.DateTimeFormat('vi-VN').format(new Date(item.performedAt)),
      time: new Intl.DateTimeFormat('vi-VN', { hour: '2-digit', minute: '2-digit' }).format(new Date(item.performedAt)),
      status: String(item.status ?? '').toLowerCase(),
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
