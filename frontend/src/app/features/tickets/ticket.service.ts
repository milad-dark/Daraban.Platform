import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  Ticket,
  TicketPagedResult,
  TicketTask,
  TicketTemplate,
  TicketHistoryEntry,
  CreateTicketRequest,
  UpdateTicketRequest,
  CreateTicketTaskRequest,
  TicketStatus,
} from './models/ticket.models';

@Injectable({ providedIn: 'root' })
export class TicketService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiUrl}/v1/tickets`;

  // ── CRUD ──

  getPaged(filters: {
    type?: number;
    status?: number;
    priority?: number;
    assignedUserId?: string;
    search?: string;
    page?: number;
    pageSize?: number;
  }): Observable<TicketPagedResult> {
    let params = new HttpParams()
      .set('page', (filters.page ?? 1).toString())
      .set('pageSize', (filters.pageSize ?? 20).toString());

    if (filters.type != null) params = params.set('type', filters.type.toString());
    if (filters.status != null) params = params.set('status', filters.status.toString());
    if (filters.priority != null) params = params.set('priority', filters.priority.toString());
    if (filters.assignedUserId) params = params.set('assignedUserId', filters.assignedUserId);
    if (filters.search) params = params.set('search', filters.search);

    return this.http.get<TicketPagedResult>(this.baseUrl, { params });
  }

  getById(id: string): Observable<Ticket> {
    return this.http.get<Ticket>(`${this.baseUrl}/${id}`);
  }

  create(request: CreateTicketRequest): Observable<Ticket> {
    return this.http.post<Ticket>(this.baseUrl, request);
  }

  update(id: string, request: UpdateTicketRequest): Observable<Ticket> {
    return this.http.put<Ticket>(`${this.baseUrl}/${id}`, request);
  }

  delete(id: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${id}`);
  }

  // ── Workflow ──

  changeStatus(id: string, status: TicketStatus, reason?: string): Observable<Ticket> {
    return this.http.put<Ticket>(`${this.baseUrl}/${id}/status`, { status, reason });
  }

  assign(id: string, assignedUserId?: string | null, assignedGroupId?: string | null): Observable<Ticket> {
    return this.http.put<Ticket>(`${this.baseUrl}/${id}/assign`, {
      assignedUserId: assignedUserId ?? null,
      assignedGroupId: assignedGroupId ?? null,
    });
  }

  escalate(id: string): Observable<Ticket> {
    return this.http.put<Ticket>(`${this.baseUrl}/${id}/escalate`, {});
  }

  solve(id: string, solution?: string): Observable<Ticket> {
    return this.http.put<Ticket>(`${this.baseUrl}/${id}/solve`, { solution });
  }

  close(id: string): Observable<Ticket> {
    return this.http.put<Ticket>(`${this.baseUrl}/${id}/close`, {});
  }

  // ── History ──

  getHistory(id: string): Observable<TicketHistoryEntry[]> {
    return this.http.get<TicketHistoryEntry[]>(`${this.baseUrl}/${id}/history`);
  }

  // ── Counters ──

  getOpenCount(): Observable<{ count: number }> {
    return this.http.get<{ count: number }>(`${this.baseUrl}/count/open`);
  }

  getOverdueCount(): Observable<{ count: number }> {
    return this.http.get<{ count: number }>(`${this.baseUrl}/count/overdue`);
  }

  // ── Tasks / Followups ──

  getTasks(ticketId: string): Observable<TicketTask[]> {
    return this.http.get<TicketTask[]>(`${this.baseUrl}/${ticketId}/tasks`);
  }

  createTask(ticketId: string, request: CreateTicketTaskRequest): Observable<TicketTask> {
    return this.http.post<TicketTask>(`${this.baseUrl}/${ticketId}/tasks`, request);
  }

  deleteTask(ticketId: string, taskId: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${ticketId}/tasks/${taskId}`);
  }

  // ── Templates ──

  getTemplates(includeInactive = false): Observable<TicketTemplate[]> {
    const params = new HttpParams().set('includeInactive', includeInactive.toString());
    return this.http.get<TicketTemplate[]>(`${environment.apiUrl}/v1/ticket-templates`, { params });
  }
}
