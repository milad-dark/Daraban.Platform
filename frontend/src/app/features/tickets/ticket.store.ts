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
import { TicketService } from './ticket.service';
import {
  Ticket,
  TicketListItem,
  TicketTask,
  TicketTemplate,
  TicketHistoryEntry,
  TicketStatus,
  TicketType,
  TicketPriority,
  CreateTicketRequest,
  UpdateTicketRequest,
  CreateTicketTaskRequest,
} from './models/ticket.models';

interface TicketFilters {
  type: TicketType | null;
  status: TicketStatus | null;
  priority: TicketPriority | null;
  search: string | null;
  myTicketsOnly: boolean;
}

interface TicketState {
  // List
  items: TicketListItem[];
  totalCount: number;
  page: number;
  pageSize: number;
  // Selected ticket
  selectedTicket: Ticket | null;
  tasks: TicketTask[];
  history: TicketHistoryEntry[];
  // Reference data
  templates: TicketTemplate[];
  // Filters
  filters: TicketFilters;
  // Loading / error
  isLoading: boolean;
  isLoadingDetail: boolean;
  isSaving: boolean;
  error: string | null;
}

const initialState: TicketState = {
  items: [],
  totalCount: 0,
  page: 1,
  pageSize: 20,
  selectedTicket: null,
  tasks: [],
  history: [],
  templates: [],
  filters: { type: null, status: null, priority: null, search: null, myTicketsOnly: false },
  isLoading: false,
  isLoadingDetail: false,
  isSaving: false,
  error: null,
};

