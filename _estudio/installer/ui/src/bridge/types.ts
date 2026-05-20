// Types matching the C# backend bridge protocol.
// These must stay in sync with BridgeMessages.cs and GlobalState.cs.

export type BridgeAction =
  | 'diagnoseEnvironment'
  | 'createSetupPlan'
  | 'applyPlan'
  | 'cancelPlan'
  | 'repairComponent'
  | 'configureGithub'
  | 'changeGithubAccount'
  | 'configureExercism'
  | 'openExercismTokenPage'
  | 'configureWorkspace'
  | 'openVSCode'
  | 'openLogs'
  | 'exportDiagnostics'
  | 'runSmokeTest'
  | 'getCurrentState';

export type BridgeEventType =
  | 'diagnosticStarted'
  | 'diagnosticUpdated'
  | 'diagnosticCompleted'
  | 'planCreated'
  | 'stepStarted'
  | 'stepProgress'
  | 'stepNeedsUserInput'
  | 'stepSucceeded'
  | 'stepFailed'
  | 'stepSkipped'
  | 'verificationStarted'
  | 'verificationCompleted'
  | 'globalStateChanged'
  | 'logUpdated'
  | 'error';

export type GlobalState =
  | 'analyzing'
  | 'needsSetup'
  | 'needsRepair'
  | 'needsAuthentication'
  | 'needsUserAction'
  | 'readyToConfigure'
  | 'configuring'
  | 'partiallyReady'
  | 'readyToStudy'
  | 'failed';

export type ResourceStatus =
  | 'ready'
  | 'missing'
  | 'broken'
  | 'outdated'
  | 'needsAuth'
  | 'needsUserAction'
  | 'optional'
  | 'skipped'
  | 'installing'
  | 'repairing'
  | 'failed';

export interface ResourceState {
  id: string;
  displayName: string;
  status: ResourceStatus;
  category: string;
  isCritical: boolean;
  version?: string;
  path?: string;
  humanStatus?: string;
  humanDescription?: string;
  actionLabel?: string;
  actionId?: string;
  error?: InstallerError;
}

export interface InstallerError {
  code: string;
  title: string;
  description: string;
  probableCause: string;
  recommendedAction: string;
  technicalDetails?: string;
  canRetry: boolean;
  canContinueSafely: boolean;
}

export interface AccountState {
  configured: boolean;
  userName?: string;
  host?: string;
  validatedAtUtc?: string;
  storageWarning?: string;
}

export interface SetupAction {
  id: string;
  title: string;
  description: string;
  category: string;
  severity: 'critical' | 'recommended' | 'optional';
  requiresAdmin: boolean;
  requiresUser: boolean;
  canSkip: boolean;
  dependsOn: string[];
  dependencyId?: string;
  status: string;
  statusMessage?: string;
  progressPercent: number;
}

export interface SetupPlan {
  createdAtUtc: string;
  actions: SetupAction[];
  totalActions: number;
  criticalActions: number;
  completedActions: number;
  summary: string;
}

export interface UIStateSnapshot {
  globalState: GlobalState;
  globalMessage: string;
  resources: ResourceState[];
  currentPlan?: SetupPlan;
  gitHub?: AccountState;
  exercism?: AccountState;
  workspacePath?: string;
  workspaceValid: boolean;
  buildFlowValid: boolean;
  configuratorVersion: string;
}

export interface ProgressEvent {
  stepId: string;
  title: string;
  message: string;
  percent: number;
  dependency?: string;
  status: string;
}

export interface BridgeRequest {
  id: string;
  type: BridgeAction;
  payload: Record<string, unknown>;
}

export interface BridgeResponse {
  id: string;
  ok: boolean;
  type: string;
  payload?: unknown;
  error?: InstallerError;
  requestId?: string;
  success?: boolean;
  data?: unknown;
}

export interface BridgeEvent {
  type: BridgeEventType;
  payload?: unknown;
  timestamp: string;
}
