import { create } from 'zustand';
import type {
  GlobalState,
  ResourceState,
  SetupPlan,
  AccountState,
  ProgressEvent,
  InstallerError,
} from '../bridge/types';

export type Screen =
  | 'welcome'
  | 'diagnosis'
  | 'plan'
  | 'github'
  | 'exercism'
  | 'progress'
  | 'complete';

interface AppState {
  // Navigation
  screen: Screen;
  setScreen: (screen: Screen) => void;

  // Global state from backend
  globalState: GlobalState;
  globalMessage: string;
  setGlobalState: (state: GlobalState, message: string) => void;

  // Resources
  resources: ResourceState[];
  setResources: (resources: ResourceState[]) => void;

  // Plan
  plan: SetupPlan | null;
  setPlan: (plan: SetupPlan | null) => void;

  // Auth
  github: AccountState | null;
  exercism: AccountState | null;
  setAuth: (github: AccountState | null, exercism: AccountState | null) => void;

  // Progress
  currentStep: ProgressEvent | null;
  completedSteps: string[];
  setProgress: (step: ProgressEvent) => void;
  addCompletedStep: (stepId: string) => void;
  resetProgress: () => void;

  // Workspace
  workspacePath: string | null;
  workspaceValid: boolean;
  setWorkspace: (path: string | null, valid: boolean) => void;

  // Errors
  lastError: InstallerError | null;
  setError: (error: InstallerError | null) => void;

  // Loading
  loading: boolean;
  setLoading: (loading: boolean) => void;

  // Config version
  version: string;
}

export const useStore = create<AppState>((set) => ({
  screen: 'welcome',
  setScreen: (screen) => set({ screen }),

  globalState: 'analyzing',
  globalMessage: '',
  setGlobalState: (globalState, globalMessage) =>
    set({ globalState, globalMessage }),

  resources: [],
  setResources: (resources) => set({ resources }),

  plan: null,
  setPlan: (plan) => set({ plan }),

  github: null,
  exercism: null,
  setAuth: (github, exercism) => set({ github, exercism }),

  currentStep: null,
  completedSteps: [],
  setProgress: (currentStep) => set({ currentStep }),
  addCompletedStep: (stepId) =>
    set((state) => ({
      completedSteps: state.completedSteps.includes(stepId)
        ? state.completedSteps
        : [...state.completedSteps, stepId],
    })),
  resetProgress: () => set({ currentStep: null, completedSteps: [] }),

  workspacePath: null,
  workspaceValid: false,
  setWorkspace: (workspacePath, workspaceValid) =>
    set({ workspacePath, workspaceValid }),

  lastError: null,
  setError: (lastError) => set({ lastError }),

  loading: false,
  setLoading: (loading) => set({ loading }),

  version: '2.0.0',
}));
