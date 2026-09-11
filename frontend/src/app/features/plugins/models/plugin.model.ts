/**
 * Plugin registry models (Task 7.5). Shape mirrors PluginDto /
 * PluginMenuItemDto from Daraban.Modules.Plugins -- camelCase on the wire.
 */

/** Registry row states. Tombstones ("uninstalled") are never listed. */
export type PluginStatus = 'enabled' | 'disabled' | 'uninstalled';

/** The four plugin archetypes the backend catalog accepts. */
export type PluginType =
  | 'asset'
  | 'ticket-automation'
  | 'report'
  | 'integration';

export interface Plugin {
  id: string;
  /** Slug id from the manifest (e.g. "asset-importer"). */
  pluginId: string;
  name: string;
  /** Semantic version from the manifest. */
  version: string;
  type: PluginType;
  status: PluginStatus;
  installedAt: string;
  /** Raw manifest JSON, echoed read-only for the detail view. */
  manifestJson: string | null;
}

/** Menu entry contributed by an enabled plugin, for the shell. */
export interface PluginMenuItem {
  title: string;
  route: string;
  icon: string;
  order: number;
  /** Permission the shell needs to check before showing the entry. */
  requiredPermission: string | null;
}

/** Friendly labels for the plugin type badges. */
export const PLUGIN_TYPE_LABELS: Record<PluginType, string> = {
  asset: 'Asset',
  'ticket-automation': 'Ticket automation',
  report: 'Report',
  integration: 'Integration',
};
