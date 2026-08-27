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
import { AssetStore } from '../asset.store';
import { AssetService } from '../asset.service';
import { AssetList as AssetListItem, ASSET_STATUS_OPTIONS } from '../models/asset.model';

@Component({
  selector: 'app-asset-list',
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
  ],
  templateUrl: './asset-list.component.html',
  styleUrl: './asset-list.component.scss',
})
export class AssetListComponent implements OnInit, OnDestroy {
  protected readonly store = inject(AssetStore);
  private readonly router = inject(Router);
  private readonly assetService = inject(AssetService);

  protected readonly searchControl = new FormControl('', { nonNullable: true });
  protected readonly statusFilter = new FormControl<string | null>(null);
  protected readonly statusOptions = ASSET_STATUS_OPTIONS;

  protected readonly displayedColumns = [
    'name',
    'assetTag',
    'assetTypeName',
    'status',
    'locationName',
    'warrantyExpiry',
    'actions',
  ];

  private readonly destroy$ = new Subject<void>();

  ngOnInit(): void {
    // Load initial data
    this.store.loadAssets();
    this.store.loadReferenceData();

    // Debounced search
    this.searchControl.valueChanges
      .pipe(debounceTime(300), takeUntil(this.destroy$))
      .subscribe((search) => {
        this.store.updateFilters({ search: search || null });
      });

    // Status filter
    this.statusFilter.valueChanges
      .pipe(takeUntil(this.destroy$))
      .subscribe((status) => {
        this.store.updateFilters({ status });
      });
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  onPageChange(event: PageEvent): void {
    // setPageSize already resets page to 1; only call setPage when page size unchanged
    if (event.pageSize !== this.store.pageSize()) {
      this.store.setPageSize(event.pageSize);
    } else {
      this.store.setPage(event.pageIndex + 1);
    }
  }

  onSort(sort: Sort): void {
    // Sorting handled client-side on current page
  }

  onView(asset: AssetListItem): void {
    this.router.navigate(['/assets', asset.id]);
  }

  onEdit(asset: AssetListItem): void {
    this.router.navigate(['/assets', asset.id, 'edit']);
  }

  async onDelete(asset: AssetListItem): Promise<void> {
    if (confirm(`Delete asset "${asset.name}"?`)) {
      const success = await this.store.deleteAsset(asset.id);
      if (success) this.store.loadAssets();
    }
  }

  onExport(format: 'csv' | 'xlsx'): void {
    const f = this.store.filters();
    const url = this.assetService.exportAssets(
      format,
      f.status ?? undefined,
      f.assetTypeId ?? undefined,
      f.locationId ?? undefined,
      f.search ?? undefined
    );
    window.open(url, '_blank');
  }

  onCreate(): void {
    this.router.navigate(['/assets/new']);
  }

  getStatusColor(status: string): string {
    const colors: Record<string, string> = {
      InStock: 'primary',
      InUse: 'accent',
      UnderMaintenance: 'warn',
      Archived: 'disabled',
      Retired: 'warn',
      Disposed: 'disabled',
    };
    return colors[status] || 'primary';
  }

  getStatusLabel(status: string): string {
    return ASSET_STATUS_OPTIONS.find((s) => s.value === status)?.label ?? status;
  }

  trackById = (_: number, item: AssetListItem) => item.id;
}
