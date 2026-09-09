import { ChangeDetectionStrategy, Component, computed, inject, input } from '@angular/core';
import { DashboardStore } from '../dashboard.store';

/**
 * Big-number widget (OpenTicketsCount, SlaComplianceRate) (Task 7.1).
 * Reads the first value of the widget payload; `format` renders a suffix like '%'.
 */
@Component({
  selector: 'app-stat-widget',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="stat">
      <span class="value" [class.warn]="isWarning()">{{ displayValue() }}</span>
      @if (suffix(); as suffix) {
        <span class="suffix">{{ suffix }}</span>
      }
    </div>
  `,
  styles: [
    `
      :host {
        display: block;
        height: 100%;
        display: flex;
        align-items: center;
        justify-content: center;
      }
      .stat {
        display: flex;
        align-items: baseline;
        gap: 4px;
      }
      .value {
        font-size: 48px;
        font-weight: 500;
        line-height: 1;
        color: var(--mat-sys-primary, #3f51b5);
      }
      .value.warn {
        color: var(--mat-sys-error, #f44336);
      }
      .suffix {
        font-size: 20px;
        color: var(--mat-sys-on-surface-variant, #757575);
      }
    `,
  ],
})
export class StatWidgetComponent {
  private readonly store = inject(DashboardStore);

  widgetType = input.required<string>();
  /** Renders after the number (e.g. '%' for SLA compliance). */
  suffix = input<string>('');
  /** Below this value the number turns red (e.g. SLA compliance threshold). */
  warningBelow = input<number | null>(null);

  private readonly value = computed(
    () => this.store.data()[this.widgetType()]?.values?.[0]?.value ?? null
  );

  readonly displayValue = computed(() => {
    const v = this.value();
    return v === null ? '—' : v.toLocaleString();
  });

  readonly isWarning = computed(() => {
    const threshold = this.warningBelow();
    const v = this.value();
    return threshold !== null && v !== null && v < threshold;
  });
}
