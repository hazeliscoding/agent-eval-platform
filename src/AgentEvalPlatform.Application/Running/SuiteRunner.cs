using AgentEvalPlatform.Application.Agents;
using AgentEvalPlatform.Application.Scoring;
using AgentEvalPlatform.Domain.Assertions;

namespace AgentEvalPlatform.Application.Running;

/// <summary>The result of running a whole suite under one configuration.</summary>
public sealed record SuiteRunResult(
    string SuiteName,
    RunConfiguration Configuration,
    IReadOnlyList<ScenarioResult> Scenarios,
    Score Score);

/// <summary>
/// Runs every scenario in a suite under one configuration and scores the set. The
/// runner and evaluator are deterministic given the model's turns, so a fixed model
/// (or a fake) yields a fixed score — the property the comparison relies on.
/// </summary>
public sealed class SuiteRunner(IAgentModel model, ISchemaValidator schemaValidator, ModelPricing? pricing = null)
{
    private readonly ScenarioRunner _runner = new(model);
    private readonly AssertionEvaluator _evaluator = new(schemaValidator);
    private readonly ModelPricing _pricing = pricing ?? new ModelPricing();

    public async Task<SuiteRunResult> RunAsync(
        EvalSuite suite,
        RunConfiguration configuration,
        CancellationToken cancellationToken = default)
    {
        var results = new List<ScenarioResult>();
        foreach (var scenario in suite.Scenarios)
        {
            var run = await _runner.RunAsync(scenario, configuration, cancellationToken);
            var assertions = _evaluator.Evaluate(scenario.Assertions, run);
            results.Add(new ScenarioResult(scenario.Name, run, assertions));
        }

        var score = Score.From(configuration.Model, results, _pricing);
        return new SuiteRunResult(suite.Name, configuration, results, score);
    }
}
