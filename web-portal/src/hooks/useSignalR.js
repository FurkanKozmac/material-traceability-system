import { useEffect, useRef } from 'react';
import { HubConnectionBuilder, LogLevel } from '@microsoft/signalr';
import { getStoredUser } from '../auth';

const getHubUrl = () => {
  const apiUrl = import.meta.env.VITE_API_URL || 'http://localhost:5059/api';
  return apiUrl.replace(/\/api\/?$/, '') + '/hubs/traceability';
};

export default function useSignalR(eventName, onEvent) {
  const handlerRef = useRef(onEvent);
  handlerRef.current = onEvent;

  useEffect(() => {
    const user = getStoredUser();
    const connection = new HubConnectionBuilder()
      .withUrl(getHubUrl(), {
        accessTokenFactory: () => user?.accessToken || user?.token || '',
      })
      .withAutomaticReconnect()
      .configureLogging(import.meta.env.DEV ? LogLevel.Warning : LogLevel.Error)
      .build();

    connection.on(eventName, (payload) => handlerRef.current?.(payload));
    connection.start().catch(() => {});

    return () => {
      connection.off(eventName);
      connection.stop().catch(() => {});
    };
  }, [eventName]);
}
