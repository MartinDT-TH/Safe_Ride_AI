const API_BASE_URL = import.meta.env.VITE_API_BASE_URL ?? '/api';
const ACCESS_TOKEN_KEY = 'saferide_access_token';
const REFRESH_TOKEN_KEY = 'saferide_refresh_token';
export class ApiError extends Error {
    status;
    code;
    detail;
    traceId;
    constructor(message, status, { code, detail, traceId } = {}) {
        super(message);
        this.name = 'ApiError';
        this.status = status;
        this.code = code;
        this.detail = detail ?? message;
        this.traceId = traceId;
    }
}
export function saveAuthTokens(accessToken, refreshToken) {
    localStorage.setItem(ACCESS_TOKEN_KEY, accessToken);
    localStorage.setItem(REFRESH_TOKEN_KEY, refreshToken);
}
export function clearAuthTokens() {
    localStorage.removeItem(ACCESS_TOKEN_KEY);
    localStorage.removeItem(REFRESH_TOKEN_KEY);
}
export function getAccessToken() {
    return localStorage.getItem(ACCESS_TOKEN_KEY);
}
export async function apiRequest(path, { auth = true, headers, body, ...init } = {}) {
    const requestHeaders = new Headers(headers);
    if (body && !(body instanceof FormData) && !requestHeaders.has('Content-Type')) {
        requestHeaders.set('Content-Type', 'application/json');
    }
    if (auth) {
        const token = getAccessToken();
        if (token) {
            requestHeaders.set('Authorization', `Bearer ${token}`);
        }
    }
    const response = await fetch(`${API_BASE_URL}${path}`, {
        ...init,
        body,
        headers: requestHeaders,
    });
    if (!response.ok) {
        const problem = await readErrorDetails(response);
        throw new ApiError(problem.message, response.status, problem);
    }
    if (response.status === 204) {
        return undefined;
    }
    return response.json();
}

/** Download a protected API response without forcing JSON parsing. */
export async function apiDownload(path, { auth = true, headers, ...init } = {}) {
    const requestHeaders = new Headers(headers);
    if (auth) {
        const token = getAccessToken();
        if (token) requestHeaders.set('Authorization', `Bearer ${token}`);
    }
    const response = await fetch(`${API_BASE_URL}${path}`, { ...init, headers: requestHeaders });
    if (!response.ok) {
        const problem = await readErrorDetails(response);
        throw new ApiError(problem.message, response.status, problem);
    }
    return {
        blob: await response.blob(),
        fileName: getDownloadFileName(response.headers.get('Content-Disposition')),
    };
}

function getDownloadFileName(disposition) {
    if (!disposition) return undefined;
    const utf8 = disposition.match(/filename\*=UTF-8''([^;]+)/i);
    if (utf8) return decodeURIComponent(utf8[1]);
    return disposition.match(/filename="?([^";]+)"?/i)?.[1];
}
async function readErrorDetails(response) {
    try {
        const payload = await response.json();
        const detail = payload.detail ?? payload.message ?? payload.title ?? `HTTP ${response.status}`;
        return {
            message: detail,
            detail,
            code: payload.code,
            traceId: payload.traceId,
        };
    }
    catch {
        return { message: `HTTP ${response.status}`, detail: `HTTP ${response.status}` };
    }
}
