using System.Text.Json;
using AgentEvalPlatform.Application.Agents;
using Anthropic;
using Anthropic.Models.Messages;

namespace AgentEvalPlatform.Infrastructure;

/// <summary>
/// <see cref="IAgentModel"/> adapter over the official Anthropic SDK. Stateless per call:
/// the neutral conversation is mapped to Anthropic content blocks every request, any
/// thinking blocks are echoed back verbatim (signatures intact) as the API requires, and
/// per-turn token usage is carried back so the platform can score cost. Thinking is left
/// unset so the adapter works across the whole model matrix the comparison drives —
/// adaptive thinking 400s on models that don't support it (e.g. Haiku 4.5), and evals
/// judge tool behavior, not reasoning depth. This is the platform's one live,
/// non-deterministic seam — everything downstream of the returned turn is deterministic.
/// </summary>
public sealed class AnthropicAgentModel(AnthropicClient? client = null, long maxTokens = 8000) : IAgentModel
{
    // Created on first use so a missing ANTHROPIC_API_KEY only fails an actual live run,
    // not construction (deterministic tests never touch this).
    private readonly Lazy<AnthropicClient> _client = new(() => client ?? new AnthropicClient());

    public async Task<AgentTurn> NextTurnAsync(string model, AgentRequest request, CancellationToken cancellationToken)
    {
        var parameters = new MessageCreateParams
        {
            Model = model,
            MaxTokens = maxTokens,
            System = request.SystemPrompt,
            Tools = request.Tools.Select(ToAnthropicTool).ToList(),
            Messages = request.Messages.Select(ToMessageParam).ToList(),
        };

        var response = await _client.Value.Messages.Create(parameters, cancellationToken: cancellationToken);
        return ToAgentTurn(response);
    }

    private static ToolUnion ToAnthropicTool(AgentToolDefinition definition)
    {
        var schema = JsonDocument.Parse(definition.InputSchemaJson).RootElement;
        var properties = new Dictionary<string, JsonElement>();
        if (schema.TryGetProperty("properties", out var propertiesElement)
            && propertiesElement.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in propertiesElement.EnumerateObject())
            {
                properties[property.Name] = property.Value.Clone();
            }
        }

        var required = new List<string>();
        if (schema.TryGetProperty("required", out var requiredElement)
            && requiredElement.ValueKind == JsonValueKind.Array)
        {
            required.AddRange(requiredElement.EnumerateArray()
                .Where(e => e.ValueKind == JsonValueKind.String)
                .Select(e => e.GetString()!));
        }

        return new Tool
        {
            Name = definition.Name,
            Description = definition.Description,
            InputSchema = new() { Properties = properties, Required = required },
        };
    }

    private static MessageParam ToMessageParam(AgentMessage message) => message switch
    {
        AgentMessage.User user => new MessageParam { Role = Role.User, Content = user.Text },
        AgentMessage.Assistant assistant => new MessageParam
        {
            Role = Role.Assistant,
            Content = ToAssistantContent(assistant.Turn),
        },
        AgentMessage.ToolResults results => new MessageParam
        {
            Role = Role.User,
            Content = results.Results
                .Select(r => (ContentBlockParam)new ToolResultBlockParam
                {
                    ToolUseID = r.ToolCallId,
                    Content = r.ContentJson,
                    IsError = r.IsError,
                })
                .ToList(),
        },
        _ => throw new ArgumentOutOfRangeException(nameof(message), message.GetType().Name, "Unknown message kind."),
    };

    private static List<ContentBlockParam> ToAssistantContent(AgentTurn turn)
    {
        var content = new List<ContentBlockParam>();
        foreach (var thinking in turn.Thinking)
        {
            if (thinking.RedactedData is not null)
            {
                content.Add(new RedactedThinkingBlockParam { Data = thinking.RedactedData });
            }
            else
            {
                // The signature must round-trip untouched — the API rejects tampered blocks.
                content.Add(new ThinkingBlockParam
                {
                    Thinking = thinking.Text ?? string.Empty,
                    Signature = thinking.Signature ?? string.Empty,
                });
            }
        }

        if (!string.IsNullOrEmpty(turn.Text))
        {
            content.Add(new TextBlockParam { Text = turn.Text });
        }

        foreach (var call in turn.ToolCalls)
        {
            content.Add(new ToolUseBlockParam
            {
                ID = call.Id,
                Name = call.Name,
                Input = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(call.ArgumentsJson) ?? [],
            });
        }

        return content;
    }

    private static AgentTurn ToAgentTurn(Message response)
    {
        var thinking = new List<AgentThinking>();
        var textParts = new List<string>();
        var toolCalls = new List<AgentToolCall>();

        foreach (var block in response.Content)
        {
            if (block.TryPickThinking(out ThinkingBlock? thinkingBlock))
            {
                thinking.Add(new AgentThinking(thinkingBlock.Thinking, thinkingBlock.Signature, null));
            }
            else if (block.TryPickRedactedThinking(out RedactedThinkingBlock? redacted))
            {
                thinking.Add(new AgentThinking(null, null, redacted.Data));
            }
            else if (block.TryPickText(out TextBlock? text))
            {
                textParts.Add(text.Text);
            }
            else if (block.TryPickToolUse(out ToolUseBlock? toolUse))
            {
                toolCalls.Add(new AgentToolCall(
                    toolUse.ID,
                    toolUse.Name,
                    JsonSerializer.Serialize(toolUse.Input)));
            }
        }

        return new AgentTurn(
            thinking,
            textParts.Count == 0 ? null : string.Join("\n", textParts),
            toolCalls,
            response.Usage.InputTokens,
            response.Usage.OutputTokens);
    }
}
