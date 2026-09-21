import axios from 'axios';

const api = axios.create({
  baseURL: import.meta.env.VITE_API_URL || 'http://localhost:5059/api',
});

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
  (error) => {
    if (error.response?.status === 401 && !window.location.pathname.includes('/login')) {
      localStorage.removeItem('user');
      window.location.assign('/login');
    }

    return Promise.reject(error);
  },
);

export default api;
