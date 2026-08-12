import { getCurrentManagementRole, MANAGEMENT_ROLES } from '../auth/managementRoles';

const currencyFormatter = new Intl.NumberFormat('vi-VN', {
  style: 'currency',
  currency: 'VND',
  maximumFractionDigits: 0,
});

const numberFormatter = new Intl.NumberFormat('vi-VN', {
  maximumFractionDigits: 1,
});

export function getAdminTripDetailsPath({ tripId, bookingId } = {}) {
  if (getCurrentManagementRole() === MANAGEMENT_ROLES.staff) {
    if (bookingId) {
      return `/staff/trips/by-booking/${encodeURIComponent(bookingId)}`;
    }

    if (tripId) {
      return `/staff/trips/${encodeURIComponent(tripId)}`;
    }
  }

  if (bookingId) {
    return `/admin/trips/by-booking/${encodeURIComponent(bookingId)}`;
  }

  if (tripId) {
    return `/admin/trips/${encodeURIComponent(tripId)}`;
  }

  return '';
}

export function mapAdminTripDetails(item = {}) {
  const tripStatus = String(item.tripStatus ?? 'ACCEPTED');
  const bookingStatus = String(item.bookingStatus ?? 'Searching');
  const route = item.route ?? {};
  const fare = item.fare ?? {};
  const payment = item.payment ?? null;
  const timeline = item.timeline ?? {};
  const promotions = (item.promotions ?? []).map(mapPromotion);
  const distanceKm = route.actualDistanceKm ?? route.estimatedDistanceKm ?? null;
  const durationMinutes =
    route.actualDurationMinutes ?? route.estimatedDurationMinutes ?? null;
  const adjustmentAmount = fare.actualFare && fare.actualFare !== fare.estimatedFare
    ? fare.actualFare - fare.estimatedFare
    : 0;

  return {
    raw: item,
    tripId: item.tripId,
    tripCode: item.tripCode ?? `SR-${item.tripId}`,
    bookingId: item.bookingId,
    bookingCode: item.bookingCode ?? `SR-${item.bookingId}`,
    tripStatus,
    tripStatusLabel: mapTripStatusLabel(tripStatus),
    tripStatusVariant: mapTripStatusVariant(tripStatus),
    bookingStatus,
    bookingStatusLabel: mapBookingStatusLabel(bookingStatus),
    bookingTypeLabel: mapBookingTypeLabel(item.bookingType),
    serviceName: item.serviceName ?? 'Chưa cập nhật',
    customer: mapUser(item.customer, 'Khách hàng'),
    driver: mapDriver(item.driver),
    vehicle: mapVehicle(item.vehicle),
    pickup: mapLocation(item.pickupLocation),
    destination: mapLocation(item.destinationLocation),
    route: {
      ...route,
      distanceLabel: formatDistance(distanceKm),
      durationLabel: formatDuration(durationMinutes),
      safetyLabel: mapSafetyLabel(route),
      safetyTone: mapSafetyTone(route),
      safetyNote: mapSafetyNote(route),
    },
    timeline: {
      ...timeline,
      bookingCreatedAtLabel: formatDateTime(timeline.bookingCreatedAt),
      scheduledAtLabel: formatDateTime(timeline.scheduledAt),
      driverAssignedAtLabel: formatDateTime(timeline.driverAssignedAt),
      arrivedAtLabel: formatDateTime(timeline.arrivedAt),
      startedAtLabel: formatDateTime(timeline.startedAt),
      endedAtLabel: formatDateTime(timeline.endedAt),
      completedAtLabel: formatDateTime(timeline.completedAt),
      items: buildTimelineItems(timeline, tripStatus),
    },
    fare: {
      ...fare,
      estimatedFareLabel: formatMoney(fare.estimatedFare),
      actualFareLabel: formatMoney(fare.actualFare),
      finalFareLabel: formatMoney(fare.finalFare),
      discountAmountLabel: formatMoney(fare.discountAmount),
      paymentRows: buildPaymentRows(fare, promotions, adjustmentAmount, distanceKm),
    },
    payment: payment ? mapPayment(payment) : null,
    promotions,
    tripNotes: item.tripNotes || 'Không có ghi chú cho chuyến đi này.',
    rating: item.rating ? mapRating(item.rating) : null,
    createdAtLabel: formatDateTime(item.createdAt),
    lastUpdatedAtLabel: formatDateTime(item.lastUpdatedAt),
    completedAtSummaryLabel: buildCompletionSummary(timeline.completedAt, timeline.endedAt),
  };
}

