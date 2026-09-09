import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import {
  DashboardLayoutDto,
  WidgetData,
  WidgetDefinition,
  WidgetPlacement,
} from './dashboard.models';

/** HTTP client for the dashboard API (Task 7.1). */
@Injectable({ providedIn: 'root' })
export class DashboardService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = '/api/v1/dashboard';

  /** Available widget types with display metadata. */
  getWidgets(): Observable<WidgetDefinition[]> {
    return this.http.get<WidgetDefinition[]>(`${this.baseUrl}/widgets`);
  }

  /** The calling user's saved layout (empty widgets array when none saved). */
  getLayout(): Observable<DashboardLayoutDto> {
    return this.http.get<DashboardLayoutDto>(`${this.baseUrl}/layout`);
  }

  /** Persists the calling user's layout. */
  saveLayout(widgets: WidgetPlacement[]): Observable<DashboardLayoutDto> {
    return this.http.put<DashboardLayoutDto>(`${this.baseUrl}/layout`, { widgets });
  }

  /** Data payload for one widget. */
  getWidgetData(widgetType: string): Observable<WidgetData> {
    return this.http.get<WidgetData>(
      `${this.baseUrl}/data/${encodeURIComponent(widgetType)}`
    );
  }
}
