using System.Globalization;
using System.Text;

namespace AgentEvalPlatform.Application.Regression;

/// <summary>Renders a <see cref="RegressionReport"/> as Markdown for CI logs and PR comments.</summary>
public static class RegressionReportWriter
{
    public static string Write(RegressionReport report)
    {
        var sb = new StringBuilder();
        sb.AppendLine(CultureInfo.InvariantCulture, $"# Regression check — {report.SuiteName}");
        sb.AppendLine();
        sb.AppendLine(report.Passed ? "**Result: PASS ✅**" : "**Result: FAIL ⛔**");
        sb.AppendLine();
        sb.AppendLine("| Gate | Result | Detail |");
        sb.AppendLine("|---|---|---|");
        foreach (var check in report.Checks)
        {
            sb.AppendLine(CultureInfo.InvariantCulture,
                $"| {check.Kind} | {(check.Passed ? "pass" : "**fail**")} | {check.Detail} |");
        }

        return sb.ToString();
    }
}
