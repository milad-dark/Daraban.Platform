// ── Agent List ──

export type AgentStatus = 'Active' | 'Suspended' | 'Deactivated';
export type AgentType =
  | 'Generic'
  | 'InventoryScanner'
  | 'AssetMonitor'
  | 'ServiceDeskBot'
  | 'IntegrationConnector';

export interface AgentListItem {
  id: string;
  name: string;
  description: string | null;
  type: AgentType;
  status: AgentStatus;
  hostname: string | null;
  operatingSystem: string | null;
  lastActiveAt: string | null;
  isOnline: boolean;
  pendingCommandCount: number;
  totalCommandCount: number;
  createdAt: string;
}

export interface AgentListResult {
  items: AgentListItem[];
  totalCount: number;
  page: number;
  pageSize: number;
}

// ── Fleet Summary ──

export interface AgentFleetSummary {
  totalAgents: number;
  onlineAgents: number;
  offlineAgents: number;
  suspendedAgents: number;
  totalCommandsToday: number;
  pendingCommands: number;
  failedCommandsLast24h: number;
}

// ── Agent Detail ──

export interface AgentDetail {
  agent: {
    id: string;
    name: string;
    description: string | null;
    ownerUserId: string | null;
    entityId: string | null;
    type: AgentType;
    status: AgentStatus;
    allowedScopes: string;
    rateLimitPerMinute: number;
    tags: string | null;
    lastActiveAt: string | null;
    createdAt: string;
    updatedAt: string;
  };
  credentialCount: number;
  totalCommands: number;
  completedCommands: number;
  failedCommands: number;
  pendingCommands: number;
  lastInventoryAt: string | null;
  lastInventoryStatus: string | null;
}

// ── Inventory Snapshot ──

export interface AgentInventorySnapshot {
  submissionId: number;
  deviceId: string;
  itemType: string | null;
  action: string;
  status: string;
  deviceCount: number | null;
  submittedAt: string;
  receivedAt: string;
  processedAt: string | null;
}

// ── Command History ──

export type CommandStatus =
  | 'Created'
  | 'Queued'
  | 'Dispatched'
  | 'Received'
  | 'Executing'
  | 'Completed'
  | 'Failed'
  | 'TimedOut'
  | 'Cancelled';

export type CommandType =
  | 'RunScript'
  | 'InstallSoftware'
  | 'UninstallSoftware'
  | 'RestartService'
  | 'RebootDevice'
  | 'CollectInventoryNow';

export interface AgentCommandHistoryEntry {
  commandId: string;
  commandType: CommandType;
  status: CommandStatus;
  payload: string | null;
  exitCode: number | null;
  lastError: string | null;
  createdAt: string;
  completedAt: string | null;
  executionDurationMs: number;
}

export interface CommandHistoryResult {
  items: AgentCommandHistoryEntry[];
  totalCount: number;
  page: number;
  pageSize: number;
}

// ── Command Dispatch ──

export interface CreateCommandRequest {
  agentId: string;
  commandType: CommandType;
  payload?: string;
  timeoutSeconds?: number;
  maxRetries?: number;
}

export interface CommandDto {
  id: string;
  agentId: string;
  commandType: string;
  status: string;
  payload: string | null;
  timeoutSeconds: number | null;
  retryCount: number;
  maxRetries: number;
  lastError: string | null;
  createdAt: string;
  updatedAt: string;
  dispatchedAt: string | null;
  completedAt: string | null;
}

// ── Auth Models ──

export const AGENT_STATUS_OPTIONS: { value: AgentStatus; label: string }[] = [
  { value: 'Active', label: 'Active' },
  { value: 'Suspended', label: 'Suspended' },
  { value: 'Deactivated', label: 'Deactivated' },
];

export const AGENT_TYPE_OPTIONS: { value: AgentType; label: string }[] = [
  { value: 'Generic', label: 'Generic' },
  { value: 'InventoryScanner', label: 'Inventory Scanner' },
  { value: 'AssetMonitor', label: 'Asset Monitor' },
  { value: 'ServiceDeskBot', label: 'Service Desk Bot' },
  { value: 'IntegrationConnector', label: 'Integration Connector' },
];

export const COMMAND_TYPE_OPTIONS: { value: CommandType; label: string; icon: string }[] = [
  { value: 'RunScript', label: 'Run Script', icon: 'terminal' },
  { value: 'InstallSoftware', label: 'Install Software', icon: 'download' },
  { value: 'UninstallSoftware', label: 'Uninstall Software', icon: 'delete_forever' },
  { value: 'RestartService', label: 'Restart Service', icon: 'refresh' },
  { value: 'RebootDevice', label: 'Reboot Device', icon: 'power_settings_new' },
  { value: 'CollectInventoryNow', label: 'Collect Inventory', icon: 'inventory_2' },
];
