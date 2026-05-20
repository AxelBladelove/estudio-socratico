import { useState } from 'react';
import type { ResourceState } from '../bridge/types';
import { StatusBadge } from './StatusBadge';

interface ResourceCardProps {
  resource: ResourceState;
  onAction?: (actionId: string) => void;
}

export function ResourceCard({ resource, onAction }: ResourceCardProps) {
  const [expanded, setExpanded] = useState(false);

  return (
    <div
      className={`group rounded-lg border transition-all duration-200 ${
        resource.status === 'ready'
          ? 'border-border/50 bg-surface'
          : resource.status === 'failed' || resource.status === 'broken'
            ? 'border-error/30 bg-error-soft'
            : 'border-border bg-surface'
      } hover:border-border`}
    >
      <div
        className="flex items-center gap-4 px-4 py-3 cursor-pointer select-none"
        onClick={() => setExpanded(!expanded)}
      >
        <div className="flex-1 min-w-0">
          <div className="flex items-center gap-3">
            <span className="font-medium text-text-primary truncate">
              {resource.displayName}
            </span>
            {resource.isCritical && (
              <span className="text-[10px] uppercase tracking-wider text-text-muted font-semibold">
                requerido
              </span>
            )}
          </div>
          {resource.humanDescription && (
            <p className="text-sm text-text-secondary mt-0.5 truncate">
              {resource.humanDescription}
            </p>
          )}
        </div>

        <div className="flex items-center gap-3 shrink-0">
          <StatusBadge status={resource.status} label={resource.humanStatus ?? undefined} size="sm" />
          {resource.actionLabel && resource.actionId && (
            <button
              onClick={(e) => {
                e.stopPropagation();
                onAction?.(resource.actionId!);
              }}
              className="text-sm text-accent hover:text-accent/80 font-medium transition-colors px-2 py-1 rounded hover:bg-accent-soft"
            >
              {resource.actionLabel}
            </button>
          )}
          <svg
            className={`w-4 h-4 text-text-muted transition-transform duration-200 ${expanded ? 'rotate-180' : ''}`}
            fill="none"
            viewBox="0 0 24 24"
            stroke="currentColor"
          >
            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M19 9l-7 7-7-7" />
          </svg>
        </div>
      </div>

      {expanded && (
        <div className="px-4 pb-3 border-t border-border/50 pt-3 animate-fade-in">
          <div className="grid grid-cols-2 gap-2 text-sm">
            {resource.version && (
              <div>
                <span className="text-text-muted">Versión:</span>{' '}
                <span className="text-text-secondary">{resource.version}</span>
              </div>
            )}
            {resource.path && (
              <div className="col-span-2">
                <span className="text-text-muted">Ruta:</span>{' '}
                <span className="text-text-secondary font-mono text-xs">{resource.path}</span>
              </div>
            )}
            {resource.error && (
              <div className="col-span-2 mt-2 p-2 rounded bg-error-soft text-error text-sm">
                <p className="font-medium">{resource.error.title}</p>
                <p className="text-error/80 mt-1">{resource.error.description}</p>
                {resource.error.recommendedAction && (
                  <p className="text-text-secondary mt-1 text-xs">💡 {resource.error.recommendedAction}</p>
                )}
              </div>
            )}
          </div>
        </div>
      )}
    </div>
  );
}
