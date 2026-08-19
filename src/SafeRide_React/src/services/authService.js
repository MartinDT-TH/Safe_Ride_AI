import { apiRequest, saveAuthTokens } from "../shared/api/apiClient";

async function login(credentials) {
  const response = await apiRequest("/admin/auth/login", {
    auth: false,
    method: "POST",
    body: JSON.stringify({
      email: credentials.email,
      password: credentials.password,
      deviceName: "SafeRide Admin Web",
    }),
  });

  saveAuthTokens(response.accessToken, response.refreshToken);
  return response;
}

const authService = { login };

export default authService;
