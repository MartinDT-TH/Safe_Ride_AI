import { apiRequest } from "../shared/api/apiClient";

const apiService = {
  get(path, { signal } = {}) {
    return apiRequest(path, { signal });
  },
};

export default apiService;
