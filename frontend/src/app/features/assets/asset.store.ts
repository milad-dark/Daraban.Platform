import { inject, computed } from '@angular/core';
import {
  signalStore,
  withState,
  withMethods,
  withComputed,
  patchState,
} from '@ngrx/signals';
import { firstValueFrom } from 'rxjs';
import { AssetService } from './asset.service';
import {
  Asset,
  AssetList,
  AssetType,
  Location,
  AssetAssignment,
  AssetStatusHistoryEntry,
  AssetStatus,
} from './models/asset.model';

interface AssetFilters {
  status: string | null;
  assetTypeId: string | null;
  locationId: string | null;
  search: string | null;
}

interface AssetState {
  // List
  items: AssetList[];
  totalCount: number;
  page: number;
  pageSize: number;
  // Selected asset
  selectedAsset: Asset | null;
  assignments: AssetAssignment[];
  lifecycleHistory: AssetStatusHistoryEntry[];
  // Reference data
  assetTypes: AssetType[];
  locations: Location[];
  // Filters
  filters: AssetFilters;
  // Loading / error
  isLoading: boolean;
  isLoadingDetail: boolean;
  isSaving: boolean;
  error: string | null;
}

const initialState: AssetState = {
  items: [],
  totalCount: 0,
  page: 1,
  pageSize: 20,
  selectedAsset: null,
  assignments: [],
  lifecycleHistory: [],
  assetTypes: [],
  locations: [],
  filters: { status: null, assetTypeId: null, locationId: null, search: null },
  isLoading: false,
  isLoadingDetail: false,
  isSaving: false,
  error: null,
};

export const AssetStore = signalStore(
  { providedIn: 'root' },

  withState<AssetState>(initialState),

  withComputed((store) => ({
    totalPages: computed(() =>
      Math.ceil(store.totalCount() / store.pageSize())
    ),
    hasItems: computed(() => store.items().length > 0),
    statusCounts: computed(() => {
      const counts: Record<string, number> = {};
      for (const item of store.items()) {
        counts[item.status] = (counts[item.status] || 0) + 1;
      }
      return counts;
    }),
  })),

  withMethods((store, assetService = inject(AssetService)) => ({
    // ── List ──
    async loadAssets(): Promise<void> {
      patchState(store, { isLoading: true, error: null });
      try {
        const f = store.filters();
        const result = await firstValueFrom(
          assetService.getPaged(
            f.status ?? undefined,
            f.assetTypeId ?? undefined,
            f.locationId ?? undefined,
            f.search ?? undefined,
            store.page(),
            store.pageSize()
          )
        );
        patchState(store, {
          items: result.items,
          totalCount: result.totalCount,
          page: result.page,
          pageSize: result.pageSize,
          isLoading: false,
        });
      } catch (err: unknown) {
        patchState(store, {
          isLoading: false,
          error: extractError(err),
        });
      }
    },

    setPage(page: number): void {
      patchState(store, { page });
      store.loadAssets();
    },

    setPageSize(pageSize: number): void {
      patchState(store, { pageSize, page: 1 });
      store.loadAssets();
    },

    updateFilters(filters: Partial<AssetFilters>): void {
      patchState(store, {
        filters: { ...store.filters(), ...filters },
        page: 1,
      });
      store.loadAssets();
    },

    clearFilters(): void {
      patchState(store, { filters: initialState.filters, page: 1 });
      store.loadAssets();
    },

    // ── Detail ──
    async loadAsset(id: string): Promise<void> {
      patchState(store, { isLoadingDetail: true, error: null });
      try {
        const [asset, assignments, history] = await Promise.all([
          firstValueFrom(assetService.getById(id)),
          firstValueFrom(assetService.getAssignmentHistory(id)),
          firstValueFrom(assetService.getLifecycleHistory(id)),
        ]);
        patchState(store, {
          selectedAsset: asset,
          assignments,
          lifecycleHistory: history,
          isLoadingDetail: false,
        });
      } catch (err: unknown) {
        patchState(store, {
          isLoadingDetail: false,
          error: extractError(err),
        });
      }
    },

    clearSelectedAsset(): void {
      patchState(store, {
        selectedAsset: null,
        assignments: [],
        lifecycleHistory: [],
      });
    },

    // ── Reference data ──
    async loadReferenceData(): Promise<void> {
      try {
        const [assetTypes, locations] = await Promise.all([
          firstValueFrom(assetService.getAssetTypes()),
          firstValueFrom(assetService.getLocations()),
        ]);
        patchState(store, { assetTypes, locations });
      } catch {
        // Reference data load failure is non-critical
      }
    },

    // ── CRUD ──
    async createAsset(request: Parameters<AssetService['create']>[0]): Promise<string | null> {
      patchState(store, { isSaving: true, error: null });
      try {
        const result = await firstValueFrom(assetService.create(request));
        patchState(store, { isSaving: false });
        return result.id;
      } catch (err: unknown) {
        patchState(store, { isSaving: false, error: extractError(err) });
        return null;
      }
    },

    async updateAsset(
      id: string,
      request: Parameters<AssetService['update']>[1]
    ): Promise<boolean> {
      patchState(store, { isSaving: true, error: null });
      try {
        await firstValueFrom(assetService.update(id, request));
        patchState(store, { isSaving: false });
        return true;
      } catch (err: unknown) {
        patchState(store, { isSaving: false, error: extractError(err) });
        return false;
      }
    },

    async deleteAsset(id: string): Promise<boolean> {
      patchState(store, { isSaving: true, error: null });
      try {
        await firstValueFrom(assetService.delete(id));
        patchState(store, { isSaving: false });
        return true;
      } catch (err: unknown) {
        patchState(store, { isSaving: false, error: extractError(err) });
        return false;
      }
    },

    // ── Lifecycle ──
    async transitionAsset(
      assetId: string,
      toStatus: AssetStatus,
      reason?: string,
      notes?: string
    ): Promise<boolean> {
      patchState(store, { isSaving: true, error: null });
      try {
        await firstValueFrom(
          assetService.transition(assetId, toStatus, reason, notes)
        );
        // Reload detail to reflect new status
        await store.loadAsset(assetId);
        return true;
      } catch (err: unknown) {
        patchState(store, { isSaving: false, error: extractError(err) });
        return false;
      }
    },

    clearError(): void {
      patchState(store, { error: null });
    },
  }))
);

function extractError(err: unknown): string {
  if (err && typeof err === 'object' && 'error' in err) {
    const httpError = err as { error: { detail?: string; title?: string } };
    if (httpError.error?.detail) return httpError.error.detail;
    if (httpError.error?.title) return httpError.error.title;
  }
  return 'An unexpected error occurred.';
}
