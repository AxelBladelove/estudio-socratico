import { useStore } from '../state/store';
import { getResponsePayload, normalizeInstallerError, sendToHost } from '../bridge/bridge';
import { ResourceCard } from '../components/ResourceCard';
import { ActionButton } from '../components/ActionButton';

export function Diagnosis() {
  const resources = useStore((s) => s.resources);
  const globalState = useStore((s) => s.globalState);
  const globalMessage = useStore((s) => s.globalMessage);
  const setScreen = useStore((s) => s.setScreen);
  const setPlan = useStore((s) => s.setPlan);
  const setLoading = useStore((s) => s.setLoading);
  const setError = useStore((s) => s.setError);
  const loading = useStore((s) => s.loading);

  const needsAction = resources.filter(
    (r) => r.status !== 'ready' && r.status !== 'skipped' && r.status !== 'optional'
  );
  const readyCount = resources.filter((r) => r.status === 'ready').length;

  const handleContinue = async () => {
    setError(null);
    if (globalState === 'readyToStudy' || globalState === 'partiallyReady') {
      setScreen('complete');
      return;
    }

    setLoading(true);
    try {
      const response = await sendToHost('createSetupPlan');
      const plan = getResponsePayload<import('../bridge/types').SetupPlan>(response);
      if (!plan) {
        throw new Error('El backend no devolvio un plan de configuracion.');
      }

      setPlan(plan);
      setScreen('plan');
    } catch (error) {
      setError(
        normalizeInstallerError(error, {
          title: 'No se pudo crear el plan',
          recommendedAction: 'Corrige el problema reportado o exporta el diagnostico antes de continuar.',
        })
      );
    } finally {
      setLoading(false);
    }
  };

  const stateIcon: Record<string, string> = {
    readyToStudy: '✓',
    partiallyReady: '!',
    needsSetup: '⚙',
    needsRepair: '🔧',
    needsAuthentication: '🔑',
    needsUserAction: '✋',
    failed: '✗',
    analyzing: '⟳',
  };

  const stateColor: Record<string, string> = {
    readyToStudy: 'text-success',
    partiallyReady: 'text-warning',
    needsSetup: 'text-accent',
    needsRepair: 'text-warning',
    needsAuthentication: 'text-accent',
    needsUserAction: 'text-accent',
    failed: 'text-error',
    analyzing: 'text-text-muted',
  };

  return (
    <div className="animate-fade-in">
      {/* Header */}
      <div className="mb-8">
        <div className="flex items-center gap-3 mb-2">
          <span className={`text-2xl ${stateColor[globalState] ?? 'text-text-muted'}`}>
            {stateIcon[globalState] ?? '○'}
          </span>
          <h1 className="text-2xl font-bold text-text-primary">{globalMessage}</h1>
        </div>
        {needsAction.length > 0 && (
          <p className="text-text-secondary">
            {needsAction.length} componente{needsAction.length === 1 ? '' : 's'} necesita{needsAction.length === 1 ? '' : 'n'} atención.{' '}
            {readyCount} listo{readyCount === 1 ? '' : 's'}.
          </p>
        )}
      </div>

      {/* Resource list */}
      <div className="space-y-2 stagger-children mb-8">
        {resources.map((resource) => (
          <ResourceCard key={resource.id} resource={resource} />
        ))}
      </div>

      {/* Actions */}
      <div className="flex items-center gap-4">
        <ActionButton
          onClick={handleContinue}
          loading={loading}
          variant={globalState === 'readyToStudy' ? 'primary' : 'primary'}
        >
          {globalState === 'readyToStudy'
            ? 'Todo listo — Continuar'
            : globalState === 'partiallyReady'
              ? 'Continuar con lo disponible'
              : 'Crear plan de configuración'}
        </ActionButton>

        <ActionButton
          variant="ghost"
          onClick={() => setScreen('welcome')}
        >
          Volver
        </ActionButton>
      </div>
    </div>
  );
}
