import { useStore } from '../state/store';
import { getResponsePayload, normalizeInstallerError, sendToHost } from '../bridge/bridge';
import { ActionButton } from '../components/ActionButton';

export function Welcome() {
  const setScreen = useStore((s) => s.setScreen);
  const setLoading = useStore((s) => s.setLoading);
  const setResources = useStore((s) => s.setResources);
  const setGlobalState = useStore((s) => s.setGlobalState);
  const setAuth = useStore((s) => s.setAuth);
  const setWorkspace = useStore((s) => s.setWorkspace);
  const setError = useStore((s) => s.setError);
  const loading = useStore((s) => s.loading);

  const handleStart = async () => {
    setError(null);
    setLoading(true);
    try {
      const response = await sendToHost('diagnoseEnvironment');
      const snapshot = getResponsePayload<import('../bridge/types').UIStateSnapshot>(response);
      if (!snapshot) {
        throw new Error('El backend no devolvio el diagnostico inicial.');
      }

      if (snapshot) {
        setGlobalState(snapshot.globalState, snapshot.globalMessage);
        setResources(snapshot.resources);
        setAuth(snapshot.gitHub ?? null, snapshot.exercism ?? null);
        setWorkspace(snapshot.workspacePath ?? null, snapshot.workspaceValid);
      }
      setScreen('diagnosis');
    } catch (error) {
      setError(
        normalizeInstallerError(error, {
          title: 'No se pudo completar el diagnostico',
          recommendedAction: 'Abre los logs del configurador y vuelve a intentar Empezar configuracion.',
        })
      );
    } finally {
      setLoading(false);
    }
  };

  const handleAction = async (action: 'openLogs' | 'exportDiagnostics') => {
    setError(null);
    try {
      await sendToHost(action);
    } catch (error) {
      setError(
        normalizeInstallerError(error, {
          title: action === 'openLogs' ? 'No se pudieron abrir los logs' : 'No se pudo exportar el diagnostico',
        })
      );
    }
  };

  return (
    <div className="flex flex-col items-center justify-center min-h-[70vh] text-center animate-fade-in">
      {/* Hero icon */}
      <div className="w-20 h-20 rounded-2xl bg-gradient-to-br from-accent to-accent/60 flex items-center justify-center mb-8 shadow-2xl shadow-accent/30 animate-pulse-glow">
        <span className="text-4xl">🎓</span>
      </div>

      {/* Title */}
      <h1 className="text-3xl font-bold text-text-primary mb-3 tracking-tight">
        Configuremos tu entorno
      </h1>
      <h2 className="text-3xl font-bold bg-gradient-to-r from-accent to-success bg-clip-text text-transparent mb-6">
        de Estudio Socrático
      </h2>

      {/* Description */}
      <p className="text-text-secondary max-w-md mb-10 leading-relaxed">
        Instalaremos las herramientas necesarias, conectaremos GitHub y Exercism,
        y dejaremos VS Code listo para estudiar C.
      </p>

      {/* Primary CTA */}
      <ActionButton size="lg" onClick={handleStart} loading={loading}>
        Empezar configuración
      </ActionButton>

      {/* Secondary actions */}
      <div className="flex items-center gap-6 mt-8 text-sm">
        <button
          onClick={() => handleAction('openLogs')}
          className="text-text-muted hover:text-text-secondary transition-colors"
        >
          Ver logs
        </button>
        <span className="text-border">·</span>
        <button
          onClick={() => handleAction('exportDiagnostics')}
          className="text-text-muted hover:text-text-secondary transition-colors"
        >
          Exportar diagnóstico
        </button>
      </div>
    </div>
  );
}
