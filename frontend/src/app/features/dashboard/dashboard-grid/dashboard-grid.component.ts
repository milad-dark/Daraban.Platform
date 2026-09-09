import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  input,
  output,
} from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatMenuModule, MatMenu } from '@angular/material/menu';
import { MatTooltipModule } from '@angular/material/tooltip';
import {
  CdkDragDrop,
  DragDropModule,
  moveItemInArray,
} from '@angular/cdk/drag-drop';
import { WidgetDefinition, WidgetPlacement } from '../dashboard.models';
import { DashboardStore } from '../dashboard.store';
import { WidgetHostComponent } from '../widget-host/widget-host.component';

/**
 * The dashboard grid (Task 7.1): a 12-column CSS grid with CDK drag-to-reorder. Editing is
 * opt-in via `editing` -- when off, widgets render statically and the drag handle is hidden,
 * so an accidental drag cannot silently reorder a user's dashboard.
 */
@Component({
  selector: 'app-dashboard-grid',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DragDropModule, MatIconModule, MatButtonModule, MatMenuModule, MatTooltipModule, WidgetHostComponent],
  template: `
    <div class="toolbar">
      <div class="actions">
        @if (editing()) {
          <button
            mat-button
            class="add-widget"
            [matMenuTriggerFor]="widgetMenu"
            type="button"
          >
            <mat-icon>add</mat-icon> Add widget
          </button>
          <button
            mat-flat-button
            color="primary"
            (click)="save.emit()"
            [disabled]="store.saving()"
            type="button"
          >
            {{ store.saving() ? 'Saving…' : 'Save layout' }}
          </button>
          <button mat-button (click)="editEnd.emit()" type="button">Done</button>
        } @else {
          <button mat-icon-button (click)="editStart.emit()" matTooltip="Edit layout" type="button">
            <mat-icon>edit</mat-icon>
          </button>
        }
      </div>
    </div>

    <!-- Menu lives in the template; defined inline to avoid a separate file. -->
    <mat-menu #widgetMenu="matMenu">
      @for (w of availableWidgets(); track w.name) {
        <button mat-menu-item (click)="addWidget.emit(w)">{{ w.name }}</button>
      }
    </mat-menu>

    <div
      cdkDropList
      cdkDropListOrientation="mixed"
      class="grid"
      (cdkDropListDropped)="onDrop($event)"
    >
      @for (entry of placed(); track entry.placement.widgetType; let i = $index) {
        <div
          class="card"
          cdkDrag
          [cdkDragDisabled]="!editing()"
          [style.grid-column]="span(entry.placement)"
          [style.grid-row]="'span ' + entry.placement.h"
        >
          <div class="card-header">
            <span class="title">{{ entry.definition?.name }}</span>
            @if (editing()) {
              <div class="card-actions">
                <mat-icon cdkDragHandle class="handle" title="Drag to move">drag_indicator</mat-icon>
                <button
                  mat-icon-button
                  class="remove"
                  (click)="removeWidget.emit(entry.placement.widgetType)"
                  matTooltip="Remove widget"
                  type="button"
                >
                  <mat-icon>close</mat-icon>
                </button>
              </div>
            }
          </div>
          <div class="card-body">
            <app-widget-host [widgetType]="entry.placement.widgetType" />
          </div>
        </div>
      } @empty {
        <div class="empty">
          <mat-icon>dashboard_customize</mat-icon>
          <p>No widgets yet. Start editing to add some.</p>
        </div>
      }
    </div>
  `,
  styles: [
    `
      .toolbar {
        display: flex;
        justify-content: flex-end;
        margin-bottom: 8px;
      }
      .actions {
        display: flex;
        gap: 8px;
        align-items: center;
      }
      .grid {
        display: grid;
        grid-template-columns: repeat(12, 1fr);
        grid-auto-rows: 90px;
        gap: 12px;
      }
      .card {
        background: var(--mat-sys-surface, #fff);
        border: 1px solid var(--mat-sys-outline-variant, #e0e0e0);
        border-radius: 12px;
        display: flex;
        flex-direction: column;
        overflow: hidden;
      }
      .cdk-drag-preview .card {
        box-shadow: 0 4px 16px rgba(0, 0, 0, 0.2);
      }
      .card-header {
        display: flex;
        align-items: center;
        justify-content: space-between;
        padding: 8px 12px 0;
      }
      .title {
        font-size: 13px;
        font-weight: 600;
        color: var(--mat-sys-on-surface-variant, #5f6368);
        letter-spacing: 0.3px;
      }
      .card-actions {
        display: flex;
        align-items: center;
        gap: 2px;
      }
      .handle {
        cursor: grab;
        color: var(--mat-sys-on-surface-variant, #9e9e9e);
      }
      .remove {
        width: 28px;
        height: 28px;
        line-height: 28px;
      }
      .remove .mat-icon {
        width: 18px;
        height: 18px;
        font-size: 18px;
      }
      .card-body {
        flex: 1;
        min-height: 0;
        padding: 4px 12px 12px;
      }
      .empty {
        grid-column: 1 / -1;
        display: flex;
        flex-direction: column;
        align-items: center;
        gap: 8px;
        color: var(--mat-sys-on-surface-variant, #757575);
        padding: 48px 0;
      }
    `,
  ],
})
export class DashboardGridComponent {
  protected readonly store = inject(DashboardStore);

  editing = input.required<boolean>();
  editStart = output<void>();
  editEnd = output<void>();
  save = output<void>();
  addWidget = output<WidgetDefinition>();
  removeWidget = output<string>();

  /** Catalog entries not yet placed -- the "Add widget" menu shows only these. */
  readonly availableWidgets = computed(() => {
    const placed = new Set(this.store.layout().map((p) => p.widgetType));
    return this.store.widgets().filter((w) => !placed.has(w.name.replace(/ /g, '')));
  });

  readonly placed = computed(() => this.store.placedWidgets());

  /** CSS grid span from a placement: width in columns of 12. */
  span(p: WidgetPlacement): string {
    return `span ${Math.min(Math.max(p.w, 1), 12)}`;
  }

  onDrop(event: CdkDragDrop<unknown>): void {
    const layout = [...this.store.layout()];
    const from = this.currentIndex(event.previousIndex);
    const to = this.currentIndex(event.currentIndex);
    if (from === -1 || to === -1) return;
    moveItemInArray(layout, from, to);
    // Keep grid coordinates roughly in sync so saved placements preserve visual order.
    this.store.setLayout(layout);
  }

  /** CDK reports preview indices; our store order is the render order. */
  private currentIndex(dropIndex: number | undefined): number {
    if (dropIndex === undefined || dropIndex < 0) return -1;
    return dropIndex < this.store.layout().length ? dropIndex : -1;
  }
}
