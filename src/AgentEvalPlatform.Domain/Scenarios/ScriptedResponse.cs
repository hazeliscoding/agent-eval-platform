namespace AgentEvalPlatform.Domain.Scenarios;

/// <summary>
/// One predefined tool response in a scenario's script. A closed union: the simulator
/// switches on the concrete type, and later phases (fault injection) extend the set
/// with new variants rather than adding flags to existing ones.
/// </summary>
public abstract record ScriptedResponse
{
    private ScriptedResponse() { }

    /// <summary>The tool succeeds and returns <paramref name="Payload"/> verbatim.</summary>
    public sealed record Success(string Payload) : ScriptedResponse;

    /// <summary>
    /// The tool never answers within the agent's patience. The simulator reports the
    /// timeout without actually sleeping — evals must stay fast and deterministic.
    /// </summary>
    public sealed record Timeout(TimeSpan After) : ScriptedResponse;

    /// <summary>
    /// The tool answers with bytes that don't parse as the tool's contract.
    /// <paramref name="RawText"/> is returned exactly as scripted so scenarios can
    /// probe how an agent copes with specific kinds of garbage.
    /// </summary>
    public sealed record Malformed(string RawText) : ScriptedResponse;

    /// <summary>The tool fails outright with an error message (the HTTP-500 of tools).</summary>
    public sealed record Exception(string Message) : ScriptedResponse;

    /// <summary>
    /// The tool returns only a prefix of its real answer — a connection that died
    /// mid-body. <paramref name="Payload"/> is the truncated text as the agent sees it.
    /// </summary>
    public sealed record Partial(string Payload) : ScriptedResponse;

    /// <summary>
    /// The tool succeeds, but only after <paramref name="Latency"/>. Like timeouts,
    /// the latency is reported rather than slept.
    /// </summary>
    public sealed record Slow(TimeSpan Latency, string Payload) : ScriptedResponse;

    /// <summary>
    /// The tool delivers a payload the agent has effectively seen before — a
    /// re-delivered event. The payload itself carries no marker; whether the agent
    /// notices the duplication is what the scenario is probing.
    /// </summary>
    public sealed record Duplicate(string Payload) : ScriptedResponse;

    /// <summary>
    /// The tool answers with data that is <paramref name="Age"/> out of date. As with
    /// duplicates, staleness is visible only through the payload's own content
    /// (timestamps etc.); the age here is ground truth for the transcript.
    /// </summary>
    public sealed record Stale(TimeSpan Age, string Payload) : ScriptedResponse;

    /// <summary>
    /// The tool itself denies the call ("token expired") even though the scenario
    /// allows the agent to use it. Distinct from the simulator's policy refusals,
    /// which mean the *agent* overstepped.
    /// </summary>
    public sealed record Unauthorized(string Message) : ScriptedResponse;
}
