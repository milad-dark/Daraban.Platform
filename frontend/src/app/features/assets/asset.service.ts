import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  Asset,
  AssetPagedResult,
  AssetType,
  Location,
  AssetAssignment,
  AssetStatusHistoryEntry,
  CreateAssetRequest,
  UpdateAssetRequest,
  ImportResult,
} from './models/asset.model';

@Injectable({ providedIn: 'root' })
export class AssetService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiUrl}/v1/assets`;

  // ── CRUD ──

  getPaged(
    status?: string,
    assetTypeId?: string,
    locationId?: string,
    search?: string,
    page = 1,
    pageSize = 20
  ): Observable<AssetPagedResult> {
    let params = new HttpParams()
      .set('page', page.toString())
      .set('pageSize', pageSize.toString());

    if (status) params = params.set('status', status);
    if (assetTypeId) params = params.set('assetTypeId', assetTypeId);
    if (locationId) params = params.set('locationId', locationId);
    if (search) params = params.set('search', search);

    return this.http.get<AssetPagedResult>(this.baseUrl, { params });
  }

  getById(id: string): Observable<Asset> {
    return this.http.get<Asset>(`${this.baseUrl}/${id}`);
  }

  create(request: CreateAssetRequest): Observable<Asset> {
    return this.http.post<Asset>(this.baseUrl, request);
  }

  update(id: string, request: UpdateAssetRequest): Observable<Asset> {
    return this.http.put<Asset>(`${this.baseUrl}/${id}`, request);
  }

  delete(id: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${id}`);
  }

  // ── Asset Types ──

  getAssetTypes(): Observable<AssetType[]> {
    return this.http.get<AssetType[]>(`${this.baseUrl}-types`);
  }

  // ── Locations ──

  getLocations(): Observable<Location[]> {
    return this.http.get<Location[]>(`${this.baseUrl.replace('/assets', '')}/v1/locations`);
  }

  // ── Assignments ──

  getAssignmentHistory(assetId: string): Observable<AssetAssignment[]> {
    return this.http.get<AssetAssignment[]>(
      `${this.baseUrl}/${assetId}/assignments`
    );
  }

  getCurrentAssignment(assetId: string): Observable<AssetAssignment | null> {
    return this.http.get<AssetAssignment | null>(
      `${this.baseUrl}/${assetId}/assignments/current`
    );
  }

  assign(
    assetId: string,
    targetType: string,
    targetId: string,
    notes?: string
  ): Observable<AssetAssignment> {
    return this.http.post<AssetAssignment>(
      `${this.baseUrl}/${assetId}/assignments`,
      { targetType, targetId, notes }
    );
  }

  unassign(assetId: string): Observable<void> {
    return this.http.delete<void>(
      `${this.baseUrl}/${assetId}/assignments/current`
    );
  }

  // ── Lifecycle ──

  getLifecycleHistory(
    assetId: string
  ): Observable<AssetStatusHistoryEntry[]> {
    return this.http.get<AssetStatusHistoryEntry[]>(
      `${this.baseUrl}/${assetId}/lifecycle/history`
    );
  }

  transition(
    assetId: string,
    toStatus: string,
    reason?: string,
    notes?: string
  ): Observable<void> {
    return this.http.post<void>(
      `${this.baseUrl}/${assetId}/lifecycle/transition`,
      { toStatus, reason, notes }
    );
  }

  // ── Import / Export ──

  importAssets(
    file: File,
    dryRun = false
  ): Observable<ImportResult> {
    const formData = new FormData();
    formData.append('file', file);
    const params = new HttpParams().set('dryRun', dryRun.toString());
    return this.http.post<ImportResult>(`${this.baseUrl}/import`, formData, {
      params,
    });
  }

  getImportTemplate(): string {
    return `${this.baseUrl}/import/template`;
  }

  exportAssets(
    format: 'csv' | 'xlsx',
    status?: string,
    assetTypeId?: string,
    locationId?: string,
    search?: string
  ): string {
    let params = new HttpParams().set('format', format);
    if (status) params = params.set('status', status);
    if (assetTypeId) params = params.set('assetTypeId', assetTypeId);
    if (locationId) params = params.set('locationId', locationId);
    if (search) params = params.set('search', search);
    return `${this.baseUrl}/export?${params.toString()}`;
  }
}
