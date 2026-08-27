import {
  Component,
  OnInit,
  ChangeDetectionStrategy,
  inject,
  input,
  output,
} from '@angular/core';
import { Router } from '@angular/router';
import {
  ReactiveFormsModule,
  FormBuilder,
  FormGroup,
  Validators,
} from '@angular/forms';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatNativeDateModule } from '@angular/material/core';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { AssetStore } from '../asset.store';
import { AuthStore } from '../../../core/auth/auth.store';
import { Asset, CreateAssetRequest, UpdateAssetRequest } from '../models/asset.model';

@Component({
  selector: 'app-asset-form',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    ReactiveFormsModule,
    MatCardModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatButtonModule,
    MatIconModule,
    MatDatepickerModule,
    MatNativeDateModule,
    MatProgressBarModule,
  ],
  template: `
    <div class="asset-form-page">
      <div class="page-header">
        <button mat-icon-button (click)="onCancel()">
          <mat-icon>arrow_back</mat-icon>
        </button>
        <h1>{{ assetId() ? 'Edit Asset' : 'New Asset' }}</h1>
      </div>

      @if (store.isSaving()) {
        <mat-progress-bar mode="indeterminate"></mat-progress-bar>
      }

      <mat-card>
        <mat-card-content>
          <form [formGroup]="form" (ngSubmit)="onSubmit()">
            <div class="form-grid">
              <mat-form-field appearance="outline">
                <mat-label>Name</mat-label>
                <input matInput formControlName="name" placeholder="Asset name" />
                @if (form.controls.name.hasError('required')) {
                  <mat-error>Name is required</mat-error>
                }
              </mat-form-field>

              <mat-form-field appearance="outline">
                <mat-label>Asset Type</mat-label>
                <mat-select formControlName="assetTypeId">
                  @for (type of store.assetTypes(); track type.id) {
                    <mat-option [value]="type.id">{{ type.name }}</mat-option>
                  }
                </mat-select>
                @if (form.controls.assetTypeId.hasError('required')) {
                  <mat-error>Asset type is required</mat-error>
                }
              </mat-form-field>

              <mat-form-field appearance="outline">
                <mat-label>Asset Tag</mat-label>
                <input matInput formControlName="assetTag" placeholder="e.g. A-001" />
              </mat-form-field>

              <mat-form-field appearance="outline">
                <mat-label>Serial Number</mat-label>
                <input matInput formControlName="serialNumber" />
              </mat-form-field>

              <mat-form-field appearance="outline">
                <mat-label>Location</mat-label>
                <mat-select formControlName="locationId">
                  <mat-option [value]="null">None</mat-option>
                  @for (loc of store.locations(); track loc.id) {
                    <mat-option [value]="loc.id">{{ loc.name }}</mat-option>
                  }
                </mat-select>
              </mat-form-field>

              <mat-form-field appearance="outline">
                <mat-label>Purchase Date</mat-label>
                <input matInput [matDatepicker]="picker" formControlName="purchaseDate" />
                <mat-datepicker-toggle matSuffix [for]="picker"></mat-datepicker-toggle>
                <mat-datepicker #picker></mat-datepicker>
              </mat-form-field>

              <mat-form-field appearance="outline">
                <mat-label>Purchase Cost</mat-label>
                <input matInput type="number" formControlName="purchaseCost" />
              </mat-form-field>

              <mat-form-field appearance="outline">
                <mat-label>Currency</mat-label>
                <mat-select formControlName="purchaseCurrency">
                  <mat-option value="USD">USD</mat-option>
                  <mat-option value="EUR">EUR</mat-option>
                  <mat-option value="GBP">GBP</mat-option>
                </mat-select>
              </mat-form-field>

              <mat-form-field appearance="outline">
                <mat-label>Warranty Expiry</mat-label>
                <input matInput [matDatepicker]="warrantyPicker" formControlName="warrantyExpiry" />
                <mat-datepicker-toggle matSuffix [for]="warrantyPicker"></mat-datepicker-toggle>
                <mat-datepicker #warrantyPicker></mat-datepicker>
              </mat-form-field>

              <mat-form-field appearance="outline">
                <mat-label>Order Number</mat-label>
                <input matInput formControlName="orderNumber" />
              </mat-form-field>

              <mat-form-field appearance="outline">
                <mat-label>Supplier</mat-label>
                <input matInput formControlName="supplierName" />
              </mat-form-field>

              <mat-form-field appearance="outline" class="full-width">
                <mat-label>Notes</mat-label>
                <textarea matInput formControlName="notes" rows="3"></textarea>
              </mat-form-field>
            </div>

            <div class="form-actions">
              <button mat-button type="button" (click)="onCancel()">Cancel</button>
              <button mat-flat-button color="primary" type="submit" [disabled]="form.invalid || store.isSaving()">
                {{ assetId() ? 'Update' : 'Create' }}
              </button>
            </div>
          </form>
        </mat-card-content>
      </mat-card>

      @if (store.error()) {
        <div class="error-banner">
          <mat-icon>error</mat-icon>
          {{ store.error() }}
        </div>
      }
    </div>
  `,
  styleUrl: './asset-form.component.scss',
})
export class AssetFormComponent implements OnInit {
  protected readonly store = inject(AssetStore);
  private readonly authStore = inject(AuthStore);
  private readonly fb = inject(FormBuilder);
  private readonly router = inject(Router);

