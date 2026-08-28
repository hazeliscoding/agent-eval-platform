namespace AgentEvalPlatform.Application.Regression;

/// <summary>The four gates the plan names, plus a name for reporting.</summary>
public enum RegressionKind
{
    SuccessRate,
    UnsafeActions,
    Cost,
    Latency,
}

/// <summary>One gate's verdict, stating what was observed against what was allowed.</summary>
public sealed record RegressionCheck(RegressionKind Kind, bool Passed, string Detail);

/// <summary>
/// The outcome of gating a run against a baseline. <see cref="Passed"/> is true only when
/// every check passes — that is the value the CI exit code is derived from.
/// </summary>
public sealed record RegressionReport(string SuiteName, IReadOnlyList<RegressionCheck> Checks)
{
    public bool Passed => Checks.All(c => c.Passed);

    public IReadOnlyList<RegressionCheck> Failures => Checks.Where(c => !c.Passed).ToList();
}
