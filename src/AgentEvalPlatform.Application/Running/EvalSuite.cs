using AgentEvalPlatform.Domain;
using AgentEvalPlatform.Domain.Scenarios;

namespace AgentEvalPlatform.Application.Running;

/// <summary>A named set of scenarios run together and scored as a unit.</summary>
public sealed class EvalSuite
{
    public string Name { get; }
    public IReadOnlyList<EvalScenario> Scenarios { get; }

    public EvalSuite(string name, IReadOnlyList<EvalScenario> scenarios)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainRuleException("A suite requires a name.");
        }

        if (scenarios.Count == 0)
        {
            throw new DomainRuleException($"Suite '{name}' has no scenarios.");
        }

        var duplicate = scenarios.GroupBy(s => s.Name, StringComparer.Ordinal).FirstOrDefault(g => g.Count() > 1);
        if (duplicate is not null)
        {
            throw new DomainRuleException($"Suite '{name}' has more than one scenario named '{duplicate.Key}'.");
        }

        Name = name;
        Scenarios = scenarios;
    }
}
