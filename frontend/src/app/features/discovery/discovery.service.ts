import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import {
  DiscoveryRange,
  DiscoveryScan,
  DiscoveredDevice,
  SnmpCredential,
  DiscoveryRule,
  DiscoveryDashboard,
  ScanProgress
} from './models/discovery.models';

@Injectable({
  providedIn: 'root'
})
export class DiscoveryService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = '/api/v1/discovery';

  // Dashboard
  getDashboard(): Observable<DiscoveryDashboard> {
    return this.http.get<DiscoveryDashboard>(`${this.baseUrl}/dashboard`);
  }

  // Ranges
  getRanges(): Observable<DiscoveryRange[]> {
    return this.http.get<DiscoveryRange[]>(`${this.baseUrl}/ranges`);
  }

  getRangeById(id: string): Observable<DiscoveryRange> {
    return this.http.get<DiscoveryRange>(`${this.baseUrl}/ranges/${id}`);
  }

  createRange(range: Partial<DiscoveryRange>): Observable<DiscoveryRange> {
    return this.http.post<DiscoveryRange>(`${this.baseUrl}/ranges`, range);
  }

  updateRange(id: string, range: Partial<DiscoveryRange>): Observable<DiscoveryRange> {
    return this.http.put<DiscoveryRange>(`${this.baseUrl}/ranges/${id}`, range);
  }

  deleteRange(id: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/ranges/${id}`);
  }

  startScan(rangeId: string): Observable<DiscoveryScan> {
    return this.http.post<DiscoveryScan>(`${this.baseUrl}/ranges/${rangeId}/scan`, {});
  }

  // Scans
  getScansByRange(rangeId: string, page = 1, pageSize = 10): Observable<DiscoveryScan[]> {
    const params = new HttpParams()
      .set('page', page.toString())
      .set('pageSize', pageSize.toString());
    return this.http.get<DiscoveryScan[]>(`${this.baseUrl}/scans/range/${rangeId}`, { params });
  }

  getScanById(id: string): Observable<DiscoveryScan> {
    return this.http.get<DiscoveryScan>(`${this.baseUrl}/scans/${id}`);
  }

  // Devices
  getDevicesByScan(scanId: string): Observable<DiscoveredDevice[]> {
    return this.http.get<DiscoveredDevice[]>(`${this.baseUrl}/scans/${scanId}/devices`);
  }

  getDevicesByRange(rangeId: string): Observable<DiscoveredDevice[]> {
    return this.http.get<DiscoveredDevice[]>(`${this.baseUrl}/ranges/${rangeId}/devices`);
  }

  getDeviceById(id: number): Observable<DiscoveredDevice> {
    return this.http.get<DiscoveredDevice>(`${this.baseUrl}/devices/${id}`);
  }

  // Credentials
  getCredentials(): Observable<SnmpCredential[]> {
    return this.http.get<SnmpCredential[]>(`${this.baseUrl}/credentials`);
  }

  createCredential(credential: Partial<SnmpCredential>): Observable<SnmpCredential> {
    return this.http.post<SnmpCredential>(`${this.baseUrl}/credentials`, credential);
  }

  updateCredential(id: string, credential: Partial<SnmpCredential>): Observable<SnmpCredential> {
    return this.http.put<SnmpCredential>(`${this.baseUrl}/credentials/${id}`, credential);
  }

  deleteCredential(id: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/credentials/${id}`);
  }

  // Rules
  getRules(): Observable<DiscoveryRule[]> {
    return this.http.get<DiscoveryRule[]>(`${this.baseUrl}/rules`);
  }

  createRule(rule: Partial<DiscoveryRule>): Observable<DiscoveryRule> {
    return this.http.post<DiscoveryRule>(`${this.baseUrl}/rules`, rule);
  }

  updateRule(id: string, rule: Partial<DiscoveryRule>): Observable<DiscoveryRule> {
    return this.http.put<DiscoveryRule>(`${this.baseUrl}/rules/${id}`, rule);
  }

  deleteRule(id: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/rules/${id}`);
  }

  // Import Rules (GLPI-style)
  getImportRules(): Observable<any[]> {
    return this.http.get<any[]>(`${this.baseUrl}/import-rules`);
  }

  getImportRuleById(id: string): Observable<any> {
    return this.http.get<any>(`${this.baseUrl}/import-rules/${id}`);
  }

  createImportRule(rule: any): Observable<any> {
    return this.http.post<any>(`${this.baseUrl}/import-rules`, rule);
  }

  updateImportRule(id: string, rule: any): Observable<any> {
    return this.http.put<any>(`${this.baseUrl}/import-rules/${id}`, rule);
  }

  deleteImportRule(id: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/import-rules/${id}`);
  }

  getImportRuleFields(): Observable<string[]> {
    return this.http.get<string[]>(`${this.baseUrl}/import-rules/fields`);
  }

  getImportRuleOperators(): Observable<string[]> {
    return this.http.get<string[]>(`${this.baseUrl}/import-rules/operators`);
  }

  getImportRuleActionTypes(): Observable<string[]> {
    return this.http.get<string[]>(`${this.baseUrl}/import-rules/action-types`);
  }
}
