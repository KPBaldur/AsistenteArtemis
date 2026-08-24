import React from 'react';
import { TaskPriority, TaskStatus } from '../types';
import { CheckCircle2, Circle, Trash2, ChevronDown, ChevronUp } from 'lucide-react';

interface TaskItemProps {
    task: any;
    onToggle: (id: string, currentStatus: TaskStatus) => void;
    onDelete: (id: string) => void;
    onToggleSubtask: (subtaskId: string) => void;
}

const TaskItem: React.FC<TaskItemProps> = ({ task, onToggle, onDelete, onToggleSubtask }) => {
    const [isExpanded, setIsExpanded] = React.useState(false);

    const priorityColors: Record<number, string> = {
        [TaskPriority.Low]: 'text-blue-400',
        [TaskPriority.Medium]: 'text-yellow-400',
        [TaskPriority.High]: 'text-red-400',
    };

    const priorityName = Object.keys(TaskPriority).find(key => (TaskPriority as any)[key] === task.priority) || 'Medium';

    return (
        <div className="bg-gray-800 border border-gray-700 rounded-xl overflow-hidden mb-4 transition-all hover:border-blue-500/50">
            <div className="p-4 flex items-center gap-4">
                <button 
                    onClick={() => onToggle(task.id, task.status)}
                    className="text-gray-500 hover:text-blue-500 transition-colors"
                >
                    {task.status === TaskStatus.Completed ? (
                        <CheckCircle2 className="text-green-500" size={24} />
                    ) : (
                        <Circle size={24} />
                    )}
                </button>

                <div className="flex-1">
                    <div className="flex items-center gap-2">
                        <h3 className={`font-semibold ${task.status === TaskStatus.Completed ? 'line-through text-gray-500' : 'text-white'}`}>
                            {task.title}
                        </h3>
                        <span className={`text-xs font-bold uppercase ${priorityColors[task.priority as number] || 'text-gray-400'}`}>
                            {priorityName}
                        </span>
                    </div>
                    {task.description && (
                        <p className="text-gray-400 text-sm mt-1">{task.description}</p>
                    )}
                </div>

                <div className="flex items-center gap-2">
                    {task.subtasks && task.subtasks.length > 0 && (
                        <button 
                            onClick={() => setIsExpanded(!isExpanded)}
                            className="p-2 text-gray-400 hover:text-white transition-colors"
                        >
                            {isExpanded ? <ChevronUp size={20} /> : <ChevronDown size={20} />}
                        </button>
                    )}
                    <button 
                        onClick={() => onDelete(task.id)}
                        className="p-2 text-gray-400 hover:text-red-500 transition-colors"
                    >
                        <Trash2 size={20} />
                    </button>
                </div>
            </div>

            {isExpanded && task.subtasks && task.subtasks.length > 0 && (
                <div className="bg-gray-900/50 p-4 border-t border-gray-700 space-y-3">
                    {task.subtasks.map((sub: any) => (
                        <div key={sub.id} className="flex items-center gap-3 pl-8">
                            <button 
                                onClick={() => onToggleSubtask(sub.id)}
                                className="text-gray-500 hover:text-blue-500 transition-colors"
                            >
                                {sub.isDone ? (
                                    <CheckCircle2 className="text-green-500" size={18} />
                                ) : (
                                    <Circle size={18} />
                                )}
                            </button>
                            <span className={`text-sm ${sub.isDone ? 'line-through text-gray-500' : 'text-gray-300'}`}>
                                {sub.title}
                            </span>
                        </div>
                    ))}
                </div>
            )}
        </div>
    );
};

export default TaskItem;
