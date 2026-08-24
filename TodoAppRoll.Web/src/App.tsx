
import { BrowserRouter as Router, Routes, Route, Navigate } from 'react-router-dom';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { useAuthStore } from './store/authStore';
import LoginPage from './pages/LoginPage';
import Dashboard from './pages/Dashboard';

const queryClient = new QueryClient();

export default function App() {
  const { isAuthenticated }: any = useAuthStore();
  
  return (
    <QueryClientProvider client={queryClient}>
      <Router>
        <div className="min-h-screen bg-gray-900 flex items-center justify-center">
          <Routes>
            <Route path="/login" element={!isAuthenticated ? <LoginPage /> : <Navigate to="/" />} />
            <Route path="/" element={isAuthenticated ? <Dashboard /> : <Navigate to="/login" />} />
          </Routes>
        </div>
      </Router>
    </QueryClientProvider>
  );
}
