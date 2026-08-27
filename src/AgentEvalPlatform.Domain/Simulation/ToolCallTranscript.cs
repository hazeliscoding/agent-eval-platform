namespace AgentEvalPlatform.Domain.Simulation;

/// <summary>One recorded tool call: the agent's request and the simulator's answer.</summary>
public sealed record ToolCall(
    int Sequence,
    string ToolName,
    string? ArgumentsJson,
    ToolCallOutcome Outcome,
    DateTimeOffset At);

/// <summary>
/// The append-only record of everything the agent did with its tools during a run.
/// This is the raw material later phases assert against (tool_called, call counts,
/// unauthorized actions), so refusals are first-class entries, not omissions.
/// </summary>
public sealed class ToolCallTranscript
{
    private readonly List<ToolCall> _calls = [];

    public IReadOnlyList<ToolCall> Calls => _calls;

    public int CallCount(string toolName) =>
        _calls.Count(c => string.Equals(c.ToolName, toolName, StringComparison.Ordinal));

    public bool WasCalled(string toolName) => CallCount(toolName) > 0;

    /// <summary>Calls that were refused — the scenario's unauthorized-action evidence.</summary>
    public IReadOnlyList<ToolCall> Refusals =>
        _calls.Where(c => c.Outcome is ToolCallOutcome.RefusedForbidden or ToolCallOutcome.RefusedUnknown).ToList();

    /// <summary>The calls that delivered an adversarial payload to the agent, in order.</summary>
    public IReadOnlyList<ToolCall> Injections =>
        _calls.Where(c => c.Outcome is ToolCallOutcome.Injected).ToList();

    internal void Append(ToolCall call) => _calls.Add(call);
}
