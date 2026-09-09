import { ChangeDetectionStrategy, Component, computed, inject, input } from '@angular/core';
import { NgxEchartsDirective, provideEchartsCore } from 'ngx-echarts';
import * as echarts from 'echarts/core';
import { BarChart, PieChart } from 'echarts/charts';
import { GridComponent, TooltipComponent, LegendComponent } from 'echarts/components';
import { CanvasRenderer } from 'echarts/renderers';
import { DashboardStore } from '../dashboard.store';

echarts.use([BarChart, PieChart, GridComponent, TooltipComponent, LegendComponent, CanvasRenderer]);

/**
 * Renders a label/value widget payload as an ECharts bar or pie chart (Task 7.1).
 * One shared chart keeps every distribution widget visually consistent and means a
 * chart-related fix lands everywhere at once.
 */
@Component({
  selector: 'app-chart-widget',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [NgxEchartsDirective],
  providers: [provideEchartsCore({ echarts })],
  template: `
    @if (chartOptions(); as options) {
      <div echarts [options]="options" class="chart"></div>
    } @else {
      <div class="empty">No data available.</div>
    }
  `,
  styles: [
    `
      :host {
        display: block;
        height: 100%;
      }
      .chart {
        height: 100%;
        min-height: 160px;
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
export class ChartWidgetComponent {
  private readonly store = inject(DashboardStore);

  /** API widget name this chart renders (e.g. 'TicketsByStatus'). */
  widgetType = input.required<string>();
  /** 'bar' for comparisons, 'pie' for share-of-total. */
  chartKind = input<'bar' | 'pie'>('bar');

  private readonly data = computed(() => this.store.data()[this.widgetType()]);

  private readonly palette = [
    '#5470c6', '#91cc75', '#fac858', '#ee6666', '#73c0de',
    '#3ba272', '#fc8452', '#9a60b4', '#ea7ccc',
  ];

  readonly chartOptions = computed(() => {
    const values = this.data()?.values;
    if (!values || values.length === 0) return null;

    const common = {
      tooltip: { trigger: 'item' as const },
      // Colors come from the fixed palette above; labels come from the server's closed
      // widget catalog -- no user-controlled HTML reaches the chart.
    };

    if (this.chartKind() === 'pie') {
      return {
        ...common,
        legend: { bottom: 0, type: 'scroll' as const },
        series: [
          {
            type: 'pie',
            radius: ['38%', '68%'],
            avoidLabelOverlap: true,
            data: values.map((v, i) => ({
              name: v.label,
              value: v.value,
              itemStyle: { color: this.palette[i % this.palette.length] },
            })),
          },
        ],
      };
    }

    return {
      ...common,
      grid: { left: 8, right: 8, top: 8, bottom: 8, containLabel: true },
      xAxis: {
        type: 'category' as const,
        data: values.map((v) => v.label),
        axisLabel: { rotate: 30 },
      },
      yAxis: { type: 'value' as const, minInterval: 1 },
      series: [
        {
          type: 'bar',
          data: values.map((v, i) => ({
            value: v.value,
            itemStyle: { color: this.palette[i % this.palette.length] },
          })),
        },
      ],
    };
  });
}
