import React, { useEffect, useRef } from 'react';

function Streamer({ sessionToken, onCard, onStatusChange }) {
  const eventSourceRef = useRef(null);

  useEffect(() => {
    if (!sessionToken) return undefined;

    onStatusChange('connecting');
    const source = new EventSource(`/api/stream?session=${encodeURIComponent(sessionToken)}`);
    eventSourceRef.current = source;

    source.onmessage = (event) => {
      try {
        const data = JSON.parse(event.data);
        onCard(data);
        onStatusChange('idle');
      } catch (err) {
        console.error('Failed to parse stream event', err);
      }
    };

    source.onerror = () => {
      onStatusChange('reconnecting');
    };

    return () => {
      source.close();
    };
  }, [sessionToken, onCard, onStatusChange]);

  return null;
}

export default Streamer;