function mapUser(user, fallbackName) {
  const name = user?.name || fallbackName;
  return {
    id: user?.id,
    name,
    phone: user?.phone || 'Chưa cập nhật',
    email: user?.email || 'Chưa cập nhật',
    avatarUrl: user?.avatarUrl,
    initials: initialsOf(name),
  };
}

function mapDriver(driver) {
  const user = mapUser(driver, 'Tài xế SafeRide');
  return {
    ...user,
    workStatus: driver?.workStatus ?? 'Offline',
    experienceYears: driver?.experienceYears,
    averageRating: driver?.averageRating,
    averageRatingLabel: driver?.averageRating
      ? numberFormatter.format(driver.averageRating)
      : 'Chưa có',
  };
}

function mapVehicle(vehicle = {}) {
  return {
    id: vehicle.id,
    brandModel: vehicle.brandModel ?? 'Chưa cập nhật',
    plateNumber: vehicle.plateNumber ?? 'Chưa cập nhật',
    color: vehicle.color ?? 'Chưa cập nhật',
    vehicleType: vehicle.vehicleType ?? 'Car',
    vehicleTypeLabel: vehicle.vehicleType === 'Motorbike' ? 'Xe máy' : 'Ô tô',
    engineType: vehicle.engineType ?? 'ICE',
    transmissionType: vehicle.transmissionType ?? 'None',
    engineCapacityCc: vehicle.engineCapacityCc,
    requiredLicenseClass: vehicle.requiredLicenseClass ?? 'Chưa cập nhật',
  };
}

function mapLocation(location = {}) {
  return {
    address: location?.address || 'Chưa cập nhật',
    latitude: location?.latitude,
    longitude: location?.longitude,
    coordinateLabel:
      location?.latitude && location?.longitude
        ? `${location.latitude.toFixed(5)}, ${location.longitude.toFixed(5)}`
        : 'Chưa có tọa độ',
  };
}

function mapPayment(payment) {
  return {
    ...payment,
    paymentMethodLabel: mapPaymentMethodLabel(payment.paymentMethod),
    paymentStatusLabel: mapPaymentStatusLabel(payment.paymentStatus),
    paymentStatusVariant: mapPaymentStatusVariant(payment.paymentStatus),
    amountLabel: formatMoney(payment.amount),
    paidAtLabel: formatDateTime(payment.paidAt),
  };
}

function mapPromotion(promotion) {
  return {
    ...promotion,
    discountAmountLabel: formatMoney(promotion.discountAmount),
    discountValueLabel:
      promotion.discountType === 'Percentage'
        ? `${numberFormatter.format(promotion.discountValue)}%`
        : formatMoney(promotion.discountValue),
  };
}

function mapRating(rating) {
  return {
    ...rating,
    createdAtLabel: formatDateTime(rating.createdAt),
  };
}

