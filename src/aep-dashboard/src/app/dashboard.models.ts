// Mirrors the C# DashboardDataset DTOs (AgentEvalPlatform.Application.Reporting.Dashboard).
// The generator (tools/AgentEvalPlatform.SampleData) and `aep compare` emit this shape.

export interface DashboardDataset {
  suiteName: string;
  generatedAt: string;
  configurations: ConfigurationScore[];
  scenarios: ScenarioRow[];
  regressions: ScenarioDelta[];
  improvements: ScenarioDelta[];
  history: HistoryPoint[];
}

export interface ConfigurationScore {
  label: string;
  model: string;
  score: ScoreView;
}

export interface ScoreView {
  scenarioCount: number;
  passedScenarios: number;
  successRate: number;
  assertionPassRate: number;
  totalToolCalls: number;
  unauthorizedAttempts: number;
  totalTokens: number;
  totalDurationSeconds: number;
  totalCost: number;
}

export interface ScenarioRow {
  name: string;
  results: Record<string, ScenarioCell>;
}

export interface ScenarioCell {
  passed: boolean;
  toolCalls: number;
  unauthorizedAttempts: number;
  injectionsObeyed: number;
  toolUsage: ToolUsage[];
  assertions: AssertionView[];
  output: string;
}

export interface ToolUsage {
  tool: string;
  count: number;
}

export interface AssertionView {
  kind: string;
  passed: boolean;
  message: string;
}

export interface ScenarioDelta {
  scenario: string;
  configuration: string;
}

export interface HistoryPoint {
  recordedAt: string;
  label: string;
  successRate: number;
  unauthorizedAttempts: number;
  cost: number;
  latencySeconds: number;
}
