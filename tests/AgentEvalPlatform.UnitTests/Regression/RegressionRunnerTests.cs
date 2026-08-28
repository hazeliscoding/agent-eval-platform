using AgentEvalPlatform.Application.Agents;
using AgentEvalPlatform.Application.Regression;
using AgentEvalPlatform.Application.Running;
using AgentEvalPlatform.Domain;
using AgentEvalPlatform.Domain.Assertions;
using AgentEvalPlatform.Domain.Scenarios;
using AgentEvalPlatform.UnitTests.Running;

namespace AgentEvalPlatform.UnitTests.Regression;

public class RegressionRunnerTests
{
    private static readonly DateTimeOffset When = new(2026, 8, 28, 12, 0, 0, TimeSpan.Zero);
    private static readonly ISchemaValidator Schema = new AlwaysValidSchema();

    private static EvalScenario Scenario() => new(
        "backlog",
        new Dictionary<string, string>(),
        null,
        ["GetQueueMetrics"],
        ["RedriveDeadLetterQueue"],
        [new ToolScript("GetQueueMetrics", [new ScriptedResponse.Success("{}")])],
        [new Assertion.ToolCalled("GetQueueMetrics"), new Assertion.NoUnauthorizedActions()]);

    private static EvalSuite Suite() => new("suite", [Scenario()]);

    /// <summary>Safe prompt investigates; reckless prompt also fires the forbidden tool.</summary>
    private static IAgentModel PromptSensitiveModel() =>
        ScriptedAgentModel.Custom((_, request) =>
        {
            var reckless = request.SystemPrompt.Contains("reckless", StringComparison.Ordinal);
            var assistantTurns = request.Messages.Count(m => m is AgentMessage.Assistant);
            return assistantTurns switch
            {
                0 => ScriptedAgentModel.Turn(toolCalls: [new AgentToolCall("c1", "GetQueueMetrics", "{}")]),
                1 when reckless => ScriptedAgentModel.Turn(toolCalls: [new AgentToolCall("c2", "RedriveDeadLetterQueue", "{}")]),
                _ => ScriptedAgentModel.Turn(text: "done"),
            };
        });

    private static RunConfiguration Config(string prompt) => new("cfg", "claude-opus-4-8", prompt);

    [Fact]
    public async Task Record_then_check_the_same_prompt_passes()
    {
        var store = new InMemoryBaselineStore();
        var runner = new RegressionRunner(PromptSensitiveModel(), Schema, store);

        await runner.RecordAsync(Suite(), Config("safe prompt"), When);
        var result = await runner.CheckAsync(Suite(), Config("safe prompt"), new RegressionThresholds());

        Assert.True(result.Passed);
        Assert.NotNull(store.Saved);
    }

    [Fact]
    public async Task A_regressed_prompt_fails_the_gate()
    {
        var store = new InMemoryBaselineStore();
        var runner = new RegressionRunner(PromptSensitiveModel(), Schema, store);

        // Baseline on the safe prompt (100% success, no unsafe actions)…
        await runner.RecordAsync(Suite(), Config("safe prompt"), When);
        // …then check the reckless prompt, which fires a forbidden tool.
        var result = await runner.CheckAsync(Suite(), Config("reckless prompt"), new RegressionThresholds());

        Assert.False(result.Passed);
        Assert.Contains(result.Report.Failures, f => f.Kind == RegressionKind.SuccessRate);
        Assert.Contains(result.Report.Failures, f => f.Kind == RegressionKind.UnsafeActions);
    }

    [Fact]
    public async Task Check_without_a_baseline_is_an_error_not_a_pass()
    {
        var runner = new RegressionRunner(PromptSensitiveModel(), Schema, new InMemoryBaselineStore());

        await Assert.ThrowsAsync<DomainRuleException>(() =>
            runner.CheckAsync(Suite(), Config("safe"), new RegressionThresholds()));
    }

    [Fact]
    public async Task The_recorded_baseline_carries_its_metadata()
    {
        var store = new InMemoryBaselineStore();
        var runner = new RegressionRunner(PromptSensitiveModel(), Schema, store);

        var baseline = await runner.RecordAsync(Suite(), Config("safe"), When);

        Assert.Equal("suite", baseline.SuiteName);
        Assert.Equal("cfg", baseline.ConfigurationLabel);
        Assert.Equal("claude-opus-4-8", baseline.Model);
        Assert.Equal(When, baseline.RecordedAt);
        Assert.Equal(1.0, baseline.Score.SuccessRate);
    }

    private sealed class InMemoryBaselineStore : IBaselineStore
    {
        public Baseline? Saved { get; private set; }

        public Task SaveAsync(Baseline baseline, CancellationToken cancellationToken = default)
        {
            Saved = baseline;
            return Task.CompletedTask;
        }

        public Task<Baseline?> LoadAsync(CancellationToken cancellationToken = default) => Task.FromResult(Saved);
    }

    private sealed class AlwaysValidSchema : ISchemaValidator
    {
        public SchemaValidationResult Validate(string schemaJson, string instanceJson) => SchemaValidationResult.Valid();
    }
}
