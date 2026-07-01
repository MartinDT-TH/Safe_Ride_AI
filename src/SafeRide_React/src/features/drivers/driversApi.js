import { apiRequest } from '../../shared/api/apiClient';
export function getDrivers(status = 'all') {
    return apiRequest(getDriversPath(status)).then(mapDriverList);
}
export function getDriversPath(status) {
    return status === 'all'
        ? '/admin/drivers'
        : `/admin/drivers?status=${encodeURIComponent(status)}`;
}
export function mapDriverList(response) {
    return {
        drivers: response.drivers.map(mapDriver),
        counts: response.counts,
    };
}
export function blockDriver(driverId, reason) {
    return apiRequest(`/admin/drivers/${driverId}/block`, {
        method: 'PATCH',
        body: JSON.stringify({ reason }),
    }).then(mapDriver);
}
export function unlockDriver(driverId) {
    return apiRequest(`/admin/drivers/${driverId}/unlock`, {
        method: 'PATCH',
    }).then(mapDriver);
}
export function reviewDriverKyc(driverId, status, rejectionReason) {
    return apiRequest(`/admin/drivers/${driverId}/kyc`, {
        method: 'PATCH',
        body: JSON.stringify({ status, rejectionReason }),
    }).then(mapDriver);
}
function mapDriver(driver) {
    const idCard = driver.documents.find((document) => document.documentType === 'ID_CARD');
    return {
        id: driver.id,
        driverCode: driver.driverCode,
        name: driver.name,
        email: driver.email ?? 'Chưa cập nhật',
        phone: driver.phone ?? 'Chưa cập nhật',
        avatar: avatarColorFromId(driver.id),
        avatarUrl: driver.avatarUrl,
        initials: getInitials(driver.name),
        rating: driver.rating,
        trips: driver.trips,
        joinDate: formatShortDate(driver.createdAt),
        registeredDate: formatShortDate(driver.createdAt),
        city: getCity(driver.address),
        status: driver.status,
        workStatus: driver.workStatus,
        isActive: driver.isActive,
        banReason: driver.banReason,
        documents: driver.documents,
        kyc: {
            citizenId: driver.citizenId ?? idCard?.documentNumber ?? 'Chưa cập nhật',
            fullName: driver.name.toLocaleUpperCase('vi-VN'),
            dateOfBirth: driver.dateOfBirth ? formatShortDate(driver.dateOfBirth) : 'Chưa cập nhật',
            gender: mapGender(driver.gender),
            address: driver.address ?? 'Chưa cập nhật',
        },
    };
}
function formatShortDate(value) {
    const date = new Date(value);
    if (Number.isNaN(date.getTime())) {
        return value;
    }
    return new Intl.DateTimeFormat('vi-VN', {
        day: '2-digit',
        month: '2-digit',
        year: 'numeric',
    }).format(date);
}
function getInitials(name) {
    const words = name.trim().split(/\s+/);
    return words
        .slice(-2)
        .map((word) => word[0])
        .join('')
        .toLocaleUpperCase('vi-VN');
}
function getCity(address) {
    if (!address) {
        return 'Chưa cập nhật';
    }
    return address.split(',').at(-1)?.trim() ?? address;
}
function mapGender(gender) {
    if (!gender) {
        return 'Chưa cập nhật';
    }
    return gender === 'Male' ? 'Nam' : gender === 'Female' ? 'Nữ' : gender;
}
function avatarColorFromId(id) {
    const colors = ['#073946', '#5a6c3a', '#7c6e5a', '#4a5a6c', '#007582'];
    const index = id.split('').reduce((sum, char) => sum + char.charCodeAt(0), 0) % colors.length;
    return colors[index];
}
