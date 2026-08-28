using AgentEvalPlatform.Application.Agents;

namespace AgentEvalPlatform.UnitTests.Running;

/// <summary>
/// A deterministic fake model: it plays a predetermined script of turns, so the runner
/// and comparison can be tested without a live API. A turn is either a set of tool calls
/// or a final text answer. When configured per-model, it can behave differently across
/// configurations — the setup a model/prompt comparison needs.
/// </summary>
public sealed class ScriptedAgentModel : IAgentModel
{
    private readonly Func<string, AgentRequest, AgentTurn> _behavior;

    private ScriptedAgentModel(Func<string, AgentRequest, AgentTurn> behavior) => _behavior = behavior;

    public Task<AgentTurn> NextTurnAsync(string model, AgentRequest request, CancellationToken cancellationToken) =>
        Task.FromResult(_behavior(model, request));

    /// <summary>Calls one tool (once), then answers with <paramref name="finalText"/> on the next turn.</summary>
    public static ScriptedAgentModel CallThenAnswer(string tool, string arguments, string finalText)
    {
        var calls = 0;
        return new ScriptedAgentModel((_, _) =>
            calls++ == 0
                ? Turn(toolCalls: [new AgentToolCall($"call-{calls}", tool, arguments)])
                : Turn(text: finalText));
    }

    /// <summary>Answers immediately with <paramref name="finalText"/>, calling no tools.</summary>
    public static ScriptedAgentModel AnswerOnly(string finalText) =>
        new((_, _) => Turn(text: finalText));

    /// <summary>Behaviour chosen per model id — for comparing configurations.</summary>
    public static ScriptedAgentModel PerModel(Func<string, IAgentModel> pick) =>
        new((model, request) => pick(model).NextTurnAsync(model, request, CancellationToken.None).Result);

    /// <summary>Full control: supply the turn for each (model, request).</summary>
    public static ScriptedAgentModel Custom(Func<string, AgentRequest, AgentTurn> behavior) => new(behavior);

    public static AgentTurn Turn(
        string? text = null,
        IReadOnlyList<AgentToolCall>? toolCalls = null,
        long inputTokens = 100,
        long outputTokens = 20) =>
        new([], text, toolCalls ?? [], inputTokens, outputTokens);
}
