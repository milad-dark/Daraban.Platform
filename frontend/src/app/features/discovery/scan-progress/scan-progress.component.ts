import { Component, Input, OnChanges, SimpleChanges } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ScanProgress, ScanStatus } from '../models/discovery.models';

@Component({
  selector: 'app-scan-progress',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="scan-progress" [class.hidden]="!progress">
      <div class="progress-header">
        <h3>Scan in Progress</h3>
        <span class="status-badge" [class]="getStatusClass()">
          {{ getStatusLabel() }}
        </span>
      </div>

      @if (progress) {
        <div class="progress-bar-container">
          <div class="progress-bar" [style.width.%]="progress.percentage"></div>
        </div>

        <div class="progress-stats">
          <div class="stat">
            <span class="stat-value">{{ progress.ipsScanned }}</span>
            <span class="stat-label">IPs Scanned</span>
          </div>
          <div class="stat">
            <span class="stat-value">{{ progress.totalIps }}</span>
            <span class="stat-label">Total IPs</span>
          </div>
          <div class="stat">
            <span class="stat-value">{{ progress.devicesFound }}</span>
            <span class="stat-label">Devices Found</span>
          </div>
          <div class="stat">
            <span class="stat-value">{{ progress.percentage }}%</span>
            <span class="stat-label">Complete</span>
          </div>
        </div>

        @if (progress.currentIp) {
          <div class="current-ip">
            Scanning: <code>{{ progress.currentIp }}</code>
          </div>
        }
      }
    </div>
  `,
  styles: [`
    .scan-progress {
      background: rgba(255, 255, 255, 0.03);
      border: 1px solid rgba(255, 255, 255, 0.08);
      border-radius: 16px;
      padding: 20px;
      margin-bottom: 24px;
      transition: all 0.3s ease;
    }

    .scan-progress.hidden {
      display: none;
    }

    .progress-header {
      display: flex;
      justify-content: space-between;
      align-items: center;
      margin-bottom: 16px;
    }

    .progress-header h3 {
      margin: 0;
      color: #fff;
      font-size: 1rem;
    }

    .status-badge {
      padding: 4px 12px;
      border-radius: 20px;
      font-size: 0.75rem;
    }

    .status-badge.running {
      background: rgba(59, 130, 246, 0.2);
      color: #3b82f6;
      animation: pulse 2s infinite;
    }

    .status-badge.completed {
      background: rgba(16, 185, 129, 0.2);
      color: #10b981;
    }

    .status-badge.failed {
      background: rgba(239, 68, 68, 0.2);
      color: #ef4444;
    }

    @keyframes pulse {
      0%, 100% { opacity: 1; }
      50% { opacity: 0.7; }
    }

    .progress-bar-container {
      background: rgba(255, 255, 255, 0.1);
      border-radius: 8px;
      height: 8px;
      margin-bottom: 16px;
      overflow: hidden;
    }

    .progress-bar {
      background: linear-gradient(90deg, #3b82f6, #8b5cf6);
      height: 100%;
      border-radius: 8px;
      transition: width 0.3s ease;
    }

    .progress-stats {
      display: grid;
      grid-template-columns: repeat(4, 1fr);
      gap: 16px;
      margin-bottom: 16px;
    }

    .stat {
      text-align: center;
    }

    .stat-value {
      display: block;
      font-size: 1.5rem;
      font-weight: 600;
      color: #fff;
    }

    .stat-label {
      display: block;
      font-size: 0.75rem;
      color: #a0a0b0;
      margin-top: 4px;
    }

    .current-ip {
      text-align: center;
      color: #a0a0b0;
      font-size: 0.85rem;
    }

    .current-ip code {
      background: rgba(255, 255, 255, 0.1);
      padding: 4px 8px;
      border-radius: 4px;
      color: #3b82f6;
    }

    @media (max-width: 768px) {
      .progress-stats {
        grid-template-columns: repeat(2, 1fr);
      }
    }
  `]
})
export class ScanProgressComponent implements OnChanges {
  @Input() progress: ScanProgress | null = null;

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['progress']) {
      // Progress updated
    }
  }

  getStatusClass(): string {
    if (!this.progress) return '';
    return this.progress.status === ScanStatus.Running ? 'running' :
           this.progress.status === ScanStatus.Completed ? 'completed' :
           this.progress.status === ScanStatus.Failed ? 'failed' : '';
  }

  getStatusLabel(): string {
    if (!this.progress) return '';
    return ScanStatus[this.progress.status] || 'Unknown';
  }
}
