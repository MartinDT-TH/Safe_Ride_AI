import { useMemo, useState } from 'react';
import { useEffect } from 'react';
import { AdminLayout } from '../shared/layouts/AdminLayout';
import { DriverStats, DriverTable, DriverVerificationDetail } from '../features/drivers/components';
import useFetch from '../shared/hooks/useFetch';
import useAdminSearch from '../shared/hooks/useAdminSearch';
import { blockDriver, getDriversPath, mapDriverList, reviewDriverKyc, unlockDriver, } from '../features/drivers/driversApi';
import ActionFeedback from '../shared/components/ActionFeedback/ActionFeedback';
import AccountActionDialog from '../shared/components/AccountActionDialog/AccountActionDialog';
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
    const [successMessage, setSuccessMessage] = useState(null);
    const [pendingDriverAction, setPendingDriverAction] = useState(null);
    const { query } = useAdminSearch({
        placeholder: 'Tìm kiếm tài xế, mã tài xế, email hoặc số điện thoại...',
    });
    const driversPath = useMemo(() => getDriversPath(activeTab), [activeTab]);
    const { data, isLoading, error, refetch, setData, } = useFetch(driversPath, {
        select: mapDriverList,
    });
    const safeDrivers = useMemo(() => data?.drivers ?? [], [data]);
    const visibleDrivers = useMemo(() => safeDrivers.filter((driver) => driverMatchesSearch(driver, query)), [query, safeDrivers]);
    const counts = data?.counts ?? EMPTY_COUNTS;

    useEffect(() => {
        if (!successMessage) {
            return undefined;
        }

        const timeoutId = window.setTimeout(() => {
            setSuccessMessage(null);
        }, 4000);

        return () => {
            window.clearTimeout(timeoutId);
        };
    }, [successMessage]);

    const updateDriver = (nextDriver) => {
        setData({
            counts,
            drivers: driverMatchesFilter(nextDriver, activeTab)
                ? safeDrivers.map((driver) => (driver.id === nextDriver.id ? nextDriver : driver))
                : safeDrivers.filter((driver) => driver.id !== nextDriver.id),
        });
        setSelectedDriver((current) => (current?.id === nextDriver.id ? nextDriver : current));
    };

    const closeAccountActionDialog = () => {
        if (actionDriverId) {
            return;
        }

        setMutationError(null);
        setPendingDriverAction(null);
    };

    const handleConfirmDriverAction = async (payload = {}) => {
        const driver = pendingDriverAction?.driver;
        const actionMode = pendingDriverAction?.mode;
        if (!driver || !actionMode) {
            return;
        }

        setActionDriverId(driver.id);
        setMutationError(null);

        try {
            const nextDriver = actionMode === 'lock'
                ? await blockDriver(driver.id, payload.reason)
                : await unlockDriver(driver.id);
            updateDriver(nextDriver);
            refetch();
            setSuccessMessage(
                actionMode === 'lock'
                    ? 'Đã khóa tài khoản tài xế thành công.'
                    : 'Đã mở khóa tài khoản tài xế thành công.',
            );
            setPendingDriverAction(null);
        }
        catch (caughtError) {
            setMutationError(caughtError instanceof Error ? caughtError.message : 'KhÃ´ng thá»ƒ cáº­p nháº­t tÃ i xáº¿.');
        }
        finally {
            setActionDriverId(null);
        }
    };
    const handleToggleBlock = async (driver) => {
        if (isValidatedDriverAccountFlowEnabled()) {
            setMutationError(null);
            setPendingDriverAction({
                mode: driver.isActive ? 'lock' : 'unlock',
                driver,
            });
            return;
        }

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

          <ActionFeedback message={successMessage} />

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
          <DriverTable drivers={visibleDrivers} counts={counts} activeTab={activeTab} onTabChange={setActiveTab} onSelectDriver={setSelectedDriver} onToggleBlock={handleToggleBlock} actionDriverId={actionDriverId}/>

          <AccountActionDialog
            key={pendingDriverAction ? `${pendingDriverAction.mode}-${pendingDriverAction.driver.id}` : 'driver-account-action-closed'}
            isOpen={Boolean(pendingDriverAction)}
            mode={pendingDriverAction?.mode}
            accountType="driver"
            accountName={pendingDriverAction?.driver?.name}
            currentReason={pendingDriverAction?.driver?.banReason}
            isSubmitting={actionDriverId === pendingDriverAction?.driver?.id}
            errorMessage={pendingDriverAction ? mutationError : null}
            onClose={closeAccountActionDialog}
            onConfirm={handleConfirmDriverAction}
          />
        </>)}
    </AdminLayout>);
}
function driverMatchesSearch(driver, query) {
    const normalizedQuery = normalizeSearchQuery(query);
    if (!normalizedQuery) {
        return true;
    }
    const digitQuery = normalizeDigits(query);
    const searchableValues = [
        driver.name,
        driver.email,
        driver.phone,
        driver.id,
        driver.driverCode,
    ];
    return searchableValues
        .map(normalizeSearchQuery)
        .some((value) => value.includes(normalizedQuery))
        || (digitQuery.length > 0 && normalizeDigits(driver.phone).includes(digitQuery));
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
function normalizeSearchQuery(value) {
    return String(value ?? '').trim().toLocaleLowerCase('vi-VN');
}
function normalizeDigits(value) {
    return String(value ?? '').replace(/\D/g, '');
}

function isValidatedDriverAccountFlowEnabled() {
    return typeof window !== 'undefined';
}

export default DriversPage;
