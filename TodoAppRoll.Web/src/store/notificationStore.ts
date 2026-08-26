import { create } from 'zustand';

export type MonitorAlertKind = 'down' | 'recovered';

export interface MonitorAlert {
    id: string;
    monitorName: string;
    kind: MonitorAlertKind;
    message: string;
    createdAt: number;
}

interface NotificationState {
    alerts: MonitorAlert[];
    addAlert: (alert: Omit<MonitorAlert, 'id' | 'createdAt'>) => void;
    dismissAlert: (id: string) => void;
}

export const useNotificationStore = create<NotificationState>((set) => ({
    alerts: [],
    addAlert: (alert) => {
        const id = crypto.randomUUID();
        set((state) => ({
            alerts: [...state.alerts, { ...alert, id, createdAt: Date.now() }],
        }));
    },
    dismissAlert: (id) => {
        set((state) => ({ alerts: state.alerts.filter((a) => a.id !== id) }));
    },
}));
