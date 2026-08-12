import { useEffect, useState } from 'react';
import { FontAwesomeIcon } from '@fortawesome/react-fontawesome';
import { faEye, faPhone, faTriangleExclamation, faXmark } from '@fortawesome/free-solid-svg-icons';
import { TopHeader } from '../../../shared/components/TopHeader';
import { getCurrentManagementRole, MANAGEMENT_ROLES } from '../../auth/managementRoles';
import { createAdminSOSConnection } from './adminSOSRealtime';
import { getActiveAdminSOSAlerts, getAdminSOSAlert } from './adminSOSAlertsApi';
import './AdminSOSAlertCenter.css';

const DISMISSED_SOS_KEY = 'saferide_admin_dismissed_sos';

function AdminSOSAlertCenter() {
    const shouldLoadAdminSOS = getCurrentManagementRole() === MANAGEMENT_ROLES.admin;
    const [alerts, setAlerts] = useState([]);
    const [isPanelOpen, setIsPanelOpen] = useState(false);
    const [isRealtimeConnected, setIsRealtimeConnected] = useState(true);
    const [loadError, setLoadError] = useState('');
    const [selectedAlert, setSelectedAlert] = useState(null);
    const [detailError, setDetailError] = useState('');
    const [isDetailLoading, setIsDetailLoading] = useState(false);

    useEffect(() => {
        if (getCurrentManagementRole() !== MANAGEMENT_ROLES.admin) {
            return undefined;
        }

        const controller = new AbortController();
        let active = true;
        const realtime = createAdminSOSConnection({
            onAlert: (alert) => {
                if (!active || isDismissed(alert.sosAlertId)) return;
                setAlerts((current) => prependUnique(current, alert));
                setIsPanelOpen(true);
            },
            onConnectionChanged: (connected) => {
                if (active) setIsRealtimeConnected(connected);
            },
        });

        getActiveAdminSOSAlerts({ signal: controller.signal })
            .then((response) => {
                if (!active) return;
                setAlerts((current) => mergeUnique(
                    response.items.filter((alert) => !isDismissed(alert.sosAlertId)),
                    current,
                ));
            })
            .catch((error) => {
                if (active && error.name !== 'AbortError') {
                    setLoadError('Không thể tải cảnh báo SOS đang hoạt động');
                }
            });
        realtime.start();

        return () => {
            active = false;
            controller.abort();
            realtime.stop();
        };
    }, []);

    if (!shouldLoadAdminSOS) {
        return null;
    }

    const dismissAlert = (sosAlertId) => {
        rememberDismissed(sosAlertId);
        setAlerts((current) => current.filter((alert) => alert.sosAlertId !== sosAlertId));
        if (selectedAlert?.sosAlertId === sosAlertId) setSelectedAlert(null);
    };

    const openDetails = async (alert) => {
        setSelectedAlert(alert);
        setDetailError('');
        setIsDetailLoading(true);
        try {
            setSelectedAlert(await getAdminSOSAlert(alert.sosAlertId));
        }
        catch {
            setDetailError('Không thể tải chi tiết cảnh báo SOS');
        }
        finally {
            setIsDetailLoading(false);
        }
    };

    return (
        <>
            <TopHeader
                sosAlertCount={alerts.length}
                onSOSAlertsClick={() => setIsPanelOpen((open) => !open)}
            />

            {isPanelOpen && (
                <aside className="admin-sos-panel" aria-label="Cảnh báo SOS khẩn cấp">
                    <header>
                        <div>
                            <span>Cảnh báo khẩn cấp</span>
                            <h2>Cảnh báo SOS khẩn cấp</h2>
                        </div>
                        <button type="button" onClick={() => setIsPanelOpen(false)} title="Đóng" aria-label="Đóng">
                            <FontAwesomeIcon icon={faXmark} />
                        </button>
                    </header>

                    {!isRealtimeConnected && <p className="admin-sos-connection-error">Không thể kết nối cảnh báo SOS</p>}
                    {loadError && <p className="admin-sos-load-error">{loadError}</p>}
                    {alerts.length === 0 && <p className="admin-sos-empty">Không có cảnh báo SOS đang hoạt động.</p>}

                    <div className="admin-sos-list">
                        {alerts.map((alert) => (
                            <SOSAlertCard
                                key={alert.sosAlertId}
                                alert={alert}
                                onView={() => openDetails(alert)}
                                onDismiss={() => dismissAlert(alert.sosAlertId)}
                            />
                        ))}
                    </div>
                </aside>
            )}

            {selectedAlert && (
                <SOSDetailModal
                    alert={selectedAlert}
                    isLoading={isDetailLoading}
                    error={detailError}
                    onClose={() => setSelectedAlert(null)}
                    onDismiss={() => dismissAlert(selectedAlert.sosAlertId)}
                />
            )}
        </>
    );
}