function buildTimelineItems(timeline, tripStatus) {
  return [
    {
      id: 'booking',
      title: 'Đặt chuyến',
      time: formatTime(timeline.bookingCreatedAt),
      detail: timeline.scheduledAt
        ? `Đặt lịch ${formatDateTime(timeline.scheduledAt)}`
        : 'Đã ghi nhận yêu cầu',
      done: Boolean(timeline.bookingCreatedAt),
    },
    {
      id: 'assigned',
      title: 'Tài xế nhận chuyến',
      time: formatTime(timeline.driverAssignedAt),
      detail: 'Đã phân công tài xế',
      done: Boolean(timeline.driverAssignedAt),
    },
    {
      id: 'arrived',
      title: 'Tài xế đã đến',
      time: formatTime(timeline.arrivedAt),
      detail: 'Đến điểm đón',
      done: Boolean(timeline.arrivedAt),
    },
    {
      id: 'started',
      title: 'Bắt đầu di chuyển',
      time: formatTime(timeline.startedAt),
      detail: 'Theo dõi lộ trình',
      done: Boolean(timeline.startedAt),
    },
    {
      id: 'completed',
      title: tripStatus === 'CANCELLED' ? 'Chuyến đi đã hủy' : 'Kết thúc chuyến đi',
      time: formatTime(timeline.completedAt ?? timeline.endedAt),
      detail: tripStatus === 'COMPLETED' ? 'Trả khách an toàn' : mapTripStatusLabel(tripStatus),
      done: tripStatus === 'COMPLETED' || tripStatus === 'CANCELLED',
      terminal: true,
    },
  ];
}

function buildPaymentRows(fare, promotions, adjustmentAmount, distanceKm) {
  const rows = [
    {
      id: 'estimated',
      label: `Giá cước ước tính (${formatDistance(distanceKm)})`,
      amount: fare.estimatedFare,
    },
  ];

  if (adjustmentAmount) {
    rows.push({
      id: 'adjustment',
      label: adjustmentAmount > 0 ? 'Điều chỉnh cước thực tế' : 'Giảm cước thực tế',
      amount: adjustmentAmount,
      tone: adjustmentAmount > 0 ? 'positive' : 'discount',
    });
  }

  promotions.forEach((promotion) => {
    rows.push({
      id: `promotion-${promotion.id}`,
      label: `Khuyến mãi (${promotion.promotionCode})`,
      amount: -promotion.discountAmount,
      tone: 'discount',
    });
  });

  rows.push({
    id: 'total',
    label: 'Tổng cộng',
    amount: fare.finalFare,
    total: true,
  });

  return rows.map((row) => ({
    ...row,
    amountLabel: formatSignedMoney(row.amount),
  }));
}

function buildCompletionSummary(completedAt, endedAt) {
  const value = completedAt ?? endedAt;
  if (!value) {
    return 'Chưa kết thúc';
  }

  return `Kết thúc lúc ${formatDateTime(value)}`;
}

function formatMoney(value) {
  if (value === null || value === undefined) {
    return 'Chưa cập nhật';
  }

  return currencyFormatter.format(value);
}

function formatSignedMoney(value) {
  if (value === null || value === undefined) {
    return 'Chưa cập nhật';
  }

  if (value > 0) {
    return `+ ${formatMoney(value)}`;
  }

  if (value < 0) {
    return `- ${formatMoney(Math.abs(value))}`;
  }

  return formatMoney(0);
}

function formatDistance(value) {
  if (value === null || value === undefined) {
    return 'Chưa cập nhật';
  }

  return `${numberFormatter.format(value)} km`;
}

function formatDuration(value) {
  if (value === null || value === undefined) {
    return 'Chưa cập nhật';
  }

  return `${value} phút`;
}

function formatDateTime(value) {
  if (!value) {
    return 'Chưa cập nhật';
  }

  const date = new Date(value);
  if (Number.isNaN(date.getTime())) {
    return value;
  }

  return new Intl.DateTimeFormat('vi-VN', {
    day: '2-digit',
    month: '2-digit',
    year: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
  }).format(date);
}

function formatTime(value) {
  if (!value) {
    return '--:--';
  }

  const date = new Date(value);
  if (Number.isNaN(date.getTime())) {
    return value;
  }

  return new Intl.DateTimeFormat('vi-VN', {
    hour: '2-digit',
    minute: '2-digit',
  }).format(date);
}

