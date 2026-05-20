import { useStore } from '../state/store';
import { normalizeInstallerError, sendToHost } from '../bridge/bridge';
import { ActionButton } from '../components/ActionButton';

export function Complete() {
  const globalState = useStore((s) => s.globalState);
  const globalMessage = useStore((s) => s.globalMessage);
  const resources = useStore((s) => s.resources);
  const setScreen = useStore((s) => s.setScreen);
  const workspacePath = useStore((s) => s.workspacePath);
  const setError = useStore((s) => s.setError);

  const isSuccess = globalState === 'readyToStudy';
  const isPartial = globalState === 'partiallyReady';
  const isFailed = globalState === 'failed' || globalState === 'needsSetup' || globalState === 'needsRepair';

  const failedResources = resources.filter(
    (r) => r.status === 'failed' || r.status === 'broken' || r.status === 'missing'
  );

  const handleOpenVSCode = async () => {
    setError(null);
    try {
      await sendToHost('openVSCode', { workspace: workspacePath });
    } catch (error) {
      setError(
        normalizeInstallerError(error, {
          title: 'No se pudo abrir VS Code',
        })
      );
    }
  };

  const handleExport = async () => {
    setError(null);
    try {
      await sendToHost('exportDiagnostics');
    } catch (error) {
      setError(
        normalizeInstallerError(error, {
          title: 'No se pudo exportar el diagnostico',
        })
      );
    }
  };

  const handleRetry = () => {
    setScreen('diagnosis');
  };

  return (
    <div className="flex flex-col items-center justify-center min-h-[70vh] text-center animate-slide-up">
      {/* Icon */}
      <div className={`w-20 h-20 rounded-full flex items-center justify-center mb-8 ${
        isSuccess ? 'bg-success/20' :
        isPartial ? 'bg-warning/20' :
        'bg-error/20'
      }`}>
        <span className={`text-4xl ${
          isSuccess ? 'text-success' :
          isPartial ? 'text-warning' :
          'text-error'
        }`}>
          {isSuccess ? '✓' : isPartial ? '!' : '✗'}
        </span>
      </div>

      {/* Title */}
      <h1 className="text-2xl font-bold text-text-primary mb-3">
        {isSuccess
          ? 'Todo está listo para estudiar C'
          : isPartial
            ? 'Configuración parcial'
            : 'Configuración incompleta'}
      </h1>

      {/* Message */}
      <p className="text-text-secondary max-w-md mb-8 leading-relaxed">
        {isSuccess
          ? 'Tu entorno fue configurado y validado. Abre VS Code y empieza con F9.'
          : isPartial
            ? 'Puedes empezar a estudiar, pero algunos componentes opcionales están pendientes.'
            : globalMessage}
      </p>

      {/* Failed resources */}
      {isFailed && failedResources.length > 0 && (
        <div className="w-full max-w-md mb-8 text-left">
          <h2 className="text-sm font-semibold text-text-muted uppercase tracking-wider mb-3">
            Falta completar
          </h2>
          <div className="space-y-2">
            {failedResources.map((r) => (
              <div
                key={r.id}
                className="flex items-start gap-3 p-3 rounded-lg bg-surface border border-error/20"
              >
                <span className="text-error">✗</span>
                <div>
                  <p className="font-medium text-text-primary">{r.displayName}</p>
                  <p className="text-sm text-text-secondary mt-0.5">
                    {r.humanDescription ?? r.humanStatus}
                  </p>
                </div>
              </div>
            ))}
          </div>
        </div>
      )}

      {/* Actions */}
      <div className="flex flex-col items-center gap-4">
        {(isSuccess || isPartial) && (
          <ActionButton size="lg" onClick={handleOpenVSCode}>
            Abrir VS Code
          </ActionButton>
        )}

        {isFailed && (
          <ActionButton size="lg" onClick={handleRetry}>
            Reintentar
          </ActionButton>
        )}

        <div className="flex items-center gap-6 text-sm">
          {(isSuccess || isPartial) && (
            <button
              onClick={handleExport}
              className="text-text-muted hover:text-text-secondary transition-colors"
            >
              Exportar diagnóstico
            </button>
          )}
          {isFailed && (
            <>
              <button
                onClick={handleExport}
                className="text-text-muted hover:text-text-secondary transition-colors"
              >
                Exportar diagnóstico
              </button>
              <span className="text-border">·</span>
              <button
                onClick={handleOpenVSCode}
                className="text-text-muted hover:text-text-secondary transition-colors"
              >
                Abrir VS Code parcial
              </button>
            </>
          )}
        </div>
      </div>
    </div>
  );
}
