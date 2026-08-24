using AgentEvalPlatform.Domain.Scenarios;

namespace AgentEvalPlatform.Domain.Simulation;

/// <summary>
/// Replays a scenario's tool scripts deterministically: the Nth call to a tool always
/// gets the Nth scripted response, forbidden and unknown tools get refusals, and every
/// call — refused or not — lands in the transcript. Timeouts are reported, not slept,
/// so a full eval suite runs in milliseconds. One instance is one agent run.
/// </summary>
public sealed class DeterministicToolSimulator
{
    private readonly EvalScenario _scenario;
    private readonly TimeProvider _time;
    private readonly Dictionary<string, int> _callCounts = new(StringComparer.Ordinal);
    private readonly Lock _gate = new();
    private int _sequence;

    public DeterministicToolSimulator(EvalScenario scenario, TimeProvider? time = null)
    {
        _scenario = scenario;
        _time = time ?? TimeProvider.System;
    }

    public ToolCallTranscript Transcript { get; } = new();

    /// <exception cref="ScriptExhaustedException">
    /// The tool's script ran out of responses; the call is already in the transcript.
    /// </exception>
    public ToolCallOutcome Call(string toolName, string? argumentsJson = null)
    {
        // Agents may fan out tool calls in parallel; serialize so sequence numbers
        // and per-tool script positions stay deterministic.
        lock (_gate)
        {
            var outcome = Resolve(toolName, out var exhausted);
            Transcript.Append(new ToolCall(++_sequence, toolName, argumentsJson, outcome, _time.GetUtcNow()));

            if (exhausted is not null)
            {
                throw exhausted;
            }

            return outcome;
        }
    }

    private ToolCallOutcome Resolve(string toolName, out ScriptExhaustedException? exhausted)
    {
        exhausted = null;

        if (_scenario.ForbiddenTools.Contains(toolName))
        {
            return new ToolCallOutcome.RefusedForbidden();
        }

        if (!_scenario.AllowedTools.Contains(toolName))
        {
            return new ToolCallOutcome.RefusedUnknown();
        }

        // Allowed but unscripted counts as exhausted at call one: the scenario let the
        // agent use the tool but never said what it answers.
        _scenario.ToolScripts.TryGetValue(toolName, out var script);
        var callNumber = _callCounts.GetValueOrDefault(toolName) + 1;
        _callCounts[toolName] = callNumber;

        if (script is null || callNumber > script.Responses.Count)
        {
            exhausted = new ScriptExhaustedException(toolName, callNumber, script?.Responses.Count ?? 0);
            return new ToolCallOutcome.ScriptExhausted(callNumber);
        }

        return script.Responses[callNumber - 1] switch
        {
            ScriptedResponse.Success s => new ToolCallOutcome.Success(s.Payload),
            ScriptedResponse.Timeout t => new ToolCallOutcome.Timeout(t.After),
            ScriptedResponse.Malformed m => new ToolCallOutcome.Malformed(m.RawText),
            var unknown => throw new DomainRuleException($"Unhandled scripted response type {unknown.GetType().Name}."),
        };
    }
}
