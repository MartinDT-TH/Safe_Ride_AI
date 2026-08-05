import * as signalR from '@microsoft/signalr';
import { getAccessToken } from '../../../shared/api/apiClient';
import { mapAdminSOSAlert } from './adminSOSAlertsApi';

const SOS_EVENT = 'SOSTriggered';
const JOIN_METHOD = 'JoinAdminSOSGroup';
const LEAVE_METHOD = 'LeaveAdminSOSGroup';

export function createAdminSOSConnection({ onAlert, onConnectionChanged }) {
    const connection = new signalR.HubConnectionBuilder()
        .withUrl(getRealtimeHubUrl(), {
            accessTokenFactory: () => getAccessToken() ?? '',
        })
        .withAutomaticReconnect([0, 2000, 5000, 10000])
        .configureLogging(signalR.LogLevel.Warning)
        .build();

    const handleSOS = (payload) => onAlert(mapAdminSOSAlert(payload));
    connection.on(SOS_EVENT, handleSOS);
    connection.onreconnecting(() => onConnectionChanged(false));
    connection.onreconnected(async () => {
        try {
            await connection.invoke(JOIN_METHOD);
            onConnectionChanged(true);
        }
        catch {
            onConnectionChanged(false);
        }
    });
    connection.onclose(() => onConnectionChanged(false));

    return {
        async start() {
            try {
                await connection.start();
                await connection.invoke(JOIN_METHOD);
                onConnectionChanged(true);
            }
            catch {
                onConnectionChanged(false);
            }
        },
        async stop() {
            connection.off(SOS_EVENT, handleSOS);
            if (connection.state === signalR.HubConnectionState.Connected) {
                try {
                    await connection.invoke(LEAVE_METHOD);
                }
                catch {
                    // Connection cleanup continues even if the hub is already unavailable.
                }
            }
            await connection.stop();
        },
    };
}

function getRealtimeHubUrl() {
    const configuredHubUrl = import.meta.env.VITE_REALTIME_HUB_URL;
    if (configuredHubUrl) return configuredHubUrl;

    const apiBaseUrl = import.meta.env.VITE_API_BASE_URL ?? '/api';
    const apiUrl = new URL(apiBaseUrl, window.location.origin);
    apiUrl.pathname = apiUrl.pathname.replace(/\/api\/?$/, '/hubs/saferide');
    apiUrl.search = '';
    apiUrl.hash = '';
    return apiUrl.toString();
}
