import { inject, computed } from '@angular/core';
import {
  signalStore,
  withState,
  withMethods,
  withComputed,
  patchState,
  WritableStateSource,
} from '@ngrx/signals';
import { firstValueFrom } from 'rxjs';
import { extractError } from '../../core/utils/error.util';
import { PluginsService } from './plugins.service';
import { Plugin } from './models/plugin.model';

interface PluginsState {
  plugins: Plugin[];
  isLoading: boolean;
  /** True while a package upload is in flight. */
  isInstalling: boolean;
  /** pluginId with an action (enable/disable/uninstall) in flight. */
  actionInFlight: string | null;
  error: string | null;
}

const initialState: PluginsState = {
  plugins: [],
  isLoading: false,
  isInstalling: false,
  actionInFlight: null,
  error: null,
};

/**
 * State for the plugin manager screen. Actions return boolean success so the
 * component can collapse its per-row busy state; failures land in the error
 * banner via extractError (backend ProblemDetails codes like PLUGINS.INVALID_STATE
 * arrive as readable messages).
 */
export const PluginsStore = signalStore(
  { providedIn: 'root' },

  withState<PluginsState>(initialState),

  withComputed((store) => ({
    hasPlugins: computed(() => store.plugins().length > 0),
    enabledCount: computed(
      () => store.plugins().filter((p) => p.status === 'enabled').length
    ),
  })),

  withMethods((store, pluginsService = inject(PluginsService)) => ({
    async loadPlugins(): Promise<void> {
      patchState(store, { isLoading: true, error: null });
      try {
        const plugins = await firstValueFrom(pluginsService.list());
        patchState(store, { plugins, isLoading: false });
      } catch (err: unknown) {
        patchState(store, { isLoading: false, error: extractError(err) });
      }
    },

    async installPlugin(packageFile: File): Promise<boolean> {
      patchState(store, { isInstalling: true, error: null });
      try {
        const installed = await firstValueFrom(
          pluginsService.install(packageFile)
        );
        patchState(store, {
          plugins: [...store.plugins(), installed],
          isInstalling: false,
        });
        return true;
      } catch (err: unknown) {
        patchState(store, { isInstalling: false, error: extractError(err) });
        return false;
      }
    },
  })),

  // Row actions live in a second withMethods block (same-feature methods are
  // not visible to each other -- platform convention, see SettingsStore).
  withMethods((store, pluginsService = inject(PluginsService)) => ({
    async enablePlugin(pluginId: string): Promise<boolean> {
      return applyRowAction(store, pluginId, () =>
        pluginsService.enable(pluginId)
      );
    },

    async disablePlugin(pluginId: string): Promise<boolean> {
      return applyRowAction(store, pluginId, () =>
        pluginsService.disable(pluginId)
      );
    },

    async uninstallPlugin(pluginId: string): Promise<boolean> {
      return applyRowAction(store, pluginId, () =>
        pluginsService.uninstall(pluginId)
      );
    },

    clearError(): void {
      patchState(store, { error: null });
    },
  }))
);

/** Shared engine for enable/disable/uninstall: mark the row busy, swap the row
 * on success, surface the backend's message on failure. Typed as the store view
 * patchState needs ([STATE_SOURCE] plus the two accessors we touch). */
type PluginsStoreSource = WritableStateSource<PluginsState> & {
  plugins: () => Plugin[];
  actionInFlight: () => string | null;
};

async function applyRowAction(
  store: PluginsStoreSource,
  pluginId: string,
  action: () => ReturnType<PluginsService['enable']>
): Promise<boolean> {
  patchState(store, { actionInFlight: pluginId, error: null });
  try {
    const updated = await firstValueFrom(action());
    patchState(store, {
      plugins: store.plugins().map((p) =>
        p.pluginId === updated.pluginId ? updated : p
      ),
      actionInFlight: null,
    });
    return true;
  } catch (err: unknown) {
    patchState(store, { actionInFlight: null, error: extractError(err) });
    return false;
  }
}
