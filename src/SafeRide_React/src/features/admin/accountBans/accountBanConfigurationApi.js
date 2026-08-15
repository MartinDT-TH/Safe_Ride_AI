import { apiRequest } from '../../../shared/api/apiClient';

const CONFIG_PATH = '/admin/account-ban-configuration';

const dateTimeFormatter = new Intl.DateTimeFormat('vi-VN', {
  timeZone: 'Asia/Ho_Chi_Minh',
  day: '2-digit',
  month: '2-digit',
  year: 'numeric',
  hour: '2-digit',
  minute: '2-digit',
});

export function getAccountBanConfiguration() {
  return apiRequest(CONFIG_PATH, { method: 'GET' }).then(mapConfiguration);
}

export function updateAccountBanConfiguration(payload) {
  return apiRequest(CONFIG_PATH, {
    method: 'PUT',
    body: JSON.stringify(payload),
  }).then(mapConfiguration);
}

export function createAccountBanFormValues(configuration = {}) {
  return {
    negativeFeedbackThreshold: String(configuration.negativeFeedbackThreshold ?? ''),
    negativeRatingMaxScore: String(configuration.negativeRatingMaxScore ?? ''),
    temporaryBanDurationDays: String(configuration.temporaryBanDurationDays ?? ''),
    maximumTemporaryBans: String(configuration.maximumTemporaryBans ?? ''),
    isEnabled: configuration.isEnabled !== false,
  };
}

export function toAccountBanPayload(values) {
  return {
    negativeFeedbackThreshold: toInteger(values.negativeFeedbackThreshold),
    negativeRatingMaxScore: toInteger(values.negativeRatingMaxScore),
    temporaryBanDurationDays: toInteger(values.temporaryBanDurationDays),
    maximumTemporaryBans: toInteger(values.maximumTemporaryBans),
    isEnabled: values.isEnabled === true,
  };
}

export function validateAccountBanValues(values) {
  const errors = {};
  const threshold = toInteger(values.negativeFeedbackThreshold);
  const maxScore = toInteger(values.negativeRatingMaxScore);
  const duration = toInteger(values.temporaryBanDurationDays);
  const maxTemporaryBans = toInteger(values.maximumTemporaryBans);

  if (!Number.isInteger(threshold) || threshold <= 0) {
    errors.negativeFeedbackThreshold = 'Ngưỡng phản hồi tiêu cực phải lớn hơn 0.';
  }
  if (!Number.isInteger(maxScore) || maxScore < 1 || maxScore > 5) {
    errors.negativeRatingMaxScore = 'Điểm đánh giá tiêu cực phải từ 1 đến 5.';
  }
  if (!Number.isInteger(duration) || duration <= 0) {
    errors.temporaryBanDurationDays = 'Thời gian khóa tạm thời phải lớn hơn 0 ngày.';
  }
  if (!Number.isInteger(maxTemporaryBans) || maxTemporaryBans <= 0) {
    errors.maximumTemporaryBans = 'Số lần khóa tạm thời tối đa phải lớn hơn 0.';
  }

  return errors;
}

function mapConfiguration(response = {}) {
  const updatedAt = read(response, 'updatedAt', 'UpdatedAt');
  const createdAt = read(response, 'createdAt', 'CreatedAt');

  return {
    id: read(response, 'id', 'Id'),
    negativeFeedbackThreshold: toInteger(read(response, 'negativeFeedbackThreshold', 'NegativeFeedbackThreshold')),
    negativeRatingMaxScore: toInteger(read(response, 'negativeRatingMaxScore', 'NegativeRatingMaxScore')),
    temporaryBanDurationDays: toInteger(read(response, 'temporaryBanDurationDays', 'TemporaryBanDurationDays')),
    maximumTemporaryBans: toInteger(read(response, 'maximumTemporaryBans', 'MaximumTemporaryBans')),
    isEnabled: read(response, 'isEnabled', 'IsEnabled') !== false,
    createdAt,
    updatedAt,
    updatedAtLabel: formatDateTime(updatedAt),
    createdAtLabel: formatDateTime(createdAt),
    updatedByUserId: read(response, 'updatedByUserId', 'UpdatedByUserId') ?? null,
  };
}

function formatDateTime(value) {
  if (!value) return 'Chưa cập nhật';
  const timestamp = new Date(value).getTime();
  return Number.isNaN(timestamp)
    ? 'Chưa cập nhật'
    : dateTimeFormatter.format(timestamp);
}

function toInteger(value) {
  const number = Number(value);
  return Number.isInteger(number) ? number : NaN;
}

function read(source, camelCaseKey, pascalCaseKey) {
  if (!source || typeof source !== 'object') return undefined;
  return source[camelCaseKey] ?? source[pascalCaseKey];
}
