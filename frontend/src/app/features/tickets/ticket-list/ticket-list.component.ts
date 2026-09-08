import {
  Component,
  OnInit,
  OnDestroy,
  ChangeDetectionStrategy,
  inject,
} from '@angular/core';
import { Router } from '@angular/router';
import { Subject, debounceTime, takeUntil } from 'rxjs';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { MatTableModule } from '@angular/material/table';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatSortModule, Sort } from '@angular/material/sort';
import { MatInputModule } from '@angular/material/input';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatSelectModule } from '@angular/material/select';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatChipsModule } from '@angular/material/chips';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatCardModule } from '@angular/material/card';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { DatePipe } from '@angular/common';
import { TicketStore } from '../ticket.store';
import {
  TicketListItem,
  TICKET_TYPE_OPTIONS,
  TICKET_STATUS_OPTIONS,
  TICKET_PRIORITY_OPTIONS,
  TicketType,
  TicketStatus,
  TicketPriority,
} from '../models/ticket.models';

@Component({
  selector: 'app-ticket-list',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    ReactiveFormsModule,
    DatePipe,
    MatTableModule,
    MatPaginatorModule,
    MatSortModule,
    MatInputModule,
    MatFormFieldModule,
    MatSelectModule,
    MatButtonModule,
    MatIconModule,
    MatChipsModule,
    MatProgressBarModule,
    MatTooltipModule,
    MatCardModule,
    MatCheckboxModule,
  ],
  template: `
    <div class="ticket-list-page">
      <!-- Header -->
      <div class="page-header">
        <div>
          <h1>Tickets</h1>
          <p class="subtitle">{{ store.totalCount() }} tickets · {{ store.openCount() }} open on this page</p>
        </div>
        <div class="header-actions">
          <button mat-flat-button color="primary" (click)="onCreate()">
            <mat-icon>add</mat-icon>
            New Ticket
          </button>
        </div>
      </div>

      <!-- Filters -->
      <mat-card class="filter-card">
        <div class="filter-row">
          <mat-form-field appearance="outline" class="search-field">
            <mat-label>Search tickets</mat-label>
            <input matInput [formControl]="searchControl" placeholder="Title, description..." />
            <mat-icon matSuffix>search</mat-icon>
          </mat-form-field>

          <mat-form-field appearance="outline">
            <mat-label>Type</mat-label>
            <mat-select [formControl]="typeFilter">
              <mat-option [value]="null">All Types</mat-option>
              @for (option of typeOptions; track option.value) {
                <mat-option [value]="option.value">{{ option.label }}</mat-option>
              }
            </mat-select>
          </mat-form-field>

          <mat-form-field appearance="outline">
            <mat-label>Status</mat-label>
            <mat-select [formControl]="statusFilter">
              <mat-option [value]="null">All Statuses</mat-option>
              @for (option of statusOptions; track option.value) {
                <mat-option [value]="option.value">{{ option.label }}</mat-option>
              }
            </mat-select>
          </mat-form-field>

          <mat-form-field appearance="outline">
            <mat-label>Priority</mat-label>
            <mat-select [formControl]="priorityFilter">
              <mat-option [value]="null">All Priorities</mat-option>
              @for (option of priorityOptions; track option.value) {
                <mat-option [value]="option.value">{{ option.label }}</mat-option>
              }
            </mat-select>
          </mat-form-field>

          <mat-checkbox [formControl]="myTickets" class="my-tickets">My tickets</mat-checkbox>
        </div>
      </mat-card>

      @if (store.isLoading()) {
        <mat-progress-bar mode="indeterminate"></mat-progress-bar>
      }

      <!-- Data Table -->
      <mat-card class="table-card">
        <table mat-table [dataSource]="store.items()" matSort (matSortChange)="onSort($event)">
          <!-- Title -->
          <ng-container matColumnDef="title">
            <th mat-header-cell *matHeaderCellDef mat-sort-header>Title</th>
            <td mat-cell *matCellDef="let row">
              <a class="ticket-link" (click)="onView(row)">{{ row.title }}</a>
            </td>
          </ng-container>

          <!-- Type -->
          <ng-container matColumnDef="type">
            <th mat-header-cell *matHeaderCellDef>Type</th>
            <td mat-cell *matCellDef="let row">
              <span class="type-badge" [attr.data-type]="row.type">{{ getTypeLabel(row.type) }}</span>
            </td>
          </ng-container>

          <!-- Status -->
          <ng-container matColumnDef="status">
            <th mat-header-cell *matHeaderCellDef mat-sort-header>Status</th>
            <td mat-cell *matCellDef="let row">
              <span class="status-badge" [attr.data-status]="row.status">{{ getStatusLabel(row.status) }}</span>
            </td>
          </ng-container>

          <!-- Priority -->
          <ng-container matColumnDef="priority">
            <th mat-header-cell *matHeaderCellDef mat-sort-header>Priority</th>
            <td mat-cell *matCellDef="let row">
              <span class="priority-badge" [attr.data-priority]="row.priority">{{ getPriorityLabel(row.priority) }}</span>
            </td>
          </ng-container>

          <!-- Escalated -->
          <ng-container matColumnDef="isEscalated">
            <th mat-header-cell *matHeaderCellDef></th>
            <td mat-cell *matCellDef="let row">
              @if (row.isEscalated) {
                <mat-icon class="escalated-icon" matTooltip="Escalated">trending_up</mat-icon>
              }
            </td>
          </ng-container>

          <!-- Opened -->
          <ng-container matColumnDef="openedAt">
            <th mat-header-cell *matHeaderCellDef mat-sort-header>Opened</th>
            <td mat-cell *matCellDef="let row">{{ row.openedAt | date: 'short' }}</td>
          </ng-container>

          <!-- Due -->
          <ng-container matColumnDef="dueDate">
            <th mat-header-cell *matHeaderCellDef mat-sort-header>Due</th>
            <td mat-cell *matCellDef="let row">
              @if (row.dueDate) {
                <span [class.overdue]="isOverdue(row)">{{ row.dueDate | date: 'short' }}</span>
              } @else {
                —
              }
            </td>
          </ng-container>

          <!-- Actions -->
          <ng-container matColumnDef="actions">
            <th mat-header-cell *matHeaderCellDef></th>
            <td mat-cell *matCellDef="let row">
              <button mat-icon-button (click)="onView(row)" matTooltip="Open ticket">
                <mat-icon>open_in_new</mat-icon>
              </button>
            </td>
          </ng-container>

          <tr mat-header-row *matHeaderRowDef="displayedColumns"></tr>
          <tr mat-row *matRowDef="let row; columns: displayedColumns" (click)="onView(row)"></tr>
        </table>

        @if (!store.isLoading() && store.items().length === 0) {
          <div class="empty-state">
            <mat-icon>confirmation_number</mat-icon>
            <p>No tickets found</p>
            <button mat-flat-button color="primary" (click)="onCreate()">
              <mat-icon>add</mat-icon>
              Create First Ticket
            </button>
          </div>
        }

        <mat-paginator
          [length]="store.totalCount()"
          [pageSize]="store.pageSize()"
          [pageIndex]="store.page() - 1"
          [pageSizeOptions]="[10, 20, 50, 100]"
          (page)="onPageChange($event)"
          showFirstLastButtons>
        </mat-paginator>
      </mat-card>

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
    .ticket-list-page { padding: 24px; max-width: 1400px; margin: 0 auto; }
    .page-header { display: flex; justify-content: space-between; align-items: flex-start; margin-bottom: 24px; }
    .page-header h1 { margin: 0 0 4px; font-size: 1.75rem; }
    .subtitle { margin: 0; color: #a0a0b0; font-size: 0.875rem; }
    .header-actions { display: flex; gap: 12px; }
    .filter-card { margin-bottom: 16px; padding: 16px; }
    .filter-row { display: flex; gap: 16px; align-items: center; flex-wrap: wrap; }
    .search-field { flex: 1; min-width: 240px; }
    .filter-row mat-form-field:not(.search-field) { width: 160px; }
    .my-tickets { white-space: nowrap; }
    .ticket-link { color: #3b82f6; cursor: pointer; text-decoration: none; }
    .ticket-link:hover { text-decoration: underline; }
    .status-badge, .type-badge, .priority-badge {
      padding: 3px 10px; border-radius: 12px; font-size: 0.75rem; white-space: nowrap;
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
    .status-badge[data-status="7"] { background: rgba(107, 114, 128, 0.15); color: #6b7280; }
    .status-badge[data-status="8"] { background: rgba(107, 114, 128, 0.15); color: #6b7280; }
    .priority-badge[data-priority="1"] { background: rgba(107, 114, 128, 0.15); color: #9ca3af; }
    .priority-badge[data-priority="2"] { background: rgba(59, 130, 246, 0.15); color: #3b82f6; }
    .priority-badge[data-priority="3"] { background: rgba(245, 158, 11, 0.15); color: #f59e0b; }
    .priority-badge[data-priority="4"] { background: rgba(249, 115, 22, 0.15); color: #f97316; }
    .priority-badge[data-priority="5"] { background: rgba(239, 68, 68, 0.15); color: #ef4444; }
    .escalated-icon { color: #f97316; }
    .overdue { color: #ef4444; font-weight: 500; }
    .empty-state { text-align: center; padding: 48px 24px; color: #a0a0b0; }
    .empty-state mat-icon { font-size: 48px; width: 48px; height: 48px; margin-bottom: 12px; }
    .error-banner {
      display: flex; align-items: center; gap: 12px;
      background: rgba(239, 68, 68, 0.1); border: 1px solid rgba(239, 68, 68, 0.3);
      border-radius: 8px; padding: 12px 16px; margin-top: 16px; color: #ef4444;
    }
  `],
})
export class TicketListComponent implements OnInit, OnDestroy {
  protected readonly store = inject(TicketStore);
  private readonly router = inject(Router);

