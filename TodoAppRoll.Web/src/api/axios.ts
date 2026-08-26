import axios from 'axios';

const api = axios.create({
    baseURL: import.meta.env.VITE_API_URL || 'https://localhost:7214/api',
    headers: {
        'Content-Type': 'application/json',
    },
});

// Interceptor para añadir el token JWT
api.interceptors.request.use((config) => {
    const token = localStorage.getItem('token');
    if (token) {
        config.headers.Authorization = `Bearer ${token}`;
    }
    return config;
});

let isRefreshing = false;
let pendingRequests: Array<(token: string | null) => void> = [];

function resolvePendingRequests(newToken: string | null) {
    pendingRequests.forEach((resolve) => resolve(newToken));
    pendingRequests = [];
}

function clearSessionAndRedirect() {
    localStorage.removeItem('user');
    localStorage.removeItem('token');
    localStorage.removeItem('refreshToken');
    window.location.href = '/login';
}

// Interceptor de respuesta: ante un 401, intenta renovar el access token
// una sola vez con el refresh token antes de forzar logout.
api.interceptors.response.use(
    (response) => response,
    async (error) => {
        const originalRequest = error.config;

        if (error.response?.status !== 401 || originalRequest._retry || originalRequest.url?.includes('/Auth/')) {
            return Promise.reject(error);
        }

        const refreshToken = localStorage.getItem('refreshToken');
        const token = localStorage.getItem('token');

        if (!refreshToken || !token) {
            clearSessionAndRedirect();
            return Promise.reject(error);
        }

        originalRequest._retry = true;

        if (isRefreshing) {
            return new Promise((resolve, reject) => {
                pendingRequests.push((newToken) => {
                    if (!newToken) {
                        reject(error);
                        return;
                    }
                    originalRequest.headers.Authorization = `Bearer ${newToken}`;
                    resolve(api(originalRequest));
                });
            });
        }

        isRefreshing = true;
        try {
            const response = await axios.post(`${api.defaults.baseURL}/Auth/refresh`, {
                token,
                refreshToken,
            });

            const { token: newToken, refreshToken: newRefreshToken } = response.data;
            localStorage.setItem('token', newToken);
            localStorage.setItem('refreshToken', newRefreshToken);

            resolvePendingRequests(newToken);
            originalRequest.headers.Authorization = `Bearer ${newToken}`;
            return api(originalRequest);
        } catch (refreshError) {
            resolvePendingRequests(null);
            clearSessionAndRedirect();
            return Promise.reject(refreshError);
        } finally {
            isRefreshing = false;
        }
    }
);

export default api;
