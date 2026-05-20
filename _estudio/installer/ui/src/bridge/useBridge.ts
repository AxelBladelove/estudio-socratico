import { useEffect, useRef } from 'react';
import { onHostEvent } from './bridge';
import type { BridgeEvent, BridgeEventType } from './types';

/**
 * React hook that subscribes to bridge events and calls the handler.
 * Automatically cleans up on unmount.
 */
export function useBridgeEvent(
  handler: (event: BridgeEvent) => void,
  types?: BridgeEventType[]
) {
  const handlerRef = useRef(handler);
  handlerRef.current = handler;

  useEffect(() => {
    const unsubscribe = onHostEvent((event) => {
      if (!types || types.includes(event.type)) {
        handlerRef.current(event);
      }
    });
    return unsubscribe;
  }, [types]);
}
