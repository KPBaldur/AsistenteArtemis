import { useEffect, useRef } from 'react';
import { HubConnectionBuilder, LogLevel, type HubConnection } from '@microsoft/signalr';
import api from '../api/axios';
import { useAuthStore } from '../store/authStore';
import { useNotificationStore } from '../store/notificationStore';

interface MonitorDto {
    id: string;
    name: string;
    lastStatus: number; // MonitorStatus del lado .NET: 0 Unknown, 1 Up, 2 Down (viaja como número, no como string)
}

interface MonitorStatusChangedPayload {
    monitorId: string;
    name: string;
    status: 'Up' | 'Down' | 'Unknown';
}

const STATUS_BY_NUMBER = ['Unknown', 'Up', 'Down'] as const;

function hubBaseUrl(): string {
    const apiUrl = import.meta.env.VITE_API_URL || 'https://localhost:7214/api';
    return apiUrl.replace(/\/api\/?$/, '');
}

// El backend transmite MonitorStatusChanged en cada chequeo (no solo en
// cambios de estado) -- este hook guarda el último status conocido por
// monitor para detectar transiciones reales (recién caído / recién
// recuperado) en vez de repetir la misma alerta cada 5 minutos.
export function useMonitorAlerts() {
    const token = useAuthStore((state) => state.token);
    const addAlert = useNotificationStore((state) => state.addAlert);
    const lastStatusRef = useRef<Record<string, string>>({});

    useEffect(() => {
        if (!token) return;

        let connection: HubConnection | null = null;
        let cancelled = false;

        const start = async () => {
            try {
                const { data: monitors } = await api.get<MonitorDto[]>('/Monitor');
                if (cancelled) return;
                for (const monitor of monitors) {
                    lastStatusRef.current[monitor.id] = STATUS_BY_NUMBER[monitor.lastStatus] ?? 'Unknown';
                }
            } catch {
                // Si falla el seed inicial, seguimos igual -- peor caso es una
                // alerta de más en la primera transición que se vea.
            }

            connection = new HubConnectionBuilder()
                .withUrl(`${hubBaseUrl()}/hubs/monitors`, { accessTokenFactory: () => token })
                .withAutomaticReconnect()
                .configureLogging(LogLevel.Warning)
                .build();

            connection.on('MonitorStatusChanged', (payload: MonitorStatusChangedPayload) => {
                const previous = lastStatusRef.current[payload.monitorId];
                lastStatusRef.current[payload.monitorId] = payload.status;

                if (payload.status === 'Down' && previous !== 'Down') {
                    addAlert({
                        monitorName: payload.name,
                        kind: 'down',
                        message: `${payload.name} no está respondiendo`,
                    });
                } else if (payload.status === 'Up' && previous === 'Down') {
                    addAlert({
                        monitorName: payload.name,
                        kind: 'recovered',
                        message: `${payload.name} volvió a estar arriba`,
                    });
                }
            });

            try {
                await connection.start();
            } catch {
                // Sin conexión en vivo por ahora; el próximo intento de
                // reconexión automática (withAutomaticReconnect) lo resuelve.
            }
        };

        start();

        return () => {
            cancelled = true;
            connection?.stop();
        };
    }, [token, addAlert]);
}
