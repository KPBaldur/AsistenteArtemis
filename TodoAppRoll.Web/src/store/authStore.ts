import { create } from 'zustand';

interface AuthState {
    user: any | null;
    token: string | null;
    isAuthenticated: boolean;
    setAuth: (user: any, token: string) => void;
    logout: () => void;
}

const getInitialUser = () => {
    try {
        const storedUser = localStorage.getItem('user');
        return storedUser ? JSON.parse(storedUser) : null;
    } catch (error) {
        localStorage.removeItem('user');
        return null;
    }
};

export const useAuthStore = create<AuthState>((set) => ({
    user: getInitialUser(),
    token: localStorage.getItem('token'),
    isAuthenticated: !!localStorage.getItem('token'),
    setAuth: (user, token) => {
        localStorage.setItem('user', JSON.stringify(user));
        localStorage.setItem('token', token);
        set({ user, token, isAuthenticated: true });
    },
    logout: () => {
        localStorage.removeItem('user');
        localStorage.removeItem('token');
        set({ user: null, token: null, isAuthenticated: false });
    },
}));
