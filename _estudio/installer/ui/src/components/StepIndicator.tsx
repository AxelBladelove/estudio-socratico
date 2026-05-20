interface StepIndicatorProps {
  steps: { id: string; title: string; status: 'pending' | 'active' | 'done' | 'failed' | 'skipped' }[];
}

const stepIcons: Record<string, string> = {
  pending: '○',
  active: '●',
  done: '✓',
  failed: '✗',
  skipped: '–',
};

const stepColors: Record<string, string> = {
  pending: 'text-text-muted',
  active: 'text-accent',
  done: 'text-success',
  failed: 'text-error',
  skipped: 'text-text-muted',
};

export function StepIndicator({ steps }: StepIndicatorProps) {
  return (
    <div className="space-y-1">
      {steps.map((step, i) => (
        <div key={step.id} className="flex items-start gap-3">
          <div className="flex flex-col items-center">
            <span className={`text-lg leading-none ${stepColors[step.status]}`}>
              {stepIcons[step.status]}
            </span>
            {i < steps.length - 1 && (
              <div className={`w-px h-6 mt-1 ${
                step.status === 'done' ? 'bg-success/40' : 'bg-border'
              }`} />
            )}
          </div>
          <span className={`text-sm pt-0.5 ${
            step.status === 'active' ? 'text-text-primary font-medium' :
            step.status === 'done' ? 'text-text-secondary' :
            'text-text-muted'
          }`}>
            {step.title}
          </span>
        </div>
      ))}
    </div>
  );
}
