using AgentEvalPlatform.Application.Agents;
using AgentEvalPlatform.Application.Running;
using AgentEvalPlatform.Application.Scoring;
using AgentEvalPlatform.Domain;
using AgentEvalPlatform.Domain.Assertions;

namespace AgentEvalPlatform.Application.Regression;

/// <summary>What a check run concluded, and everything needed to report it.</summary>
public sealed record RegressionRunResult(SuiteRunResult Run, Baseline Baseline, RegressionReport Report)
{
    public bool Passed => Report.Passed;
}

/// <summary>
/// The CI core: run a suite, then either record its score as the baseline or gate it
/// against the stored baseline. The CLI is a thin shell over this — arg parsing and a
/// live model — so the whole record→check flow is testable with a fake model.
/// </summary>
public sealed class RegressionRunner(
    IAgentModel model,
    ISchemaValidator schemaValidator,
    IBaselineStore store,
    ModelPricing? pricing = null)
{
    private readonly SuiteRunner _suiteRunner = new(model, schemaValidator, pricing);

    /// <summary>Runs the suite and writes its score as the new baseline.</summary>
    public async Task<Baseline> RecordAsync(
        EvalSuite suite,
        RunConfiguration configuration,
        DateTimeOffset recordedAt,
        CancellationToken cancellationToken = default)
    {
        var run = await _suiteRunner.RunAsync(suite, configuration, cancellationToken);
        var baseline = new Baseline(suite.Name, configuration.Label, configuration.Model, run.Score, recordedAt);
        await store.SaveAsync(baseline, cancellationToken);
        return baseline;
    }

    /// <summary>
    /// Runs the suite and gates it against the stored baseline. Throws if no baseline has
    /// been recorded — a check with nothing to compare against is a setup error, not a pass.
    /// </summary>
    public async Task<RegressionRunResult> CheckAsync(
        EvalSuite suite,
        RunConfiguration configuration,
        RegressionThresholds thresholds,
        CancellationToken cancellationToken = default)
    {
        var baseline = await store.LoadAsync(cancellationToken)
            ?? throw new DomainRuleException(
                $"No baseline recorded for suite '{suite.Name}'. Run 'record' before 'check'.");

        var run = await _suiteRunner.RunAsync(suite, configuration, cancellationToken);
        var report = RegressionGate.Compare(baseline, run.Score, thresholds);
        return new RegressionRunResult(run, baseline, report);
    }
}
