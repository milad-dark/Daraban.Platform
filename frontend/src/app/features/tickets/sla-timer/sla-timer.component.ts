import {
  Component,
  Input,
  OnChanges,
  OnDestroy,
  ChangeDetectionStrategy,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatIconModule } from '@angular/material/icon';
import { interval, Subscription } from 'rxjs';

/**
 * Live SLA countdown. Ticks every second from DueDate; turns amber under
 * thresholdPercent remaining, red once breached. Pure display — the SignalR
 * SlaBreach event just triggers a reload upstream; this component does the math
 * locally so it works even without a hub connection.
 */
@Component({
  selector: 'app-sla-timer',
  standalone: true,
  imports: [CommonModule, MatIconModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (dueDate && !isTerminal) {
      <div class="sla-timer" [attr.data-state]="state()">
        <mat-icon aria-hidden="true">schedule</mat-icon>
        <div class="sla-text">
          <span class="sla-label">{{ stateLabel() }}</span>
          <span class="sla-value">{{ countdown() }}</span>
        </div>
      </div>
    }
  `,
  styles: [`
    .sla-timer {
      display: inline-flex; align-items: center; gap: 8px;
      padding: 8px 14px; border-radius: 10px;
      background: rgba(255, 255, 255, 0.05); border: 1px solid rgba(255, 255, 255, 0.1);
    }
    .sla-timer[data-state="ok"] mat-icon { color: #10b981; }
    .sla-timer[data-state="warning"] { border-color: rgba(245, 158, 11, 0.4); }
    .sla-timer[data-state="warning"] mat-icon, .sla-timer[data-state="warning"] .sla-value { color: #f59e0b; }
    .sla-timer[data-state="breached"] { border-color: rgba(239, 68, 68, 0.4); background: rgba(239, 68, 68, 0.08); }
    .sla-timer[data-state="breached"] mat-icon, .sla-timer[data-state="breached"] .sla-value { color: #ef4444; }
    .sla-text { display: flex; flex-direction: column; }
    .sla-label { font-size: 0.6875rem; color: #a0a0b0; text-transform: uppercase; letter-spacing: 0.05em; }
    .sla-value { font-size: 0.9375rem; font-variant-numeric: tabular-nums; color: #fff; }
  `],
})
export class SlaTimerComponent implements OnChanges, OnDestroy {
  @Input() dueDate: string | null = null;
  @Input() status: number = 0;

  /** Statuses where the SLA clock no longer runs. */
  private static readonly TERMINAL = new Set([6, 7, 8]); // Solved, Closed, Cancelled

  private readonly now = new Date();
  private timerSub: Subscription | null = null;

  isTerminal = false;

  ngOnChanges(): void {
    this.isTerminal = SlaTimerComponent.TERMINAL.has(this.status);
    if (!this.timerSub && !this.isTerminal) {
      // Tick once per second to refresh the countdown via change detection.
      this.timerSub = interval(1000).subscribe(() => void 0);
    }
  }

  ngOnDestroy(): void {
    this.timerSub?.unsubscribe();
  }

  remainingMs(): number {
    if (!this.dueDate) return 0;
    return new Date(this.dueDate).getTime() - Date.now();
  }

  state(): 'ok' | 'warning' | 'breached' {
    const ms = this.remainingMs();
    if (ms <= 0) return 'breached';
    // Warning under 25% of the total SLA window (approximated: under 4 hours or 25% remaining).
    const total = Math.max(
      new Date(this.dueDate ?? '').getTime() - new Date(this.openedFallback()).getTime(),
      1
    );
    if (ms < total * 0.25) return 'warning';
    return 'ok';
  }

  stateLabel(): string {
    const s = this.state();
    if (s === 'breached') return 'SLA breached';
    if (s === 'warning') return 'SLA at risk';
    return 'SLA remaining';
  }

  countdown(): string {
    const ms = this.remainingMs();
    if (ms <= 0) {
      const overdue = Math.abs(ms);
      return `+${this.format(overdue)} over`;
    }
    return this.format(ms);
  }

  private format(ms: number): string {
    const totalSeconds = Math.floor(ms / 1000);
    const d = Math.floor(totalSeconds / 86400);
    const h = Math.floor((totalSeconds % 86400) / 3600);
    const m = Math.floor((totalSeconds % 3600) / 60);
    const s = totalSeconds % 60;
    if (d > 0) return `${d}d ${h}h ${m}m`;
    if (h > 0) return `${h}h ${m}m ${s}s`;
    if (m > 0) return `${m}m ${s}s`;
    return `${s}s`;
  }

  private openedFallback(): string {
    // Without the opened timestamp input, approximate the window from 24h before due.
    const due = this.dueDate ? new Date(this.dueDate) : new Date();
    return new Date(due.getTime() - 24 * 3600 * 1000).toISOString();
  }
}
