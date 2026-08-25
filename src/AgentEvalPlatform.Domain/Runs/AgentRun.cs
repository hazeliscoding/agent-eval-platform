using AgentEvalPlatform.Domain.Simulation;

namespace AgentEvalPlatform.Domain.Runs;

/// <summary>
/// Everything one agent run produced that assertions can judge: the tool transcript,
/// the agent's final output, the workflow states it moved through (in order), and the
/// run's cost envelope. Runner phases produce this; the assertion evaluator consumes it.
/// </summary>
public sealed record AgentRun
{
    public ToolCallTranscript Transcript { get; }
    public string Output { get; }
    public IReadOnlyList<string> ReachedStates { get; }
    public long TokensUsed { get; }
    public TimeSpan Duration { get; }

    public AgentRun(
        ToolCallTranscript transcript,
        string output,
        IReadOnlyList<string> reachedStates,
        long tokensUsed,
        TimeSpan duration)
    {
        if (tokensUsed < 0)
        {
            throw new DomainRuleException("A run cannot use a negative number of tokens.");
        }

        if (duration < TimeSpan.Zero)
        {
            throw new DomainRuleException("A run cannot have a negative duration.");
        }

        Transcript = transcript;
        Output = output;
        ReachedStates = reachedStates;
        TokensUsed = tokensUsed;
        Duration = duration;
    }
}
