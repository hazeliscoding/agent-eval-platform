using AgentEvalPlatform.Domain;

namespace AgentEvalPlatform.Application.Running;

/// <summary>
/// One point in the comparison space: a labelled combination of model, system prompt,
/// and turn bound. The plan's three axes — model version, prompt version, agent version
/// — all collapse into "a named configuration"; a comparison is just several of these
/// run against the same suite.
/// </summary>
public sealed record RunConfiguration
{
    public string Label { get; }
    public string Model { get; }
    public string SystemPrompt { get; }
    public int MaxTurns { get; }

    public RunConfiguration(string label, string model, string systemPrompt, int maxTurns = 12)
    {
        if (string.IsNullOrWhiteSpace(label))
        {
            throw new DomainRuleException("A run configuration requires a label.");
        }

        if (string.IsNullOrWhiteSpace(model))
        {
            throw new DomainRuleException("A run configuration requires a model id.");
        }

        if (maxTurns < 1)
        {
            throw new DomainRuleException("A run configuration must allow at least one turn.");
        }

        Label = label;
        Model = model;
        SystemPrompt = systemPrompt;
        MaxTurns = maxTurns;
    }
}
