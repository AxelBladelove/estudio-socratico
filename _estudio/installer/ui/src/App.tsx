import { useEffect } from 'react';
import { initBridge, normalizeInstallerError } from './bridge/bridge';
import { useBridgeEvent } from './bridge/useBridge';
import { useStore } from './state/store';
import { Layout } from './components/Layout';
import { Welcome } from './screens/Welcome';
import { Diagnosis } from './screens/Diagnosis';
import { Plan } from './screens/Plan';
import { GitHub } from './screens/GitHub';
import { Exercism } from './screens/Exercism';
import { Progress } from './screens/Progress';
import { Complete } from './screens/Complete';

export default function App() {
  const screen = useStore((s) => s.screen);
  const setError = useStore((s) => s.setError);

  useEffect(() => {
    initBridge();
  }, []);

  useBridgeEvent((event) => {
    if (event.type !== 'error') {
      return;
    }

    setError(
      normalizeInstallerError(event.payload, {
        title: 'El backend devolvio un error',
      })
    );
  }, ['error']);

  const renderScreen = () => {
    switch (screen) {
      case 'welcome':
        return <Welcome />;
      case 'diagnosis':
        return <Diagnosis />;
      case 'plan':
        return <Plan />;
      case 'github':
        return <GitHub />;
      case 'exercism':
        return <Exercism />;
      case 'progress':
        return <Progress />;
      case 'complete':
        return <Complete />;
      default:
        return <Welcome />;
    }
  };

  return <Layout>{renderScreen()}</Layout>;
}
