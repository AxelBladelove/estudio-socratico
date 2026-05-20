import type {
  BridgeAction,
  BridgeEvent,
  BridgeRequest,
  BridgeResponse,
  InstallerError,
} from './types';

type EventHandler = (event: BridgeEvent) => void;
type PendingRequest = {
  handleResponse: (response: BridgeResponse) => void;
  reject: (error: InstallerError) => void;
  timeoutId: ReturnType<typeof setTimeout>;
};

const pendingRequests = new Map<string, PendingRequest>();
const eventHandlers = new Set<EventHandler>();

let requestCounter = 0;
const BRIDGE_TIMEOUT_MS = 30_000;

export function normalizeInstallerError(
  error: unknown,
  overrides: Partial<InstallerError> = {}
): InstallerError {
  if (isInstallerError(error)) {
    return { ...error, ...overrides };
  }

  const message =
    error instanceof Error
      ? error.message
      : typeof error === 'string'
        ? error
        : 'El backend no devolvio un error legible.';

  return {
    code: 'UNKNOWN_ERROR',
    title: 'No se pudo completar la accion',
    description: message,
    probableCause: 'La solicitud al backend fallo o no respondio a tiempo.',
    recommendedAction: 'Revisa los logs del configurador y vuelve a intentar.',
    canRetry: true,
    canContinueSafely: false,
    ...overrides,
  };
}

export function getResponsePayload<T>(response: BridgeResponse): T | undefined {
  return (response.payload ?? response.data) as T | undefined;
}

/**
 * Send a request to the C# backend via WebView2 PostMessage.
 */
export function sendToHost(action: BridgeAction, payload: Record<string, unknown> = {}): Promise<BridgeResponse> {
  return new Promise((resolve, reject) => {
    const id = `req_${++requestCounter}_${Date.now()}`;
    const request: BridgeRequest = {
      id,
      type: action,
      payload,
    };

    const timeoutId = setTimeout(() => {
      const pending = pendingRequests.get(id);
      if (!pending) {
        return;
      }

      clearTimeout(pending.timeoutId);
      pendingRequests.delete(id);
      pending.reject(
        normalizeInstallerError(new Error(`Bridge request timed out: ${action}`), {
          code: 'BRIDGE_TIMEOUT',
          title: 'El configurador no respondio a tiempo',
          description: `La accion ${action} no devolvio respuesta dentro del tiempo esperado.`,
          probableCause: 'El backend fallo, quedo bloqueado o la UI no recibio la respuesta.',
        })
      );
    }, BRIDGE_TIMEOUT_MS);

    pendingRequests.set(id, {
      timeoutId,
      handleResponse: (response: BridgeResponse) => {
        clearTimeout(timeoutId);
        pendingRequests.delete(id);
        if (response.ok) {
          resolve(response);
          return;
        }

        reject(normalizeInstallerError(response.error));
      },
      reject: (error: InstallerError) => {
        clearTimeout(timeoutId);
        pendingRequests.delete(id);
        reject(error);
      },
    });

    // WebView2's window.chrome.webview.postMessage
    if (window.chrome?.webview?.postMessage) {
      window.chrome.webview.postMessage(request);
    } else {
      // Dev mode fallback — simulate a delayed response
      console.warn('[Bridge] No WebView2 host. Running in dev mode.');
      clearTimeout(timeoutId);
      pendingRequests.delete(id);
      resolve({
        id,
        ok: true,
        type: `${action}.result`,
        payload: { devMode: true },
      });
    }
  });
}

/**
 * Register an event handler for backend push events.
 */
export function onHostEvent(handler: EventHandler): () => void {
  eventHandlers.add(handler);
  return () => eventHandlers.delete(handler);
}

/**
 * Initialize the bridge by setting up global window handlers.
 * Called once at app startup.
 */
export function initBridge() {
  // C# calls: window.__bridgeResponse(jsonString)
  (window as any).__bridgeResponse = (jsonStr: string) => {
    try {
      const parsed = JSON.parse(jsonStr) as BridgeResponse;
      const response = normalizeBridgeResponse(parsed);
      const handler = pendingRequests.get(response.id);
      handler?.handleResponse(response);
    } catch (e) {
      console.error('[Bridge] Failed to parse response:', e);
    }
  };

  // C# calls: window.__bridgeEvent(jsonString)
  (window as any).__bridgeEvent = (jsonStr: string) => {
    try {
      const event: BridgeEvent = JSON.parse(jsonStr);
      eventHandlers.forEach((handler) => {
        try {
          handler(event);
        } catch (e) {
          console.error('[Bridge] Event handler error:', e);
        }
      });
    } catch (e) {
      console.error('[Bridge] Failed to parse event:', e);
    }
  };
}

function normalizeBridgeResponse(response: BridgeResponse): BridgeResponse {
  return {
    ...response,
    id: response.id ?? response.requestId ?? '',
    ok: response.ok ?? response.success ?? false,
    type: response.type ?? 'unknown.result',
    payload: response.payload ?? response.data,
  };
}

function isInstallerError(error: unknown): error is InstallerError {
  if (!error || typeof error !== 'object') {
    return false;
  }

  return 'code' in error && 'description' in error && 'recommendedAction' in error;
}

// Type augmentation for WebView2
declare global {
  interface Window {
    chrome?: {
      webview?: {
        postMessage: (message: unknown) => void;
      };
    };
    __bridgeResponse?: (jsonStr: string) => void;
    __bridgeEvent?: (jsonStr: string) => void;
  }
}
