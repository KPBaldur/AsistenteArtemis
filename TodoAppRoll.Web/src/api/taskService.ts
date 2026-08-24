import api from './axios';
import type { Task, TaskCreateDto, TaskUpdateDto } from '../types';

export const taskService = {
    getTasks: async () => {
        const response = await api.get<Task[]>('/Task');
        return response.data;
    },
    createTask: async (task: TaskCreateDto) => {
        const response = await api.post<Task>('/Task', task);
        return response.data;
    },
    updateTask: async (id: string, task: TaskUpdateDto) => {
        await api.put(`/Task/${id}`, task);
    },
    deleteTask: async (id: string) => {
        await api.delete(`/Task/${id}`);
    },
    createSubtask: async (taskId: string, title: string, order: number) => {
        const response = await api.post(`/Task/${taskId}/subtask`, { title, order });
        return response.data;
    },
    toggleSubtask: async (subtaskId: string) => {
        const response = await api.patch(`/Task/subtask/${subtaskId}/toggle`);
        return response.data;
    }
};
