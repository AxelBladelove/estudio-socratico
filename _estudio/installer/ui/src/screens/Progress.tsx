import { useStore } from '../state/store';
import { useBridgeEvent } from '../bridge/useBridge';
import { normalizeInstallerError } from '../bridge/bridge';
import { StepIndicator } from '../components/StepIndicator';
import { ActionButton } from '../components/ActionButton';
import type { ProgressEvent } from '../bridge/types';

interface SetupSummary {
  succeeded: boolean;
  globalState: string;
  globalMessage: string;
}

export function Progress() {
  const currentStep = useStore((s) => s.currentStep);
  const completedSteps = useStore((s) => s.completedSteps);
  const plan = useStore((s) => s.plan);
  const setScreen = useStore((s) => s.setScreen);
  const setProgress = useStore((s) => s.setProgress);
  const addCompletedStep = useStore((s) => s.addCompletedStep);
  const setGlobalState = useStore((s) => s.setGlobalState);
  const setError = useStore((s) => s.setError);

  const resolvePlanActionId = (progress: ProgressEvent | null | undefined) => {
    if (!progress) {
      return null;
    }

    if (progress.dependency) {
      const dependencyMatch = plan?.actions.find((action) => action.dependencyId === progress.dependency);
      if (dependencyMatch) {
        return dependencyMatch.id;
      }
    }

    const aliases: Record<string, string> = {
      github: 'auth-github',
      exercism: 'auth-exercism',
      workspace: 'setup-workspace',
      vscode: 'setup-vscode',
      compat: 'validate-environment',
      complete: 'validate-environment',
    };

    return aliases[progress.stepId] ?? progress.stepId;
  };

  // Listen for step events from backend
  useBridgeEvent((event) => {
    switch (event.type) {
      case 'stepStarted':
      case 'stepProgress':
        setProgress(event.payload as ProgressEvent);
        break;
      case 'stepSucceeded':
        const successStep = event.payload as ProgressEvent;
        addCompletedStep(resolvePlanActionId(successStep) ?? successStep.stepId);
        setProgress(successStep);
        break;
      case 'stepFailed':
        setProgress(event.payload as ProgressEvent);
        break;
      case 'verificationCompleted': {
        const summary = event.payload as SetupSummary;
        if (summary) {
          setGlobalState(
            summary.globalState as import('../bridge/types').GlobalState,
            summary.globalMessage
          );
        }
        setScreen('complete');
        break;
      }
      case 'error':
        setError(
          normalizeInstallerError(event.payload, {
            title: 'La configuracion encontro un error',
          })
        );
        break;
    }
  });

  // Build step indicator from plan
  const currentActionId = resolvePlanActionId(currentStep);
  const steps = plan?.actions.map((action) => ({
    id: action.id,
    title: action.title,
    status: completedSteps.includes(action.id)
      ? ('done' as const)
      : currentActionId === action.id
        ? ('active' as const)
        : ('pending' as const),
  })) ?? [];

  const activeIndex = steps.findIndex((s) => s.status === 'active');
  const totalSteps = steps.length;
  const percent = currentStep?.percent ?? (totalSteps > 0 ? (completedSteps.length / totalSteps) * 100 : 0);

  return (
    <div className="animate-fade-in">
      {/* Header */}
      <div className="mb-8">
        <h1 className="text-2xl font-bold text-text-primary mb-2">
          Configurando tu entorno de estudio
        </h1>
        <p className="text-text-secondary">
          {currentStep
            ? `Paso ${activeIndex + 1} de ${totalSteps} · ${currentStep.title}`
            : 'Preparando...'}
        </p>
      </div>

      {/* Progress bar */}
      <div className="mb-8">
        <div className="progress-bar">
          <div
            className="progress-bar-fill shimmer"
            style={{ width: `${Math.min(percent, 100)}%` }}
          />
        </div>
        <div className="flex justify-between mt-2 text-xs text-text-muted">
          <span>{currentStep?.message ?? ''}</span>
          <span>{Math.round(percent)}%</span>
        </div>
      </div>

      {/* Step indicator */}
      <div className="bg-surface rounded-xl border border-border/50 p-6 mb-8">
        <StepIndicator steps={steps} />
      </div>

      {/* Cancel */}
      <ActionButton
        variant="ghost"
        onClick={async () => {
          const { sendToHost } = await import('../bridge/bridge');
          await sendToHost('cancelPlan');
          setScreen('diagnosis');
        }}
      >
        Cancelar
      </ActionButton>
    </div>
  );
}
