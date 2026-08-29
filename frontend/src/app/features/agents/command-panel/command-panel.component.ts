import {
  Component,
  Input,
  ChangeDetectionStrategy,
  inject,
} from '@angular/core';
import { FormControl, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatSelectModule } from '@angular/material/select';
import { MatInputModule } from '@angular/material/input';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatCardModule } from '@angular/material/card';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatTooltipModule } from '@angular/material/tooltip';
import { AgentStore } from '../agent.store';
import { COMMAND_TYPE_OPTIONS, CommandType } from '../models/agent.model';

@Component({
  selector: 'app-command-panel',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    ReactiveFormsModule,
    MatSelectModule,
    MatInputModule,
    MatFormFieldModule,
    MatButtonModule,
    MatIconModule,
    MatCardModule,
    MatProgressBarModule,
    MatTooltipModule,
  ],
  template: `
    <mat-card class="command-panel">
      <h3>Dispatch Command</h3>
      <div class="command-form">
        <mat-form-field appearance="outline" class="command-type-field">
          <mat-label>Command Type</mat-label>
          <mat-select [formControl]="commandTypeControl">
            @for (option of commandOptions; track option.value) {
              <mat-option [value]="option.value">
                <mat-icon>{{ option.icon }}</mat-icon>
                {{ option.label }}
              </mat-option>
            }
          </mat-select>
        </mat-form-field>

        <mat-form-field appearance="outline" class="payload-field">
          <mat-label>Payload (optional)</mat-label>
          <textarea
            matInput
            [formControl]="payloadControl"
            rows="3"
            placeholder="Script content, package name, service name..."
          ></textarea>
        </mat-form-field>

        <mat-form-field appearance="outline" class="timeout-field">
          <mat-label>Timeout (seconds)</mat-label>
          <input matInput type="number" [formControl]="timeoutControl" />
        </mat-form-field>

        <button
          mat-flat-button
          color="primary"
          [disabled]="!commandTypeControl.value || store.isDispatching()"
          (click)="onDispatch()"
        >
          <mat-icon>send</mat-icon>
          Dispatch
        </button>
      </div>

      @if (store.isDispatching()) {
        <mat-progress-bar mode="indeterminate"></mat-progress-bar>
      }

      @if (lastDispatchResult === 'success') {
        <div class="dispatch-result success">
          <mat-icon>check_circle</mat-icon>
          Command dispatched successfully
        </div>
      }
      @if (lastDispatchResult === 'error') {
        <div class="dispatch-result error">
          <mat-icon>error</mat-icon>
          Failed to dispatch command
        </div>
      }
    </mat-card>
  `,
  styles: [`
    .command-panel {
      padding: 16px;
      margin-bottom: 16px;

      h3 {
        margin: 0 0 16px;
        font-size: 16px;
        font-weight: 500;
      }
    }

    .command-form {
      display: flex;
      gap: 16px;
      align-items: flex-start;
      flex-wrap: wrap;
    }

    .command-type-field {
      min-width: 200px;
    }

    .payload-field {
      flex: 1;
      min-width: 250px;
    }

    .timeout-field {
      width: 120px;
    }

    .dispatch-result {
      display: flex;
      align-items: center;
      gap: 8px;
      padding: 8px 12px;
      border-radius: 4px;
      margin-top: 12px;
      font-size: 14px;

      &.success {
        background: #e8f5e9;
        color: #2e7d32;
      }
      &.error {
        background: #ffebee;
        color: #c62828;
      }
    }
  `],
})
export class CommandPanelComponent {
  @Input({ required }) agentId!: string;

  protected readonly store = inject(AgentStore);
  protected readonly commandOptions = COMMAND_TYPE_OPTIONS;

  protected readonly commandTypeControl = new FormControl<CommandType | null>(null, Validators.required);
  protected readonly payloadControl = new FormControl('');
  protected readonly timeoutControl = new FormControl<number | null>(null);

  protected lastDispatchResult: 'success' | 'error' | null = null;

  async onDispatch(): Promise<void> {
    if (!this.commandTypeControl.value || !this.agentId) return;

    this.lastDispatchResult = null;
    const success = await this.store.dispatchCommand(
      this.agentId,
      this.commandTypeControl.value,
      this.payloadControl.value ?? undefined,
      this.timeoutControl.value ?? undefined
    );

    this.lastDispatchResult = success ? 'success' : 'error';

    // Clear form on success
    if (success) {
      this.commandTypeControl.reset();
      this.payloadControl.reset();
      this.timeoutControl.reset();
      // Auto-clear success message after 3s
      setTimeout(() => {
        if (this.lastDispatchResult === 'success') {
          this.lastDispatchResult = null;
        }
      }, 3000);
    }
  }
}
