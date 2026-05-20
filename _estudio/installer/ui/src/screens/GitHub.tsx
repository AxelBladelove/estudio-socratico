import { useStore } from '../state/store';
import { getResponsePayload, normalizeInstallerError, sendToHost } from '../bridge/bridge';
import { ActionButton } from '../components/ActionButton';
import { useState } from 'react';

export function GitHub() {
  const github = useStore((s) => s.github);
  const setAuth = useStore((s) => s.setAuth);
  const exercism = useStore((s) => s.exercism);
  const setScreen = useStore((s) => s.setScreen);
  const setError = useStore((s) => s.setError);
  const [loading, setLoading] = useState(false);

  const handleLogin = async () => {
    setError(null);
    setLoading(true);
    try {
      const response = await sendToHost('configureGithub');
      const account = getResponsePayload<import('../bridge/types').AccountState>(response);
      if (!account) {
        throw new Error('El backend no devolvio el estado de GitHub.');
      }

      setAuth(account, exercism);
    } catch (error) {
      setError(
        normalizeInstallerError(error, {
          title: 'No se pudo conectar GitHub',
        })
      );
    } finally {
      setLoading(false);
    }
  };

  const handleChangeAccount = async () => {
    setError(null);
    setLoading(true);
    try {
      const response = await sendToHost('changeGithubAccount');
      const account = getResponsePayload<import('../bridge/types').AccountState>(response);
      if (!account) {
        throw new Error('El backend no devolvio la nueva cuenta de GitHub.');
      }

      setAuth(account, exercism);
    } catch (error) {
      setError(
        normalizeInstallerError(error, {
          title: 'No se pudo cambiar la cuenta de GitHub',
        })
      );
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="animate-fade-in">
      <div className="mb-8">
        <h1 className="text-2xl font-bold text-text-primary mb-2">
          Conectar cuenta de GitHub
        </h1>
        <p className="text-text-secondary">
          Necesitamos tu cuenta de GitHub para preparar el fork del repositorio de estudio
          y configurar los remotes de Git.
        </p>
      </div>

      <div className="bg-surface rounded-xl border border-border/50 p-6 mb-8">
        {github?.configured ? (
          <div className="flex items-center gap-4">
            <div className="w-12 h-12 rounded-full bg-success/20 flex items-center justify-center">
              <span className="text-success text-xl">✓</span>
            </div>
            <div className="flex-1">
              <p className="font-medium text-text-primary">
                Conectado como {github.userName ?? 'usuario autenticado'}
              </p>
              <p className="text-sm text-text-secondary">
                {github.host ?? 'github.com'}
              </p>
            </div>
            <ActionButton variant="secondary" onClick={handleChangeAccount} loading={loading}>
              Cambiar cuenta
            </ActionButton>
          </div>
        ) : (
          <div className="text-center py-6">
            <div className="w-16 h-16 rounded-full bg-surface-alt flex items-center justify-center mx-auto mb-4">
              <span className="text-3xl">🔑</span>
            </div>
            <p className="text-text-secondary mb-6">
              Se abrirá una ventana del navegador para iniciar sesión con GitHub CLI.
            </p>
            <ActionButton onClick={handleLogin} loading={loading}>
              Iniciar sesión con GitHub
            </ActionButton>
          </div>
        )}
      </div>

      <div className="flex items-center gap-4">
        <ActionButton
          variant={github?.configured ? 'primary' : 'ghost'}
          onClick={() => setScreen('exercism')}
        >
          {github?.configured ? 'Continuar' : 'Omitir por ahora'}
        </ActionButton>
        <ActionButton variant="ghost" onClick={() => setScreen('plan')}>
          Volver
        </ActionButton>
      </div>
    </div>
  );
}