function mapTripStatusLabel(status) {
  return {
    ACCEPTED: 'Đã nhận chuyến',
    DRIVER_ARRIVING: 'Đang đến điểm đón',
    ARRIVED: 'Tài xế đã đến',
    IN_PROGRESS: 'Đang di chuyển',
    WAITING_RETURN_CONFIRM: 'Chờ xác nhận trả xe',
    RETURN_CONFIRMED: 'Đã xác nhận trả xe',
    WAITING_PAYMENT: 'Chờ thanh toán',
    COMPLETED: 'Hoàn thành',
    CANCELLED: 'Đã hủy',
  }[status] ?? status;
}

function mapTripStatusVariant(status) {
  if (status === 'COMPLETED') return 'green';
  if (status === 'CANCELLED') return 'red';
  if (status === 'WAITING_PAYMENT' || status === 'WAITING_RETURN_CONFIRM') return 'yellow';
  return 'teal';
}

function mapBookingStatusLabel(status) {
  return {
    PendingSchedule: 'Đã lên lịch',
    Searching: 'Đang tìm tài xế',
    DriverAssigned: 'Đã chỉ định',
    Cancelled: 'Đã hủy',
    Expired: 'Hết hạn',
    Completed: 'Hoàn thành',
  }[status] ?? status;
}

function mapBookingTypeLabel(type) {
  return type === 'Scheduled' ? 'Đặt lịch' : 'Đặt ngay';
}

function mapPaymentMethodLabel(method) {
  return method === 'CASH' ? 'Tiền mặt' : method === 'QR' ? 'Ví / QR' : 'Chưa phát sinh';
}

function mapPaymentStatusLabel(status) {
  return {
    Pending: 'Chờ thanh toán',
    Success: 'Đã thanh toán',
    Failed: 'Thanh toán lỗi',
    Unpaid: 'Chưa thanh toán',
    Cancelled: 'Đã hủy thanh toán',
    Disputed: 'Đang tranh chấp',
    Refunded: 'Đã hoàn tiền',
  }[status] ?? 'Chưa phát sinh';
}

function mapPaymentStatusVariant(status) {
  if (status === 'Success') return 'green';
  if (status === 'Failed' || status === 'Cancelled' || status === 'Disputed') return 'red';
  return 'yellow';
}

function mapSafetyLabel(route) {
  if (route?.isSosActivated || route?.sosAlertCount > 0) {
    return 'Có cảnh báo';
  }

  if (route?.routeDeviationCount > 0) {
    return 'Có lệch hướng';
  }

  return 'Đúng lộ trình';
}

function mapSafetyTone(route) {
  if (route?.isSosActivated || route?.sosAlertCount > 0) {
    return 'danger';
  }

  if (route?.routeDeviationCount > 0) {
    return 'warning';
  }

  return 'safe';
}

function mapSafetyNote(route) {
  if (route?.isSosActivated || route?.sosAlertCount > 0) {
    return 'Hệ thống ghi nhận cảnh báo an toàn trong chuyến đi này. Vui lòng kiểm tra nhật ký hỗ trợ liên quan.';
  }

  if (route?.routeDeviationCount > 0) {
    return `Hệ thống ghi nhận ${route.routeDeviationCount} lần lệch hướng và cần đối chiếu khi xử lý hỗ trợ.`;
  }

  return 'Hệ thống không ghi nhận cảnh báo tốc độ, SOS hoặc lệch hướng nghiêm trọng trong suốt hành trình.';
}

function initialsOf(name = '') {
  const words = name.trim().split(/\s+/).filter(Boolean);
  return words.length
    ? words
        .slice(-2)
        .map((word) => word[0])
        .join('')
        .toLocaleUpperCase('vi-VN')
    : 'SR';
}
