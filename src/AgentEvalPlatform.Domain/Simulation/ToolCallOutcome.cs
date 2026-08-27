namespace AgentEvalPlatform.Domain.Simulation;

/// <summary>
/// What the simulator actually handed back for one tool call. Mirrors the scripted
/// response variants, plus the two refusals the simulator issues on its own — an
/// agent touching a forbidden or unknown tool gets a refusal as data, never an
/// exception, because observing that behavior is the point of the eval.
/// </summary>
public abstract record ToolCallOutcome
{
    private ToolCallOutcome() { }

    public sealed record Success(string Payload) : ToolCallOutcome;

    public sealed record Timeout(TimeSpan After) : ToolCallOutcome;

    public sealed record Malformed(string RawText) : ToolCallOutcome;

    public sealed record Exception(string Message) : ToolCallOutcome;

    public sealed record Partial(string Payload) : ToolCallOutcome;

    public sealed record Slow(TimeSpan Latency, string Payload) : ToolCallOutcome;

    public sealed record Duplicate(string Payload) : ToolCallOutcome;

    public sealed record Stale(TimeSpan Age, string Payload) : ToolCallOutcome;

    /// <summary>The tool denied the call itself — not a policy refusal by the simulator.</summary>
    public sealed record Unauthorized(string Message) : ToolCallOutcome;

    /// <summary>
    /// A response carrying an adversarial payload. <see cref="Payload"/> is what the
    /// agent saw (benign content + embedded attack); <see cref="Injection"/> is the
    /// ground truth the agent never sees.
    /// </summary>
    public sealed record Injected(Injections.Injection Injection, string Payload) : ToolCallOutcome;

    /// <summary>The tool is in the scenario's forbiddenTools list.</summary>
    public sealed record RefusedForbidden : ToolCallOutcome;

    /// <summary>The tool is neither allowed nor forbidden — the scenario doesn't know it.</summary>
    public sealed record RefusedUnknown : ToolCallOutcome;

    /// <summary>The tool is allowed but its script had no response left for this call.</summary>
    public sealed record ScriptExhausted(int CallNumber) : ToolCallOutcome;
}
