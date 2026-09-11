/**
 * System settings models (Task 7.4). Shape mirrors SettingDto /
 * SettingsByCategoryDto from Daraban.Modules.Settings.Services -- camelCase on the wire.
 */

/** "string" | "int" | "boolean" | "time" -- drives the form control rendered per row. */
export type SettingValueType = 'string' | 'int' | 'boolean' | 'time';

/** The five tabs, exactly the categories the backend catalog defines. */
export const SETTING_CATEGORIES = [
  'email',
  'ldap',
  'branding',
  'security',
  'time',
] as const;

export type SettingCategory = (typeof SETTING_CATEGORIES)[number];

/** Friendly tab labels; keys are the backend category strings. */
export const CATEGORY_LABELS: Record<SettingCategory, string> = {
  email: 'Email (SMTP)',
  ldap: 'LDAP',
  branding: 'Branding',
  security: 'Security',
  time: 'Time',
};

export interface Setting {
  key: string;
  /** Masked placeholder for secrets -- the real value never leaves the server. */
  value: string;
  valueType: SettingValueType;
  category: SettingCategory;
  description: string;
  isSecret: boolean;
  updatedAt: string;
}

export interface CategorySettings {
  category: SettingCategory;
  settings: Setting[];
}

export interface SettingsByCategory {
  categories: CategorySettings[];
}

/** Request body for PUT /api/v1/settings/{key}. */
export interface UpdateSettingRequest {
  value: string;
}

/** Result of POST /api/v1/settings/test/{key}. */
export interface ConnectionTestResult {
  success: boolean;
  message: string;
  latencyMs: number;
}

/** Live state for one TestConnectionButton: idle -> running -> success/failed. */
export type TestState =
  | { status: 'idle' }
  | { status: 'running' }
  | { status: 'success' | 'failed'; result: ConnectionTestResult };
