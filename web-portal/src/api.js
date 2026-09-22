import axios from 'axios';

const api = axios.create({
  baseURL: import.meta.env.VITE_API_URL || 'http://localhost:5059/api',
});

let refreshPromise = null;

const refreshAccessToken = async () => {
  if (refreshPromise) return refreshPromise;
  refreshPromise = (async () => {
    const storedUser = JSON.parse(localStorage.getItem('user') || 'null');
    if (!storedUser?.refreshToken) throw new Error('Refresh token bulunamadı');
    const response = await axios.post(`${api.defaults.baseURL}/auth/refresh`, {
      refreshToken: storedUser.refreshToken,
    });
    localStorage.setItem('user', JSON.stringify(response.data));
    return response.data.accessToken || response.data.token;
  })().finally(() => { refreshPromise = null; });
  return refreshPromise;
};

api.interceptors.request.use((config) => {
  try {
    const storedUser = JSON.parse(localStorage.getItem('user') || 'null');
    const token = storedUser?.accessToken || storedUser?.token;

    if (token) {
      config.headers.Authorization = `Bearer ${token}`;
    }
  } catch {
    localStorage.removeItem('user');
  }

  return config;
});

api.interceptors.response.use(
  (response) => response,
  async (error) => {
    const originalRequest = error.config;
    if (error.response?.status === 401 && !originalRequest?._retry &&
      !originalRequest?.url?.includes('/auth/login') && !originalRequest?.url?.includes('/auth/refresh')) {
      originalRequest._retry = true;
      try {
        const token = await refreshAccessToken();
        originalRequest.headers = { ...originalRequest.headers, Authorization: `Bearer ${token}` };
        return api(originalRequest);
      } catch {
        localStorage.removeItem('user');
      }
    }

    if (error.response?.status === 401 && !window.location.pathname.includes('/login')) {
      localStorage.removeItem('user');
      window.location.assign('/login');
    }

    return Promise.reject(error);
  },
);

export default api;
