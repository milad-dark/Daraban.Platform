import {
  ChangeDetectionStrategy,
  Component,
  OnInit,
  computed,
  inject,
  signal,
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import { DatePipe } from '@angular/common';
import { MatTabsModule } from '@angular/material/tabs';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatTooltipModule } from '@angular/material/tooltip';
import { SettingsStore } from './settings.store';
import {
  Setting,
  SettingCategory,
  CATEGORY_LABELS,
} from './models/settings.model';
import { TestConnectionButtonComponent } from './test-connection-button/test-connection-button.component';

/**
 * System settings (Task 7.4): one tab per category, one row per setting. Editing is
 * row-local -- a "Save" button per changed row, a secret-aware editor (password field
 * that round-trips the server's mask so untouched secrets stay untouched), and the
 * inline TestConnectionButton on the Email and LDAP tabs.
 *
 * Permission model: the backend gates reads behind `settings.read` and writes behind
 * `settings.write`. A user with read-only access sees the values but every save fails
 * with the API's 403 message in the error banner -- the UI does not pretend to know the
 * permission set (the platform resolves permissions server-side).
 */
@Component({
  selector: 'app-settings',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    FormsModule,
    DatePipe,
    MatTabsModule,
    MatCardModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatSlideToggleModule,
    MatButtonModule,
    MatIconModule,
    MatProgressBarModule,
    MatTooltipModule,
    TestConnectionButtonComponent,
  ],
  template: `
    <div class="settings-page">
      <div class="page-header">
        <h1>System Settings</h1>
        <span class="hint">Changes apply platform-wide and take effect immediately.</span>
      </div>

      @if (store.isLoading()) {
        <mat-progress-bar mode="indeterminate"></mat-progress-bar>
      }

      <mat-tab-group
        dynamicHeight
        animationDuration="120ms"
        [selectedIndex]="selectedTab()"
        (selectedIndexChange)="selectedTab.set($event)"
      >
        @for (category of store.categories(); track category.category) {
          <mat-tab [label]="labelFor(category.category)">
            <div class="tab-body">
              <div class="tab-toolbar">
                @if (category.category === 'email' || category.category === 'ldap') {
                  <app-test-connection-button
                    [testKey]="firstKeyOf(category)"
                  ></app-test-connection-button>
                }
              </div>

              <mat-card class="settings-card">
                @for (setting of category.settings; track setting.key) {
                  <div class="setting-row">
                    <div class="setting-meta">
                      <span class="setting-key">{{ humanize(setting.key) }}</span>
                      <span class="setting-desc">{{ setting.description }}</span>
                      <span class="setting-updated">
                        Updated {{ setting.updatedAt | date : 'MMM d, y, HH:mm' }}
                      </span>
                    </div>

                    <div class="setting-editor">
                      @switch (setting.valueType) {
                        @case ('boolean') {
                          <mat-slide-toggle
                            [ngModel]="setting.value === 'true'"
                            [disabled]="isBusy(setting)"
                            (ngModelChange)="onToggle(setting, $event)"
                          >
                          </mat-slide-toggle>
                        }
                        @case ('int') {
                          <mat-form-field appearance="outline" subscriptSizing="dynamic">
                            <input
                              matInput
                              type="number"
                              [ngModel]="setting.value"
                              [disabled]="isBusy(setting)"
                              (ngModelChange)="onEdit(setting, $event)"
                            />
                          </mat-form-field>
                        }
                        @case ('time') {
                          <mat-form-field appearance="outline" subscriptSizing="dynamic">
                            <input
                              matInput
                              type="time"
                              [ngModel]="setting.value"
                              [disabled]="isBusy(setting)"
                              (ngModelChange)="onEdit(setting, $event)"
                            />
                          </mat-form-field>
                        }
                        @case ('string') {
                          @if (setting.isSecret) {
                            <mat-form-field appearance="outline" subscriptSizing="dynamic">
                              <input
                                matInput
                                [type]="revealedKeys().has(setting.key) ? 'text' : 'password'"
                                [ngModel]="setting.value"
                                [disabled]="isBusy(setting)"
                                (ngModelChange)="onEdit(setting, $event)"
                                autocomplete="new-password"
                              />
                              <button
                                mat-icon-button
                                matSuffix
                                type="button"
                                (click)="toggleReveal(setting.key)"
                                [matTooltip]="revealedKeys().has(setting.key) ? 'Hide' : 'Show'"
                              >
                                <mat-icon>{{
                                  revealedKeys().has(setting.key) ? 'visibility_off' : 'visibility'
                                }}</mat-icon>
                              </button>
                            </mat-form-field>
                          } @else if (setting.key === 'email.smtp_tls') {
                            <mat-form-field appearance="outline" subscriptSizing="dynamic">
                              <mat-select
                                [ngModel]="setting.value"
                                [disabled]="isBusy(setting)"
                                (ngModelChange)="onEdit(setting, $event)"
                              >
                                <mat-option value="none">none</mat-option>
                                <mat-option value="starttls">STARTTLS</mat-option>
                                <mat-option value="ssl">SSL/TLS</mat-option>
                              </mat-select>
                            </mat-form-field>
                          } @else {
                            <mat-form-field appearance="outline" subscriptSizing="dynamic" class="wide">
                              <input
                                matInput
                                [ngModel]="setting.value"
                                [disabled]="isBusy(setting)"
                                (ngModelChange)="onEdit(setting, $event)"
                                [autocomplete]="setting.isSecret ? 'new-password' : 'off'"
                              />
                            </mat-form-field>
                          }
                        }
                      }

                      <button
                        mat-flat-button
                        color="primary"
                        [disabled]="!isDirty(setting) || isBusy(setting)"
                        (click)="save(setting)"
                      >
                        @if (store.isSavingKey() === setting.key) {
                          Saving...
                        } @else {
                          Save
                        }
                      </button>
                    </div>
                  </div>
                }
              </mat-card>
            </div>
          </mat-tab>
        }
      </mat-tab-group>

      @if (store.error(); as error) {
        <div class="error-banner">
          <mat-icon>error</mat-icon>
          {{ error }}
          <button mat-icon-button (click)="store.clearError()">
            <mat-icon>close</mat-icon>
          </button>
        </div>
      }
    </div>
  `,
  styles: `
    .settings-page {
      padding: 24px;
    }

    .page-header {
      display: flex;
      align-items: baseline;
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

    .tab-body {
      padding-top: 8px;
    }

    .tab-toolbar {
      display: flex;
      justify-content: flex-end;
      margin-bottom: 8px;
    }

    .settings-card {
      padding: 8px 0;
    }

    .setting-row {
      display: flex;
      align-items: center;
      gap: 24px;
      padding: 14px 20px;
      border-bottom: 1px solid #eeeeee;

      &:last-child {
        border-bottom: none;
      }
    }

    .setting-meta {
      flex: 1;
      min-width: 260px;
      display: flex;
      flex-direction: column;
      gap: 2px;
    }

    .setting-key {
      font-weight: 500;
    }

    .setting-desc {
      color: #757575;
      font-size: 12.5px;
    }

    .setting-updated {
      color: #bdbdbd;
      font-size: 11px;
    }

    .setting-editor {
      display: flex;
      align-items: center;
      gap: 12px;

      mat-form-field {
        width: 260px;

        &.wide {
          width: 320px;
        }
      }
    }

    .error-banner {
      display: flex;
      align-items: center;
      gap: 8px;
      padding: 10px 14px;
      border-radius: 4px;
      background: #fdecea;
      color: #b71c1c;
      margin-top: 16px;

      mat-icon {
        font-size: 20px;
        width: 20px;
        height: 20px;
      }
    }
  `,
})
export class SettingsComponent implements OnInit {
  protected readonly store = inject(SettingsStore);

