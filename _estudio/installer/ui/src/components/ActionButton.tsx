import type { ReactNode } from 'react';

interface ActionButtonProps {
  variant?: 'primary' | 'secondary' | 'ghost' | 'danger';
  size?: 'sm' | 'md' | 'lg';
  onClick?: () => void;
  disabled?: boolean;
  loading?: boolean;
  children: ReactNode;
  className?: string;
}

const variants = {
  primary:
    'bg-accent text-white hover:bg-accent/90 active:bg-accent/80 shadow-lg shadow-accent/20',
  secondary:
    'bg-surface-alt text-text-primary border border-border hover:bg-border/50 active:bg-border',
  ghost:
    'text-text-secondary hover:text-text-primary hover:bg-surface-alt active:bg-border/50',
  danger:
    'bg-error/10 text-error border border-error/30 hover:bg-error/20 active:bg-error/30',
};

const sizes = {
  sm: 'text-sm px-3 py-1.5 rounded-lg',
  md: 'text-sm px-5 py-2.5 rounded-xl',
  lg: 'text-base px-8 py-3.5 rounded-xl font-semibold',
};

export function ActionButton({
  variant = 'primary',
  size = 'md',
  onClick,
  disabled,
  loading,
  children,
  className = '',
}: ActionButtonProps) {
  return (
    <button
      onClick={onClick}
      disabled={disabled || loading}
      className={`inline-flex items-center justify-center gap-2 font-medium transition-all duration-200
        ${variants[variant]} ${sizes[size]}
        disabled:opacity-40 disabled:cursor-not-allowed disabled:pointer-events-none
        ${className}`}
    >
      {loading && (
        <svg className="animate-spin w-4 h-4" viewBox="0 0 24 24" fill="none">
          <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4" />
          <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4z" />
        </svg>
      )}
      {children}
    </button>
  );
}
