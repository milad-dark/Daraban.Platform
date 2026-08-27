import {
  Component,
  ChangeDetectionStrategy,
  inject,
  signal,
} from '@angular/core';
import { Router } from '@angular/router';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatListModule } from '@angular/material/list';
import { MatDividerModule } from '@angular/material/divider';
import { MatChipsModule } from '@angular/material/chips';
import { FormsModule } from '@angular/forms';
import { AssetService } from '../asset.service';
import { AssetStore } from '../asset.store';
import { ImportResult } from '../models/asset.model';

@Component({
  selector: 'app-asset-import',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    MatCardModule,
    MatButtonModule,
    MatIconModule,
    MatProgressBarModule,
    MatCheckboxModule,
    MatListModule,
    MatDividerModule,
    MatChipsModule,
    FormsModule,
  ],
  template: `
    <div class="import-page">
      <div class="page-header">
        <button mat-icon-button (click)="onBack()">
          <mat-icon>arrow_back</mat-icon>
        </button>
        <h1>Import Assets</h1>
      </div>

      <!-- Upload Card -->
      <mat-card>
        <mat-card-content>
          <div
            class="upload-zone"
            (click)="fileInput.click()"
            (dragover)="$event.preventDefault()"
            (drop)="onDrop($event)">
            <input
              #fileInput
              type="file"
              accept=".csv,.xlsx"
              hidden
              (change)="onFileSelected($event)" />
            <mat-icon class="upload-icon">cloud_upload</mat-icon>
            <p class="upload-text">
              @if (selectedFile()) {
                {{ selectedFile()!.name }} ({{ formatSize(selectedFile()!.size) }})
              } @ else {
                Click or drag a CSV/XLSX file here
              }
            </p>
          </div>

          <div class="upload-options">
            <mat-checkbox [ngModel]="dryRun()" (ngModelChange)="dryRun.set($event)">
              Dry run (validate only, no import)
            </mat-checkbox>
          </div>

          <div class="upload-actions">
            <button mat-stroked-button (click)="downloadTemplate()">
              <mat-icon>download</mat-icon>
              Download Template
            </button>
            <button
              mat-flat-button
              color="primary"
              [disabled]="!selectedFile() || isUploading()"
              (click)="onImport()">
              @if (isUploading()) {
                Importing...
              } @ else {
                {{ dryRun() ? 'Validate' : 'Import' }}
              }
            </button>
          </div>
        </mat-card-content>
      </mat-card>

      @if (isUploading()) {
        <mat-progress-bar mode="indeterminate"></mat-progress-bar>
      }

      <!-- Results -->
      @if (importResult(); as result) {
        <mat-card>
          <mat-card-header>
            <mat-icon mat-card-avatar [color]="result.errorCount > 0 ? 'warn' : 'primary'">
              {{ result.errorCount > 0 ? 'warning' : 'check_circle' }}
            </mat-icon>
            <mat-card-title>
              {{ result.dryRun ? 'Validation' : 'Import' }} Complete
            </mat-card-title>
            <mat-card-subtitle>
              {{ result.successCount }} succeeded · {{ result.errorCount }} failed · {{ result.totalRows }} total
            </mat-card-subtitle>
          </mat-card-header>
          <mat-card-content>
            <mat-list>
              @for (row of result.rows; track row.rowNumber) {
                <mat-list-item [class.error-row]="!row.success">
                  <mat-icon matListItemIcon [color]="row.success ? 'primary' : 'warn'">
                    {{ row.success ? 'check' : 'error' }}
                  </mat-icon>
                  <span matListItemTitle>
                    Row {{ row.rowNumber }}: {{ row.assetName ?? 'Unknown' }}
                  </span>
                  @if (row.errors.length > 0) {
                    <span matListItemLine class="error-text">
                      {{ row.errors.join(', ') }}
                    </span>
                  }
                </mat-list-item>
              }
            </mat-list>
          </mat-card-content>
        </mat-card>
      }

      @if (error()) {
        <div class="error-banner">
          <mat-icon>error</mat-icon>
          {{ error() }}
        </div>
      }
    </div>
  `,
  styleUrl: './asset-import.component.scss',
})
export class AssetImportComponent {
  private readonly assetService = inject(AssetService);
  private readonly store = inject(AssetStore);
  private readonly router = inject(Router);

  protected readonly selectedFile = signal<File | null>(null);
  protected readonly isUploading = signal(false);
  protected readonly importResult = signal<ImportResult | null>(null);
  protected readonly error = signal<string | null>(null);
  protected readonly dryRun = signal(false);

  onFileSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    if (input.files?.length) {
      this.selectedFile.set(input.files[0]);
    }
  }

  onDrop(event: DragEvent): void {
    event.preventDefault();
    const file = event.dataTransfer?.files?.[0];
    if (file) this.selectedFile.set(file);
  }

  async onImport(): Promise<void> {
    const file = this.selectedFile();
    if (!file) return;

    this.isUploading.set(true);
    this.error.set(null);
    this.importResult.set(null);

    this.assetService.importAssets(file, this.dryRun()).subscribe({
      next: (result) => {
        this.importResult.set(result);
        this.isUploading.set(false);
        if (!this.dryRun() && result.errorCount === 0) {
          this.store.loadAssets();
        }
      },
      error: (err) => {
        this.error.set(err?.error?.title ?? 'Import failed');
        this.isUploading.set(false);
      },
    });
  }

  downloadTemplate(): void {
    window.open(this.assetService.getImportTemplate(), '_blank');
  }

  onBack(): void {
    this.router.navigate(['/assets']);
  }

  formatSize(bytes: number): string {
    if (bytes < 1024) return `${bytes} B`;
    if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
    return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
  }
}