export const TicketStore = signalStore(
  { providedIn: 'root' },

  withState<TicketState>(initialState),

  withComputed((store) => ({
    totalPages: computed(() => Math.ceil(store.totalCount() / store.pageSize())),
    hasItems: computed(() => store.items().length > 0),
    openCount: computed(
      () =>
        store.items().filter(
          (t) =>
            t.status === TicketStatus.New ||
            t.status === TicketStatus.Assigned ||
            t.status === TicketStatus.InProgress
        ).length
    ),
  })),

  withMethods((store, ticketService = inject(TicketService)) => ({
    // ── List ──
    async loadTickets(): Promise<void> {
      patchState(store, { isLoading: true, error: null });
      try {
        const f = store.filters();
        const result = await firstValueFrom(
          ticketService.getPaged({
            type: f.type ?? undefined,
            status: f.status ?? undefined,
            priority: f.priority ?? undefined,
            search: f.search ?? undefined,
            assignedUserId: f.myTicketsOnly ? 'me' : undefined,
            page: store.page(),
            pageSize: store.pageSize(),
          })
        );
        patchState(store, {
          items: result.items,
          totalCount: result.totalCount,
          page: result.page,
          pageSize: result.pageSize,
          isLoading: false,
        });
      } catch (err: unknown) {
        patchState(store, { isLoading: false, error: extractError(err) });
      }
    },

    // ── Detail ──
    async loadTicket(id: string): Promise<void> {
      patchState(store, { isLoadingDetail: true, error: null });
      try {
        const [ticket, tasks, history] = await Promise.all([
          firstValueFrom(ticketService.getById(id)),
          firstValueFrom(ticketService.getTasks(id)),
          firstValueFrom(ticketService.getHistory(id)),
        ]);
        patchState(store, {
          selectedTicket: ticket,
          tasks,
          history,
          isLoadingDetail: false,
        });
      } catch (err: unknown) {
        patchState(store, { isLoadingDetail: false, error: extractError(err) });
      }
    },

    clearSelectedTicket(): void {
      patchState(store, { selectedTicket: null, tasks: [], history: [] });
    },

    // ── Reference data ──
    async loadTemplates(): Promise<void> {
      try {
        const templates = await firstValueFrom(ticketService.getTemplates());
        patchState(store, { templates });
      } catch {
        // Non-critical — templates are optional.
      }
    },

    // ── CRUD ──
    async createTicket(request: CreateTicketRequest): Promise<string | null> {
      patchState(store, { isSaving: true, error: null });
      try {
        const result = await firstValueFrom(ticketService.create(request));
        patchState(store, { isSaving: false });
        return result.id;
      } catch (err: unknown) {
        patchState(store, { isSaving: false, error: extractError(err) });
        return null;
      }
    },

    async updateTicket(id: string, request: UpdateTicketRequest): Promise<boolean> {
      patchState(store, { isSaving: true, error: null });
      try {
        const updated = await firstValueFrom(ticketService.update(id, request));
        patchState(store, { isSaving: false, selectedTicket: updated });
        return true;
      } catch (err: unknown) {
        patchState(store, { isSaving: false, error: extractError(err) });
        return false;
      }
    },

    async deleteTicket(id: string): Promise<boolean> {
      patchState(store, { isSaving: true, error: null });
      try {
        await firstValueFrom(ticketService.delete(id));
        patchState(store, { isSaving: false });
        return true;
      } catch (err: unknown) {
        patchState(store, { isSaving: false, error: extractError(err) });
        return false;
      }
    },

    clearError(): void {
      patchState(store, { error: null });
    },
  })),

  // Second withMethods feature: these methods call loadTicket/loadTickets, which
  // live in the first feature — methods of the same feature can't see each other,
  // so workflow + pagination live here where the full store type is visible.
  withMethods((store, ticketService = inject(TicketService)) => ({
    // ── Pagination / filters ──
    setPage(page: number): void {
      patchState(store, { page });
      store.loadTickets();
    },

    setPageSize(pageSize: number): void {
      patchState(store, { pageSize, page: 1 });
      store.loadTickets();
    },

    updateFilters(filters: Partial<TicketFilters>): void {
      patchState(store, {
        filters: { ...store.filters(), ...filters },
        page: 1,
      });
      store.loadTickets();
    },

    clearFilters(): void {
      patchState(store, { filters: initialState.filters, page: 1 });
      store.loadTickets();
    },

    // ── Workflow ──
    async changeStatus(id: string, status: TicketStatus, reason?: string): Promise<boolean> {
      patchState(store, { isSaving: true, error: null });
      try {
        await firstValueFrom(ticketService.changeStatus(id, status, reason));
        await store.loadTicket(id);
        return true;
      } catch (err: unknown) {
        patchState(store, { isSaving: false, error: extractError(err) });
        return false;
      }
    },

    async assign(id: string, assignedUserId?: string | null, assignedGroupId?: string | null): Promise<boolean> {
      patchState(store, { isSaving: true, error: null });
      try {
        await firstValueFrom(ticketService.assign(id, assignedUserId, assignedGroupId));
        await store.loadTicket(id);
        return true;
      } catch (err: unknown) {
        patchState(store, { isSaving: false, error: extractError(err) });
        return false;
      }
    },

    async escalate(id: string): Promise<boolean> {
      patchState(store, { isSaving: true, error: null });
      try {
        await firstValueFrom(ticketService.escalate(id));
        await store.loadTicket(id);
        return true;
      } catch (err: unknown) {
        patchState(store, { isSaving: false, error: extractError(err) });
        return false;
      }
    },

    async solve(id: string, solution?: string): Promise<boolean> {
      patchState(store, { isSaving: true, error: null });
      try {
        await firstValueFrom(ticketService.solve(id, solution));
        await store.loadTicket(id);
        return true;
      } catch (err: unknown) {
        patchState(store, { isSaving: false, error: extractError(err) });
        return false;
      }
    },

    async close(id: string): Promise<boolean> {
      patchState(store, { isSaving: true, error: null });
      try {
        await firstValueFrom(ticketService.close(id));
        await store.loadTicket(id);
        return true;
      } catch (err: unknown) {
        patchState(store, { isSaving: false, error: extractError(err) });
        return false;
      }
    },

    // ── Tasks / Followups ──
    async addTask(ticketId: string, request: CreateTicketTaskRequest): Promise<boolean> {
      patchState(store, { isSaving: true, error: null });
      try {
        await firstValueFrom(ticketService.createTask(ticketId, request));
        const tasks = await firstValueFrom(ticketService.getTasks(ticketId));
        patchState(store, { tasks, isSaving: false });
        return true;
      } catch (err: unknown) {
        patchState(store, { isSaving: false, error: extractError(err) });
        return false;
      }
    },

    async deleteTask(ticketId: string, taskId: string): Promise<boolean> {
      patchState(store, { isSaving: true, error: null });
      try {
        await firstValueFrom(ticketService.deleteTask(ticketId, taskId));
        const tasks = await firstValueFrom(ticketService.getTasks(ticketId));
        patchState(store, { tasks, isSaving: false });
        return true;
      } catch (err: unknown) {
        patchState(store, { isSaving: false, error: extractError(err) });
        return false;
      }
    },

    /** Called by SignalR when a followup arrives so the thread updates live. */
    refreshTasks(ticketId: string): void {
      firstValueFrom(ticketService.getTasks(ticketId))
        .then((tasks) => patchState(store, { tasks }))
        .catch(() => undefined);
    },
  }))
);
