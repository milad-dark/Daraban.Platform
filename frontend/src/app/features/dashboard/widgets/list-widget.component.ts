import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, input } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { AssetNearingEnd, RecentInventoryItem } from '../dashboard.models';
import { DashboardStore } from '../dashboard.store';

/**
 * Table-style widget for the two list payloads: RecentInventory and AssetsNearingEnd
 * (Task 7.1). All content is interpolated -- Angular sanitizes by default, so agent-
 * supplied strings (device IDs, hostnames) can never inject markup (OWASP A03).
 */
@Component({
  selector: 'app-list-widget',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatIconModule, DatePipe],
  template: `
    @switch (widgetType()) {
      @case ('RecentInventory') {
        @for (item of inventoryItems(); track item.id) {
          <div class="row">
            <span class="device">{{ item.deviceId }}</span>
            <span class="meta">{{ item.itemType || 'inventory' }}</span>
            <span class="badge" [class]="'badge ' + item.status.toLowerCase()">
              {{ item.status }}
            </span>
            <span class="time">{{ item.submittedAt | date: 'MMM d, HH:mm' }}</span>
          </div>
        } @empty {
          <div class="empty">No inventory submissions yet.</div>
        }
      }
      @case ('AssetsNearingEnd') {
        @for (asset of warrantyAssets(); track asset.id) {
          <div class="row">
            <span class="device">{{ asset.name }}</span>
            <span class="meta">{{ asset.typeName || '' }}</span>
            <span class="badge warn">
              @if (asset.daysRemaining <= 14) {
                <mat-icon inline>warning</mat-icon>
              }
              {{ asset.daysRemaining }}d
            </span>
            <span class="time">{{ asset.warrantyExpiry | date: 'MMM d, y' }}</span>
          </div>
        } @empty {
          <div class="empty">No warranties expiring soon.</div>
        }
      }
      @default {
        <div class="empty">Unsupported widget.</div>
      }
    }
  `,
  styles: [
    `
      :host {
        display: block;
        height: 100%;
        overflow: auto;
      }
      .row {
        display: flex;
        align-items: center;
        gap: 8px;
        padding: 6px 4px;
        border-bottom: 1px solid var(--mat-sys-outline-variant, #e0e0e0);
        font-size: 13px;
      }
      .device {
        flex: 1;
        overflow: hidden;
        text-overflow: ellipsis;
        white-space: nowrap;
        font-weight: 500;
      }
      .meta {
        color: var(--mat-sys-on-surface-variant, #757575);
      }
      .time {
        color: var(--mat-sys-on-surface-variant, #757575);
        font-size: 12px;
      }
      .badge {
        padding: 1px 8px;
        border-radius: 10px;
        font-size: 11px;
        text-transform: capitalize;
        background: rgba(0, 0, 0, 0.06);
      }
      .badge.pending {
        background: #fff3e0;
        color: #e65100;
      }
      .badge.processed {
        background: #e8f5e9;
        color: #2e7d32;
      }
      .badge.failed,
      .badge.warn {
        background: #ffebee;
        color: #c62828;
      }
      .empty {
        height: 100%;
        display: flex;
        align-items: center;
        justify-content: center;
        color: var(--mat-sys-on-surface-variant, #757575);
        font-size: 13px;
      }
    `,
  ],
})
export class ListWidgetComponent {
  private readonly store = inject(DashboardStore);

  widgetType = input.required<string>();

  private readonly items = computed(() => this.store.data()[this.widgetType()]?.items ?? []);

  readonly inventoryItems = computed(
    () => this.items() as unknown as RecentInventoryItem[]
  );

  readonly warrantyAssets = computed(
    () => this.items() as unknown as AssetNearingEnd[]
  );
}
