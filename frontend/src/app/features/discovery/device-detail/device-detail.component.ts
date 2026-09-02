import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { DiscoveryService } from '../discovery.service';
import { DiscoveryStore } from '../discovery.store';
import { DiscoveredDevice } from '../models/discovery.models';

@Component({
  selector: 'app-device-detail',
  standalone: true,
  imports: [CommonModule, RouterLink],
  template: `
    <div class="device-detail">
      <div class="header">
        <h1>Device Details</h1>
        <button class="btn-secondary" (click)="goBack()">← Back to Scan</button>
      </div>

      @if (store.loading()) {
        <div class="loading">Loading device details...</div>
      }

      @if (store.selectedDevice(); as device) {
        <div class="device-grid">
          <!-- Basic Info -->
          <div class="detail-card">
            <h3>Basic Information</h3>
            <div class="detail-row">
              <span class="label">IP Address</span>
              <span class="value">{{ device.ipAddress }}</span>
            </div>
            <div class="detail-row">
              <span class="label">MAC Address</span>
              <span class="value">{{ device.macAddress || 'Not available' }}</span>
            </div>
            <div class="detail-row">
              <span class="label">Hostname</span>
              <span class="value">{{ device.hostname || 'Not available' }}</span>
            </div>
            <div class="detail-row">
              <span class="label">Discovered At</span>
              <span class="value">{{ device.discoveredAt | date:'medium' }}</span>
            </div>
            <div class="detail-row">
              <span class="label">Last Seen</span>
              <span class="value">{{ device.lastSeenAt | date:'medium' }}</span>
            </div>
          </div>

          <!-- OS Information -->
          <div class="detail-card">
            <h3>Operating System</h3>
            <div class="detail-row">
              <span class="label">OS Guess</span>
              <span class="value">{{ device.osGuess || 'Unknown' }}</span>
            </div>
            <div class="detail-row">
              <span class="label">OS Version</span>
              <span class="value">{{ device.osVersion || 'Unknown' }}</span>
            </div>
            <div class="detail-row">
              <span class="label">TTL</span>
              <span class="value">{{ device.ttl || 'N/A' }}</span>
            </div>
          </div>

          <!-- SNMP Information -->
          <div class="detail-card">
            <h3>SNMP Information</h3>
            <div class="detail-row">
              <span class="label">System Description</span>
              <span class="value sys-descr">{{ device.sysDescr || 'Not available' }}</span>
            </div>
            <div class="detail-row">
              <span class="label">System Name</span>
              <span class="value">{{ device.sysName || 'Not available' }}</span>
            </div>
            <div class="detail-row">
              <span class="label">System Location</span>
              <span class="value">{{ device.sysLocation || 'Not available' }}</span>
            </div>
            <div class="detail-row">
              <span class="label">System Contact</span>
              <span class="value">{{ device.sysContact || 'Not available' }}</span>
            </div>
            <div class="detail-row">
              <span class="label">Uptime</span>
              <span class="value">{{ formatUptime(device.snmpUptime) }}</span>
            </div>
          </div>

          <!-- Network Information -->
          <div class="detail-card">
            <h3>Network Information</h3>
            <div class="detail-row">
              <span class="label">Ping Response</span>
              <span class="value">{{ device.pingMs }}ms</span>
            </div>
            <div class="detail-row">
              <span class="label">Vendor</span>
              <span class="value">{{ device.vendor || 'Unknown' }}</span>
            </div>
            <div class="detail-row">
              <span class="label">Model</span>
              <span class="value">{{ device.model || 'Unknown' }}</span>
            </div>
            <div class="detail-row">
              <span class="label">Serial Number</span>
              <span class="value">{{ device.serialNumber || 'Not available' }}</span>
            </div>
          </div>

          <!-- Open Ports -->
          <div class="detail-card full-width">
            <h3>Open Ports</h3>
            @if (device.openPorts) {
              <div class="ports-grid">
                @for (port of parsePorts(device.openPorts); track port) {
                  <div class="port-badge">
                    {{ port }}
                  </div>
                }
              </div>
            } @else {
              <div class="no-ports">No open ports detected</div>
            }
          </div>

          <!-- Asset Information -->
          <div class="detail-card">
            <h3>Asset Information</h3>
            <div class="detail-row">
              <span class="label">Asset Created</span>
              <span class="value">
                @if (device.assetCreated) {
                  <span class="asset-badge">Yes</span>
                } @else {
                  <span class="no-asset">No</span>
                }
              </span>
            </div>
            @if (device.assetId) {
              <div class="detail-row">
                <span class="label">Asset ID</span>
                <span class="value">
                  <a [routerLink]="['/assets', device.assetId]">View Asset</a>
                </span>
              </div>
            }
          </div>
        </div>
      }
    </div>
  `,
  styles: [`
    .device-detail {
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

    .device-grid {
      display: grid;
      grid-template-columns: repeat(2, 1fr);
      gap: 20px;
    }

    .detail-card {
      background: rgba(255, 255, 255, 0.03);
      border: 1px solid rgba(255, 255, 255, 0.08);
      border-radius: 16px;
      padding: 20px;
    }

    .detail-card.full-width {
      grid-column: 1 / -1;
    }

    .detail-card h3 {
      color: #fff;
      margin: 0 0 16px 0;
      font-size: 1rem;
      border-bottom: 1px solid rgba(255, 255, 255, 0.1);
      padding-bottom: 12px;
    }

    .detail-row {
      display: flex;
      justify-content: space-between;
      padding: 8px 0;
      border-bottom: 1px solid rgba(255, 255, 255, 0.05);
    }

    .detail-row:last-child {
      border-bottom: none;
    }

    .label {
      color: #a0a0b0;
      font-size: 0.85rem;
    }

    .value {
      color: #fff;
      font-size: 0.85rem;
      text-align: right;
      max-width: 60%;
      word-break: break-word;
    }

    .sys-descr {
      font-size: 0.8rem;
      line-height: 1.4;
    }

    .ports-grid {
      display: flex;
      flex-wrap: wrap;
      gap: 8px;
    }

    .port-badge {
      background: rgba(59, 130, 246, 0.2);
      color: #3b82f6;
      padding: 6px 12px;
      border-radius: 8px;
      font-size: 0.85rem;
    }

    .no-ports {
      color: #6b7280;
      font-style: italic;
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

    a {
      color: #3b82f6;
      text-decoration: none;
    }

    a:hover {
      text-decoration: underline;
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

    .loading {
      text-align: center;
      padding: 40px;
      color: #a0a0b0;
    }

    @media (max-width: 768px) {
      .device-grid {
        grid-template-columns: 1fr;
      }
    }
  `]
})
export class DeviceDetailComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly discoveryService = inject(DiscoveryService);
  readonly store = inject(DiscoveryStore);

  ngOnInit(): void {
    const deviceId = this.route.snapshot.paramMap.get('id');
    if (deviceId) {
      this.loadDevice(parseInt(deviceId, 10));
    }
  }

  loadDevice(id: number): void {
    this.store.setLoading(true);
    this.discoveryService.getDeviceById(id).subscribe({
      next: (device) => {
        this.store.setSelectedDevice(device);
        this.store.setLoading(false);
      },
      error: (err) => {
        this.store.setError('Failed to load device');
        this.store.setLoading(false);
      }
    });
  }

  goBack(): void {
    window.history.back();
  }

  parsePorts(ports: string): number[] {
    return ports.split(',').map(p => parseInt(p.trim(), 10)).filter(p => !isNaN(p));
  }

  formatUptime(uptime?: number): string {
    if (!uptime) return 'N/A';
    
    const seconds = Math.floor(uptime / 100);
    const minutes = Math.floor(seconds / 60);
    const hours = Math.floor(minutes / 60);
    const days = Math.floor(hours / 24);

    if (days > 0) return `${days}d ${hours % 24}h`;
    if (hours > 0) return `${hours}h ${minutes % 60}m`;
    return `${minutes}m ${seconds % 60}s`;
  }
}
