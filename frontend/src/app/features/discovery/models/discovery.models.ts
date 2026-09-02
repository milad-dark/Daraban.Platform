export interface DiscoveryRange {
  id: string;
  name: string;
  cidrRange: string;
  startIp?: string;
  endIp?: string;
  scanType: ScanType;
  snmpCredentialId?: string;
  snmpCredentialName?: string;
  isActive: boolean;
  scanIntervalHours: number;
  lastScanAt?: string;
  createdAt: string;
}

export interface DiscoveryScan {
  id: string;
  rangeId: string;
  rangeName: string;
  status: ScanStatus;
  scanType: ScanType;
  devicesFound: number;
  ipsResponded: number;
  totalIps: number;
  queuedAt: string;
  startedAt?: string;
  completedAt?: string;
  duration?: string;
  errorMessage?: string;
  initiatedBy?: string;
}

export interface DiscoveredDevice {
  id: number;
  scanId: string;
  rangeId: string;
  ipAddress: string;
  macAddress?: string;
  hostname?: string;
  osGuess?: string;
  osVersion?: string;
  vendor?: string;
  model?: string;
  serialNumber?: string;
  openPorts?: string;
  sysDescr?: string;
  sysName?: string;
  sysLocation?: string;
  sysContact?: string;
  snmpUptime?: number;
  pingMs?: number;
  ttl?: number;
  assetCreated: boolean;
  assetId?: string;
  discoveredAt: string;
  lastSeenAt?: string;
}

export interface SnmpCredential {
  id: string;
  name: string;
  version: SnmpVersion;
  isActive: boolean;
  createdAt: string;
}

export interface DiscoveryRule {
  id: string;
  name: string;
  description?: string;
  isActive: boolean;
  priority: number;
  filterCriteria: string;
  action: MatchAction;
  assetType?: string;
  entityId?: string;
  tag?: string;
  notifyOnCreate: boolean;
  assetsCreatedCount: number;
  createdAt: string;
  lastExecutedAt?: string;
  lastMatchedAt?: string;
}

export interface DiscoveryDashboard {
  totalRanges: number;
  activeRanges: number;
  totalScans: number;
  completedScans: number;
  failedScans: number;
  totalDevices: number;
  assetsCreated: number;
  recentScans: DiscoveryScan[];
  recentDevices: DiscoveredDevice[];
}

export interface ScanProgress {
  scanId: string;
  status: ScanStatus;
  totalIps: number;
  ipsScanned: number;
  devicesFound: number;
  currentIp?: string;
  percentage: number;
}

export enum ScanType {
  Ping = 0,
  Snmp = 1,
  Wmi = 2,
  Ssh = 3,
  Http = 4,
  Combined = 5
}

export enum ScanStatus {
  Queued = 0,
  Running = 1,
  Completed = 2,
  Failed = 3,
  Cancelled = 4
}

export enum SnmpVersion {
  V1 = 0,
  V2c = 1,
  V3 = 2
}

export enum MatchAction {
  CreateAsset = 0,
  UpdateAsset = 1,
  Ignore = 2,
  CreateAndAssign = 3
}
