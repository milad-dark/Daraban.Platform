import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { DashboardGridComponent } from './dashboard-grid/dashboard-grid.component';
import { WidgetDefinition, WidgetPlacement } from './dashboard.models';
import { DashboardStore } from './dashboard.store';

/**
 * Dashboard page (Task 7.2). Owns the edit-mode flag and maps grid events to store calls;
 * all data flows through the DashboardStore per ADR-006 (one store per feature).
 *
 * First visit: the server returns an empty layout, so we seed a sensible default locally.
 * It is only persisted when the user saves, so the server remains the single source of truth.
 */
@Component({
  selector: 'app-dashboard',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatButtonModule, MatIconModule, DashboardGridComponent],
  template: `
    @if (store.error(); as error) {
      <div class="error-banner" role="alert">
        <mat-icon inline>error_outline</mat-icon>
        <span>{{ error }}</span>
        <button mat-icon-button (click)="dismissError()" aria-label="Dismiss error" type="button">
          <mat-icon>close</mat-icon>
        </button>
      </div>
    }

    @if (store.loading()) {
      <div class="loading">
        <mat-icon class="spin">sync</mat-icon>
        <p>Loading dashboard…</p>
      </div>
    } @else {
      <app-dashboard-grid
        [editing]="editing()"
        (editStart)="editing.set(true)"
        (editEnd)="editing.set(false)"
        (save)="saveLayout()"
        (addWidget)="addWidget($event)"
        (removeWidget)="removeWidget($event)"
      />
    }
  `,
  styles: [
    `
      .error-banner {
        display: flex;
        align-items: center;
        gap: 8px;
        background: var(--mat-sys-error-container, #ffebee);
        color: var(--mat-sys-on-error-container, #b71c1c);
        border-radius: 8px;
        padding: 8px 12px;
        margin-bottom: 12px;
        font-size: 14px;
      }
      .error-banner button {
        margin-left: auto;
      }
      .loading {
        display: flex;
        flex-direction: column;
        align-items: center;
        gap: 8px;
        padding: 64px 0;
        color: var(--mat-sys-on-surface-variant, #757575);
      }
      .spin {
        animation: spin 1.2s linear infinite;
      }
      @keyframes spin {
        from {
          transform: rotate(0deg);
        }
        to {
          transform: rotate(360deg);
        }
      }
    `,
  ],
})
export class DashboardComponent {
  readonly store = inject(DashboardStore);

  readonly editing = signal(false);

  /** True while the server has no saved layout (we seed a default locally). */
  private readonly needsDefaultLayout = computed(() => this.store.layout().length === 0);

  constructor() {
    void this.store.load().then(() => {
      if (this.needsDefaultLayout()) {
        this.store.setLayout(this.defaultLayout());
      }
    });
  }

  dismissError(): void {
    this.store.clearError();
  }

  saveLayout(): void {
    void this.store.saveLayout().then(() => {
      if (!this.store.error()) {
        this.editing.set(false);
      }
    });
  }

  addWidget(definition: WidgetDefinition): void {
    const layout = [...this.store.layout()];
    const nextY = layout.reduce((max, p) => Math.max(max, p.y + p.h), 0);
    const placement: WidgetPlacement = {
      widgetType: definition.name.replace(/ /g, ''),
      x: 0,
      y: nextY,
      w: Number(definition.defaultSizeW) || 4,
      h: Number(definition.defaultSizeH) || 2,
    };
    this.store.setLayout([...layout, placement]);
    void this.store.loadWidgetData(placement.widgetType);
  }

  removeWidget(widgetType: string): void {
    this.store.setLayout(this.store.layout().filter((p) => p.widgetType !== widgetType));
  }

  /** Ordered default arrangement shown before the user saves their own layout. */
  private defaultLayout(): WidgetPlacement[] {
    return [
      { widgetType: 'OpenTicketsCount', x: 0, y: 0, w: 3, h: 2 },
      { widgetType: 'SlaComplianceRate', x: 3, y: 0, w: 3, h: 2 },
      { widgetType: 'AgentStatusSummary', x: 6, y: 0, w: 6, h: 2 },
      { widgetType: 'TicketsByStatus', x: 0, y: 2, w: 6, h: 3 },
      { widgetType: 'TicketsByPriority', x: 6, y: 2, w: 6, h: 3 },
      { widgetType: 'AssetCountByType', x: 0, y: 5, w: 6, h: 3 },
      { widgetType: 'AssetsNearingEnd', x: 6, y: 5, w: 6, h: 3 },
      { widgetType: 'RecentInventory', x: 0, y: 8, w: 12, h: 3 },
    ];
  }
}