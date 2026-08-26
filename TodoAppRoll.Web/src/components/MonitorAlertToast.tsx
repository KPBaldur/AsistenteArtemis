import React, { useEffect } from 'react';
import { AlertTriangle, CheckCircle2, X } from 'lucide-react';
import { useNotificationStore } from '../store/notificationStore';

const AUTO_DISMISS_MS = 8000;

const MonitorAlertToast: React.FC = () => {
    const alerts = useNotificationStore((state) => state.alerts);
    const dismissAlert = useNotificationStore((state) => state.dismissAlert);

    useEffect(() => {
        const timers = alerts.map((alert) =>
            setTimeout(() => dismissAlert(alert.id), AUTO_DISMISS_MS)
        );
        return () => timers.forEach(clearTimeout);
    }, [alerts, dismissAlert]);

    if (alerts.length === 0) return null;

    return (
        <div className="fixed bottom-6 right-6 z-50 flex flex-col gap-3 w-80">
            {alerts.map((alert) => {
                const isDown = alert.kind === 'down';
                return (
                    <div
                        key={alert.id}
                        className={`bg-gray-800 border-l-4 ${isDown ? 'border-red-500' : 'border-green-500'} rounded-lg shadow-xl p-4 flex items-start gap-3`}
                    >
                        {isDown ? (
                            <AlertTriangle className="text-red-400 shrink-0 mt-0.5" size={20} />
                        ) : (
                            <CheckCircle2 className="text-green-400 shrink-0 mt-0.5" size={20} />
                        )}
                        <div className="flex-1 min-w-0">
                            <p className="text-white text-sm font-medium">{alert.message}</p>
                            <p className="text-gray-400 text-xs mt-0.5">
                                {isDown ? 'Servidor caído' : 'Servidor recuperado'}
                            </p>
                        </div>
                        <button
                            onClick={() => dismissAlert(alert.id)}
                            className="text-gray-500 hover:text-white shrink-0"
                            aria-label="Cerrar"
                        >
                            <X size={16} />
                        </button>
                    </div>
                );
            })}
        </div>
    );
};

export default MonitorAlertToast;
