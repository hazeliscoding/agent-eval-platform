using AgentEvalPlatform.Application.Agents;
using AgentEvalPlatform.Application.Scoring;
using AgentEvalPlatform.Domain;
using AgentEvalPlatform.Domain.Assertions;

namespace AgentEvalPlatform.Application.Running;

/// <summary>How one scenario fared across the compared configurations.</summary>
public sealed record ScenarioComparison(string ScenarioName, IReadOnlyDictionary<string, bool> PassedByConfiguration);

/// <summary>A scenario whose pass/fail flipped relative to the baseline configuration.</summary>
public sealed record ScenarioDelta(string ScenarioName, string ConfigurationLabel);

/// <summary>
/// The result of running one suite under several configurations. The first configuration
/// is the baseline; regressions and improvements are measured against it — the plan's
/// "run the same suite across versions and diff the reports".
/// </summary>
public sealed record ComparisonResult(
    string SuiteName,
    IReadOnlyList<SuiteRunResult> Runs,
    IReadOnlyList<ScenarioComparison> Scenarios)
{
    public SuiteRunResult Baseline => Runs[0];

    /// <summary>Scenarios that passed under the baseline but fail under a later configuration.</summary>
    public IReadOnlyList<ScenarioDelta> Regressions => Deltas(baselinePassed: true, otherPassed: false);

    /// <summary>Scenarios that failed under the baseline but pass under a later configuration.</summary>
    public IReadOnlyList<ScenarioDelta> Improvements => Deltas(baselinePassed: false, otherPassed: true);

    private IReadOnlyList<ScenarioDelta> Deltas(bool baselinePassed, bool otherPassed)
    {
        var baselineLabel = Baseline.Configuration.Label;
        var deltas = new List<ScenarioDelta>();
        foreach (var scenario in Scenarios)
        {
            if (scenario.PassedByConfiguration[baselineLabel] != baselinePassed)
            {
                continue;
            }

            foreach (var run in Runs.Skip(1))
            {
                if (scenario.PassedByConfiguration[run.Configuration.Label] == otherPassed)
                {
                    deltas.Add(new ScenarioDelta(scenario.ScenarioName, run.Configuration.Label));
                }
            }
        }

        return deltas;
    }
}

/// <summary>
/// Runs a suite under several configurations and assembles the comparison. Each
/// configuration is scored independently by a <see cref="SuiteRunner"/>; the diff is
/// pure bookkeeping over their results.
/// </summary>
public sealed class SuiteComparison(IAgentModel model, ISchemaValidator schemaValidator, ModelPricing? pricing = null)
{
    private readonly SuiteRunner _suiteRunner = new(model, schemaValidator, pricing);

    public async Task<ComparisonResult> RunAsync(
        EvalSuite suite,
        IReadOnlyList<RunConfiguration> configurations,
        CancellationToken cancellationToken = default)
    {
        if (configurations.Count < 2)
        {
            throw new DomainRuleException("A comparison needs at least two configurations.");
        }

        var duplicate = configurations.GroupBy(c => c.Label, StringComparer.Ordinal).FirstOrDefault(g => g.Count() > 1);
        if (duplicate is not null)
        {
            throw new DomainRuleException($"Comparison has more than one configuration labelled '{duplicate.Key}'.");
        }

        var runs = new List<SuiteRunResult>();
        foreach (var configuration in configurations)
        {
            runs.Add(await _suiteRunner.RunAsync(suite, configuration, cancellationToken));
        }

        var scenarios = suite.Scenarios
            .Select(s => new ScenarioComparison(
                s.Name,
                runs.ToDictionary(
                    r => r.Configuration.Label,
                    r => r.Scenarios.Single(sr => sr.ScenarioName == s.Name).Passed,
                    StringComparer.Ordinal)))
            .ToList();

        return new ComparisonResult(suite.Name, runs, scenarios);
    }
}
