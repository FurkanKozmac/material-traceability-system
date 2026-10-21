import { useEffect, useRef } from 'react';
import { HubConnectionBuilder, LogLevel } from '@microsoft/signalr';
import { refreshAccessToken } from '../api';
import { clearSession, getStoredUser } from '../auth';
import { startHubWithSingleRefresh } from '../domain/signalrAuth';

const getHubUrl = () => {
  const apiUrl = import.meta.env.VITE_API_URL || 'http://localhost:5059/api';
  return apiUrl.replace(/\/api\/?$/, '') + '/hubs/traceability';
};

export default function useSignalR(eventName, onEvent) {
  const handlerRef = useRef(onEvent);
  handlerRef.current = onEvent;

  useEffect(() => {
    const connection = new HubConnectionBuilder()
      .withUrl(getHubUrl(), {
        accessTokenFactory: () => {
          const currentUser = getStoredUser();
          return currentUser?.accessToken || currentUser?.token || '';
        },
      })
      .withAutomaticReconnect()
      .configureLogging(import.meta.env.DEV ? LogLevel.Warning : LogLevel.Error)
      .build();

    connection.on(eventName, (payload) => handlerRef.current?.(payload));
    void startHubWithSingleRefresh({
      start: () => connection.start(),
      refresh: refreshAccessToken,
      clearSession,
      redirectToLogin: () => {
        if (!window.location.pathname.includes('/login')) {
          window.location.assign('/login');
        }
      },
    });

    return () => {
      connection.off(eventName);
      connection.stop().catch(() => {});
    };
  }, [eventName]);
}
