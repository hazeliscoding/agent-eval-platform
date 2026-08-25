using AgentEvalPlatform.Domain.Assertions;
using AgentEvalPlatform.Domain.Runs;
using AgentEvalPlatform.Domain.Scenarios;
using AgentEvalPlatform.Domain.Simulation;

namespace AgentEvalPlatform.UnitTests.Assertions;

public class AssertionEvaluatorTests
{
    private readonly ScriptedSchemaValidator _schemaValidator = new();
    private readonly AssertionEvaluator _evaluator;

    public AssertionEvaluatorTests() => _evaluator = new AssertionEvaluator(_schemaValidator);

    /// <summary>Builds a run through the real simulator so transcripts look like production ones.</summary>
    private static AgentRun Run(
        string[]? calls = null,
        string output = "",
        string[]? states = null,
        long tokens = 0,
        TimeSpan? duration = null)
    {
        var scenario = new EvalScenario(
            "eval-test",
            new Dictionary<string, string>(),
            null,
            ["GetQueueMetrics", "GetServiceHealth"],
            ["RedriveDeadLetterQueue"],
            [
                new ToolScript("GetQueueMetrics", Enumerable.Repeat<ScriptedResponse>(new ScriptedResponse.Success("{}"), 5).ToList()),
                new ToolScript("GetServiceHealth", Enumerable.Repeat<ScriptedResponse>(new ScriptedResponse.Success("{}"), 5).ToList()),
            ]);
        var simulator = new DeterministicToolSimulator(scenario);
        foreach (var tool in calls ?? [])
        {
            simulator.Call(tool);
        }

        return new AgentRun(simulator.Transcript, output, states ?? [], tokens, duration ?? TimeSpan.Zero);
    }

    [Fact]
    public void ToolCalled_passes_when_called_and_fails_when_not()
    {
        var run = Run(calls: ["GetQueueMetrics"]);

        Assert.True(_evaluator.Evaluate(new Assertion.ToolCalled("GetQueueMetrics"), run).Passed);

        var failed = _evaluator.Evaluate(new Assertion.ToolCalled("GetServiceHealth"), run);
        Assert.False(failed.Passed);
        Assert.Equal("'GetServiceHealth' was never called.", failed.Message);
    }

    [Fact]
    public void ToolNotCalled_fails_with_the_observed_count()
    {
        var run = Run(calls: ["GetQueueMetrics", "GetQueueMetrics"]);

        var result = _evaluator.Evaluate(new Assertion.ToolNotCalled("GetQueueMetrics"), run);

        Assert.False(result.Passed);
        Assert.Contains("was called 2 time(s)", result.Message);
    }

    [Fact]
    public void ToolCallCount_is_exact()
    {
        var run = Run(calls: ["GetQueueMetrics", "GetQueueMetrics"]);

        Assert.True(_evaluator.Evaluate(new Assertion.ToolCallCount("GetQueueMetrics", 2), run).Passed);

        var failed = _evaluator.Evaluate(new Assertion.ToolCallCount("GetQueueMetrics", 3), run);
        Assert.False(failed.Passed);
        Assert.Equal("Expected exactly 3 call(s) to 'GetQueueMetrics', observed 2.", failed.Message);
    }

    [Fact]
    public void OutputContains_is_case_sensitive()
    {
        var run = Run(output: "Diagnosis: worker-unavailable");

        Assert.True(_evaluator.Evaluate(new Assertion.OutputContains("worker-unavailable"), run).Passed);
        Assert.False(_evaluator.Evaluate(new Assertion.OutputContains("Worker-Unavailable"), run).Passed);
    }

    [Fact]
    public void OutputMatchesSchema_reports_the_validators_errors()
    {
        _schemaValidator.NextResult = SchemaValidationResult.Invalid("$.diagnosis: expected string");
        var run = Run(output: """{"diagnosis": 42}""");

        var result = _evaluator.Evaluate(new Assertion.OutputMatchesSchema("{}"), run);

        Assert.False(result.Passed);
        Assert.Contains("$.diagnosis: expected string", result.Message);
    }

    [Fact]
    public void WorkflowReachedState_lists_the_actual_path_on_failure()
    {
        var run = Run(states: ["Detected", "Investigating"]);

        Assert.True(_evaluator.Evaluate(new Assertion.WorkflowReachedState("Investigating"), run).Passed);

        var failed = _evaluator.Evaluate(new Assertion.WorkflowReachedState("Resolved"), run);
        Assert.False(failed.Passed);
        Assert.Contains("Detected -> Investigating", failed.Message);
    }

    [Fact]
    public void NoUnauthorizedActions_passes_on_a_clean_run()
    {
        var run = Run(calls: ["GetQueueMetrics"]);

        Assert.True(_evaluator.Evaluate(new Assertion.NoUnauthorizedActions(), run).Passed);
    }

    [Fact]
    public void NoUnauthorizedActions_names_each_refused_call()
    {
        var run = Run(calls: ["GetQueueMetrics", "RedriveDeadLetterQueue", "LaunchTheMissiles"]);

        var result = _evaluator.Evaluate(new Assertion.NoUnauthorizedActions(), run);

        Assert.False(result.Passed);
        Assert.Contains("2 unauthorized tool call(s)", result.Message);
        Assert.Contains("'RedriveDeadLetterQueue' (call #2, forbidden)", result.Message);
        Assert.Contains("'LaunchTheMissiles' (call #3, unknown)", result.Message);
    }

    [Fact]
    public void Budgets_compare_against_observed_usage()
    {
        var run = Run(tokens: 25_000, duration: TimeSpan.FromSeconds(45));

        var tokensResult = _evaluator.Evaluate(new Assertion.MaximumTokenUsage(20_000), run);
        Assert.False(tokensResult.Passed);
        Assert.Equal("Run used 25000 token(s); budget 20000.", tokensResult.Message);

        var timeResult = _evaluator.Evaluate(new Assertion.MaximumExecutionTime(TimeSpan.FromSeconds(30)), run);
        Assert.False(timeResult.Passed);
        Assert.Equal("Run took 45s; budget 30s.", timeResult.Message);

        Assert.True(_evaluator.Evaluate(new Assertion.MaximumTokenUsage(25_000), run).Passed);
        Assert.True(_evaluator.Evaluate(new Assertion.MaximumExecutionTime(TimeSpan.FromSeconds(45)), run).Passed);
    }

    [Fact]
    public void Evaluate_returns_one_result_per_assertion_in_order()
    {
        var run = Run(calls: ["GetQueueMetrics"], output: "worker-unavailable");
        var assertions = new Assertion[]
        {
            new Assertion.ToolCalled("GetQueueMetrics"),
            new Assertion.ToolNotCalled("GetQueueMetrics"),
            new Assertion.OutputContains("worker-unavailable"),
        };

        var results = _evaluator.Evaluate(assertions, run);

        Assert.Equal([true, false, true], results.Select(r => r.Passed));
        Assert.Equal(assertions, results.Select(r => r.Assertion));
    }

    private sealed class ScriptedSchemaValidator : ISchemaValidator
    {
        public SchemaValidationResult NextResult { get; set; } = SchemaValidationResult.Valid();

        public SchemaValidationResult Validate(string schemaJson, string instanceJson) => NextResult;
    }
}
