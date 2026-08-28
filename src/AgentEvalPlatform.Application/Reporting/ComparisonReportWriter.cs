using System.Globalization;
using System.Text;
using AgentEvalPlatform.Application.Running;

namespace AgentEvalPlatform.Application.Reporting;

/// <summary>
/// Renders a <see cref="ComparisonResult"/> as Markdown: a score table across
/// configurations, the regressions and improvements against the baseline, and a
/// per-scenario pass/fail matrix. Pure formatting — deterministic given the result.
/// </summary>
public static class ComparisonReportWriter
{
    public static string Write(ComparisonResult result)
    {
        var sb = new StringBuilder();
        var configs = result.Runs.Select(r => r.Configuration.Label).ToList();

        sb.AppendLine(CultureInfo.InvariantCulture, $"# Comparison — {result.SuiteName}");
        sb.AppendLine();
        sb.AppendLine(CultureInfo.InvariantCulture,
            $"Baseline: **{result.Baseline.Configuration.Label}**. {result.Scenarios.Count} scenario(s) across {configs.Count} configurations.");
        sb.AppendLine();

        sb.AppendLine("## Scores");
        sb.AppendLine();
        sb.AppendLine("| Metric | " + string.Join(" | ", configs) + " |");
        sb.AppendLine("|---|" + string.Concat(Enumerable.Repeat("---|", configs.Count)));
        Row(sb, "Success rate", result.Runs, r => Pct(r.Score.SuccessRate));
        Row(sb, "Assertion pass rate", result.Runs, r => Pct(r.Score.AssertionPassRate));
        Row(sb, "Scenarios passed", result.Runs, r => $"{r.Score.PassedScenarios}/{r.Score.ScenarioCount}");
        Row(sb, "Tool calls", result.Runs, r => r.Score.TotalToolCalls.ToString(CultureInfo.InvariantCulture));
        Row(sb, "Unauthorized attempts", result.Runs, r => r.Score.UnauthorizedAttempts.ToString(CultureInfo.InvariantCulture));
        Row(sb, "Tokens", result.Runs, r => r.Score.TotalTokens.ToString("N0", CultureInfo.InvariantCulture));
        Row(sb, "Simulated latency", result.Runs, r => $"{r.Score.TotalDuration.TotalSeconds.ToString("0.###", CultureInfo.InvariantCulture)}s");
        Row(sb, "Cost (USD)", result.Runs, r => $"${r.Score.TotalCost.ToString("0.####", CultureInfo.InvariantCulture)}");
        sb.AppendLine();

        sb.AppendLine("## Regressions vs. baseline");
        sb.AppendLine();
        if (result.Regressions.Count == 0)
        {
            sb.AppendLine("None. 🎉");
        }
        else
        {
            foreach (var delta in result.Regressions)
            {
                sb.AppendLine(CultureInfo.InvariantCulture, $"- ⛔ **{delta.ScenarioName}** passed on baseline but fails on **{delta.ConfigurationLabel}**");
            }
        }

        sb.AppendLine();
        sb.AppendLine("## Improvements vs. baseline");
        sb.AppendLine();
        if (result.Improvements.Count == 0)
        {
            sb.AppendLine("None.");
        }
        else
        {
            foreach (var delta in result.Improvements)
            {
                sb.AppendLine(CultureInfo.InvariantCulture, $"- ✅ **{delta.ScenarioName}** failed on baseline but passes on **{delta.ConfigurationLabel}**");
            }
        }

        sb.AppendLine();
        sb.AppendLine("## Per-scenario results");
        sb.AppendLine();
        sb.AppendLine("| Scenario | " + string.Join(" | ", configs) + " |");
        sb.AppendLine("|---|" + string.Concat(Enumerable.Repeat("---|", configs.Count)));
        foreach (var scenario in result.Scenarios)
        {
            var cells = configs.Select(c => scenario.PassedByConfiguration[c] ? "pass" : "**fail**");
            sb.AppendLine(CultureInfo.InvariantCulture, $"| {scenario.ScenarioName} | {string.Join(" | ", cells)} |");
        }

        return sb.ToString();
    }

    private static void Row(StringBuilder sb, string metric, IReadOnlyList<SuiteRunResult> runs, Func<SuiteRunResult, string> cell) =>
        sb.AppendLine(CultureInfo.InvariantCulture, $"| {metric} | {string.Join(" | ", runs.Select(cell))} |");

    private static string Pct(double fraction) => (fraction * 100).ToString("0.#", CultureInfo.InvariantCulture) + "%";
}
