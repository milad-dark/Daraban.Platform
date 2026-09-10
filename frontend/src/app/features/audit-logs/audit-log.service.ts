import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { AuditLogEntry, AuditLogPage, AuditLogFilters } from './models/audit-log.model';

/**
 * Read-only access to the audit trail API (Task 7.3). The backend enforces the
 * `identity.auditlogs.read` permission; a 403 here surfaces through extractError
 * in the store, exactly like every other feature's error path.
 */
@Injectable({ providedIn: 'root' })
export class AuditLogService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiUrl}/v1/audit-logs`;

  getLogs(filters: AuditLogFilters, page = 1, pageSize = 20): Observable<AuditLogPage> {
    let params = new HttpParams()
      .set('page', page.toString())
      .set('pageSize', pageSize.toString());

    for (const [key, value] of Object.entries(filters)) {
      // Blank values must never reach the API: an empty entityId is not a filter,
      // it is a guaranteed 400 from Guid model binding.
      if (value) {
        params = params.set(key, value);
      }
    }

    return this.http.get<AuditLogPage>(this.baseUrl, { params });
  }

  /** Change history for one record -- backs the inline EntityHistoryComponent. */
  getEntityHistory(entityType: string, entityId: string, limit = 50): Observable<AuditLogEntry[]> {
    return this.http.get<AuditLogEntry[]>(
      `${this.baseUrl}/${encodeURIComponent(entityType)}/${encodeURIComponent(entityId)}`,
      { params: new HttpParams().set('limit', limit.toString()) }
    );
  }
}
