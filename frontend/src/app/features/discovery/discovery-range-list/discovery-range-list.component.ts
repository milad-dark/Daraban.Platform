import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { DiscoveryService } from '../discovery.service';
import { DiscoveryStore } from '../discovery.store';
import { DiscoveryRange, ScanType } from '../models/discovery.models';

@Component({
  selector: 'app-discovery-range-list',
  standalone: true,
  imports: [CommonModule, RouterLink],
  template: `
    <div class="discovery-ranges">
      <div class="header">
        <h1>Discovery Ranges</h1>
        <button class="btn-primary" (click)="createRange()">
          + New Range
        </button>
      </div>

      @if (store.loading()) {
        <div class="loading">Loading...</div>
      }

      @if (store.error()) {
        <div class="error">{{ store.error() }}</div>
      }

      <div class="range-grid">
        @for (range of store.ranges(); track range.id) {
          <div class="range-card" [class.inactive]="!range.isActive">
            <div class="range-header">
              <h3>{{ range.name }}</h3>
              <span class="status-badge" [class.active]="range.isActive">
                {{ range.isActive ? 'Active' : 'Inactive' }}
              </span>
            </div>
            
            <div class="range-details">
              <div class="detail-row">
                <span class="label">CIDR:</span>
                <span class="value">{{ range.cidrRange }}</span>
              </div>
              <div class="detail-row">
                <span class="label">Scan Type:</span>
                <span class="value">{{ getScanTypeLabel(range.scanType) }}</span>
              </div>
              <div class="detail-row">
                <span class="label">Interval:</span>
                <span class="value">{{ range.scanIntervalHours }}h</span>
              </div>
              <div class="detail-row">
                <span class="label">Last Scan:</span>
                <span class="value">{{ range.lastScanAt | date:'short' }}</span>
              </div>
            </div>

            <div class="range-actions">
              <button class="btn-secondary" (click)="startScan(range)">
                Start Scan
              </button>
              <button class="btn-secondary" [routerLink]="[range.id]">
                View Details
              </button>
              <button class="btn-danger" (click)="deleteRange(range)">
                Delete
              </button>
            </div>
          </div>
        }
      </div>
    </div>
  `,
  styles: [`
    .discovery-ranges {
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

    .range-grid {
      display: grid;
      grid-template-columns: repeat(auto-fill, minmax(350px, 1fr));
      gap: 20px;
    }

    .range-card {
      background: rgba(255, 255, 255, 0.03);
      border: 1px solid rgba(255, 255, 255, 0.08);
      border-radius: 16px;
      padding: 20px;
      transition: all 0.3s ease;
    }

    .range-card:hover {
      border-color: rgba(255, 255, 255, 0.15);
      transform: translateY(-2px);
    }

    .range-card.inactive {
      opacity: 0.6;
    }

    .range-header {
      display: flex;
      justify-content: space-between;
      align-items: center;
      margin-bottom: 16px;
    }

    .range-header h3 {
      margin: 0;
      color: #fff;
      font-size: 1.1rem;
    }

    .status-badge {
      padding: 4px 12px;
      border-radius: 20px;
      font-size: 0.75rem;
      background: rgba(239, 68, 68, 0.2);
      color: #ef4444;
    }

    .status-badge.active {
      background: rgba(16, 185, 129, 0.2);
      color: #10b981;
    }

    .range-details {
      margin-bottom: 16px;
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
    }

    .range-actions {
      display: flex;
      gap: 8px;
      flex-wrap: wrap;
    }

    .btn-primary {
      background: linear-gradient(135deg, #3b82f6, #8b5cf6);
      color: #fff;
      border: none;
      padding: 10px 20px;
      border-radius: 8px;
      cursor: pointer;
      font-weight: 500;
      transition: all 0.3s ease;
    }

    .btn-primary:hover {
      transform: translateY(-1px);
      box-shadow: 0 4px 12px rgba(59, 130, 246, 0.3);
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

    .btn-danger {
      background: rgba(239, 68, 68, 0.1);
      color: #ef4444;
      border: 1px solid rgba(239, 68, 68, 0.2);
      padding: 8px 16px;
      border-radius: 8px;
      cursor: pointer;
      font-size: 0.85rem;
      transition: all 0.3s ease;
    }

    .btn-danger:hover {
      background: rgba(239, 68, 68, 0.2);
    }

    .loading, .error {
      text-align: center;
      padding: 40px;
      color: #a0a0b0;
    }

    .error {
      color: #ef4444;
    }
  `]
})
export class DiscoveryRangeListComponent implements OnInit {
  private readonly discoveryService = inject(DiscoveryService);
  readonly store = inject(DiscoveryStore);

  ngOnInit(): void {
    this.loadRanges();
  }

  loadRanges(): void {
    this.store.setLoading(true);
    this.discoveryService.getRanges().subscribe({
      next: (ranges) => {
        this.store.setRanges(ranges);
        this.store.setLoading(false);
      },
      error: (err) => {
        this.store.setError('Failed to load ranges');
        this.store.setLoading(false);
      }
    });
  }

  createRange(): void {
    // TODO: Open modal or navigate to create form
    console.log('Create range');
  }

  startScan(range: DiscoveryRange): void {
    this.discoveryService.startScan(range.id).subscribe({
      next: (scan) => {
        console.log('Scan started:', scan);
        // TODO: Show success message
      },
      error: (err) => {
        console.error('Failed to start scan:', err);
        // TODO: Show error message
      }
    });
  }

  deleteRange(range: DiscoveryRange): void {
    if (confirm(`Are you sure you want to delete "${range.name}"?`)) {
      this.discoveryService.deleteRange(range.id).subscribe({
        next: () => {
          this.store.removeRange(range.id);
        },
        error: (err) => {
          console.error('Failed to delete range:', err);
        }
      });
    }
  }

  getScanTypeLabel(scanType: ScanType): string {
    const labels: Record<ScanType, string> = {
      [ScanType.Ping]: 'Ping',
      [ScanType.Snmp]: 'SNMP',
      [ScanType.Wmi]: 'WMI',
      [ScanType.Ssh]: 'SSH',
      [ScanType.Http]: 'HTTP',
      [ScanType.Combined]: 'Combined'
    };
    return labels[scanType] || 'Unknown';
  }
}
