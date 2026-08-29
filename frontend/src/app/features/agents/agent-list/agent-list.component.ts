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
import { MatMenuModule } from '@angular/material/menu';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatCardModule } from '@angular/material/card';
import { AgentStore } from '../agent.store';
import {
  AGENT_STATUS_OPTIONS,
  AGENT_TYPE_OPTIONS,
  AgentListItem,
} from '../models/agent.model';
import { HeartbeatIndicatorComponent } from '../heartbeat-indicator/heartbeat-indicator.component';
import { DatePipe } from '@angular/common';

@Component({
  selector: 'app-agent-list',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    ReactiveFormsModule,
    MatTableModule,
    MatPaginatorModule,
    MatSortModule,
    MatInputModule,
    MatFormFieldModule,
    MatSelectModule,
    MatButtonModule,
    MatIconModule,
    MatChipsModule,
    MatMenuModule,
    MatProgressBarModule,
    MatTooltipModule,
    MatCardModule,
    HeartbeatIndicatorComponent,
    DatePipe,
  ],
  templateUrl: './agent-list.component.html',
  styleUrl: './agent-list.component.scss',
})
export class AgentListComponent implements OnInit, OnDestroy {
  protected readonly store = inject(AgentStore);
  private readonly router = inject(Router);

  protected readonly searchControl = new FormControl('', { nonNullable: true });
  protected readonly statusFilter = new FormControl<string | null>(null);
  protected readonly typeFilter = new FormControl<string | null>(null);
  protected readonly statusOptions = AGENT_STATUS_OPTIONS;
  protected readonly typeOptions = AGENT_TYPE_OPTIONS;

  protected readonly displayedColumns = [
    'status',
    'name',
    'hostname',
    'type',
    'os',
    'lastSeen',
    'commands',
    'actions',
  ];

  private readonly destroy$ = new Subject<void>();

  ngOnInit(): void {
    this.store.loadAgents();
    this.store.loadFleetSummary();

    this.searchControl.valueChanges
      .pipe(debounceTime(300), takeUntil(this.destroy$))
      .subscribe((search) => {
        this.store.updateFilters({ search: search || null });
      });

    this.statusFilter.valueChanges
      .pipe(takeUntil(this.destroy$))
      .subscribe((status) => {
        this.store.updateFilters({ status });
      });

    this.typeFilter.valueChanges
      .pipe(takeUntil(this.destroy$))
      .subscribe((type) => {
        this.store.updateFilters({ type });
      });
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

  onSort(sort: Sort): void {
    // Client-side sort on current page
  }

  onView(agent: AgentListItem): void {
    this.router.navigate(['/agents', agent.id]);
  }

  getHeartbeatStatus(agent: AgentListItem): 'online' | 'stale' | 'offline' | 'suspended' {
    if (agent.status === 'Suspended' || agent.status === 'Deactivated')
      return 'suspended';
    if (agent.isOnline) return 'online';
    if (!agent.lastActiveAt) return 'offline';

    const lastActive = new Date(agent.lastActiveAt).getTime();
    const now = Date.now();
    const diffMin = (now - lastActive) / 60000;

    if (diffMin <= 5) return 'online';
    if (diffMin <= 15) return 'stale';
    return 'offline';
  }

  getStatusColor(status: string): string {
    const colors: Record<string, string> = {
      Active: 'primary',
      Suspended: 'warn',
      Deactivated: 'disabled',
    };
    return colors[status] || 'primary';
  }

  getCommandStatusColor(pending: number, total: number): string {
    if (pending > 0) return 'accent';
    return 'primary';
  }

  trackById = (_: number, item: AgentListItem) => item.id;
}
