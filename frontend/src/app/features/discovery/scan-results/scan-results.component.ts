import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { DiscoveryService } from '../discovery.service';
import { DiscoveryStore } from '../discovery.store';
import { DiscoveryScan, DiscoveredDevice, ScanStatus } from '../models/discovery.models';

@Component({
  selector: 'app-scan-results',
  standalone: true,
  imports: [CommonModule, RouterLink],
  template: `
    <div class="scan-results">
      <div class="header">
        <h1>Scan Results</h1>
        <button class="btn-secondary" (click)="goBack()">← Back to Ranges</button>
      </div>

      @if (store.loading()) {
        <div class="loading">Loading scan results...</div>
      }

      @if (store.selectedScan(); as scan) {
        <div class="scan-info">
          <div class="info-card">
            <span class="label">Status</span>
            <span class="status-badge" [class]="getStatusClass(scan.status)">
              {{ getStatusLabel(scan.status) }}
            </span>
          </div>
          <div class="info-card">
            <span class="label">Devices Found</span>
            <span class="value">{{ scan.devicesFound }}</span>
          </div>
          <div class="info-card">
            <span class="label">IPs Responded</span>
            <span class="value">{{ scan.ipsResponded }} / {{ scan.totalIps }}</span>
          </div>
          <div class="info-card">
            <span class="label">Started</span>
            <span class="value">{{ scan.startedAt | date:'short' }}</span>
          </div>
          <div class="info-card">
            <span class="label">Completed</span>
            <span class="value">{{ scan.completedAt | date:'short' }}</span>
          </div>
        </div>

        @if (scan.errorMessage) {
          <div class="error-message">
            <strong>Error:</strong> {{ scan.errorMessage }}
          </div>
        }
      }

      <div class="devices-section">
        <h2>Discovered Devices</h2>
        
        @if (store.devices().length === 0) {
          <div class="empty-state">
            No devices discovered in this scan.
          </div>
        } @else {
          <div class="devices-table">
            <table>
              <thead>
                <tr>
                  <th>IP Address</th>
                  <th>Hostname</th>
                  <th>OS</th>
                  <th>Open Ports</th>
                  <th>Last Seen</th>
                  <th>Asset</th>
                  <th>Actions</th>
                </tr>
              </thead>
              <tbody>
                @for (device of store.devices(); track device.id) {
                  <tr [class.offline]="!device.lastSeenAt">
                    <td>{{ device.ipAddress }}</td>
                    <td>{{ device.hostname || '-' }}</td>
                    <td>{{ device.osGuess || '-' }}</td>
                    <td>{{ device.openPorts || '-' }}</td>
                    <td>{{ device.lastSeenAt | date:'short' }}</td>
                    <td>
                      @if (device.assetCreated) {
                        <span class="asset-badge">Created</span>
                      } @else {
                        <span class="no-asset">None</span>
                      }
                    </td>
                    <td>
                      <button class="btn-link" [routerLink]="['/discovery/devices', device.id]">
                        View Details
                      </button>
                    </td>
                  </tr>
                }
              </tbody>
            </table>
          </div>
        }
      </div>
    </div>
  `,
  styles: [`
    .scan-results {
      padding: 24px;
    }

    .header {
      display: flex;
      justify-content: space-between;
      align-items: center;
      margin-bottom: 24px;
    }

    .header h1 {
      margin: 0;
      color: #fff;
    }

    .scan-info {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(150px, 1fr));
      gap: 16px;
      margin-bottom: 24px;
    }

    .info-card {
      background: rgba(255, 255, 255, 0.03);
      border: 1px solid rgba(255, 255, 255, 0.08);
      border-radius: 12px;
      padding: 16px;
      text-align: center;
    }

    .info-card .label {
      display: block;
      color: #a0a0b0;
      font-size: 0.75rem;
      margin-bottom: 8px;
    }

    .info-card .value {
      display: block;
      color: #fff;
      font-size: 1.25rem;
      font-weight: 600;
    }

    .status-badge {
      display: inline-block;
      padding: 4px 12px;
      border-radius: 20px;
      font-size: 0.75rem;
    }

    .status-badge.queued {
      background: rgba(245, 158, 11, 0.2);
      color: #f59e0b;
    }

    .status-badge.running {
      background: rgba(59, 130, 246, 0.2);
      color: #3b82f6;
    }

    .status-badge.completed {
      background: rgba(16, 185, 129, 0.2);
      color: #10b981;
    }

    .status-badge.failed {
      background: rgba(239, 68, 68, 0.2);
      color: #ef4444;
    }

    .error-message {
      background: rgba(239, 68, 68, 0.1);
      border: 1px solid rgba(239, 68, 68, 0.2);
      border-radius: 8px;
      padding: 16px;
      margin-bottom: 24px;
      color: #ef4444;
    }

    .devices-section {
      margin-top: 24px;
    }

    .devices-section h2 {
      color: #fff;
      margin-bottom: 16px;
    }

    .devices-table {
      overflow-x: auto;
    }

    table {
      width: 100%;
      border-collapse: collapse;
      background: rgba(255, 255, 255, 0.03);
      border-radius: 12px;
      overflow: hidden;
    }

    th, td {
      padding: 12px 16px;
      text-align: left;
      border-bottom: 1px solid rgba(255, 255, 255, 0.05);
    }

    th {
      background: rgba(255, 255, 255, 0.05);
      color: #a0a0b0;
      font-weight: 500;
      font-size: 0.85rem;
    }

    td {
      color: #fff;
      font-size: 0.85rem;
    }

    tr.offline {
      opacity: 0.6;
    }

    .asset-badge {
      background: rgba(16, 185, 129, 0.2);
      color: #10b981;
      padding: 4px 8px;
      border-radius: 4px;
      font-size: 0.75rem;
    }

    .no-asset {
      color: #6b7280;
    }

    .btn-secondary {
      background: rgba(255, 255, 255, 0.05);
      color: #fff;
      border: 1px solid rgba(255, 255, 255, 0.1);
      padding: 8px 16px;
      border-radius: 8px;
      cursor: pointer;
      font-size: 0.85rem;
      transition: all 0.3s ease;
    }

    .btn-secondary:hover {
      background: rgba(255, 255, 255, 0.1);
    }

    .btn-link {
      background: none;
      border: none;
      color: #3b82f6;
      cursor: pointer;
      font-size: 0.85rem;
    }

    .btn-link:hover {
      text-decoration: underline;
    }

    .loading, .empty-state {
      text-align: center;
      padding: 40px;
      color: #a0a0b0;
    }
  `]
})
export class ScanResultsComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly discoveryService = inject(DiscoveryService);
  readonly store = inject(DiscoveryStore);

  ngOnInit(): void {
    const scanId = this.route.snapshot.paramMap.get('id');
    if (scanId) {
      this.loadScan(scanId);
    }
  }

  loadScan(scanId: string): void {
    this.store.setLoading(true);
    this.discoveryService.getScanById(scanId).subscribe({
      next: (scan) => {
        this.store.setSelectedScan(scan);
        this.loadDevices(scanId);
      },
      error: (err) => {
        this.store.setError('Failed to load scan');
        this.store.setLoading(false);
      }
    });
  }

  loadDevices(scanId: string): void {
    this.discoveryService.getDevicesByScan(scanId).subscribe({
      next: (devices) => {
        this.store.setDevices(devices);
        this.store.setLoading(false);
      },
      error: (err) => {
        this.store.setError('Failed to load devices');
        this.store.setLoading(false);
      }
    });
  }

  goBack(): void {
    window.history.back();
  }

  getStatusClass(status: ScanStatus): string {
    const classes: Record<ScanStatus, string> = {
      [ScanStatus.Queued]: 'queued',
      [ScanStatus.Running]: 'running',
      [ScanStatus.Completed]: 'completed',
      [ScanStatus.Failed]: 'failed',
      [ScanStatus.Cancelled]: 'failed'
    };
    return classes[status] || '';
  }

  getStatusLabel(status: ScanStatus): string {
    const labels: Record<ScanStatus, string> = {
      [ScanStatus.Queued]: 'Queued',
      [ScanStatus.Running]: 'Running',
      [ScanStatus.Completed]: 'Completed',
      [ScanStatus.Failed]: 'Failed',
      [ScanStatus.Cancelled]: 'Cancelled'
    };
    return labels[status] || 'Unknown';
  }
}
