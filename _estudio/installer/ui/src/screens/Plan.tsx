import { useStore } from '../state/store';
import { normalizeInstallerError, sendToHost } from '../bridge/bridge';
import { ActionButton } from '../components/ActionButton';

export function Plan() {
  const plan = useStore((s) => s.plan);
  const setScreen = useStore((s) => s.setScreen);
  const setLoading = useStore((s) => s.setLoading);
  const setGlobalState = useStore((s) => s.setGlobalState);
  const setError = useStore((s) => s.setError);
  const loading = useStore((s) => s.loading);
  const resetProgress = useStore((s) => s.resetProgress);

  const criticalActions = plan?.actions.filter((a) => a.severity === 'critical') ?? [];
  const optionalActions = plan?.actions.filter((a) => a.severity !== 'critical') ?? [];

  const handleApply = async () => {
    setError(null);
    setLoading(true);
    resetProgress();
    setScreen('progress');
    try {
      await sendToHost('applyPlan');
    } catch (error) {
      const installerError = normalizeInstallerError(error, {
        title: 'No se pudo aplicar el plan',
        recommendedAction: 'Revisa el error mostrado, exporta el diagnostico y vuelve a intentar.',
      });
      setError(installerError);
      setGlobalState('failed', installerError.description);
      setScreen('complete');
    } finally {
      setLoading(false);
    }
  };

  const severityIcon: Record<string, string> = {
    critical: '🔴',
    recommended: '🟡',
    optional: '⚪',
  };

  return (
    <div className="animate-fade-in">
      {/* Header */}
      <div className="mb-8">
        <h1 className="text-2xl font-bold text-text-primary mb-2">
          Tu plan de configuración está listo
        </h1>
        <p className="text-text-secondary">
          {plan?.summary ?? 'Revisando acciones necesarias...'}
        </p>
      </div>

      {/* Critical actions */}
      {criticalActions.length > 0 && (
        <div className="mb-6">
          <h2 className="text-sm font-semibold text-text-muted uppercase tracking-wider mb-3">
            Acciones necesarias
          </h2>
          <div className="space-y-2 stagger-children">
            {criticalActions.map((action) => (
              <div
                key={action.id}
                className="flex items-start gap-3 p-3 rounded-lg bg-surface border border-border/50"
              >
                <span className="text-sm mt-0.5">
                  {severityIcon[action.severity]}
                </span>
                <div className="flex-1 min-w-0">
                  <p className="font-medium text-text-primary">{action.title}</p>
                  <p className="text-sm text-text-secondary mt-0.5">{action.description}</p>
                  <div className="flex items-center gap-3 mt-2 text-xs text-text-muted">
                    {action.requiresAdmin && (
                      <span className="flex items-center gap-1">
                        <span>🛡</span> Requiere permisos
                      </span>
                    )}
                    {action.requiresUser && (
                      <span className="flex items-center gap-1">
                        <span>👤</span> Requiere tu acción
                      </span>
                    )}
                  </div>
                </div>
              </div>
            ))}
          </div>
        </div>
      )}

      {/* Optional actions */}
      {optionalActions.length > 0 && (
        <div className="mb-8">
          <h2 className="text-sm font-semibold text-text-muted uppercase tracking-wider mb-3">
            Acciones recomendadas
          </h2>
          <div className="space-y-2">
            {optionalActions.map((action) => (
              <div
                key={action.id}
                className="flex items-start gap-3 p-3 rounded-lg bg-surface/50 border border-border/30"
              >
                <span className="text-sm mt-0.5">
                  {severityIcon[action.severity]}
                </span>
                <div>
                  <p className="text-text-secondary">{action.title}</p>
                  <p className="text-sm text-text-muted mt-0.5">{action.description}</p>
                </div>
              </div>
            ))}
          </div>
        </div>
      )}

      {/* Actions */}
      <div className="flex items-center gap-4">
        <ActionButton size="lg" onClick={handleApply} loading={loading}>
          Aplicar configuración
        </ActionButton>
        <ActionButton variant="ghost" onClick={() => setScreen('diagnosis')}>
          Volver al diagnóstico
        </ActionButton>
      </div>
    </div>
  );
}
