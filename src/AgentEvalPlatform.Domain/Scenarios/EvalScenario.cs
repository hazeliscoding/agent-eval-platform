namespace AgentEvalPlatform.Domain.Scenarios;

/// <summary>
/// One evaluation scenario: the world the agent starts in, which tools it may and
/// must not touch, what a correct diagnosis looks like, and the scripted responses
/// each tool gives. Scenarios are data — authored in YAML, validated here — so the
/// invariants live in this constructor rather than in the parser.
/// </summary>
public sealed class EvalScenario
{
    public string Name { get; }

    /// <summary>Named facts about the starting world (queue depths, worker counts…). Opaque to the platform; scenarios and assertions give them meaning.</summary>
    public IReadOnlyDictionary<string, string> InitialState { get; }

    /// <summary>The diagnosis a correct agent should reach, when the scenario defines one.</summary>
    public string? ExpectedDiagnosis { get; }

    public IReadOnlySet<string> AllowedTools { get; }
    public IReadOnlySet<string> ForbiddenTools { get; }

    /// <summary>Scripts keyed by tool name. Every scripted tool must be in <see cref="AllowedTools"/>.</summary>
    public IReadOnlyDictionary<string, ToolScript> ToolScripts { get; }

    public EvalScenario(
        string name,
        IReadOnlyDictionary<string, string> initialState,
        string? expectedDiagnosis,
        IEnumerable<string> allowedTools,
        IEnumerable<string> forbiddenTools,
        IEnumerable<ToolScript> toolScripts)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainRuleException("A scenario requires a name.");
        }

        var allowed = allowedTools.ToHashSet(StringComparer.Ordinal);
        var forbidden = forbiddenTools.ToHashSet(StringComparer.Ordinal);

        var contradictions = allowed.Intersect(forbidden).ToList();
        if (contradictions.Count > 0)
        {
            throw new DomainRuleException(
                $"Scenario '{name}' lists tool(s) as both allowed and forbidden: {string.Join(", ", contradictions)}.");
        }

        var scripts = new Dictionary<string, ToolScript>(StringComparer.Ordinal);
        foreach (var script in toolScripts)
        {
            if (!scripts.TryAdd(script.ToolName, script))
            {
                throw new DomainRuleException(
                    $"Scenario '{name}' defines more than one script for tool '{script.ToolName}'.");
            }

            if (!allowed.Contains(script.ToolName))
            {
                throw new DomainRuleException(
                    $"Scenario '{name}' scripts tool '{script.ToolName}', which is not in allowedTools.");
            }
        }

        Name = name;
        InitialState = initialState;
        ExpectedDiagnosis = expectedDiagnosis;
        AllowedTools = allowed;
        ForbiddenTools = forbidden;
        ToolScripts = scripts;
    }
}
