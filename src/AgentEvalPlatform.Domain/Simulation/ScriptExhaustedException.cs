namespace AgentEvalPlatform.Domain.Simulation;

/// <summary>
/// The agent called a tool more times than its script has responses. This aborts the
/// run loudly rather than repeating the last response: silent repetition would hide
/// both under-scripted scenarios and agents that loop on a tool unexpectedly. The
/// offending call is recorded in the transcript before this is thrown.
/// </summary>
public sealed class ScriptExhaustedException(string toolName, int callNumber, int scriptedResponses)
    : DomainException(
        $"Tool '{toolName}' was called {callNumber} time(s) but its script only has " +
        $"{scriptedResponses} response(s). Extend the scenario's script, or treat the extra call as an agent defect.")
{
    public string ToolName { get; } = toolName;
    public int CallNumber { get; } = callNumber;
    public int ScriptedResponses { get; } = scriptedResponses;
}
