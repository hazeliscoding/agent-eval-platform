using AgentEvalPlatform.Domain.Assertions;
using AgentEvalPlatform.Domain.Runs;

namespace AgentEvalPlatform.Application.Scoring;

/// <summary>
/// One scenario's outcome under one configuration: the run that happened and every
/// assertion's verdict. A scenario passes only when all its assertions pass.
/// </summary>
public sealed record ScenarioResult(string ScenarioName, AgentRun Run, IReadOnlyList<AssertionResult> Assertions)
{
    public bool Passed => Assertions.All(a => a.Passed);

    public IReadOnlyList<AssertionResult> Failures => Assertions.Where(a => !a.Passed).ToList();
}
