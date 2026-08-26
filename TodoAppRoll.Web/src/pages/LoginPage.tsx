import React, { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import api from '../api/axios';
import { useAuthStore } from '../store/authStore';

const LoginPage: React.FC = () => {
    const [isLogin, setIsLogin] = useState(true);
    const [username, setUsername] = useState('');
    const [email, setEmail] = useState('');
    const [password, setPassword] = useState('');
    const [error, setError] = useState('');
    const navigate = useNavigate();
    const setAuth = useAuthStore((state: any) => state.setAuth);

    const handleSubmit = async (e: React.FormEvent) => {
        e.preventDefault();
        setError('');
        try {
            const endpoint = isLogin ? '/Auth/login' : '/Auth/register';
            const payload = isLogin ? { username, password } : { username, email, password };
            const response = await api.post(endpoint, payload);
            
            if (isLogin) {
                setAuth(response.data.user, response.data.token, response.data.refreshToken);
                navigate('/');
            } else {
                setIsLogin(true);
                alert('Registro exitoso');
            }
        } catch (err: any) {
            setError(err.response?.data?.message || 'Error de conexión');
        }
    };

    return (
        <div className="max-w-md w-full bg-gray-800 rounded-xl shadow-2xl p-8 border border-gray-700">
            <div className="text-center mb-8">
                <h2 className="text-3xl font-bold text-blue-500">{isLogin ? 'Iniciar Sesión' : 'Registro'}</h2>
            </div>

            <form onSubmit={handleSubmit} className="space-y-4">
                <input
                    type="text"
                    placeholder="Usuario"
                    value={username}
                    onChange={(e) => setUsername(e.target.value)}
                    className="w-full p-3 bg-gray-700 rounded-lg text-white outline-none"
                    required
                />
                {!isLogin && (
                    <input
                        type="email"
                        placeholder="Email"
                        value={email}
                        onChange={(e) => setEmail(e.target.value)}
                        className="w-full p-3 bg-gray-700 rounded-lg text-white outline-none"
                        required
                    />
                )}
                <input
                    type="password"
                    placeholder="Contraseña"
                    value={password}
                    onChange={(e) => setPassword(e.target.value)}
                    className="w-full p-3 bg-gray-700 rounded-lg text-white outline-none"
                    required
                />
                {error && <p className="text-red-500 text-sm">{error}</p>}
                <button type="submit" className="w-full bg-blue-600 p-3 rounded-lg font-bold">
                    {isLogin ? 'Entrar' : 'Registrarse'}
                </button>
            </form>
            <button type="button" onClick={() => setIsLogin(!isLogin)} className="w-full mt-4 text-blue-400 text-sm">
                {isLogin ? '¿No tienes cuenta? Regístrate' : '¿Ya tienes cuenta? Login'}
            </button>
        </div>
    );
};

export default LoginPage;
