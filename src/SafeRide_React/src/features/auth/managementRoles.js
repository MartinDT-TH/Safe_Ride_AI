import { getAccessToken } from '../../shared/api/apiClient';

export const MANAGEMENT_ROLES = {
  admin: 'Admin',
  staff: 'Staff',
};

export const ADMIN_SIDEBAR_IDS = [
  'drivers',
  'customers',
  'bookings',
  'trips',
  'transactions',
  'promotions',
  'pricing',
  'account-bans',
  'revenue',
  'notifications',
  'reports',
  'risk-fund',
  'staff-customer-no-shows',
];

export const STAFF_SIDEBAR_IDS = [
  'bookings',
  'trips',
  'staff-driver-verification',
  'staff-payments',
  'staff-driver-ratings',
  'staff-notifications',
  'staff-accidents',
  'staff-customer-no-shows',
];

export function getCurrentManagementRole(user) {
  return getManagementRoleFromRoles(user?.roles)
    ?? getManagementRoleFromRoles([user?.roleKey, user?.role])
    ?? getManagementRoleFromToken();
}

export function getManagementRoleFromRoles(roles) {
  const roleValues = Array.isArray(roles) ? roles : [roles];
  if (roleValues.some((role) => equalsRole(role, MANAGEMENT_ROLES.admin))) {
    return MANAGEMENT_ROLES.admin;
  }
  if (roleValues.some((role) => equalsRole(role, MANAGEMENT_ROLES.staff))) {
    return MANAGEMENT_ROLES.staff;
  }
  return null;
}

export function getDefaultManagementSidebarId(role) {
  return role === MANAGEMENT_ROLES.staff ? 'bookings' : 'revenue';
}

export function isAllowedSidebarId(role, sidebarId) {
  const allowedIds = role === MANAGEMENT_ROLES.staff
    ? STAFF_SIDEBAR_IDS
    : ADMIN_SIDEBAR_IDS;
  return allowedIds.includes(sidebarId);
}

export function filterManagementSidebarItems(items, role = getCurrentManagementRole()) {
  const allowedIds = role === MANAGEMENT_ROLES.staff
    ? STAFF_SIDEBAR_IDS
    : ADMIN_SIDEBAR_IDS;
  return items.filter((item) => allowedIds.includes(item.id));
}

export function getManagementRoleFromToken() {
  const token = getAccessToken();
  if (!token) {
    return null;
  }

  try {
    const payload = JSON.parse(base64UrlDecode(token.split('.')[1]));
    return getManagementRoleFromRoles(readRoleClaims(payload));
  }
  catch {
    return null;
  }
}

function readRoleClaims(payload) {
  return [
    payload?.role,
    payload?.roles,
    payload?.['http://schemas.microsoft.com/ws/2008/06/identity/claims/role'],
  ].flatMap((value) => Array.isArray(value) ? value : [value]).filter(Boolean);
}

function equalsRole(value, role) {
  return String(value ?? '').toLocaleLowerCase('en-US') === role.toLocaleLowerCase('en-US');
}

function base64UrlDecode(value = '') {
  const normalized = value.replace(/-/g, '+').replace(/_/g, '/');
  const padded = normalized.padEnd(Math.ceil(normalized.length / 4) * 4, '=');
  return decodeURIComponent(
    atob(padded)
      .split('')
      .map((char) => `%${char.charCodeAt(0).toString(16).padStart(2, '0')}`)
      .join(''),
  );
}
