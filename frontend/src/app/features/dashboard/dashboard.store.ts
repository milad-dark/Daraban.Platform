import { computed, inject } from '@angular/core';
import { patchState, signalStore, withComputed, withMethods, withState } from '@ngrx/signals';
import { firstValueFrom } from 'rxjs';
import { extractError } from '../../core/utils/error.util';
import {
  DashboardLayoutDto,
  WidgetData,
  WidgetDefinition,
  WidgetPlacement,
} from './dashboard.models';
import { DashboardService } from './dashboard.service';

/**
 * Dashboard state (Task 7.2, ADR-006 pattern: one feature store per feature).
 *
 * Layout data flows server → store → grid. Widget payloads are keyed by widgetType so the
 * WidgetHostComponent can look up its data without prop-drilling. Methods that call other
 * store methods live in a later `withMethods` block, per ADR-006.
 */
export interface DashboardState {
  widgets: WidgetDefinition[];
  layout: WidgetPlacement[];
  /** Widget payloads keyed by API widget name. */
  data: Record<string, WidgetData>;
  loading: boolean;
  saving: boolean;
  /** Set when any widget request fails -- shown as a banner, per-widget errors stay local. */
  error: string | null;
}

const initialState: DashboardState = {
  widgets: [],
  layout: [],
  data: {},
  loading: false,
  saving: false,
  error: null,
};

export const DashboardStore = signalStore(
  { providedIn: 'root' },
  withState(initialState),
  withComputed((state) => ({
    /** Widgets present in the current layout, in grid order. */
    placedWidgets: computed(() => {
      const layout = state.layout();
      return layout
        .map((p) => ({
          placement: p,
          definition: state.widgets().find((w) => w.name.replace(/ /g, '') === p.widgetType),
        }))
        .filter((entry) => entry.definition !== undefined);
    }),
  })),
  withMethods((store, dashboardService = inject(DashboardService)) => ({
    /** Fetches (and caches) one widget payload. Failures are per-widget, not fatal. */
    async loadWidgetData(widgetType: string): Promise<void> {
      try {
        const data = await firstValueFrom(dashboardService.getWidgetData(widgetType));
        patchState(store, { data: { ...store.data(), [widgetType]: data } });
      } catch {
        // Absent key => widget renders its empty state. A broken widget must not
        // take the dashboard down with it.
      }
    },

    /** Replaces the local layout (drag/drop/resize) -- persisted only on saveLayout(). */
    setLayout(layout: WidgetPlacement[]): void {
      patchState(store, { layout });
    },

    /** Persists the current layout server-side. */
    async saveLayout(): Promise<void> {
      patchState(store, { saving: true, error: null });
      try {
        const saved = await firstValueFrom(dashboardService.saveLayout(store.layout()));
        patchState(store, { layout: saved.widgets, saving: false });
      } catch (err) {
        patchState(store, { saving: false, error: extractError(err) });
      }
    },

    /** Clears the current error banner. */
    clearError(): void {
      patchState(store, { error: null });
    },
  })),
  withMethods((store, dashboardService = inject(DashboardService)) => ({
    /** Loads widget catalog + saved layout, then fetches data for every placed widget. */
    async load(): Promise<void> {
      patchState(store, { loading: true, error: null });
      try {
        const [widgets, layout] = await Promise.all([
          firstValueFrom(dashboardService.getWidgets()),
          firstValueFrom(dashboardService.getLayout()),
        ]);
        patchState(store, { widgets, layout: layout.widgets, loading: false });
        await Promise.all(layout.widgets.map((p) => store.loadWidgetData(p.widgetType)));
      } catch (err) {
        patchState(store, { loading: false, error: extractError(err) });
      }
    },
  }))
);
