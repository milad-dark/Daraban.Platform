import {
  Component,
  OnInit,
  OnDestroy,
  ChangeDetectionStrategy,
  inject,
  input,
  effect,
} from '@angular/core';
import { Subject, takeUntil } from 'rxjs';
import { MatTabsModule } from '@angular/material/tabs';
import { MatCardModule } from '@angular/material/card';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatChipsModule } from '@angular/material/chips';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatDividerModule } from '@angular/material/divider';
import { MatListModule } from '@angular/material/list';
import { MatTooltipModule } from '@angular/material/tooltip';
import { DatePipe } from '@angular/common';
import { AgentStore } from '../agent.store';
import { HeartbeatIndicatorComponent } from '../heartbeat-indicator/heartbeat-indicator.component';
import { CommandPanelComponent } from '../command-panel/command-panel.component';
import { AgentListItem } from '../models/agent.model';

@Component({
  selector: 'app-agent-detail',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    MatTabsModule,
    MatCardModule,
    MatIconModule,
    MatButtonModule,
    MatChipsModule,
    MatProgressBarModule,
    MatDividerModule,
    MatListModule,
    MatTooltipModule,
    DatePipe,
    HeartbeatIndicatorComponent,
    CommandPanelComponent,
  ],
  templateUrl: './agent-detail.component.html',
  styleUrl: './agent-detail.component.scss',
})
export class AgentDetailComponent implements OnInit, OnDestroy {
  agentId = input.required<string>();

  protected readonly store = inject(AgentStore);
  private readonly destroy$ = new Subject<void>();

  constructor() {
    // React to agentId input changes
    effect(() => {
      const id = this.agentId();
      if (id) {
        this.loadAll(id);
      }
    });
  }

  ngOnInit(): void {}

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
    this.store.clearSelectedAgent();
  }

  private loadAll(agentId: string): void {
    this.store.loadAgentDetail(agentId);
    this.store.loadCommandHistory(agentId);
    this.store.loadInventorySnapshot(agentId);
  }

  onTabChange(index: number): void {
    const id = this.agentId();
    if (!id) return;

    if (index === 1) {
      this.store.loadInventorySnapshot(id);
    } else if (index === 2) {
      this.store.loadCommandHistory(id);
    }
  }

  getHeartbeatStatus(): 'online' | 'stale' | 'offline' | 'suspended' {
    const agent = this.store.selectedAgent()?.agent;
    if (!agent) return 'offline';
    if (agent.status === 'Suspended' || agent.status === 'Deactivated')
      return 'suspended';
    if (!agent.lastActiveAt) return 'offline';

    const lastActive = new Date(agent.lastActiveAt).getTime();
    const now = Date.now();
    const diffMin = (now - lastActive) / 60000;

    if (diffMin <= 5) return 'online';
    if (diffMin <= 15) return 'stale';
    return 'offline';
  }

  formatDuration(ms: number): string {
    if (ms < 1000) return `${ms}ms`;
    if (ms < 60000) return `${(ms / 1000).toFixed(1)}s`;
    return `${Math.floor(ms / 60000)}m ${Math.floor((ms % 60000) / 1000)}s`;
  }
}
