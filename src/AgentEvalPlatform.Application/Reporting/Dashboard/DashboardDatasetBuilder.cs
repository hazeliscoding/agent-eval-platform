using AgentEvalPlatform.Application.Running;
using AgentEvalPlatform.Application.Scoring;
using AgentEvalPlatform.Domain.Runs;
using AgentEvalPlatform.Domain.Simulation;

namespace AgentEvalPlatform.Application.Reporting.Dashboard;

/// <summary>
/// Projects a <see cref="ComparisonResult"/> (plus caller-supplied run history and a
/// timestamp — the application never reads the clock) into the flat
/// <see cref="DashboardDataset"/> the SPA consumes.
/// </summary>
public static class DashboardDatasetBuilder
{
    public static DashboardDataset Build(
        ComparisonResult comparison,
        IReadOnlyList<HistoryPoint> history,
        DateTimeOffset generatedAt)
    {
        var configurations = comparison.Runs
            .Select(run => new ConfigurationScore(run.Configuration.Label, run.Configuration.Model, View(run.Score)))
            .ToList();

        var scenarios = comparison.Scenarios
            .Select(scenario => new ScenarioRow(
                scenario.ScenarioName,
                comparison.Runs.ToDictionary(
                    run => run.Configuration.Label,
                    run => Cell(run.Scenarios.Single(s => s.ScenarioName == scenario.ScenarioName)),
                    StringComparer.Ordinal)))
            .ToList();

        return new DashboardDataset(
            comparison.SuiteName,
            generatedAt,
            configurations,
            scenarios,
            comparison.Regressions.Select(d => new ScenarioDelta(d.ScenarioName, d.ConfigurationLabel)).ToList(),
            comparison.Improvements.Select(d => new ScenarioDelta(d.ScenarioName, d.ConfigurationLabel)).ToList(),
            history);
    }

    private static ScoreView View(Score score) => new(
        score.ScenarioCount,
        score.PassedScenarios,
        score.SuccessRate,
        score.AssertionPassRate,
        score.TotalToolCalls,
        score.UnauthorizedAttempts,
        score.TotalTokens,
        score.TotalDuration.TotalSeconds,
        score.TotalCost);

    private static ScenarioCell Cell(ScenarioResult result)
    {
        var transcript = result.Run.Transcript;
        return new ScenarioCell(
            Passed: result.Passed,
            ToolCalls: transcript.Calls.Count,
            UnauthorizedAttempts: UnauthorizedAttempts(result.Run),
            InjectionsObeyed: InjectionsObeyed(result),
            ToolUsage: transcript.Calls
                .GroupBy(c => c.ToolName, StringComparer.Ordinal)
                .Select(g => new ToolUsage(g.Key, g.Count()))
                .OrderByDescending(u => u.Count)
                .ThenBy(u => u.Tool, StringComparer.Ordinal)
                .ToList(),
            Assertions: result.Assertions
                .Select(a => new AssertionView(a.Assertion.GetType().Name, a.Passed, a.Message))
                .ToList(),
            Output: result.Run.Output);
    }

    private static int UnauthorizedAttempts(AgentRun run) =>
        run.Transcript.Refusals.Count
        + run.Transcript.Calls.Count(c => c.Outcome is ToolCallOutcome.Unauthorized);

    /// <summary>Failed injection-resistance assertions — the count the security view keys on.</summary>
    private static int InjectionsObeyed(ScenarioResult result) =>
        result.Assertions.Count(a =>
            a.Assertion is Domain.Assertions.Assertion.InjectionResisted && !a.Passed);
}
