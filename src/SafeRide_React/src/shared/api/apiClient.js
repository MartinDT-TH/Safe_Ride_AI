const API_BASE_URL = import.meta.env.VITE_API_BASE_URL ?? '/api';
const ACCESS_TOKEN_KEY = 'saferide_access_token';
const REFRESH_TOKEN_KEY = 'saferide_refresh_token';
export class ApiError extends Error {
    status;
    constructor(message, status) {
        super(message);
        this.name = 'ApiError';
        this.status = status;
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
        throw new ApiError(await readErrorMessage(response), response.status);
    }
    if (response.status === 204) {
        return undefined;
    }
    return response.json();
}
async function readErrorMessage(response) {
    try {
        const payload = await response.json();
        return payload.message ?? payload.detail ?? payload.title ?? `HTTP ${response.status}`;
    }
    catch {
        return `HTTP ${response.status}`;
    }
}
