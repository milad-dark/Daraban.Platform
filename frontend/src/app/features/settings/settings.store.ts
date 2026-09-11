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
import { SettingsService } from './settings.service';
import {
  SettingsByCategory,
  CategorySettings,
  Setting,
  ConnectionTestResult,
  TestState,
} from './models/settings.model';

interface SettingsState {
  categories: CategorySettings[];
  isLoading: boolean;
  isSavingKey: string | null;
  /** Per-key connection-test state for the TestConnectionButton (inline live feedback). */
  testStates: Record<string, TestState>;
  error: string | null;
}

const initialState: SettingsState = {
  categories: [],
  isLoading: false,
  isSavingKey: null,
  testStates: {},
  error: null,
};

export const SettingsStore = signalStore(
  { providedIn: 'root' },

  withState<SettingsState>(initialState),

  withComputed((store) => ({
    hasCategories: computed(() => store.categories().length > 0),
  })),

  withMethods((store, settingsService = inject(SettingsService)) => ({
    async loadSettings(): Promise<void> {
      patchState(store, { isLoading: true, error: null });
      try {
        const result = await firstValueFrom(settingsService.getAll());
        patchState(store, {
          categories: result.categories,
          isLoading: false,
        });
      } catch (err: unknown) {
        patchState(store, { isLoading: false, error: extractError(err) });
      }
    },

    async saveSetting(key: string, value: string): Promise<boolean> {
      patchState(store, { isSavingKey: key, error: null });
      try {
        const updated = await firstValueFrom(
          settingsService.update(key, { value })
        );
        patchState(store, {
          categories: store.categories().map((c) => ({
            ...c,
            settings: c.settings.map((s) =>
              s.key === key ? updated : s
            ),
          })),
          isSavingKey: null,
        });
        return true;
      } catch (err: unknown) {
        patchState(store, { isSavingKey: null, error: extractError(err) });
        return false;
      }
    },

    clearError(): void {
      patchState(store, { error: null });
    },
  })),

  // Test-connection methods live in a later withMethods block so they can see the
  // earlier block's methods on the store type (same-feature methods are not visible
  // to each other -- platform convention).
  withMethods((store, settingsService = inject(SettingsService)) => ({
    async testConnection(key: string): Promise<void> {
      patchState(store, {
        testStates: { ...store.testStates(), [key]: { status: 'running' } },
      });
      try {
        const result = await firstValueFrom(
          settingsService.testConnection(key)
        );
        patchState(store, {
          testStates: {
            ...store.testStates(),
            [key]: { status: result.success ? 'success' : 'failed', result },
          },
        });
      } catch (err: unknown) {
        patchState(store, {
          testStates: {
            ...store.testStates(),
            [key]: {
              status: 'failed',
              result: { success: false, message: extractError(err), latencyMs: 0 },
            },
          },
        });
      }
    },
  }))
);