function SOSAlertCard({ alert, onView, onDismiss }) {
    return (
        <article className="admin-sos-card">
            <div className="admin-sos-card__title">
                <FontAwesomeIcon icon={faTriangleExclamation} />
                <div>
                    <strong>{alert.customerName}</strong>
                    <span>Trip #{alert.tripId ?? '—'} · Booking #{alert.bookingId ?? '—'}</span>
                </div>
            </div>
            <p><FontAwesomeIcon icon={faPhone} /> {alert.customerPhoneNumber ?? 'Chưa có số điện thoại'}</p>
            {alert.message && <p className="admin-sos-card__message">{alert.message}</p>}
            <small>{alert.createdAtLabel}</small>
            <div className="admin-sos-card__actions">
                <button type="button" onClick={onView}><FontAwesomeIcon icon={faEye} /> Xem chi tiết</button>
                <button type="button" onClick={onDismiss}>Đã xem</button>
            </div>
        </article>
    );
}

function SOSDetailModal({ alert, isLoading, error, onClose, onDismiss }) {
    return (
        <div className="admin-sos-modal-backdrop" role="presentation" onClick={onClose}>
            <div className="admin-sos-modal" role="dialog" aria-modal="true" aria-labelledby="admin-sos-modal-title" onClick={(event) => event.stopPropagation()}>
                <header>
                    <div>
                        <span>SOS #{alert.sosAlertId}</span>
                        <h2 id="admin-sos-modal-title">Chi tiết cảnh báo SOS</h2>
                    </div>
                    <button type="button" onClick={onClose} title="Đóng" aria-label="Đóng"><FontAwesomeIcon icon={faXmark} /></button>
                </header>
                {isLoading && <p>Đang tải chi tiết cảnh báo...</p>}
                {error && <p className="admin-sos-load-error">{error}</p>}
                {!isLoading && (
                    <dl className="admin-sos-detail-grid">
                        <DetailRow label="Khách hàng" value={alert.customerName} />
                        <DetailRow label="SĐT khách hàng" value={alert.customerPhoneNumber} />
                        <DetailRow label="Chuyến đi" value={`Trip #${alert.tripId ?? '—'} · Booking #${alert.bookingId ?? '—'}`} />
                        <DetailRow label="Tài xế liên quan" value={alert.driverName ?? 'Chưa có thông tin tài xế'} />
                        <DetailRow label="SĐT tài xế" value={alert.driverPhoneNumber} />
                        <DetailRow label="Thời gian" value={alert.createdAtLabel} />
                        <DetailRow label="Vị trí" value={formatLocation(alert)} />
                        <DetailRow label="Nội dung" value={alert.message} />
                    </dl>
                )}
                <footer>
                    <button type="button" onClick={onDismiss}>Đã xem</button>
                </footer>
            </div>
        </div>
    );
}

function DetailRow({ label, value }) {
    return <div><dt>{label}</dt><dd>{value ?? 'Chưa có dữ liệu'}</dd></div>;
}

function formatLocation(alert) {
    if (alert.latitude === null || alert.longitude === null) return null;
    return `${alert.latitude.toFixed(6)}, ${alert.longitude.toFixed(6)}`;
}

function prependUnique(alerts, alert) {
    return alerts.some((item) => item.sosAlertId === alert.sosAlertId)
        ? alerts
        : [alert, ...alerts];
}

function mergeUnique(primary, secondary) {
    const alerts = [...primary];
    const ids = new Set(primary.map((alert) => alert.sosAlertId));
    secondary.forEach((alert) => {
        if (!ids.has(alert.sosAlertId)) alerts.push(alert);
    });
    return alerts;
}

function isDismissed(sosAlertId) {
    return readDismissed().has(String(sosAlertId));
}

function rememberDismissed(sosAlertId) {
    const dismissed = readDismissed();
    dismissed.add(String(sosAlertId));
    sessionStorage.setItem(DISMISSED_SOS_KEY, JSON.stringify([...dismissed]));
}

function readDismissed() {
    try {
        return new Set(JSON.parse(sessionStorage.getItem(DISMISSED_SOS_KEY) ?? '[]'));
    }
    catch {
        return new Set();
    }
}

export default AdminSOSAlertCenter;
