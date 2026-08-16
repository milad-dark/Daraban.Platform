import { Component, ChangeDetectionStrategy } from '@angular/core';
import { MatCardModule } from '@angular/material/card';
import { MatIconModule } from '@angular/material/icon';

@Component({
  selector: 'app-dashboard',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatCardModule, MatIconModule],
  template: `
    <mat-card>
      <mat-card-header>
        <mat-icon mat-card-avatar>dashboard</mat-icon>
        <mat-card-title>Dashboard</mat-card-title>
        <mat-card-subtitle>Phase 7 - Task 7.1 will build this out</mat-card-subtitle>
      </mat-card-header>
      <mat-card-content>
        <p style="margin-top: 16px;">
          You are successfully authenticated. The full dashboard arrives in Phase 7.
        </p>
      </mat-card-content>
    </mat-card>
  `,
})
export class DashboardComponent {}