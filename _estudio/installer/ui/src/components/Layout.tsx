import type { ReactNode } from 'react';
import { useStore } from '../state/store';

interface LayoutProps {
  children: ReactNode;
}

export function Layout({ children }: LayoutProps) {
  const version = useStore((s) => s.version);
  const lastError = useStore((s) => s.lastError);
  const setError = useStore((s) => s.setError);

  return (
    <div className="h-full flex flex-col bg-bg">
      {/* Content */}
      <main className="flex-1 overflow-y-auto">
        <div className="max-w-3xl mx-auto px-8 py-10">
          {lastError && (
            <div className="mb-6 rounded-xl border border-error/30 bg-error-soft px-4 py-3 text-left">
              <div className="flex items-start justify-between gap-4">
                <div>
                  <p className="font-semibold text-error">{lastError.title}</p>
                  <p className="mt-1 text-sm text-text-primary">{lastError.description}</p>
                  <p className="mt-2 text-xs text-text-secondary">Sugerencia: {lastError.recommendedAction}</p>
                </div>
                <button
                  type="button"
                  onClick={() => setError(null)}
                  className="text-xs text-text-muted hover:text-text-primary transition-colors"
                >
                  Cerrar
                </button>
              </div>
            </div>
          )}
          {children}
        </div>
      </main>

      {/* Footer */}
      <footer className="flex items-center justify-between px-8 py-3 border-t border-border/50 text-xs text-text-muted">
        <span>Estudio Socrático Configurador</span>
        <span>v{version}</span>
      </footer>
    </div>
  );
}
