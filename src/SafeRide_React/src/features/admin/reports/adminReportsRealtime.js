import * as signalR from '@microsoft/signalr';
import { getAccessToken } from '../../../shared/api/apiClient';

const REPORT_CREATED_EVENT = 'ReportCreated';
const JOIN_METHOD = 'JoinAdminReportsGroup';
const LEAVE_METHOD = 'LeaveAdminReportsGroup';

export function createAdminReportsConnection({ onReportCreated, onConnectionChanged }) {
    const connection = new signalR.HubConnectionBuilder()
        .withUrl(getRealtimeHubUrl(), {
            accessTokenFactory: () => getAccessToken() ?? '',
        })
        .withAutomaticReconnect([0, 2000, 5000, 10000])
        .configureLogging(signalR.LogLevel.Warning)
        .build();

    connection.on(REPORT_CREATED_EVENT, onReportCreated);
    connection.onreconnecting(() => onConnectionChanged(false));
    connection.onreconnected(() => joinGroup(connection, onConnectionChanged));
    connection.onclose(() => onConnectionChanged(false));

    return {
        async start() {
            try {
                await connection.start();
                await joinGroup(connection, onConnectionChanged);
            }
            catch {
                onConnectionChanged(false);
            }
        },
        async stop() {
            connection.off(REPORT_CREATED_EVENT, onReportCreated);
            if (connection.state === signalR.HubConnectionState.Connected) {
                try {
                    await connection.invoke(LEAVE_METHOD);
                }
                catch {
                    // Continue cleanup when the server has already disconnected.
                }
            }
            await connection.stop();
        },
    };
}

async function joinGroup(connection, onConnectionChanged) {
    try {
        await connection.invoke(JOIN_METHOD);
        onConnectionChanged(true);
    }
    catch {
        onConnectionChanged(false);
    }
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
