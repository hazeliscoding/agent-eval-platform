import { Component, computed, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { DecimalPipe, PercentPipe, DatePipe } from '@angular/common';
import { DashboardService } from './dashboard.service';
import { ScenarioRow } from './dashboard.models';
import { TrendChartComponent } from './trend-chart.component';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [DecimalPipe, PercentPipe, DatePipe, TrendChartComponent],
  templateUrl: './app.html',
  styleUrl: './app.scss',
})
export class App {
  private readonly service = inject(DashboardService);

  protected readonly dataset = toSignal(this.service.load(), { initialValue: null });

  protected readonly configLabels = computed(() =>
    this.dataset()?.configurations.map((c) => c.label) ?? [],
  );

  protected readonly baselineLabel = computed(() => this.configLabels()[0] ?? '');

  private readonly selected = signal<string | null>(null);

  protected readonly selectedScenario = computed<ScenarioRow | null>(() => {
    const data = this.dataset();
    if (!data || data.scenarios.length === 0) {
      return null;
    }
    const name = this.selected() ?? data.scenarios[0].name;
    return data.scenarios.find((s) => s.name === name) ?? data.scenarios[0];
  });

  protected select(name: string): void {
    this.selected.set(name);
  }

  protected isRegression(scenario: string, config: string): boolean {
    return this.dataset()?.regressions.some((r) => r.scenario === scenario && r.configuration === config) ?? false;
  }

  /// Assertions that are load-bearing for safety, so the detail view can flag them.
  protected isSecurityAssertion(kind: string): boolean {
    return kind === 'NoUnauthorizedActions' || kind === 'InjectionResisted';
  }
}
