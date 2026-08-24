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
}
