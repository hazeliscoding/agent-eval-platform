namespace AgentEvalPlatform.Application.Reporting.Dashboard;

/// <summary>
/// The serializable shape the static dashboard reads. A flat, presentation-oriented
/// projection of a comparison plus run history — no domain types leak into it, so the
/// JSON contract is stable independent of internal refactors.
/// </summary>
public sealed record DashboardDataset(
    string SuiteName,
    DateTimeOffset GeneratedAt,
    IReadOnlyList<ConfigurationScore> Configurations,
    IReadOnlyList<ScenarioRow> Scenarios,
    IReadOnlyList<ScenarioDelta> Regressions,
    IReadOnlyList<ScenarioDelta> Improvements,
    IReadOnlyList<HistoryPoint> History);

/// <summary>A configuration's headline metrics.</summary>
public sealed record ConfigurationScore(
    string Label,
    string Model,
    ScoreView Score);

/// <summary>The dashboard-facing view of a score (durations and rates as plain numbers).</summary>
public sealed record ScoreView(
    int ScenarioCount,
    int PassedScenarios,
    double SuccessRate,
    double AssertionPassRate,
    int TotalToolCalls,
    int UnauthorizedAttempts,
    long TotalTokens,
    double TotalDurationSeconds,
    decimal TotalCost);

/// <summary>One scenario's cell under each configuration, keyed by configuration label.</summary>
public sealed record ScenarioRow(
    string Name,
    IReadOnlyDictionary<string, ScenarioCell> Results);

/// <summary>Everything the dashboard shows for one (scenario, configuration) pair.</summary>
public sealed record ScenarioCell(
    bool Passed,
    int ToolCalls,
    int UnauthorizedAttempts,
    int InjectionsObeyed,
    IReadOnlyList<ToolUsage> ToolUsage,
    IReadOnlyList<AssertionView> Assertions,
    string Output);

public sealed record ToolUsage(string Tool, int Count);

public sealed record AssertionView(string Kind, bool Passed, string Message);

/// <summary>A scenario whose pass/fail flipped relative to the baseline configuration.</summary>
public sealed record ScenarioDelta(string Scenario, string Configuration);

/// <summary>One point on the trend chart — a past run's headline metrics.</summary>
public sealed record HistoryPoint(
    DateTimeOffset RecordedAt,
    string Label,
    double SuccessRate,
    int UnauthorizedAttempts,
    decimal Cost,
    double LatencySeconds);
