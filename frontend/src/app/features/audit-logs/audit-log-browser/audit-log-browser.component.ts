import {
  ChangeDetectionStrategy,
  Component,
  OnInit,
  OnDestroy,
  inject,
  signal,
} from '@angular/core';
import { Subject, debounceTime, distinctUntilChanged, takeUntil } from 'rxjs';
import {
  FormControl,
  ReactiveFormsModule,
} from '@angular/forms';
import { DatePipe } from '@angular/common';
import { MatTableModule } from '@angular/material/table';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatInputModule } from '@angular/material/input';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatSelectModule } from '@angular/material/select';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatCardModule } from '@angular/material/card';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatTooltipModule } from '@angular/material/tooltip';
import { AuditLogStore } from '../audit-log.store';
import { AUDIT_ACTION_OPTIONS, AuditLogEntry } from '../models/audit-log.model';
import { DiffViewerComponent } from '../diff-viewer/diff-viewer.component';

/**
 * Audit trail browser (Task 7.3): searchable, filterable (entity / action / date range),
 * paginated table over GET /api/v1/audit-logs, with a field-level diff panel for the
 * selected row. Access is gated by the backend's `identity.auditlogs.read` permission;
 * without it the API answers 403 and the store surfaces the error banner.
 */
@Component({
  selector: 'app-audit-log-browser',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    ReactiveFormsModule,
    DatePipe,
    MatTableModule,
    MatPaginatorModule,
    MatInputModule,
    MatFormFieldModule,
    MatSelectModule,
    MatButtonModule,
    MatIconModule,
    MatCardModule,
    MatProgressBarModule,
    MatTooltipModule,
    DiffViewerComponent,
  ],
  template: `
    <div class="audit-logs-page">
      <div class="page-header">
        <h1>Audit Logs</h1>
        <button mat-button (click)="clearFilters()" [disabled]="!hasActiveFilters()">
          <mat-icon>filter_alt_off</mat-icon>
          Clear filters
        </button>
      </div>

      <!-- Filters -->
      <mat-card class="filter-card">
        <div class="filter-row">
          <mat-form-field appearance="outline" class="search-field">
            <mat-label>Entity type</mat-label>
            <input matInput [formControl]="entityTypeControl" placeholder="User, Agent, Asset..." />
            <mat-icon matSuffix>category</mat-icon>
          </mat-form-field>

          <mat-form-field appearance="outline" class="filter-field">
            <mat-label>Action</mat-label>
            <mat-select [formControl]="actionControl">
              <mat-option [value]="null">All actions</mat-option>
              @for (option of actionOptions; track option.value) {
                <mat-option [value]="option.value">{{ option.label }}</mat-option>
              }
            </mat-select>
          </mat-form-field>

          <mat-form-field appearance="outline" class="date-field">
            <mat-label>From</mat-label>
            <input matInput type="datetime-local" [formControl]="fromControl" />
          </mat-form-field>

          <mat-form-field appearance="outline" class="date-field">
            <mat-label>To</mat-label>
            <input matInput type="datetime-local" [formControl]="toControl" />
          </mat-form-field>
        </div>
      </mat-card>

      @if (store.isLoading()) {
        <mat-progress-bar mode="indeterminate"></mat-progress-bar>
      }

      <!-- Data table -->
      <mat-card class="table-card">
        <table mat-table [dataSource]="store.entries()">
          <!-- When -->
          <ng-container matColumnDef="occurredAt">
            <th mat-header-cell *matHeaderCellDef>When</th>
            <td mat-cell *matCellDef="let row" class="nowrap">
              {{ row.occurredAt | date : 'MMM d, y, HH:mm:ss' }}
            </td>
          </ng-container>

          <!-- Entity -->
          <ng-container matColumnDef="entityType">
            <th mat-header-cell *matHeaderCellDef>Entity</th>
            <td mat-cell *matCellDef="let row">
              <span class="entity-type">{{ row.entityType }}</span>
            </td>
          </ng-container>

          <!-- Entity id -->
          <ng-container matColumnDef="entityId">
            <th mat-header-cell *matHeaderCellDef>Record</th>
            <td mat-cell *matCellDef="let row">
              <code class="entity-id" [matTooltip]="row.entityId">{{ shortId(row.entityId) }}</code>
            </td>
          </ng-container>

          <!-- Action -->
          <ng-container matColumnDef="action">
            <th mat-header-cell *matHeaderCellDef>Action</th>
            <td mat-cell *matCellDef="let row">
              <span class="action-badge" [attr.data-action]="row.action">{{ row.action }}</span>
            </td>
          </ng-container>

          <!-- Actor -->
          <ng-container matColumnDef="actor">
            <th mat-header-cell *matHeaderCellDef>Actor</th>
            <td mat-cell *matCellDef="let row">
              @if (row.actorName) {
                <span>{{ row.actorName }}</span>
              } @else if (row.actorUserId) {
                <span class="muted" [matTooltip]="row.actorUserId">Unknown user</span>
              } @else {
                <span class="muted">System</span>
              }
            </td>
          </ng-container>

          <!-- Source -->
          <ng-container matColumnDef="source">
            <th mat-header-cell *matHeaderCellDef>Source</th>
            <td mat-cell *matCellDef="let row">
              @if (row.ipAddress) {
                <span class="muted">{{ row.ipAddress }}</span>
              } @else {
                <span class="muted">—</span>
              }
            </td>
          </ng-container>

          <!-- Diff toggle -->
          <ng-container matColumnDef="details">
            <th mat-header-cell *matHeaderCellDef></th>
            <td mat-cell *matCellDef="let row">
              <button
                mat-icon-button
                [matTooltip]="hasSnapshot(row) ? 'Show diff' : 'No snapshot recorded'"
                [disabled]="!hasSnapshot(row)"
                (click)="toggleDetails(row); $event.stopPropagation()"
              >
                <mat-icon>{{ isDetailed(row) ? 'unfold_less' : 'diff' }}</mat-icon>
              </button>
            </td>
          </ng-container>

          <tr mat-header-row *matHeaderRowDef="displayedColumns"></tr>
          <tr
            mat-row
            *matRowDef="let row; columns: displayedColumns"
            [class.selected]="isDetailed(row)"
            (click)="toggleDetails(row)"
          ></tr>
        </table>

        @if (!store.isLoading() && !store.hasEntries()) {
          <div class="empty-state">
            <mat-icon>history</mat-icon>
            <p>No audit entries match the current filters.</p>
          </div>
        }

        <mat-paginator
          [length]="store.totalCount()"
          [pageSize]="store.pageSize()"
          [pageIndex]="store.page() - 1"
          [pageSizeOptions]="[10, 20, 50, 100]"
          (page)="onPageChange($event)"
          showFirstLastButtons
        >
        </mat-paginator>

        <!-- Diff panel for the selected row -->
        @if (detailedEntry(); as entry) {
          <div class="detail-panel">
            <div class="detail-header">
              <span class="action-badge" [attr.data-action]="entry.action">{{ entry.action }}</span>
              <strong>{{ entry.entityType }}</strong>
              <code>{{ entry.entityId }}</code>
              @if (entry.userAgent) {
                <span class="muted" [matTooltip]="entry.userAgent">{{ entry.userAgent }}</span>
              }
              <button mat-icon-button class="close" (click)="clearDetails()" matTooltip="Close">
                <mat-icon>close</mat-icon>
              </button>
            </div>
            <app-diff-viewer
              [oldJson]="entry.oldValues"
              [newJson]="entry.newValues"
            ></app-diff-viewer>
          </div>
        }
      </mat-card>

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
    .audit-logs-page {
      padding: 24px;
    }

    .page-header {
      display: flex;
      align-items: center;
      justify-content: space-between;
      margin-bottom: 16px;

      h1 {
        margin: 0;
        font-size: 24px;
        font-weight: 500;
      }
    }

    .filter-card {
      padding: 16px;
      margin-bottom: 16px;
    }

    .filter-row {
      display: flex;
      flex-wrap: wrap;
      gap: 16px;
    }

    .search-field {
      min-width: 220px;
      flex: 1;
    }

    .filter-field {
      min-width: 160px;
      width: 180px;
    }

    .date-field {
      min-width: 210px;
      width: 220px;
    }

    .table-card {
      padding: 0;
      overflow: hidden;
    }

    table {
      width: 100%;
    }

    .nowrap {
      white-space: nowrap;
    }

    .entity-type {
      font-weight: 500;
    }

    .entity-id {
      font-size: 11px;
      background: #f5f5f5;
      padding: 1px 6px;
      border-radius: 3px;
    }

    .action-badge {
      padding: 1px 8px;
      border-radius: 10px;
      font-size: 11px;
      font-weight: 600;
      text-transform: uppercase;
      letter-spacing: 0.4px;

      &[data-action='Added'] {
        background: #e8f5e9;
        color: #2e7d32;
      }
      &[data-action='Modified'] {
        background: #e3f2fd;
        color: #1565c0;
      }
      &[data-action='Deleted'] {
        background: #fdecea;
        color: #c62828;
      }
    }

    .muted {
      color: #9e9e9e;
      font-size: 12px;
    }

    tr.selected {
      background: #e3f2fd;
    }

    .empty-state {
      text-align: center;
      padding: 32px 0;
      color: #9e9e9e;

      mat-icon {
        font-size: 40px;
        width: 40px;
        height: 40px;
      }

      p {
        margin: 8px 0 0;
      }
    }

    .detail-panel {
      border-top: 1px solid #e0e0e0;
      padding: 16px;
      background: #fafafa;
    }

    .detail-header {
      display: flex;
      align-items: center;
      flex-wrap: wrap;
      gap: 10px;
      margin-bottom: 12px;
      font-size: 13px;

      .close {
        margin-left: auto;
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
export class AuditLogBrowserComponent implements OnInit, OnDestroy {
  protected readonly store = inject(AuditLogStore);

  protected readonly actionOptions = AUDIT_ACTION_OPTIONS;
  protected readonly displayedColumns = [
    'occurredAt',
    'entityType',
    'entityId',
    'action',
    'actor',
    'source',
    'details',
  ];

  protected readonly entityTypeControl = new FormControl<string | null>(null);
  protected readonly actionControl = new FormControl<string | null>(null);
  protected readonly fromControl = new FormControl<string | null>(null);
  protected readonly toControl = new FormControl<string | null>(null);

  private readonly detailedId = signal<number | null>(null);

  private readonly destroy$ = new Subject<void>();

  ngOnInit(): void {
    this.store.loadLogs();

    this.entityTypeControl.valueChanges
      .pipe(debounceTime(300), distinctUntilChanged(), takeUntil(this.destroy$))
      .subscribe((entityType) => this.store.updateFilters({ entityType }));

    this.actionControl.valueChanges
      .pipe(takeUntil(this.destroy$))
      .subscribe((action) => this.store.updateFilters({ action }));

    this.fromControl.valueChanges
      .pipe(takeUntil(this.destroy$))
      .subscribe((from) => this.store.updateFilters({ from: toUtcIso(from) }));

    this.toControl.valueChanges
      .pipe(takeUntil(this.destroy$))
      .subscribe((to) => this.store.updateFilters({ to: toUtcIso(to) }));
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  protected onPageChange(event: PageEvent): void {
    if (event.pageSize !== this.store.pageSize()) {
      this.store.setPageSize(event.pageSize);
    } else {
      this.store.setPage(event.pageIndex + 1);
    }
  }

  protected hasActiveFilters(): boolean {
    const f = this.store.filters();
    return Boolean(f.entityType || f.action || f.from || f.to || f.entityId || f.actorUserId);
  }

  protected clearFilters(): void {
    this.entityTypeControl.reset(null, { emitEvent: false });
    this.actionControl.reset(null, { emitEvent: false });
    this.fromControl.reset(null, { emitEvent: false });
    this.toControl.reset(null, { emitEvent: false });
    this.store.clearFilters();
    this.detailedId.set(null);
  }

  protected hasSnapshot(entry: AuditLogEntry): boolean {
    return Boolean(entry.oldValues || entry.newValues);
  }

  protected isDetailed(entry: AuditLogEntry): boolean {
    return this.detailedId() === entry.id;
  }

  protected toggleDetails(entry: AuditLogEntry): void {
    if (!this.hasSnapshot(entry)) {
      return;
    }
    this.detailedId.set(this.isDetailed(entry) ? null : entry.id);
  }

  protected clearDetails(): void {
    this.detailedId.set(null);
  }

  protected detailedEntry(): AuditLogEntry | null {
    const id = this.detailedId();
    return id === null ? null : (this.store.entries().find((e) => e.id === id) ?? null);
  }

  /** Guids are noise at full width; show the leading chunk, full value on the tooltip. */
  protected shortId(id: string): string {
    return id.length > 8 ? id.slice(0, 8) : id;
  }
}

/** datetime-local value -> UTC ISO string; empty or invalid input maps to null (no filter). */
function toUtcIso(value: string | null): string | null {
  if (!value) {
    return null;
  }

  const date = new Date(value);
  if (Number.isNaN(date.getTime())) {
    return null;
  }

  return date.toISOString();
}
