import { ChangeDetectionStrategy, Component, computed, inject, input } from '@angular/core';
import { ChartWidgetComponent } from '../widgets/chart-widget.component';
import { StatWidgetComponent } from '../widgets/stat-widget.component';
import { ListWidgetComponent } from '../widgets/list-widget.component';

/**
 * Maps a widget API name to its rendering component (Task 7.1). A closed map rather than a
 * dynamic loader: only catalog-approved widgets can render, so an injected placement payload
 * cannot convince the app to instantiate arbitrary components (defense in depth on top of the
 * server-side catalog validation).
 */
@Component({
  selector: 'app-widget-host',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @switch (widgetType()) {
      @case ('OpenTicketsCount') {
        <app-stat-widget [widgetType]="widgetType()" />
      }
      @case ('SlaComplianceRate') {
        <app-stat-widget [widgetType]="widgetType()" suffix="%" [warningBelow]="80" />
      }
      @case ('TicketsByStatus') {
        <app-chart-widget [widgetType]="widgetType()" chartKind="pie" />
      }
      @case ('TicketsByPriority') {
        <app-chart-widget [widgetType]="widgetType()" chartKind="bar" />
      }
      @case ('AssetCountByType') {
        <app-chart-widget [widgetType]="widgetType()" chartKind="pie" />
      }
      @case ('AgentStatusSummary') {
        <app-chart-widget [widgetType]="widgetType()" chartKind="bar" />
      }
      @case ('RecentInventory') {
        <app-list-widget [widgetType]="widgetType()" />
      }
      @case ('AssetsNearingEnd') {
        <app-list-widget [widgetType]="widgetType()" />
      }
      @default {
        <div class="unknown">Widget not available.</div>
      }
    }
  `,
  styles: [
    `
      :host {
        display: block;
        height: 100%;
      }
      .unknown {
        height: 100%;
        display: flex;
        align-items: center;
        justify-content: center;
        color: var(--mat-sys-on-surface-variant, #757575);
        font-size: 13px;
      }
    `,
  ],
  imports: [ChartWidgetComponent, StatWidgetComponent, ListWidgetComponent],
})
export class WidgetHostComponent {
  /** API widget name to render. */
  widgetType = input.required<string>();
}