  protected readonly assetId = input<string | null>(null);
  protected readonly saved = output<string>();

  protected readonly form: FormGroup = this.fb.group({
    name: ['', Validators.required],
    assetTypeId: ['', Validators.required],
    assetTag: [''],
    serialNumber: [''],
    locationId: [null as string | null],
    purchaseDate: [null as Date | null],
    purchaseCost: [null as number | null],
    purchaseCurrency: ['USD'],
    warrantyExpiry: [null as Date | null],
    orderNumber: [''],
    supplierName: [''],
    notes: [''],
  });

  ngOnInit(): void {
    this.store.loadReferenceData();
    if (this.assetId()) {
      this.store.loadAsset(this.assetId()!);
    }
  }

  async onSubmit(): Promise<void> {
    if (this.form.invalid) return;

    const formVal = this.form.getRawValue();
    const nodeId = this.authStore.currentUser()?.activeEntityId ?? '';

    if (this.assetId()) {
      const req: UpdateAssetRequest = {
        name: formVal.name,
        assetModelId: null,
        locationId: formVal.locationId,
        assetTag: formVal.assetTag || null,
        serialNumber: formVal.serialNumber || null,
        purchaseDate: formVal.purchaseDate?.toISOString().split('T')[0] ?? null,
        purchaseCost: formVal.purchaseCost,
        purchaseCurrency: formVal.purchaseCurrency,
        orderNumber: formVal.orderNumber || null,
        supplierName: formVal.supplierName || null,
        warrantyExpiry: formVal.warrantyExpiry?.toISOString().split('T')[0] ?? null,
        notes: formVal.notes || null,
      };
      const ok = await this.store.updateAsset(this.assetId()!, req);
      if (ok) this.router.navigate(['/assets', this.assetId()]);
    } else {
      const req: CreateAssetRequest = {
        ...formVal,
        assetTypeId: formVal.assetTypeId,
        assetTag: formVal.assetTag || null,
        serialNumber: formVal.serialNumber || null,
        entityNodeId: nodeId,
        locationId: formVal.locationId,
        purchaseDate: formVal.purchaseDate?.toISOString().split('T')[0] ?? null,
        purchaseCost: formVal.purchaseCost,
        orderNumber: formVal.orderNumber || null,
        supplierName: formVal.supplierName || null,
        warrantyExpiry: formVal.warrantyExpiry?.toISOString().split('T')[0] ?? null,
        notes: formVal.notes || null,
      };
      const id = await this.store.createAsset(req);
      if (id) this.router.navigate(['/assets', id]);
    }
  }

  onCancel(): void {
    if (this.assetId()) {
      this.router.navigate(['/assets', this.assetId()]);
    } else {
      this.router.navigate(['/assets']);
    }
  }
}
