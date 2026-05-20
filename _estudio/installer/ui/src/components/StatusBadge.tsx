import type { ResourceStatus } from '../bridge/types';

interface StatusBadgeProps {
  status: ResourceStatus;
  label?: string;
  size?: 'sm' | 'md';
}

const statusConfig: Record<ResourceStatus, { bg: string; text: string; icon: string; label: string }> = {
  ready:          { bg: 'bg-success-soft', text: 'text-success',   icon: '✓', label: 'Listo' },
  missing:        { bg: 'bg-warning-soft', text: 'text-warning',   icon: '○', label: 'Pendiente' },
  broken:         { bg: 'bg-error-soft',   text: 'text-error',     icon: '✗', label: 'Requiere reparación' },
  outdated:       { bg: 'bg-warning-soft', text: 'text-warning',   icon: '↑', label: 'Necesita actualización' },
  needsAuth:      { bg: 'bg-accent-soft',  text: 'text-accent',    icon: '◑', label: 'Necesita inicio de sesión' },
  needsUserAction:{ bg: 'bg-accent-soft',  text: 'text-accent',    icon: '◑', label: 'Requiere acción' },
  optional:       { bg: 'bg-surface-alt',  text: 'text-text-muted',icon: '○', label: 'Opcional' },
  skipped:        { bg: 'bg-surface-alt',  text: 'text-text-muted',icon: '–', label: 'Omitido' },
  installing:     { bg: 'bg-accent-soft',  text: 'text-accent',    icon: '⟳', label: 'Instalando...' },
  repairing:      { bg: 'bg-accent-soft',  text: 'text-accent',    icon: '⟳', label: 'Reparando...' },
  failed:         { bg: 'bg-error-soft',   text: 'text-error',     icon: '✗', label: 'Error' },
};

export function StatusBadge({ status, label, size = 'md' }: StatusBadgeProps) {
  const config = statusConfig[status] ?? statusConfig.missing;
  const displayLabel = label ?? config.label;
  const sizeClasses = size === 'sm' ? 'text-xs px-2 py-0.5' : 'text-sm px-3 py-1';

  return (
    <span className={`inline-flex items-center gap-1.5 rounded-full font-medium ${config.bg} ${config.text} ${sizeClasses}`}>
      <span className="text-[0.75em]">{config.icon}</span>
      {displayLabel}
    </span>
  );
}
