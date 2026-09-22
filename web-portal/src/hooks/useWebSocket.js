import { useEffect, useRef } from 'react';
import { Client } from '@stomp/stompjs';

export default function useWebSocket(onUpdate) {
  const clientRef = useRef(null);
  const onUpdateRef = useRef(onUpdate);

  // Keep the ref up to date without triggering effect
  useEffect(() => {
    onUpdateRef.current = onUpdate;
  }, [onUpdate]);

  useEffect(() => {
    const brokerUrl = import.meta.env.VITE_WS_URL;
    if (!brokerUrl) {
      const timer = setInterval(() => onUpdateRef.current?.(), 30000);
      return () => clearInterval(timer);
    }

    const client = new Client({
      brokerURL: brokerUrl,
      reconnectDelay: 5000,
      heartbeatIncoming: 4000,
      heartbeatOutgoing: 4000,
      onConnect: () => {
        // Every connect/reconnect is a hint only; the callback reloads authoritative REST state.
        if (onUpdateRef.current) {
          onUpdateRef.current();
        }
        client.subscribe('/topic/updates', () => {
          if (onUpdateRef.current) {
            onUpdateRef.current();
          }
        });
      },
      onStompError: (frame) => {
        console.error('Broker reported error: ' + frame.headers['message']);
        console.error('Additional details: ' + frame.body);
      },
    });

    client.activate();
    clientRef.current = client;

    return () => {
      if (clientRef.current) {
        clientRef.current.deactivate();
      }
    };
  }, []); // Empty dependency array prevents reconnects!

  return clientRef.current;
}
