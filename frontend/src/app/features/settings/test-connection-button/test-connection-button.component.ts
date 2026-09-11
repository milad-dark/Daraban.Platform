import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  input,
} from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatTooltipModule } from '@angular/material/tooltip';
import { SettingsStore } from '../settings.store';

/**
 * Inline "Test connection" button (Task 7.4). One instance per category tab. Shows live
 * feedback in place: a running spinner, then success or failure with the server's message
 * and round-trip latency. The test executes against the SAVED settings -- unsaved edits
 * must be saved first (the backend tests what it has stored).
 */
@Component({
  selector: 'app-test-connection-button',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatButtonModule, MatIconModule, MatProgressBarModule, MatTooltipModule],
  template: `
    <div class="wrap">
      @if (state().status === 'running') {
        <mat-progress-bar mode="indeterminate" class="spinner"></mat-progress-bar>
      }

      <button
        mat-stroked-button
        [disabled]="state().status === 'running'"
        (click)="run()"
      >
        <mat-icon>
          {{ state().status === 'success' ? 'check_circle' : state().status === 'failed' ? 'error' : 'network_check' }}
        </mat-icon>
        Test connection
      </button>

      @if (finished(); as result) {
        <span
          class="outcome"
          [class.ok]="result.success"
          [class.bad]="!result.success"
          [matTooltip]="result.message"
        >
          {{ result.message }}
          @if (result.success && result.latencyMs > 0) {
            <em>({{ result.latencyMs }} ms)</em>
          }
        </span>
      }
    </div>
  `,
  styles: `
    .wrap {
      display: flex;
      align-items: center;
      gap: 12px;
      min-height: 40px;
      position: relative;
      width: 100%;
    }

    .spinner {
      position: absolute;
      top: -6px;
      left: 0;
      right: 0;
    }

    .outcome {
      font-size: 13px;
      display: inline-block;
      max-width: 480px;
      overflow: hidden;
      text-overflow: ellipsis;
      white-space: nowrap;
    }

    .outcome.ok {
      color: #2e7d32;
    }

    .outcome.bad {
      color: #c62828;
    }

    em {
      font-style: normal;
      opacity: 0.8;
    }
  `,
})
export class TestConnectionButtonComponent {
  /** Any key of the category to test -- the backend routes by category. */
  readonly testKey = input.required<string>();

  protected readonly store = inject(SettingsStore);

  /** This key's live test state (idle when never run). */
  protected readonly state = computed(
    () => this.store.testStates()[this.testKey()] ?? { status: 'idle' as const }
  );

  /** The result once the test has finished; null while idle/running (narrows the union for the template). */
  protected readonly finished = computed(() => {
    const s = this.state();
    return s.status === 'success' || s.status === 'failed' ? s.result : null;
  });

  protected run(): void {
    this.store.testConnection(this.testKey());
  }
}
