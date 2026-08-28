namespace AgentEvalPlatform.Application.Agents;

/// <summary>A tool the model may call: name, description, and a JSON Schema for its input.</summary>
public sealed record AgentToolDefinition(string Name, string Description, string InputSchemaJson);

/// <summary>A tool call the model requested in a turn.</summary>
public sealed record AgentToolCall(string Id, string Name, string ArgumentsJson);

/// <summary>The result of executing (or refusing) a tool call, returned to the model.</summary>
public sealed record AgentToolResult(string ToolCallId, string ContentJson, bool IsError);

/// <summary>
/// A reasoning block emitted by the model. Preserved verbatim — including the provider's
/// signature — so it can be echoed back unchanged on later turns, which some providers require.
/// </summary>
public sealed record AgentThinking(string? Text, string? Signature, string? RedactedData);

/// <summary>One assistant turn: optional reasoning, optional text, requested tool calls, and token usage.</summary>
public sealed record AgentTurn(
    IReadOnlyList<AgentThinking> Thinking,
    string? Text,
    IReadOnlyList<AgentToolCall> ToolCalls,
    long InputTokens = 0,
    long OutputTokens = 0);

/// <summary>Provider-neutral conversation entries. The model adapter maps these to its wire format.</summary>
public abstract record AgentMessage
{
    public sealed record User(string Text) : AgentMessage;

    public sealed record Assistant(AgentTurn Turn) : AgentMessage;

    public sealed record ToolResults(IReadOnlyList<AgentToolResult> Results) : AgentMessage;
}

/// <summary>One model request: the system prompt, the conversation so far, and the available tools.</summary>
public sealed record AgentRequest(
    string SystemPrompt,
    IReadOnlyList<AgentMessage> Messages,
    IReadOnlyList<AgentToolDefinition> Tools);

/// <summary>
/// Port for a single model turn. Implementations are stateless per call — the full
/// conversation is supplied each time — which keeps providers replaceable. This is the
/// only non-deterministic seam in the platform: tools and scoring stay deterministic,
/// the model is the thing under test.
/// </summary>
public interface IAgentModel
{
    Task<AgentTurn> NextTurnAsync(string model, AgentRequest request, CancellationToken cancellationToken);
}
