import { Component, Input, ChangeDetectionStrategy } from '@angular/core';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatIconModule } from '@angular/material/icon';

/**
 * Real-time status indicator for agent connectivity.
 *
 * - Green + pulse:   online (heartbeat within last 5 minutes)
 * - Amber + slow pulse: stale (heartbeat 5-15 minutes ago)
 * - Red:             offline (no heartbeat for >15 minutes or status != Active)
 * - Gray:            suspended/deactivated
 */
@Component({
  selector: 'app-heartbeat-indicator',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatTooltipModule, MatIconModule],
  template: `
    <span
      class="heartbeat-dot"
      [class.online]="status === 'online'"
      [class.stale]="status === 'stale'"
      [class.offline]="status === 'offline'"
      [class.suspended]="status === 'suspended'"
      [matTooltip]="tooltipText"
    >
      <span class="pulse-ring"></span>
    </span>
  `,
  styles: [`
    :host {
      display: inline-flex;
      align-items: center;
    }

    .heartbeat-dot {
      position: relative;
      width: 10px;
      height: 10px;
      border-radius: 50%;
      background-color: #9e9e9e;
      display: inline-block;
    }

    .heartbeat-dot.online {
      background-color: #4caf50;
    }

    .heartbeat-dot.online .pulse-ring {
      animation: pulse-green 2s ease-in-out infinite;
    }

    .heartbeat-dot.stale {
      background-color: #ff9800;
    }

    .heartbeat-dot.stale .pulse-ring {
      animation: pulse-amber 3s ease-in-out infinite;
    }

    .heartbeat-dot.offline {
      background-color: #f44336;
    }

    .heartbeat-dot.suspended {
      background-color: #9e9e9e;
    }

    .pulse-ring {
      position: absolute;
      top: 50%;
      left: 50%;
      transform: translate(-50%, -50%);
      width: 100%;
      height: 100%;
      border-radius: 50%;
      pointer-events: none;
    }

    @keyframes pulse-green {
      0%, 100% {
        box-shadow: 0 0 0 0 rgba(76, 175, 80, 0.6);
      }
      50% {
        box-shadow: 0 0 0 6px rgba(76, 175, 80, 0);
      }
    }

    @keyframes pulse-amber {
      0%, 100% {
        box-shadow: 0 0 0 0 rgba(255, 152, 0, 0.4);
      }
      50% {
        box-shadow: 0 0 0 4px rgba(255, 152, 0, 0);
      }
    }

    @media (prefers-reduced-motion: reduce) {
      .pulse-ring {
        animation: none !important;
      }
    }
  `],
})
export class HeartbeatIndicatorComponent {
  @Input() status: 'online' | 'stale' | 'offline' | 'suspended' = 'offline';
  @Input() lastActiveAt: string | null = null;

  get tooltipText(): string {
    switch (this.status) {
      case 'online':
        return `Online — last seen ${this.formatTime(this.lastActiveAt)}`;
      case 'stale':
        return `Stale — last seen ${this.formatTime(this.lastActiveAt)}`;
      case 'offline':
        return `Offline — last seen ${this.formatTime(this.lastActiveAt)}`;
      case 'suspended':
        return 'Suspended';
      default:
        return 'Unknown';
    }
  }

  private formatTime(isoString: string | null): string {
    if (!isoString) return 'never';
    const date = new Date(isoString);
    const now = new Date();
    const diffMs = now.getTime() - date.getTime();
    const diffMin = Math.floor(diffMs / 60000);

    if (diffMin < 1) return 'just now';
    if (diffMin < 60) return `${diffMin}m ago`;
    const diffHrs = Math.floor(diffMin / 60);
    if (diffHrs < 24) return `${diffHrs}h ago`;
    const diffDays = Math.floor(diffHrs / 24);
    return `${diffDays}d ago`;
  }
}
