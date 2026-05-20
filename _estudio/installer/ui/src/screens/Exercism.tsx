import { useStore } from '../state/store';
import { getResponsePayload, normalizeInstallerError, sendToHost } from '../bridge/bridge';
import { ActionButton } from '../components/ActionButton';
import { useState } from 'react';

export function Exercism() {
  const exercism = useStore((s) => s.exercism);
  const setAuth = useStore((s) => s.setAuth);
  const github = useStore((s) => s.github);
  const setScreen = useStore((s) => s.setScreen);
  const workspacePath = useStore((s) => s.workspacePath);
  const setError = useStore((s) => s.setError);
  const [token, setToken] = useState('');
  const [loading, setLoading] = useState(false);

  const handleConfigure = async () => {
    if (!token.trim()) return;
    setError(null);
    setLoading(true);
    try {
      const response = await sendToHost('configureExercism', {
        token: token.trim(),
        workspace: workspacePath ?? '',
      });
      const account = getResponsePayload<import('../bridge/types').AccountState>(response);
      if (!account) {
        throw new Error('El backend no devolvio el estado de Exercism.');
      }

      setAuth(github, account);
    } catch (error) {
      setError(
        normalizeInstallerError(error, {
          title: 'No se pudo configurar Exercism',
        })
      );
    } finally {
      setLoading(false);
    }
  };

  const handleOpenTokenPage = async () => {
    setError(null);
    try {
      await sendToHost('openExercismTokenPage');
    } catch (error) {
      setError(
        normalizeInstallerError(error, {
          title: 'No se pudo abrir la pagina de token de Exercism',
        })
      );
    }
  };

  return (
    <div className="animate-fade-in">
      <div className="mb-8">
        <h1 className="text-2xl font-bold text-text-primary mb-2">
          Configurar Exercism
        </h1>
        <p className="text-text-secondary">
          Exercism conecta ejercicios de práctica con tests automáticos. Necesitamos tu token de API
          para configurar el CLI.
        </p>
      </div>

      <div className="bg-surface rounded-xl border border-border/50 p-6 mb-8">
        {exercism?.configured ? (
          <div className="flex items-center gap-4">
            <div className="w-12 h-12 rounded-full bg-success/20 flex items-center justify-center">
              <span className="text-success text-xl">✓</span>
            </div>
            <div>
              <p className="font-medium text-text-primary">Exercism configurado</p>
              <p className="text-sm text-text-secondary">Token validado correctamente.</p>
            </div>
          </div>
        ) : (
          <div className="space-y-6">
            <div>
              <p className="text-sm text-text-secondary mb-4">
                1. Abre tu página de configuración de Exercism para obtener tu token.
              </p>
              <ActionButton variant="secondary" onClick={handleOpenTokenPage} size="sm">
                Abrir página de token
              </ActionButton>
            </div>

            <div>
              <p className="text-sm text-text-secondary mb-3">
                2. Pega tu token aquí:
              </p>
              <input
                type="password"
                value={token}
                onChange={(e) => setToken(e.target.value)}
                placeholder="Token de API de Exercism"
                className="w-full px-4 py-3 rounded-lg bg-surface-alt border border-border text-text-primary placeholder:text-text-muted focus:outline-none focus:ring-2 focus:ring-accent/50 focus:border-accent transition-all font-mono text-sm"
              />
            </div>

            <ActionButton
              onClick={handleConfigure}
              loading={loading}
              disabled={!token.trim()}
            >
              Configurar token
            </ActionButton>
          </div>
        )}
      </div>

      <div className="flex items-center gap-4">
        <ActionButton
          variant={exercism?.configured ? 'primary' : 'ghost'}
          onClick={() => setScreen('progress')}
        >
          {exercism?.configured ? 'Continuar configuración' : 'Omitir por ahora'}
        </ActionButton>
        <ActionButton variant="ghost" onClick={() => setScreen('github')}>
          Volver
        </ActionButton>
      </div>
    </div>
  );
}
