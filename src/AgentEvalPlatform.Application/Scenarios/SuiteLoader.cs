using AgentEvalPlatform.Application.Running;

namespace AgentEvalPlatform.Application.Scenarios;

/// <summary>The outcome of assembling a suite from several scenario documents.</summary>
public sealed record SuiteLoadResult(EvalSuite? Suite, IReadOnlyList<string> Errors)
{
    public bool IsValid => Suite is not null;
}

/// <summary>
/// Assembles an <see cref="EvalSuite"/> from a set of named YAML documents. IO-free — the
/// caller reads the files — so it stays testable; each document's own load errors are
/// surfaced with its source name, and any failure aborts the whole suite (a CI run must
/// not silently drop an unparseable scenario).
/// </summary>
public static class SuiteLoader
{
    public static SuiteLoadResult Load(string suiteName, IReadOnlyList<(string Source, string Yaml)> documents)
    {
        if (documents.Count == 0)
        {
            return new SuiteLoadResult(null, [$"No scenario documents found for suite '{suiteName}'."]);
        }

        var loader = new ScenarioLoader();
        var scenarios = new List<Domain.Scenarios.EvalScenario>();
        var errors = new List<string>();

        foreach (var (source, yaml) in documents)
        {
            var result = loader.Load(yaml);
            if (result.Scenario is not null)
            {
                scenarios.Add(result.Scenario);
            }
            else
            {
                errors.AddRange(result.Errors.Select(e => $"{source}: {e}"));
            }
        }

        if (errors.Count > 0)
        {
            return new SuiteLoadResult(null, errors);
        }

        try
        {
            return new SuiteLoadResult(new EvalSuite(suiteName, scenarios), []);
        }
        catch (Domain.DomainRuleException ex)
        {
            return new SuiteLoadResult(null, [ex.Message]);
        }
    }
}
