import { describe, expect, it } from 'vitest';
import {
  MANAGEMENT_ROLES,
  filterManagementSidebarItems,
  isAllowedSidebarId,
} from './managementRoles';

const items = [
  { id: 'risk-fund' },
  { id: 'staff-accidents' },
  { id: 'staff-payments' },
  { id: 'revenue' },
];

describe('management role filtering', () => {
  it('does not expose Risk Fund or revenue navigation to Staff', () => {
    expect(filterManagementSidebarItems(items, MANAGEMENT_ROLES.staff).map((x) => x.id))
      .toEqual(['staff-accidents', 'staff-payments']);
    expect(isAllowedSidebarId(MANAGEMENT_ROLES.staff, 'risk-fund')).toBe(false);
  });

  it('does not expose Staff operation queues to Admin navigation', () => {
    expect(filterManagementSidebarItems(items, MANAGEMENT_ROLES.admin).map((x) => x.id))
      .toEqual(['risk-fund', 'revenue']);
    expect(isAllowedSidebarId(MANAGEMENT_ROLES.admin, 'staff-accidents')).toBe(false);
  });
});
