import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  SettingsByCategory,
  Setting,
  UpdateSettingRequest,
  ConnectionTestResult,
} from './models/settings.model';

/**
 * Access to the settings API (Task 7.4). The backend enforces `settings.read` /
 * `settings.write`; a 403 surfaces through extractError in the store, exactly like every
 * other feature's error path. Secret values never travel as plaintext: the server masks
 * them, and saving the untouched mask means "keep current value" server-side.
 */
@Injectable({ providedIn: 'root' })
export class SettingsService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiUrl}/v1/settings`;

  /** All settings grouped by category, in UI tab order. Secrets masked. */
  getAll(): Observable<SettingsByCategory> {
    return this.http.get<SettingsByCategory>(this.baseUrl);
  }

  /** Updates one setting's value. */
  update(key: string, request: UpdateSettingRequest): Observable<Setting> {
    return this.http.put<Setting>(
      `${this.baseUrl}/${encodeURIComponent(key)}`,
      request
    );
  }

  /** Connectivity test for the category owning the key (email / LDAP). */
  testConnection(key: string): Observable<ConnectionTestResult> {
    return this.http.post<ConnectionTestResult>(
      `${this.baseUrl}/test/${encodeURIComponent(key)}`,
      {}
    );
  }
}
