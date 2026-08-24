export const TaskPriority = {
    Low: 0,
    Medium: 1,
    High: 2
} as const;
export type TaskPriority = typeof TaskPriority[keyof typeof TaskPriority];

export const TaskStatus = {
    Pending: 0,
    InProgress: 1,
    Completed: 2,
    Archived: 3
} as const;
export type TaskStatus = typeof TaskStatus[keyof typeof TaskStatus];

export const AlarmType = {
    Pomodoro: 0,
    DateTime: 1
} as const;
export type AlarmType = typeof AlarmType[keyof typeof AlarmType];

export interface TaskCreateDto {
    title: string;
    description: string;
    priority: TaskPriority;
    order: number;
}

export interface TaskUpdateDto {
    title?: string;
    description?: string;
    status?: TaskStatus;
    priority?: TaskPriority;
    order?: number;
    alarmId?: string;
}

export interface User {
    id: string;
    username: string;
    email: string;
    theme: string;
    notificationsEnabled: boolean;
    createdAt: string;
}

export interface Task {
    id: string;
    title: string;
    description: string;
    status: TaskStatus;
    priority: TaskPriority;
    order: number;
    completedAt?: string;
    createdAt: string;
    alarmId?: string;
    subtasks: Subtask[];
}

export interface Subtask {
    id: string;
    title: string;
    isDone: boolean;
    order: number;
}

export interface Alarm {
    id: string;
    name: string;
    type: AlarmType;
    durationMinutes?: number;
    scheduleAt?: string;
    isActive: boolean;
    audioId?: string;
    imageId?: string;
}

export interface Media {
    id: string;
    name: string;
    url: string;
    fileType: string;
    createdAt: string;
}

export interface AuthResponse {
    token: string;
    user: User;
}
