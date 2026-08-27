export interface Asset {
  id: string;
  name: string;
  assetTag: string | null;
  serialNumber: string | null;
  status: AssetStatus;
  assetTypeName: string;
  assetModelName: string | null;
  manufacturerName: string | null;
  locationName: string | null;
  purchaseDate: string | null;
  purchaseCost: number | null;
  purchaseCurrency: string | null;
  warrantyExpiry: string | null;
  notes: string | null;
  createdAt: string;
  updatedAt: string;
}

export interface AssetList {
  id: string;
  name: string;
  assetTag: string | null;
  serialNumber: string | null;
  status: AssetStatus;
  assetTypeName: string;
  locationName: string | null;
  warrantyExpiry: string | null;
}

export interface AssetPagedResult {
  items: AssetList[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export type AssetStatus =
  | 'InStock'
  | 'InUse'
  | 'UnderMaintenance'
  | 'Archived'
  | 'Retired'
  | 'Disposed';

export const ASSET_STATUS_COLORS: Record<AssetStatus, string> = {
  InStock: '#4caf50',
  InUse: '#2196f3',
  UnderMaintenance: '#ff9800',
  Archived: '#9c27b0',
  Retired: '#f44336',
  Disposed: '#9e9e9e',
};

export const ASSET_STATUS_OPTIONS: { value: AssetStatus; label: string }[] = [
  { value: 'InStock', label: 'In Stock' },
  { value: 'InUse', label: 'In Use' },
  { value: 'UnderMaintenance', label: 'Under Maintenance' },
  { value: 'Archived', label: 'Archived' },
  { value: 'Retired', label: 'Retired' },
  { value: 'Disposed', label: 'Disposed' },
];

export interface CreateAssetRequest {
  name: string;
  assetTypeId: string;
  assetModelId: string | null;
  locationId: string | null;
  entityNodeId: string;
  assetTag: string | null;
  serialNumber: string | null;
  purchaseDate: string | null;
  purchaseCost: number | null;
  purchaseCurrency: string | null;
  orderNumber: string | null;
  supplierName: string | null;
  warrantyExpiry: string | null;
  notes: string | null;
}

export interface UpdateAssetRequest {
  name: string;
  assetModelId: string | null;
  locationId: string | null;
  assetTag: string | null;
  serialNumber: string | null;
  purchaseDate: string | null;
  purchaseCost: number | null;
  purchaseCurrency: string | null;
  orderNumber: string | null;
  supplierName: string | null;
  warrantyExpiry: string | null;
  notes: string | null;
}

export interface AssetType {
  id: string;
  categoryId: string;
  categoryName: string;
  name: string;
  description: string | null;
  icon: string | null;
}

export interface Location {
  id: string;
  parentId: string | null;
  name: string;
  city: string | null;
  country: string | null;
}

export interface Manufacturer {
  id: string;
  name: string;
  website: string | null;
}

export interface AssetAssignment {
  id: string;
  targetType: 'User' | 'Department';
  targetId: string;
  targetName: string | null;
  assignedAt: string;
  unassignedAt: string | null;
  isCurrent: boolean;
  notes: string | null;
}

export interface AssetStatusHistoryEntry {
  id: string;
  fromStatus: AssetStatus;
  toStatus: AssetStatus;
  actorUserId: string;
  reason: string | null;
  occurredAt: string;
}

export interface ImportResult {
  dryRun: boolean;
  totalRows: number;
  successCount: number;
  errorCount: number;
  rows: ImportRowResult[];
}

export interface ImportRowResult {
  rowNumber: number;
  success: boolean;
  assetName: string | null;
  errors: string[];
}
