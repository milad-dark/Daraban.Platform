/**
 * Audit trail models (Task 7.3). Shape mirrors AuditLogDto from
 * Daraban.Modules.Identity.Services/Audit/AuditLogService.cs -- camelCase on the wire.
 */

export interface AuditLogEntry {
  id: number;
  entityType: string;
  entityId: string;
  /** "Added" | "Modified" | "Deleted" (EF Core state names). */
  action: string;
  actorUserId: string | null;
  /** Server-resolved display name; null for system changes or deleted users. */
  actorName: string | null;
  ipAddress: string | null;
  userAgent: string | null;
  occurredAt: string;
  /** JSON object snapshots; null for inserts (old) and deletes (new). */
  oldValues: string | null;
  newValues: string | null;
}

export interface AuditLogPage {
  items: AuditLogEntry[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export interface AuditLogFilters {
  entityType: string | null;
  entityId: string | null;
  actorUserId: string | null;
  action: string | null;
  /** ISO 8601 strings for the API. */
  from: string | null;
  to: string | null;
}

/** Options for the action filter -- exactly the values the API validates. */
export const AUDIT_ACTION_OPTIONS: ReadonlyArray<{ value: string; label: string }> = [
  { value: 'Added', label: 'Added' },
  { value: 'Modified', label: 'Modified' },
  { value: 'Deleted', label: 'Deleted' },
];
