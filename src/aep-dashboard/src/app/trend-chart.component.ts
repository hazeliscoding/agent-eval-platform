import { Component, computed, input } from '@angular/core';
import { HistoryPoint } from './dashboard.models';

/// A dependency-free inline-SVG line chart of success rate over the run history, with
/// points that regressed (any unauthorized attempt) marked in the failure colour.
@Component({
  selector: 'app-trend-chart',
  standalone: true,
  template: `
    @if (points().length > 1) {
      <svg [attr.viewBox]="'0 0 ' + width + ' ' + height" class="chart" role="img"
           aria-label="Success rate over time">
        <!-- gridlines at 0/50/100% -->
        @for (g of gridlines; track g.y) {
          <line [attr.x1]="pad" [attr.x2]="width - pad" [attr.y1]="g.y" [attr.y2]="g.y" class="grid" />
          <text [attr.x]="0" [attr.y]="g.y + 3" class="axis">{{ g.label }}</text>
        }
        <polyline [attr.points]="linePoints()" class="line" />
        @for (p of plotted(); track p.i) {
          <circle [attr.cx]="p.x" [attr.cy]="p.y" r="4" [class.regressed]="p.regressed" class="dot" />
          <title>{{ p.label }} — {{ (p.rate * 100).toFixed(0) }}%</title>
        }
      </svg>
      <div class="legend">
        <span><i class="swatch line-swatch"></i> success rate</span>
        <span><i class="swatch regressed-swatch"></i> unsafe actions present</span>
      </div>
    } @else {
      <p class="empty">Not enough history to plot a trend yet.</p>
    }
  `,
  styles: [`
    .chart { width: 100%; height: auto; }
    .grid { stroke: var(--border); stroke-width: 1; }
    .axis { fill: var(--muted); font-size: 9px; }
    .line { fill: none; stroke: var(--accent); stroke-width: 2; }
    .dot { fill: var(--accent); }
    .dot.regressed { fill: var(--fail); }
    .legend { display: flex; gap: 1.25rem; margin-top: 0.5rem; color: var(--muted); font-size: 0.8rem; }
    .swatch { display: inline-block; width: 10px; height: 10px; border-radius: 50%; vertical-align: middle; margin-right: 0.35rem; }
    .line-swatch { background: var(--accent); }
    .regressed-swatch { background: var(--fail); }
    .empty { color: var(--muted); }
  `],
})
export class TrendChartComponent {
  readonly points = input<HistoryPoint[]>([]);

  protected readonly width = 640;
  protected readonly height = 180;
  protected readonly pad = 28;

  protected readonly gridlines = [
    { y: 20, label: '100%' },
    { y: 90, label: '50%' },
    { y: 160, label: '0%' },
  ];

  protected readonly plotted = computed(() => {
    const pts = this.points();
    const n = pts.length;
    const top = 20;
    const bottom = 160;
    const usable = this.width - this.pad * 2;
    return pts.map((p, i) => ({
      i,
      x: this.pad + (n === 1 ? usable / 2 : (usable * i) / (n - 1)),
      y: bottom - (bottom - top) * p.successRate,
      rate: p.successRate,
      regressed: p.unauthorizedAttempts > 0,
      label: p.label,
    }));
  });

  protected readonly linePoints = computed(() =>
    this.plotted().map((p) => `${p.x},${p.y}`).join(' '),
  );
}