  protected readonly selectedTab = signal(0);
  /** Edited-but-unsaved values, keyed by setting key. */
  private readonly drafts = signal<Record<string, string>>({});
  /** Secret fields currently shown as plaintext. */
  protected readonly revealedKeys = signal<ReadonlySet<string>>(new Set());

  protected readonly labelFor = (category: SettingCategory): string =>
    CATEGORY_LABELS[category] ?? category;

  ngOnInit(): void {
    this.store.loadSettings();
  }

  /** The connection test runs against saved values; any key of the category works. */
  protected firstKeyOf(category: { settings: Setting[] }): string {
    return category.settings[0]?.key ?? '';
  }

  protected humanize(key: string): string {
    const tail = key.split('.')[1] ?? key;
    return tail
      .split('_')
      .map((word) => word.charAt(0).toUpperCase() + word.slice(1))
      .join(' ');
  }

  protected isBusy(setting: Setting): boolean {
    return this.store.isSavingKey() === setting.key;
  }

  protected isDirty(setting: Setting): boolean {
    const draft = this.drafts()[setting.key];
    return draft !== undefined && draft !== setting.value;
  }

  protected onEdit(setting: Setting, value: string | number | boolean): void {
    this.drafts.update((d) => ({ ...d, [setting.key]: String(value) }));
  }

  protected onToggle(setting: Setting, checked: boolean): void {
    this.onEdit(setting, checked ? 'true' : 'false');
  }

  protected toggleReveal(key: string): void {
    this.revealedKeys.update((current) => {
      const next = new Set(current);
      if (next.has(key)) {
        next.delete(key);
      } else {
        next.add(key);
      }
      return next;
    });
  }

  protected async save(setting: Setting): Promise<void> {
    const draft = this.drafts()[setting.key];
    if (draft === undefined || draft === setting.value) {
      return;
    }

    const saved = await this.store.saveSetting(setting.key, draft);
    if (saved) {
      this.drafts.update((d) => {
        const next = { ...d };
        delete next[setting.key];
        return next;
      });
    }
  }
}
