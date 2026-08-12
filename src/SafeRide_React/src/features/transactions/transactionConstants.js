export const STATUS_META = {
  success: { label: 'Thành công', variant: 'green' },
  pending: { label: 'Đang xử lý', variant: 'yellow' },
  failed: { label: 'Thất bại', variant: 'red' },
};
STATUS_META.cancelled = { label: 'Đã hủy', variant: 'gray' };
STATUS_META.unpaid = { label: 'Chưa thanh toán', variant: 'yellow' };
STATUS_META.disputed = { label: 'Tranh chấp', variant: 'red' };
STATUS_META.refunded = { label: 'Đã hoàn tiền', variant: 'gray' };
