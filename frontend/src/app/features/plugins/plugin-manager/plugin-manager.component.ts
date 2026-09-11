import {
  ChangeDetectionStrategy,
  Component,
  OnInit,
  ElementRef,
  inject,
  viewChild,
} from '@angular/core';
import { DatePipe } from '@angular/common';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatTooltipModule } from '@angular/material/tooltip';
import { PluginsStore } from '../plugins.store';
import {
  Plugin,
  PLUGIN_TYPE_LABELS,
  PluginType,
} from '../models/plugin.model';

/** Max .zip size the backend accepts (PluginsOptions.MaxPackageSizeBytes). */
const MAX_PACKAGE_BYTES = 20 * 1024 * 1024;

/**
 * Plugin manager (Task 7.5): list / install / enable / disable / uninstall.
 *
 * Permission model: the backend gates reads behind `plugins.read` and every
 * mutating action behind `plugins.write`. The UI never pretends to know the
 * permission set -- a read-only user sees the list, and denied writes surface
 * the API's 403 message in the error banner.
 *
 * Uninstall is destructive (drops the plugin's isolated schema and deletes its
 * files; a registry tombstone remains for audit) and always confirms first.
 */
@Component({
  selector: 'app-plugin-manager',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    DatePipe,
    MatButtonModule,
    MatCardModule,
    MatIconModule,
    MatProgressBarModule,
    MatTooltipModule,
  ],
  template: `
    <div class="plugins-page">
      <div class="page-header">
        <div>
          <h1>Plugins</h1>
          <span class="hint">
            Install, enable, disable and uninstall platform plugins.
            {{ store.enabledCount() }} of {{ store.plugins().length }} enabled.
          </span>
        </div>

        <input
          #packageInput
          type="file"
          accept=".zip"
          class="package-input"
          (change)="onPackageSelected($event)"
          [disabled]="store.isInstalling()"
        />
        <button
          mat-flat-button
          color="primary"
          [disabled]="store.isInstalling()"
          (click)="packageInput.click()"
        >
          <mat-icon>upload</mat-icon>
          {{ store.isInstalling() ? 'Installing…' : 'Install plugin (.zip)' }}
        </button>
      </div>

      @if (store.isLoading()) {
        <mat-progress-bar mode="indeterminate"></mat-progress-bar>
      }

      @if (store.error(); as error) {
        <div class="error-banner">
          <mat-icon>error</mat-icon>
          {{ error }}
          <button mat-icon-button (click)="store.clearError()">
            <mat-icon>close</mat-icon>
          </button>
        </div>
      }

      @if (store.hasPlugins()) {
        <mat-card class="plugins-card">
          <table class="plugins-table">
            <thead>
              <tr>
                <th>Plugin</th>
                <th>Type</th>
                <th>Version</th>
                <th>Status</th>
                <th>Installed</th>
                <th class="actions-col">Actions</th>
              </tr>
            </thead>
            <tbody>
              @for (plugin of store.plugins(); track plugin.id) {
                <tr>
                  <td>
                    <div class="plugin-name">{{ plugin.name }}</div>
                    <div class="plugin-id">{{ plugin.pluginId }}</div>
                  </td>
                  <td>
                    <span class="type-chip">{{ typeLabel(plugin.type) }}</span>
                  </td>
                  <td>{{ plugin.version }}</td>
                  <td>
                    <span
                      class="status-chip"
                      [class]="'status-' + plugin.status"
                    >
                      {{ plugin.status }}
                    </span>
                  </td>
                  <td>{{ plugin.installedAt | date : 'MMM d, y, HH:mm' }}</td>
                  <td class="actions-col">
                    @switch (plugin.status) {
                      @case ('enabled') {
                        <button
                          mat-stroked-button
                          [disabled]="isBusy(plugin)"
                          matTooltip="Unload from runtime; package and data are kept."
                          (click)="disable(plugin)"
                        >
                          Disable
                        </button>
                      }
                      @case ('disabled') {
                        <button
                          mat-flat-button
                          color="primary"
                          [disabled]="isBusy(plugin)"
                          (click)="enable(plugin)"
                        >
                          Enable
                        </button>
                      }
                    }
                    <button
                      mat-icon-button
                      class="uninstall"
                      [disabled]="isBusy(plugin)"
                      matTooltip="Uninstall: drop schema, delete files. Irreversible."
                      (click)="uninstall(plugin)"
                    >
                      <mat-icon>delete</mat-icon>
                    </button>
                  </td>
                </tr>
              }
            </tbody>
          </table>
        </mat-card>
      } @else if (!store.isLoading()) {
        <mat-card class="empty-card">
          <mat-icon class="empty-icon">extension</mat-icon>
          <p>No plugins installed.</p>
          <p class="hint">
            Package a plugin as a .zip (DLL + manifest.json + migrations) and
            upload it above. Plugins run in an isolated AssemblyLoadContext with
            their own database schema.
          </p>
        </mat-card>
      }
    </div>
  `,
  styles: `
    .plugins-page {
      padding: 24px;
    }

    .page-header {
      display: flex;
      justify-content: space-between;
      align-items: center;
      gap: 16px;
      margin-bottom: 16px;

      h1 {
        margin: 0;
        font-size: 24px;
        font-weight: 500;
      }

      .hint {
        color: #9e9e9e;
        font-size: 13px;
      }
    }

    /** Hidden but still focusable/activatable for keyboard users. */
    .package-input {
      position: absolute;
      width: 1px;
      height: 1px;
      opacity: 0;
      overflow: hidden;
    }

    .plugins-card {
      overflow-x: auto;
      padding: 0;
    }

    .plugins-table {
      width: 100%;
      border-collapse: collapse;

      th,
      td {
        text-align: left;
        padding: 12px 16px;
        border-bottom: 1px solid #eeeeee;
        font-size: 13.5px;
      }

      th {
        color: #757575;
        font-weight: 500;
      }
    }
  `,
})
export class PluginManagerComponent implements OnInit {
  protected readonly store = inject(PluginsStore);

  private readonly packageInput =
    viewChild<ElementRef<HTMLInputElement>>('packageInput');

  protected readonly typeLabel = (type: PluginType): string =>
    PLUGIN_TYPE_LABELS[type] ?? type;

  ngOnInit(): void {
    this.store.loadPlugins();
  }

  protected isBusy(plugin: Plugin): boolean {
    return this.store.actionInFlight() === plugin.pluginId;
  }

  protected async onPackageSelected(event: Event): Promise<void> {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    if (!file) {
      return;
    }

    // Client-side cap is UX only; the server independently enforces the limit.
    if (file.size > MAX_PACKAGE_BYTES) {
      alert(
        `Package is ${(file.size / (1024 * 1024)).toFixed(1)} MB; the maximum is 20 MB.`
      );
      input.value = '';
      return;
    }

    const ok = await this.store.installPlugin(file);
    input.value = '';
    if (!ok) {
      return; // error banner already carries the server's message
    }
  }

  protected async enable(plugin: Plugin): Promise<void> {
    await this.store.enablePlugin(plugin.pluginId);
  }

  protected async disable(plugin: Plugin): Promise<void> {
    await this.store.disablePlugin(plugin.pluginId);
  }

  protected async uninstall(plugin: Plugin): Promise<void> {
    if (
      !confirm(
        `Uninstall plugin "${plugin.name}"? Its database schema and files will be deleted. This cannot be undone.`
      )
    ) {
      return;
    }
    await this.store.uninstallPlugin(plugin.pluginId);
  }
}
