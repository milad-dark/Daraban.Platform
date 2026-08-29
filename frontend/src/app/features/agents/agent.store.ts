import { inject, computed } from '@angular/core';
import {
  signalStore,
  withState,
  withMethods,
  withComputed,
  patchState,
} from '@ngrx/signals';
import { firstValueFrom } from 'rxjs';
import { AgentService } from './agent.service';
import {
  AgentListItem,
  AgentDetail,
  AgentFleetSummary,
  AgentInventorySnapshot,
  AgentCommandHistoryEntry,
} from './models/agent.model';

interface AgentFilters {
  status: string | null;
  type: string | null;
  search: string | null;
}

interface AgentDashboardState {
  // Fleet summary
  fleetSummary: AgentFleetSummary | null;

  // Agent list
  agents: AgentListItem[];
  totalCount: number;
  page: number;
  pageSize: number;
  filters: AgentFilters;

  // Agent detail
  selectedAgent: AgentDetail | null;
  commandHistory: AgentCommandHistoryEntry[];
  commandHistoryTotal: number;
  commandHistoryPage: number;
  inventorySnapshot: AgentInventorySnapshot | null;

  // Loading / error
  isLoadingList: boolean;
  isLoadingDetail: boolean;
  isLoadingCommands: boolean;
  isLoadingInventory: boolean;
  isDispatching: boolean;
  error: string | null;
}

const initialState: AgentDashboardState = {
  fleetSummary: null,
  agents: [],
  totalCount: 0,
  page: 1,
  pageSize: 20,
  filters: { status: null, type: null, search: null },
  selectedAgent: null,
  commandHistory: [],
  commandHistoryTotal: 0,
  commandHistoryPage: 1,
  inventorySnapshot: null,
  isLoadingList: false,
  isLoadingDetail: false,
  isLoadingCommands: false,
  isLoadingInventory: false,
  isDispatching: false,
  error: null,
};

export const AgentStore = signalStore(
  { providedIn: 'root' },

  withState<AgentDashboardState>(initialState),

  withComputed((store) => ({
    totalPages: computed(() =>
      Math.ceil(store.totalCount() / store.pageSize())
    ),
    hasAgents: computed(() => store.agents().length > 0),
    onlineCount: computed(() =>
      store.agents().filter((a) => a.isOnline).length
    ),
    offlineCount: computed(() =>
      store.agents().filter((a) => !a.isOnline && a.status === 'Active').length
    ),
  })),

  withMethods(
    (
      store,
      agentService = inject(AgentService)
    ) => ({
      // ── Fleet Summary ──
      async loadFleetSummary(): Promise<void> {
        try {
          const summary = await firstValueFrom(agentService.getSummary());
          patchState(store, { fleetSummary: summary });
        } catch {
          // Non-critical — dashboard still works without summary cards
        }
      },

      // ── Agent List ──
      async loadAgents(): Promise<void> {
        patchState(store, { isLoadingList: true, error: null });
        try {
          const f = store.filters();
          const result = await firstValueFrom(
            agentService.getAgents(
              f.status ?? undefined,
              f.type ?? undefined,
              f.search ?? undefined,
              store.page(),
              store.pageSize()
            )
          );
          patchState(store, {
            agents: result.items,
            totalCount: result.totalCount,
            page: result.page,
            pageSize: result.pageSize,
            isLoadingList: false,
          });
        } catch (err: unknown) {
          patchState(store, {
            isLoadingList: false,
            error: extractError(err),
          });
        }
      },

      setPage(page: number): void {
        patchState(store, { page });
        store.loadAgents();
      },

      setPageSize(pageSize: number): void {
        patchState(store, { pageSize, page: 1 });
        store.loadAgents();
      },

      updateFilters(filters: Partial<AgentFilters>): void {
        patchState(store, {
          filters: { ...store.filters(), ...filters },
          page: 1,
        });
        store.loadAgents();
      },

      clearFilters(): void {
        patchState(store, { filters: initialState.filters, page: 1 });
        store.loadAgents();
      },

      // ── Agent Detail ──
      async loadAgentDetail(agentId: string): Promise<void> {
        patchState(store, { isLoadingDetail: true, error: null });
        try {
          const detail = await firstValueFrom(
            agentService.getDetail(agentId)
          );
          patchState(store, {
            selectedAgent: detail,
            isLoadingDetail: false,
          });
        } catch (err: unknown) {
          patchState(store, {
            isLoadingDetail: false,
            error: extractError(err),
          });
        }
      },

      clearSelectedAgent(): void {
        patchState(store, {
          selectedAgent: null,
          commandHistory: [],
          inventorySnapshot: null,
        });
      },

      // ── Command History ──
      async loadCommandHistory(agentId: string, page = 1): Promise<void> {
        patchState(store, { isLoadingCommands: true, error: null });
        try {
          const result = await firstValueFrom(
            agentService.getJobs(agentId, page, store.pageSize())
          );
          patchState(store, {
            commandHistory: result.items,
            commandHistoryTotal: result.totalCount,
            commandHistoryPage: result.page,
            isLoadingCommands: false,
          });
        } catch (err: unknown) {
          patchState(store, {
            isLoadingCommands: false,
            error: extractError(err),
          });
        }
      },

      // ── Inventory Snapshot ──
      async loadInventorySnapshot(agentId: string): Promise<void> {
        patchState(store, { isLoadingInventory: true, error: null });
        try {
          const snapshot = await firstValueFrom(
            agentService.getInventory(agentId)
          );
          patchState(store, {
            inventorySnapshot: snapshot,
            isLoadingInventory: false,
          });
        } catch (err: unknown) {
          patchState(store, {
            isLoadingInventory: false,
            error: extractError(err),
          });
        }
      },

      // ── Command Dispatch ──
      async dispatchCommand(
        agentId: string,
        commandType: string,
        payload?: string,
        timeoutSeconds?: number
      ): Promise<boolean> {
        patchState(store, { isDispatching: true, error: null });
        try {
          await firstValueFrom(
            agentService.dispatchCommand({
              agentId,
              commandType: commandType as any,
              payload,
              timeoutSeconds,
            })
          );
          patchState(store, { isDispatching: false });
          // Reload command history to show the new command
          await store.loadCommandHistory(agentId);
          return true;
        } catch (err: unknown) {
          patchState(store, {
            isDispatching: false,
            error: extractError(err),
          });
          return false;
        }
      },

      // ── Real-time update (from SignalR) ──
      updateAgentStatus(agentId: string, isOnline: boolean): void {
        const agents = store.agents().map((a) =>
          a.id === agentId ? { ...a, isOnline } : a
        );
        patchState(store, { agents });
      },

      updateCommandStatus(
        commandId: string,
        status: string
      ): void {
        const commandHistory = store.commandHistory().map((c) =>
          c.commandId === commandId
            ? { ...c, status: status as any, completedAt: new Date().toISOString() }
            : c
        );
        patchState(store, { commandHistory });
      },

      clearError(): void {
        patchState(store, { error: null });
      },
    })
  )
);

function extractError(err: unknown): string {
  if (err && typeof err === 'object' && 'error' in err) {
    const httpError = err as { error: { detail?: string; title?: string } };
    if (httpError.error?.detail) return httpError.error.detail;
    if (httpError.error?.title) return httpError.error.title;
  }
  return 'An unexpected error occurred.';
}