  protected readonly searchControl = new FormControl('', { nonNullable: true });
  protected readonly typeFilter = new FormControl<number | null>(null);
  protected readonly statusFilter = new FormControl<number | null>(null);
  protected readonly priorityFilter = new FormControl<number | null>(null);
  protected readonly myTickets = new FormControl<boolean>(false, { nonNullable: true });

  protected readonly typeOptions = TICKET_TYPE_OPTIONS;
  protected readonly statusOptions = TICKET_STATUS_OPTIONS;
  protected readonly priorityOptions = TICKET_PRIORITY_OPTIONS;

  protected readonly displayedColumns = [
    'title',
    'type',
    'status',
    'priority',
    'isEscalated',
    'openedAt',
    'dueDate',
    'actions',
  ];

  private readonly destroy$ = new Subject<void>();

  ngOnInit(): void {
    this.store.loadTickets();

    this.searchControl.valueChanges
      .pipe(debounceTime(300), takeUntil(this.destroy$))
      .subscribe((search) => this.store.updateFilters({ search: search || null }));

    this.typeFilter.valueChanges
      .pipe(takeUntil(this.destroy$))
      .subscribe((type) => this.store.updateFilters({ type: type as TicketType | null }));

    this.statusFilter.valueChanges
      .pipe(takeUntil(this.destroy$))
      .subscribe((status) => this.store.updateFilters({ status: status as TicketStatus | null }));

    this.priorityFilter.valueChanges
      .pipe(takeUntil(this.destroy$))
      .subscribe((priority) => this.store.updateFilters({ priority: priority as TicketPriority | null }));

    this.myTickets.valueChanges
      .pipe(takeUntil(this.destroy$))
      .subscribe((myTicketsOnly) => this.store.updateFilters({ myTicketsOnly }));
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  onPageChange(event: PageEvent): void {
    if (event.pageSize !== this.store.pageSize()) {
      this.store.setPageSize(event.pageSize);
    } else {
      this.store.setPage(event.pageIndex + 1);
    }
  }

  onSort(_sort: Sort): void {
    // Client-side sorting on the current page.
  }

  onView(ticket: TicketListItem): void {
    this.router.navigate(['/tickets', ticket.id]);
  }

  onCreate(): void {
    this.router.navigate(['/tickets/new']);
  }

  getTypeLabel(type: number): string {
    return this.typeOptions.find((o) => o.value === type)?.label ?? String(type);
  }

  getStatusLabel(status: number): string {
    return this.statusOptions.find((o) => o.value === status)?.label ?? String(status);
  }

  getPriorityLabel(priority: number): string {
    return this.priorityOptions.find((o) => o.value === priority)?.label ?? String(priority);
  }

  isOverdue(ticket: TicketListItem): boolean {
    if (!ticket.dueDate) return false;
    const open =
      ticket.status !== TicketStatus.Solved &&
      ticket.status !== TicketStatus.Closed &&
      ticket.status !== TicketStatus.Cancelled;
    return open && new Date(ticket.dueDate).getTime() < Date.now();
  }

  trackById = (_: number, item: TicketListItem) => item.id;
}
