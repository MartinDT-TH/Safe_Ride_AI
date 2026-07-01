import { useMemo, useState } from 'react';
import { AdminLayout } from '../shared/layouts/AdminLayout';
import { DriverStats, DriverTable, DriverVerificationDetail } from '../features/drivers/components';
import useFetch from '../shared/hooks/useFetch';
import { blockDriver, getDriversPath, mapDriverList, reviewDriverKyc, unlockDriver, } from '../features/drivers/driversApi';
import './DriversPage.css';
const EMPTY_COUNTS = {
    all: 0,
    active: 0,
    busy: 0,
    pendingKyc: 0,
    blocked: 0,
};
/** Quản lý Tài xế - Driver Management page */
function DriversPage() {
    const [activeTab, setActiveTab] = useState('all');
    const [selectedDriver, setSelectedDriver] = useState(null);
    const [actionDriverId, setActionDriverId] = useState(null);
    const [mutationError, setMutationError] = useState(null);
    const driversPath = useMemo(() => getDriversPath(activeTab), [activeTab]);
    const { data, isLoading, error, refetch, setData, } = useFetch(driversPath, {
        select: mapDriverList,
    });
    const safeDrivers = useMemo(() => data?.drivers ?? [], [data]);
    const counts = data?.counts ?? EMPTY_COUNTS;
    const updateDriver = (nextDriver) => {
        setData({
            counts,
            drivers: driverMatchesFilter(nextDriver, activeTab)
                ? safeDrivers.map((driver) => (driver.id === nextDriver.id ? nextDriver : driver))
                : safeDrivers.filter((driver) => driver.id !== nextDriver.id),
        });
        setSelectedDriver((current) => (current?.id === nextDriver.id ? nextDriver : current));
    };
    const handleToggleBlock = async (driver) => {
        const reason = driver.isActive
            ? window.prompt('Lý do khóa tài xế?', driver.banReason ?? '')
            : null;
        if (driver.isActive && reason === null) {
            return;
        }
        setActionDriverId(driver.id);
        setMutationError(null);
        try {
            const nextDriver = driver.isActive
                ? await blockDriver(driver.id, reason ?? undefined)
                : await unlockDriver(driver.id);
            updateDriver(nextDriver);
            refetch();
        }
        catch (caughtError) {
            setMutationError(caughtError instanceof Error ? caughtError.message : 'Không thể cập nhật tài xế.');
        }
        finally {
            setActionDriverId(null);
        }
    };
    const handleReviewKyc = async (driver, status, rejectionReason) => {
        setActionDriverId(driver.id);
        setMutationError(null);
        try {
            const nextDriver = await reviewDriverKyc(driver.id, status, rejectionReason);
            updateDriver(nextDriver);
            refetch();
        }
        catch (caughtError) {
            setMutationError(caughtError instanceof Error ? caughtError.message : 'Không thể duyệt hồ sơ KYC.');
        }
        finally {
            setActionDriverId(null);
        }
    };
    return (<AdminLayout>
      {selectedDriver ? (<DriverVerificationDetail driver={selectedDriver} onBack={() => setSelectedDriver(null)} onReviewKyc={handleReviewKyc} actionDriverId={actionDriverId}/>) : (<>
          {/* Page header */}
          <div className="page-header" id="drivers-page-header">
            <h1 className="page-title">Quản lý Tài xế</h1>
            <p className="page-subtitle">
              Giám sát và điều phối hoạt động của đội ngũ tài xế trên toàn hệ thống.
            </p>
          </div>

          {error && (<div className="drivers-feedback drivers-feedback--error">
              <span>{error}</span>
              <button type="button" onClick={refetch}>Thử lại</button>
            </div>)}

          {mutationError && (<div className="drivers-feedback drivers-feedback--error">
              <span>{mutationError}</span>
            </div>)}

          {isLoading && (<div className="drivers-feedback">
              Đang tải danh sách tài xế...
            </div>)}

          {/* Stats row */}
          <DriverStats totalDrivers={counts.all} activeDrivers={counts.active} pendingKycDrivers={counts.pendingKyc}/>

          {/* Table */}
          <DriverTable drivers={safeDrivers} counts={counts} activeTab={activeTab} onTabChange={setActiveTab} onSelectDriver={setSelectedDriver} onToggleBlock={handleToggleBlock} actionDriverId={actionDriverId}/>
        </>)}
    </AdminLayout>);
}
function driverMatchesFilter(driver, filter) {
    if (filter === 'all') {
        return true;
    }
    if (filter === 'busy') {
        return driver.workStatus === 'Busy';
    }
    return driver.status === filter;
}
export default DriversPage;
