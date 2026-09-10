import { inject, computed } from '@angular/core';
import {
  signalStore,
  withState,
  withMethods,
  withComputed,
  patchState,
} from '@ngrx/signals';
import { firstValueFrom } from 'rxjs';
import { extractError } from '../../core/utils/error.util';
import { AuditLogService } from './audit-log.service';
import { AuditLogEntry, AuditLogFilters } from './models/audit-log.model';

interface AuditLogState {
  entries: AuditLogEntry[];
  totalCount: number;
  page: number;
  pageSize: number;
  filters: AuditLogFilters;
  isLoading: boolean;
  error: string | null;
}

const initialState: AuditLogState = {
  entries: [],
  totalCount: 0,
  page: 1,
  pageSize: 20,
  filters: {
    entityType: null,
    entityId: null,
    actorUserId: null,
    action: null,
    from: null,
    to: null,
  },
  isLoading: false,
  error: null,
};

export const AuditLogStore = signalStore(
  { providedIn: 'root' },

  withState<AuditLogState>(initialState),

  withComputed((store) => ({
    totalPages: computed(() =>
      Math.max(1, Math.ceil(store.totalCount() / store.pageSize()))
    ),
    hasEntries: computed(() => store.entries().length > 0),
  })),

  withMethods((store, auditLogService = inject(AuditLogService)) => ({
    async loadLogs(): Promise<void> {
      patchState(store, { isLoading: true, error: null });
      try {
        const result = await firstValueFrom(
          auditLogService.getLogs(store.filters(), store.page(), store.pageSize())
        );
        patchState(store, {
          entries: result.items,
          totalCount: result.totalCount,
          page: result.page,
          pageSize: result.pageSize,
          isLoading: false,
        });
      } catch (err: unknown) {
        patchState(store, { isLoading: false, error: extractError(err) });
      }
    },

    clearError(): void {
      patchState(store, { error: null });
    },
  })),

  // Filter/paging methods live in a later withMethods block so they can see loadLogs on
  // the store type (same-feature methods are not visible to each other).
  withMethods((store) => ({
    setPage(page: number): void {
      patchState(store, { page });
      store.loadLogs();
    },

    setPageSize(pageSize: number): void {
      patchState(store, { pageSize, page: 1 });
      store.loadLogs();
    },

    updateFilters(filters: Partial<AuditLogFilters>): void {
      patchState(store, {
        filters: { ...store.filters(), ...filters },
        page: 1,
      });
      store.loadLogs();
    },

    clearFilters(): void {
      patchState(store, { filters: initialState.filters, page: 1 });
      store.loadLogs();
    },
  }))
);
