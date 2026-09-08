import {
  Component,
  OnInit,
  OnDestroy,
  ChangeDetectionStrategy,
  inject,
} from '@angular/core';
import { CommonModule, DatePipe } from '@angular/common';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { Subscription } from 'rxjs';
import { filter } from 'rxjs/operators';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatCardModule } from '@angular/material/card';
import { MatChipsModule } from '@angular/material/chips';
import { MatMenuModule } from '@angular/material/menu';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatDividerModule } from '@angular/material/divider';
import { MatSelectModule } from '@angular/material/select';
import { TicketStore } from '../ticket.store';
import { TicketHubService, TicketEvent } from '../ticket-hub.service';
import { FollowupComponent } from '../followup/followup.component';
import { SlaTimerComponent } from '../sla-timer/sla-timer.component';
import {
  TicketStatus,
  TICKET_STATUS_OPTIONS,
  TICKET_TYPE_OPTIONS,
  TICKET_PRIORITY_OPTIONS,
  TICKET_SOURCE_OPTIONS,
  TICKET_VALIDATION_LABELS,
} from '../models/ticket.models';

@Component({
  selector: 'app-ticket-detail',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    CommonModule,
    DatePipe,
    RouterLink,
    MatButtonModule,
    MatIconModule,
    MatCardModule,
    MatChipsModule,
    MatMenuModule,
    MatProgressBarModule,
    MatTooltipModule,
    MatDividerModule,
    MatSelectModule,
    FollowupComponent,
    SlaTimerComponent,
  ],
  template: `
    <div class="ticket-detail-page">
      @if (store.isLoadingDetail()) {
        <mat-progress-bar mode="indeterminate"></mat-progress-bar>
      }

      @if (ticket(); as t) {
        <!-- Header -->
        <div class="ticket-header">
          <div class="header-main">
            <button mat-icon-button (click)="back()" matTooltip="Back to list">
              <mat-icon>arrow_back</mat-icon>
            </button>
            <div>
              <div class="title-row">
                <span class="type-badge" [attr.data-type]="t.type">{{ typeLabel(t.type) }}</span>
                <span class="status-badge" [attr.data-status]="t.status">{{ statusLabel(t.status) }}</span>
                <span class="priority-badge" [attr.data-priority]="t.priority">{{ priorityLabel(t.priority) }}</span>
                @if (t.isEscalated) {
                  <span class="escalated-badge">Escalated L{{ t.escalationLevel }}</span>
                }
              </div>
              <h1>{{ t.title }}</h1>
              <p class="meta">
                Opened {{ t.openedAt | date: 'medium' }} ·
                Source: {{ sourceLabel(t.source) }} ·
                Requester {{ t.requesterUserId.slice(0, 8) }}
                @if (t.assignedUserId) {
                  · Assigned {{ t.assignedUserId.slice(0, 8) }}
                }
              </p>
            </div>
          </div>

          <div class="header-actions">
            <app-sla-timer [dueDate]="t.dueDate ?? null" [status]="t.status"></app-sla-timer>

            <button mat-stroked-button [matMenuTriggerFor]="statusMenu">
              <mat-icon>swap_vert</mat-icon>
              Change Status
            </button>
            <mat-menu #statusMenu="matMenu">
              @for (option of statusOptions; track option.value) {
                <button mat-menu-item (click)="onStatusChange(option.value)">
                  {{ option.label }}
                </button>
              }
            </mat-menu>

            <button mat-stroked-button (click)="onEscalate()" [disabled]="t.status === 7 || t.status === 8">
              <mat-icon>trending_up</mat-icon>
              Escalate
            </button>

            <button mat-flat-button color="primary" (click)="onSolve()" [disabled]="t.status === 6 || t.status === 7 || t.status === 8">
              <mat-icon>check_circle</mat-icon>
              Solve
            </button>

            <button mat-stroked-button (click)="onClose()" [disabled]="t.status === 7 || t.status === 8">
              <mat-icon>lock</mat-icon>
              Close
            </button>
          </div>
        </div>

        <!-- Content grid -->
        <div class="detail-grid">
          <!-- Main column -->
          <div class="main-col">
            <mat-card class="section-card">
              <h2>Description</h2>
              <div class="description" [innerHTML]="t.description || 'No description provided.'"></div>
            </mat-card>

            @if (t.solution) {
              <mat-card class="section-card solution-card">
                <h2><mat-icon>verified</mat-icon> Solution</h2>
                <div class="description" [innerHTML]="t.solution"></div>
              </mat-card>
            }

            <mat-card class="section-card">
              <h2>Followups ({{ store.tasks().length }})</h2>
              <app-followup
                [tasks]="store.tasks()"
                [submitting]="store.isSaving()"
                (postFollowup)="onPostFollowup($event)">
              </app-followup>
            </mat-card>
          </div>

          <!-- Side column -->
          <div class="side-col">
            <mat-card class="section-card">
              <h2>Details</h2>
              <dl class="detail-list">
                <div><dt>Impact</dt><dd>{{ impactLabel(t.impact) }}</dd></div>
                <div><dt>Urgency</dt><dd>{{ urgencyLabel(t.urgency) }}</dd></div>
                <div><dt>Score</dt><dd>{{ t.calculatedScore ?? '—' }}</dd></div>
                <div><dt>Validation</dt><dd>{{ validationLabel(t.validationStatus) }}</dd></div>
                <div><dt>Solved</dt><dd>{{ t.solvedAt ? (t.solvedAt | date: 'short') : '—' }}</dd></div>
                <div><dt>Closed</dt><dd>{{ t.closedAt ? (t.closedAt | date: 'short') : '—' }}</dd></div>
                <div><dt>Asset</dt>
                  <dd>
                    @if (t.assetId) {
                      <a [routerLink]="['/assets', t.assetId]">View asset</a>
                    } @else { — }
                  </dd>
                </div>
                <div><dt>Satisfaction</dt>
                  <dd>
                    @if (t.satisfactionRating) {
                      {{ '★'.repeat(t.satisfactionRating) }}{{ '☆'.repeat(5 - t.satisfactionRating) }}
                    } @else { — }
                  </dd>
                </div>
              </dl>
            </mat-card>

            <mat-card class="section-card">
              <h2>History</h2>
              @for (entry of store.history(); track entry.id) {
                <div class="history-entry">
                  <span class="history-field">{{ entry.fieldName }}</span>
                  @if (entry.oldValue || entry.newValue) {
                    <span class="history-change">{{ entry.oldValue ?? '—' }} → {{ entry.newValue ?? '—' }}</span>
                  }
                  <span class="history-time">{{ entry.occurredAt | date: 'short' }}</span>
                </div>
              } @empty {
                <p class="empty-note">No changes recorded.</p>
              }
            </mat-card>
          </div>
        </div>
      } @else if (!store.isLoadingDetail()) {
        <div class="empty-state">
          <mat-icon>search_off</mat-icon>
          <p>Ticket not found.</p>
        </div>
      }

      @if (store.error()) {
        <div class="error-banner">
          <mat-icon>error</mat-icon>
          {{ store.error() }}
          <button mat-icon-button (click)="store.clearError()">
            <mat-icon>close</mat-icon>
          </button>
        </div>
      }
    </div>
  `,
  styles: [`
    .ticket-detail-page { padding: 24px; max-width: 1400px; margin: 0 auto; }
    .ticket-header {
      display: flex; justify-content: space-between; align-items: flex-start;
      gap: 16px; margin-bottom: 24px; flex-wrap: wrap;
    }
    .header-main { display: flex; gap: 12px; align-items: flex-start; }
    .title-row { display: flex; gap: 8px; margin-bottom: 8px; flex-wrap: wrap; }
    h1 { margin: 0 0 8px; font-size: 1.5rem; color: #fff; }
    .meta { margin: 0; color: #a0a0b0; font-size: 0.8125rem; }
    .header-actions { display: flex; gap: 8px; align-items: center; flex-wrap: wrap; }
    .type-badge, .status-badge, .priority-badge, .escalated-badge {
      padding: 3px 10px; border-radius: 12px; font-size: 0.75rem;
    }
    .type-badge[data-type="1"] { background: rgba(239, 68, 68, 0.15); color: #ef4444; }
    .type-badge[data-type="2"] { background: rgba(59, 130, 246, 0.15); color: #3b82f6; }
    .type-badge[data-type="3"] { background: rgba(168, 85, 247, 0.15); color: #a855f7; }
    .type-badge[data-type="4"] { background: rgba(245, 158, 11, 0.15); color: #f59e0b; }
    .status-badge[data-status="1"] { background: rgba(59, 130, 246, 0.15); color: #3b82f6; }
    .status-badge[data-status="2"] { background: rgba(139, 92, 246, 0.15); color: #8b5cf6; }
    .status-badge[data-status="3"] { background: rgba(6, 182, 212, 0.15); color: #06b6d4; }
    .status-badge[data-status="4"], .status-badge[data-status="5"] { background: rgba(245, 158, 11, 0.15); color: #f59e0b; }
    .status-badge[data-status="6"] { background: rgba(16, 185, 129, 0.15); color: #10b981; }
    .status-badge[data-status="7"], .status-badge[data-status="8"] { background: rgba(107, 114, 128, 0.15); color: #6b7280; }
    .priority-badge[data-priority="1"] { background: rgba(107, 114, 128, 0.15); color: #9ca3af; }
    .priority-badge[data-priority="2"] { background: rgba(59, 130, 246, 0.15); color: #3b82f6; }
    .priority-badge[data-priority="3"] { background: rgba(245, 158, 11, 0.15); color: #f59e0b; }
    .priority-badge[data-priority="4"] { background: rgba(249, 115, 22, 0.15); color: #f97316; }
    .priority-badge[data-priority="5"] { background: rgba(239, 68, 68, 0.15); color: #ef4444; }
    .escalated-badge { background: rgba(249, 115, 22, 0.15); color: #f97316; }
    .detail-grid { display: grid; grid-template-columns: 2fr 1fr; gap: 16px; }
    .main-col, .side-col { display: flex; flex-direction: column; gap: 16px; }
    .section-card { padding: 20px; }
    .section-card h2 {
      display: flex; align-items: center; gap: 8px;
      margin: 0 0 16px; font-size: 1rem; color: #fff;
    }
    .description { color: #d1d5db; line-height: 1.6; }
    .solution-card { border-color: rgba(16, 185, 129, 0.3); }
    .detail-list { margin: 0; }
    .detail-list > div {
      display: flex; justify-content: space-between; padding: 8px 0;
      border-bottom: 1px solid rgba(255, 255, 255, 0.06);
    }
    .detail-list dt { color: #a0a0b0; font-size: 0.8125rem; }
    .detail-list dd { margin: 0; color: #e5e7eb; font-size: 0.875rem; text-align: right; }
    .history-entry {
      display: flex; gap: 8px; align-items: baseline; flex-wrap: wrap;
      padding: 8px 0; border-bottom: 1px solid rgba(255, 255, 255, 0.06);
      font-size: 0.8125rem;
    }
    .history-field { color: #3b82f6; font-weight: 500; }
    .history-change { color: #d1d5db; }
    .history-time { color: #6b7280; font-size: 0.75rem; margin-left: auto; }
    .empty-note { color: #6b7280; font-size: 0.8125rem; }
    .empty-state { text-align: center; padding: 64px 24px; color: #a0a0b0; }
    .error-banner {
      display: flex; align-items: center; gap: 12px;
      background: rgba(239, 68, 68, 0.1); border: 1px solid rgba(239, 68, 68, 0.3);
      border-radius: 8px; padding: 12px 16px; margin-top: 16px; color: #ef4444;
    }
    @media (max-width: 960px) {
      .detail-grid { grid-template-columns: 1fr; }
    }
  `],
})
export class TicketDetailComponent implements OnInit, OnDestroy {
  protected readonly store = inject(TicketStore);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly hub = inject(TicketHubService);

