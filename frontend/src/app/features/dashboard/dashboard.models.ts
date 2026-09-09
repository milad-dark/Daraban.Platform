/**
 * Dashboard models (Task 7.1). Mirrors Daraban.Modules.Dashboard.Services.Dtos.
 */

/** The widget types the platform ships -- must stay in sync with WidgetType enum. */
export type WidgetType =
  | 'OpenTicketsCount'
  | 'TicketsByStatus'
  | 'TicketsByPriority'
  | 'AssetCountByType'
  | 'AssetsNearingEnd'
  | 'RecentInventory'
  | 'SlaComplianceRate'
  | 'AgentStatusSummary';

/** Widget metadata from GET /api/v1/dashboard/widgets. */
export interface WidgetDefinition {
  type: number;
  name: string;
  description: string;
  defaultSizeW: string;
  defaultSizeH: string;
}

/** One widget placement in CSS-grid coordinates. */
export interface WidgetPlacement {
  widgetType: string;
  x: number;
  y: number;
  w: number;
  h: number;
}

/** GET/PUT /api/v1/dashboard/layout payload. */
export interface DashboardLayoutDto {
  widgets: WidgetPlacement[];
  updatedAt: string;
}

/** Label/value pair used by count + distribution widgets. */
export interface LabelValue {
  label: string;
  value: number;
}

/** RecentInventory row. */
export interface RecentInventoryItem {
  id: number;
  deviceId: string;
  itemType: string | null;
  status: string;
  deviceCount: number | null;
  submittedAt: string;
}

/** AssetsNearingEnd row. */
export interface AssetNearingEnd {
  id: string;
  name: string;
  assetTag: string | null;
  typeName: string | null;
  warrantyExpiry: string | null;
  daysRemaining: number;
}

/** Agent status summary payload. */
export interface AgentStatusSummary {
  active: number;
  suspended: number;
  deactivated: number;
  onlineLast5Minutes: number;
}

/**
 * Uniform envelope from GET /api/v1/dashboard/data/{widgetType}: numeric widgets emit
 * `values`, list widgets emit `items`. The widget host stays generic over this shape.
 */
export interface WidgetData {
  widgetType: string;
  values: LabelValue[] | null;
  items: Array<RecentInventoryItem | AssetNearingEnd> | null;
}
