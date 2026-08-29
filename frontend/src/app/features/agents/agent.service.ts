import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  AgentListResult,
  AgentDetail,
  AgentFleetSummary,
  AgentInventorySnapshot,
  CommandHistoryResult,
  CreateCommandRequest,
  CommandDto,
} from './models/agent.model';

@Injectable({ providedIn: 'root' })
export class AgentService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiUrl}/v1/agents`;

  // ── List ──

  getAgents(
    status?: string,
    type?: string,
    search?: string,
    page = 1,
    pageSize = 20
  ): Observable<AgentListResult> {
    let params = new HttpParams()
      .set('page', page.toString())
      .set('pageSize', pageSize.toString());

    if (status) params = params.set('status', status);
    if (type) params = params.set('type', type);
    if (search) params = params.set('search', search);

    return this.http.get<AgentListResult>(this.baseUrl, { params });
  }

  // ── Fleet Summary ──

  getSummary(): Observable<AgentFleetSummary> {
    return this.http.get<AgentFleetSummary>(`${this.baseUrl}/summary`);
  }

  // ── Detail ──

  getDetail(id: string): Observable<AgentDetail> {
    return this.http.get<AgentDetail>(`${this.baseUrl}/${id}`);
  }

  // ── Command History ──

  getJobs(
    agentId: string,
    page = 1,
    pageSize = 20
  ): Observable<CommandHistoryResult> {
    let params = new HttpParams()
      .set('page', page.toString())
      .set('pageSize', pageSize.toString());

    return this.http.get<CommandHistoryResult>(
      `${this.baseUrl}/${agentId}/jobs`,
      { params }
    );
  }

  // ── Inventory Snapshot ──

  getInventory(agentId: string): Observable<AgentInventorySnapshot | null> {
    return this.http.get<AgentInventorySnapshot | null>(
      `${this.baseUrl}/${agentId}/inventory`
    );
  }

  // ── Command Dispatch ──

  dispatchCommand(request: CreateCommandRequest): Observable<CommandDto> {
    return this.http.post<CommandDto>(
      `${this.baseUrl}/commands`,
      request
    );
  }
}
