import {
  ChangeDetectionStrategy,
  Component,
  OnInit,
  OnChanges,
  SimpleChanges,
  inject,
  input,
  signal,
} from '@angular/core';
import { DatePipe } from '@angular/common';
import { firstValueFrom } from 'rxjs';
import { MatCardModule } from '@angular/material/card';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatTooltipModule } from '@angular/material/tooltip';
import { AuditLogService } from '../audit-log.service';
import { extractError } from '../../../core/utils/error.util';
import { AuditLogEntry } from '../models/audit-log.model';
import { DiffViewerComponent } from '../diff-viewer/diff-viewer.component';

/**
 * Inline change-history panel for one record (Task 7.3). Drop it into any detail page:
 *
 * <app-entity-history entityType="Agent" [entityId]="agentId()" />
 *
 * Loads the newest 50 audit rows for that record via GET /api/v1/audit-logs/{type}/{id}
 * and expands each row into a field-level diff on demand (DiffViewerComponent). The
 * backend gates access with the `identity.auditlogs.read` permission.
 */
@Component({
  selector: 'app-entity-history',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    DatePipe,
    MatCardModule,
    MatIconModule,
    MatProgressBarModule,
    MatTooltipModule,
    DiffViewerComponent,
  ],
  template: `
    <div class="entity-history">
      @if (isLoading()) {
        <mat-progress-bar mode="indeterminate"></mat-progress-bar>
      }

      @if (error(); as err) {
        <div class="error-banner">
          <mat-icon>error</mat-icon>
          {{ err }}
        </div>
      }

      @if (!isLoading() && !error() && entries().length === 0) {
        <div class="empty-state">
          <mat-icon>history</mat-icon>
          <p>No recorded changes for this record yet.</p>
        </div>
      }

      @for (entry of entries(); track entry.id) {
        <div class="history-row">
          <button
            mat-icon-button
            class="toggle"
            (click)="toggleExpanded(entry.id)"
            [matTooltip]="isExpanded(entry.id) ? 'Hide changes' : 'Show changes'"
            [disabled]="!hasSnapshot(entry)"
          >
            <mat-icon>{{ isExpanded(entry.id) ? 'expand_less' : 'expand_more' }}</mat-icon>
          </button>

          <div class="row-body">
            <div class="row-main">
              <span class="action-badge" [attr.data-action]="entry.action">{{ entry.action }}</span>
              <span class="entity-type">{{ entry.entityType }}</span>
              <span class="actor">{{ entry.actorName || 'System' }}</span>
              <span class="when">{{ entry.occurredAt | date : 'MMM d, y, HH:mm' }}</span>
            </div>

            @if (isExpanded(entry.id)) {
              <div class="row-detail">
                <div class="meta">
                  @if (entry.ipAddress) {
                    <span><mat-icon>public</mat-icon> {{ entry.ipAddress }}</span>
                  }
                  @if (entry.userAgent) {
                    <span [matTooltip]="entry.userAgent">
                      <mat-icon>devices</mat-icon> {{ shortUserAgent(entry.userAgent) }}
                    </span>
                  }
                </div>

                @if (hasSnapshot(entry)) {
                  <app-diff-viewer
                    [oldJson]="entry.oldValues"
                    [newJson]="entry.newValues"
                  ></app-diff-viewer>
                }
              </div>
            }
          </div>
        </div>
      }
    </div>
  `,
  styles: `
    :host {
      display: block;
    }

    .history-row {
      display: flex;
      align-items: flex-start;
      gap: 4px;
      padding: 6px 0;
      border-bottom: 1px solid #eee;
    }

    .toggle {
      flex-shrink: 0;
    }

    .row-body {
      flex: 1;
      min-width: 0;
    }

    .row-main {
      display: flex;
      align-items: center;
      flex-wrap: wrap;
      gap: 10px;
      padding: 4px 0;
      font-size: 13px;
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

    .entity-type {
      color: #616161;
      font-weight: 500;
    }

    .actor {
      color: #424242;
    }

    .when {
      color: #9e9e9e;
      margin-left: auto;
      font-size: 12px;
    }

    .row-detail {
      padding: 4px 8px 10px;
    }

    .meta {
      display: flex;
      flex-wrap: wrap;
      gap: 16px;
      margin-bottom: 8px;
      color: #757575;
      font-size: 12px;

      span {
        display: inline-flex;
        align-items: center;
        gap: 4px;
      }

      mat-icon {
        font-size: 14px;
        width: 14px;
        height: 14px;
      }
    }

    .error-banner {
      display: flex;
      align-items: center;
      gap: 8px;
      padding: 8px 12px;
      border-radius: 4px;
      background: #fdecea;
      color: #b71c1c;
      margin-bottom: 8px;
      font-size: 13px;

      mat-icon {
        font-size: 18px;
        width: 18px;
        height: 18px;
      }
    }

    .empty-state {
      text-align: center;
      padding: 24px 0;
      color: #9e9e9e;

      mat-icon {
        font-size: 32px;
        width: 32px;
        height: 32px;
      }

      p {
        margin: 8px 0 0;
        font-size: 13px;
      }
    }
  `,
})
export class EntityHistoryComponent implements OnInit, OnChanges {
  /** EF entity type name as stored in the trail, e.g. "Agent", "User", "EntityNode". */
  readonly entityType = input.required<string>();
  /** Primary key of the record whose history is shown. */
  readonly entityId = input.required<string>();
  /** Newest N rows to request (server caps at 100). */
  readonly limit = input(50);

  private readonly auditLogService = inject(AuditLogService);

  protected readonly entries = signal<AuditLogEntry[]>([]);
  protected readonly isLoading = signal(false);
  protected readonly error = signal<string | null>(null);
  private readonly expandedIds = signal<Set<number>>(new Set());

  ngOnInit(): void {
    void this.load();
  }

  /** Reloads when the parent reuses this component for a different record. */
  ngOnChanges(changes: SimpleChanges): void {
    if (changes['entityId'] && !changes['entityId'].firstChange) {
      void this.load();
    }
  }

  protected isExpanded(id: number): boolean {
    return this.expandedIds().has(id);
  }

  protected toggleExpanded(id: number): void {
    const next = new Set(this.expandedIds());
    if (next.has(id)) {
      next.delete(id);
    } else {
      next.add(id);
    }
    this.expandedIds.set(next);
  }

  protected hasSnapshot(entry: AuditLogEntry): boolean {
    return Boolean(entry.oldValues || entry.newValues);
  }

  /** Truncated UA for the row; full text is on the tooltip. */
  protected shortUserAgent(userAgent: string): string {
    return userAgent.length > 40 ? `${userAgent.slice(0, 40)}…` : userAgent;
  }

  private async load(): Promise<void> {
    this.isLoading.set(true);
    this.error.set(null);
    this.expandedIds.set(new Set());

    try {
      const history = await firstValueFrom(
        this.auditLogService.getEntityHistory(
          this.entityType(),
          this.entityId(),
          this.limit()
        )
      );
      this.entries.set(history);
    } catch (err: unknown) {
      this.error.set(extractError(err));
    } finally {
      this.isLoading.set(false);
    }
  }
}