  protected readonly statusOptions = TICKET_STATUS_OPTIONS;

  private readonly subscriptions: Subscription[] = [];
  private ticketId: string | null = null;

  ticket() {
    return this.store.selectedTicket();
  }

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (!id) {
      this.router.navigate(['/tickets']);
      return;
    }
    this.ticketId = id;
    this.store.loadTicket(id);

    // Live updates: subscribe to this ticket's group, refresh the thread when a
    // followup lands, and reload on any field change.
    void this.hub.start().then(() => this.hub.subscribeToTicket(id));
    this.subscriptions.push(
      this.hub.followupAdded
        .pipe(filter((e) => e.ticketId === id))
        .subscribe(() => this.store.refreshTasks(id)),
      this.hub.ticketUpdated
        .pipe(filter((e) => e.ticketId === id))
        .subscribe(() => this.store.loadTicket(id))
    );
  }

  ngOnDestroy(): void {
    this.subscriptions.forEach((s) => s.unsubscribe());
    if (this.ticketId) {
      void this.hub.unsubscribeFromTicket(this.ticketId);
    }
  }

  back(): void {
    this.router.navigate(['/tickets']);
  }

  onStatusChange(status: TicketStatus): void {
    if (this.ticketId) void this.store.changeStatus(this.ticketId, status);
  }

  onEscalate(): void {
    if (this.ticketId) void this.store.escalate(this.ticketId);
  }

  onSolve(): void {
    if (this.ticketId) void this.store.solve(this.ticketId);
  }

  onClose(): void {
    if (this.ticketId) void this.store.close(this.ticketId);
  }

  onPostFollowup(payload: {
    content: string;
    type: number;
    timeSpentMinutes: number | null;
    isPrivate: boolean;
  }): void {
    if (!this.ticketId) return;
    void this.store
      .addTask(this.ticketId, {
        content: payload.content,
        type: payload.type,
        timeSpentMinutes: payload.timeSpentMinutes,
        isPrivate: payload.isPrivate,
      })
      .then(() => this.store.refreshTasks(this.ticketId!));
  }

  typeLabel(t: number): string {
    return TICKET_TYPE_OPTIONS.find((o) => o.value === t)?.label ?? String(t);
  }

  statusLabel(s: number): string {
    return TICKET_STATUS_OPTIONS.find((o) => o.value === s)?.label ?? String(s);
  }

  priorityLabel(p: number): string {
    return TICKET_PRIORITY_OPTIONS.find((o) => o.value === p)?.label ?? String(p);
  }

  impactLabel(i: number): string {
    return ['Low', 'Medium', 'High'][i - 1] ?? String(i);
  }

  urgencyLabel(u: number): string {
    return ['Low', 'Medium', 'High'][u - 1] ?? String(u);
  }

  sourceLabel(s: number): string {
    return TICKET_SOURCE_OPTIONS.find((o) => o.value === s)?.label ?? String(s);
  }

  validationLabel(v: number): string {
    return TICKET_VALIDATION_LABELS[v] ?? String(v);
  }
}
