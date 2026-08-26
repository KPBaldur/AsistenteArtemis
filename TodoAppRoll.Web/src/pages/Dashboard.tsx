import React, { useState } from 'react';
import { useAuthStore } from '../store/authStore';
import { useTasks } from '../hooks/useTasks';
import { useMonitorAlerts } from '../hooks/useMonitorAlerts';
import { TaskStatus, TaskPriority } from '../types';
import TaskItem from '../components/TaskItem';
import MonitorAlertToast from '../components/MonitorAlertToast';
import { LogOut, CheckCircle2, Clock, Plus, LayoutDashboard, Loader2 } from 'lucide-react';

const Dashboard: React.FC = () => {
    const { user, logout } = useAuthStore();
    const { tasks, isLoading, createTask, updateTask, deleteTask, toggleSubtask } = useTasks();
    useMonitorAlerts();
    const [isAdding, setIsAdding] = useState(false);
    const [newTitle, setNewTitle] = useState('');

    const handleCreateTask = (e: React.FormEvent) => {
        e.preventDefault();
        if (!newTitle.trim()) return;
        createTask({
            title: newTitle,
            description: '',
            priority: TaskPriority.Medium,
            order: tasks.length
        });
        setNewTitle('');
        setIsAdding(false);
    };

    const handleToggleTask = (id: string, currentStatus: TaskStatus) => {
        updateTask({
            id,
            task: { status: currentStatus === TaskStatus.Completed ? TaskStatus.Pending : TaskStatus.Completed }
        });
    };

    return (
        <div className="min-h-screen bg-gray-900 text-white flex">
            {/* Sidebar */}
            <aside className="w-64 bg-gray-800 border-r border-gray-700 flex flex-col">
                <div className="p-6">
                    <h2 className="text-2xl font-bold text-blue-500">TodoAppRoll</h2>
                </div>
                
                <nav className="flex-1 px-4 space-y-2">
                    <button className="w-full flex items-center gap-3 px-4 py-3 bg-blue-600/10 text-blue-400 rounded-lg font-medium border border-blue-500/20 text-left">
                        <LayoutDashboard size={20} />
                        Dashboard
                    </button>
                    <button className="w-full flex items-center gap-3 px-4 py-3 text-gray-400 hover:bg-gray-700/50 hover:text-white rounded-lg transition-all text-left">
                        <CheckCircle2 size={20} />
                        Tareas
                    </button>
                    <button className="w-full flex items-center gap-3 px-4 py-3 text-gray-400 hover:bg-gray-700/50 hover:text-white rounded-lg transition-all text-left">
                        <Clock size={20} />
                        Alarmas
                    </button>
                </nav>

                <div className="p-4 border-t border-gray-700">
                    <button 
                        onClick={logout}
                        className="w-full flex items-center gap-3 px-4 py-3 text-red-400 hover:bg-red-500/10 rounded-lg transition-all"
                    >
                        <LogOut size={20} />
                        Cerrar Sesión
                    </button>
                </div>
            </aside>

            {/* Main Content */}
            <main className="flex-1 overflow-y-auto">
                <header className="h-20 bg-gray-800/50 backdrop-blur-md border-b border-gray-700 px-8 flex items-center justify-between sticky top-0 z-10">
                    <h1 className="text-xl font-semibold">Dashboard Principal</h1>
                    <div className="flex items-center gap-4">
                        <span className="text-gray-400">Hola, <span className="text-white font-medium">{user?.username}</span></span>
                        <div className="w-10 h-10 bg-blue-600 rounded-full flex items-center justify-center font-bold">
                            {user?.username?.charAt(0).toUpperCase() || 'U'}
                        </div>
                    </div>
                </header>

                <div className="p-8">
                    <div className="grid grid-cols-1 md:grid-cols-3 gap-6 mb-8">
                        <div className="bg-gray-800 p-6 rounded-xl border border-gray-700 shadow-lg">
                            <p className="text-gray-400 text-sm font-medium mb-1">Tareas Pendientes</p>
                            <p className="text-3xl font-bold">{tasks.filter((t: any) => t.status !== TaskStatus.Completed).length}</p>
                        </div>
                        <div className="bg-gray-800 p-6 rounded-xl border border-gray-700 shadow-lg">
                            <p className="text-gray-400 text-sm font-medium mb-1">Próxima Alarma</p>
                            <p className="text-3xl font-bold text-blue-500">14:30</p>
                        </div>
                        <div className="bg-gray-800 p-6 rounded-xl border border-gray-700 shadow-lg">
                            <p className="text-gray-400 text-sm font-medium mb-1">Estado de Artemis</p>
                            <p className="text-3xl font-bold text-green-500">Feliz ✨</p>
                        </div>
                    </div>

                    <div className="flex items-center justify-between mb-6">
                        <h2 className="text-2xl font-bold">Tus Tareas</h2>
                        <button 
                            onClick={() => setIsAdding(true)}
                            className="bg-blue-600 hover:bg-blue-700 text-white px-4 py-2 rounded-lg flex items-center gap-2 transition-all"
                        >
                            <Plus size={20} />
                            Nueva Tarea
                        </button>
                    </div>

                    {isAdding && (
                        <form onSubmit={handleCreateTask} className="mb-6 bg-gray-800 p-4 rounded-xl border border-blue-500/30 shadow-lg">
                            <input
                                autoFocus
                                type="text"
                                value={newTitle}
                                onChange={(e) => setNewTitle(e.target.value)}
                                placeholder="¿Qué necesitas hacer?"
                                className="w-full bg-transparent border-none outline-none text-lg mb-4 text-white"
                            />
                            <div className="flex justify-end gap-3">
                                <button 
                                    type="button"
                                    onClick={() => setIsAdding(false)}
                                    className="px-4 py-2 text-gray-400 hover:text-white"
                                >
                                    Cancelar
                                </button>
                                <button 
                                    type="submit"
                                    className="bg-blue-600 px-4 py-2 rounded-lg font-bold"
                                >
                                    Guardar Tarea
                                </button>
                            </div>
                        </form>
                    )}

                    {isLoading ? (
                        <div className="flex flex-col items-center justify-center py-20">
                            <Loader2 className="animate-spin text-blue-500 mb-4" size={40} />
                            <p className="text-gray-400">Cargando tus tareas...</p>
                        </div>
                    ) : tasks.length > 0 ? (
                        <div className="space-y-4">
                            {tasks.map((task: any) => (
                                <TaskItem 
                                    key={task.id} 
                                    task={task} 
                                    onToggle={handleToggleTask}
                                    onDelete={deleteTask}
                                    onToggleSubtask={toggleSubtask}
                                />
                            ))}
                        </div>
                    ) : (
                        <div className="bg-gray-800 rounded-xl border border-gray-700 shadow-xl overflow-hidden">
                            <div className="p-12 text-center">
                                <div className="w-24 h-24 bg-gray-700/50 rounded-full flex items-center justify-center mx-auto mb-4">
                                    <LayoutDashboard size={40} className="text-gray-500" />
                                </div>
                                <h3 className="text-xl font-semibold mb-2">No hay tareas para mostrar</h3>
                                <p className="text-gray-400 max-w-sm mx-auto">Comienza creando una nueva tarea para que Artemis pueda ayudarte a ser más productivo.</p>
                            </div>
                        </div>
                    )}
                </div>
            </main>
            <MonitorAlertToast />
        </div>
    );
};

export default Dashboard;
