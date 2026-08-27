import {
  Component,
  OnInit,
  OnDestroy,
  ChangeDetectionStrategy,
  inject,
  input,
} from '@angular/core';
import { Router } from '@angular/router';
import { Subject } from 'rxjs';
import { MatTabsModule } from '@angular/material/tabs';
import { MatCardModule } from '@angular/material/card';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatChipsModule } from '@angular/material/chips';
import { MatListModule } from '@angular/material/list';
import { MatDividerModule } from '@angular/material/divider';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatDialogModule, MatDialog } from '@angular/material/dialog';
import { AssetStore } from '../asset.store';
import {
  AssetStatus,
  ASSET_STATUS_OPTIONS,
  ASSET_STATUS_COLORS,
} from '../models/asset.model';

@Component({
  selector: 'app-asset-detail',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    MatTabsModule,
    MatCardModule,
    MatIconModule,
    MatButtonModule,
    MatChipsModule,
    MatListModule,
    MatDividerModule,
    MatProgressBarModule,
    MatDialogModule,
  ],
  templateUrl: './asset-detail.component.html',
  styleUrl: './asset-detail.component.scss',
})
export class AssetDetailComponent implements OnInit, OnDestroy {
  protected readonly store = inject(AssetStore);
  private readonly router = inject(Router);
  private readonly dialog = inject(MatDialog);

  protected readonly assetId = input.required<string>();

  protected readonly statusOptions = ASSET_STATUS_OPTIONS;
  private readonly destroy$ = new Subject<void>();

  ngOnInit(): void {
    this.store.loadAsset(this.assetId());
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
    this.store.clearSelectedAsset();
  }

  get currentStatus(): AssetStatus | null {
    return this.store.selectedAsset()?.status ?? null;
  }

  get allowedTransitions(): AssetStatus[] {
    const transitions: Record<AssetStatus, AssetStatus[]> = {
      InStock: ['Archived', 'Retired'],
      InUse: ['UnderMaintenance', 'Archived', 'Retired'],
      UnderMaintenance: ['InUse', 'Archived', 'Retired'],
      Archived: ['InStock'],
      Retired: ['Disposed'],
      Disposed: [],
    };
    const status = this.currentStatus;
    return status ? transitions[status] : [];
  }

  getStatusLabel(status: AssetStatus): string {
    return ASSET_STATUS_OPTIONS.find((s) => s.value === status)?.label ?? status;
  }

  getStatusColor(status: AssetStatus): string {
    return ASSET_STATUS_COLORS[status] ?? '#757575';
  }

  onEdit(): void {
    this.router.navigate(['/assets', this.assetId(), 'edit']);
  }

  async onDelete(): void {
    const asset = this.store.selectedAsset();
    if (asset && confirm(`Delete asset "${asset.name}"?`)) {
      const success = await this.store.deleteAsset(asset.id);
      if (success) this.router.navigate(['/assets']);
    }
  }

  onBack(): void {
    this.router.navigate(['/assets']);
  }

  async onTransition(toStatus: AssetStatus): Promise<void> {
    const reason = prompt(`Reason for transitioning to ${this.getStatusLabel(toStatus)}:`);
    if (reason === null) return; // User cancelled

    await this.store.transitionAsset(this.assetId(), toStatus, reason || undefined);
  }

  trackById = (_: number, item: { id: string }) => item.id;
}
