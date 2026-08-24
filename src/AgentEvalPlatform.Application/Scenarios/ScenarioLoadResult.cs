using AgentEvalPlatform.Domain.Scenarios;

namespace AgentEvalPlatform.Application.Scenarios;

/// <summary>A problem found while loading a scenario, tied to the YAML path that caused it.</summary>
public sealed record ScenarioValidationError(string Path, string Message)
{
    public override string ToString() => $"{Path}: {Message}";
}

/// <summary>
/// Either a valid scenario or the full list of what's wrong with it — never an
/// exception with a YAML stack trace. Loaders report every error they can find in
/// one pass so scenario authors fix files in one round trip.
/// </summary>
public sealed class ScenarioLoadResult
{
    private ScenarioLoadResult(EvalScenario? scenario, IReadOnlyList<ScenarioValidationError> errors)
    {
        Scenario = scenario;
        Errors = errors;
    }

    public EvalScenario? Scenario { get; }
    public IReadOnlyList<ScenarioValidationError> Errors { get; }
    public bool IsValid => Scenario is not null;

    public static ScenarioLoadResult Valid(EvalScenario scenario) => new(scenario, []);

    public static ScenarioLoadResult Invalid(params IReadOnlyList<ScenarioValidationError> errors) => new(null, errors);
}
