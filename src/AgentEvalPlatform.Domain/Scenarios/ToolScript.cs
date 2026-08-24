namespace AgentEvalPlatform.Domain.Scenarios;

/// <summary>
/// The ordered sequence of responses one tool gives over the course of a scenario:
/// the first call gets <c>Responses[0]</c>, the second <c>Responses[1]</c>, and so on.
/// A call past the end of the script is a scenario-definition error, not something to
/// paper over by repeating the last response — silent repetition would hide agents
/// that loop on a tool far more than the scenario author anticipated.
/// </summary>
public sealed record ToolScript
{
    public string ToolName { get; }
    public IReadOnlyList<ScriptedResponse> Responses { get; }

    public ToolScript(string toolName, IReadOnlyList<ScriptedResponse> responses)
    {
        if (string.IsNullOrWhiteSpace(toolName))
        {
            throw new DomainRuleException("A tool script requires a tool name.");
        }

        if (responses.Count == 0)
        {
            throw new DomainRuleException($"Tool script for '{toolName}' has no responses.");
        }

        ToolName = toolName;
        Responses = responses;
    }
}
