import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { taskService } from '../api/taskService';
import type { TaskUpdateDto } from '../types';

export const useTasks = () => {
    const queryClient = useQueryClient();

    const tasksQuery = useQuery({
        queryKey: ['tasks'],
        queryFn: taskService.getTasks
    });

    const createTaskMutation = useMutation({
        mutationFn: taskService.createTask,
        onSuccess: () => queryClient.invalidateQueries({ queryKey: ['tasks'] })
    });

    const updateTaskMutation = useMutation({
        mutationFn: ({ id, task }: { id: string, task: TaskUpdateDto }) => taskService.updateTask(id, task),
        onSuccess: () => queryClient.invalidateQueries({ queryKey: ['tasks'] })
    });

    const deleteTaskMutation = useMutation({
        mutationFn: taskService.deleteTask,
        onSuccess: () => queryClient.invalidateQueries({ queryKey: ['tasks'] })
    });

    const toggleSubtaskMutation = useMutation({
        mutationFn: taskService.toggleSubtask,
        onSuccess: () => queryClient.invalidateQueries({ queryKey: ['tasks'] })
    });

    return {
        tasks: tasksQuery.data || [],
        isLoading: tasksQuery.isLoading,
        createTask: createTaskMutation.mutate,
        updateTask: updateTaskMutation.mutate,
        deleteTask: deleteTaskMutation.mutate,
        toggleSubtask: toggleSubtaskMutation.mutate
    };
};
