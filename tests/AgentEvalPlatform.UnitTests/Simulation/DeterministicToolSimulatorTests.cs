using AgentEvalPlatform.Domain.Scenarios;
using AgentEvalPlatform.Domain.Simulation;

namespace AgentEvalPlatform.UnitTests.Simulation;

public class DeterministicToolSimulatorTests
{
    private static readonly DateTimeOffset T0 = new(2026, 8, 24, 12, 0, 0, TimeSpan.Zero);

    private static EvalScenario Scenario(
        IEnumerable<ToolScript>? scripts = null,
        IEnumerable<string>? allowed = null,
        IEnumerable<string>? forbidden = null) =>
        new(
            "test-scenario",
            new Dictionary<string, string>(),
            null,
            allowed ?? ["GetQueueMetrics", "GetServiceHealth"],
            forbidden ?? ["RedriveDeadLetterQueue"],
            scripts ?? []);

    [Fact]
    public void Replays_the_script_in_order()
    {
        // The PLAN.md phase 1 example: call 1 → success, call 2 → timeout, call 3 → malformed.
        var scenario = Scenario([
            new ToolScript("GetQueueMetrics", [
                new ScriptedResponse.Success("""{"depth":50000}"""),
                new ScriptedResponse.Timeout(TimeSpan.FromSeconds(5)),
                new ScriptedResponse.Malformed("<<<not json"),
            ]),
        ]);
        var simulator = new DeterministicToolSimulator(scenario);

        Assert.Equal(new ToolCallOutcome.Success("""{"depth":50000}"""), simulator.Call("GetQueueMetrics"));
        Assert.Equal(new ToolCallOutcome.Timeout(TimeSpan.FromSeconds(5)), simulator.Call("GetQueueMetrics"));
        Assert.Equal(new ToolCallOutcome.Malformed("<<<not json"), simulator.Call("GetQueueMetrics"));
    }

    [Fact]
    public void Tools_advance_their_scripts_independently()
    {
        var scenario = Scenario([
            new ToolScript("GetQueueMetrics", [new ScriptedResponse.Success("metrics-1")]),
            new ToolScript("GetServiceHealth", [new ScriptedResponse.Success("health-1")]),
        ]);
        var simulator = new DeterministicToolSimulator(scenario);

        Assert.Equal(new ToolCallOutcome.Success("metrics-1"), simulator.Call("GetQueueMetrics"));
        Assert.Equal(new ToolCallOutcome.Success("health-1"), simulator.Call("GetServiceHealth"));
    }

    [Fact]
    public void Forbidden_tool_is_refused_and_recorded_without_throwing()
    {
        var simulator = new DeterministicToolSimulator(Scenario());

        var outcome = simulator.Call("RedriveDeadLetterQueue", """{"queue":"checkout-dlq"}""");

        Assert.IsType<ToolCallOutcome.RefusedForbidden>(outcome);
        var call = Assert.Single(simulator.Transcript.Refusals);
        Assert.Equal("RedriveDeadLetterQueue", call.ToolName);
        Assert.Equal("""{"queue":"checkout-dlq"}""", call.ArgumentsJson);
    }

    [Fact]
    public void Unknown_tool_is_refused_and_recorded()
    {
        var simulator = new DeterministicToolSimulator(Scenario());

        Assert.IsType<ToolCallOutcome.RefusedUnknown>(simulator.Call("LaunchTheMissiles"));
        Assert.True(simulator.Transcript.WasCalled("LaunchTheMissiles"));
    }

    [Fact]
    public void Calling_past_the_end_of_a_script_throws_after_recording_the_call()
    {
        var scenario = Scenario([
            new ToolScript("GetQueueMetrics", [new ScriptedResponse.Success("only-one")]),
        ]);
        var simulator = new DeterministicToolSimulator(scenario);
        simulator.Call("GetQueueMetrics");

        var ex = Assert.Throws<ScriptExhaustedException>(() => simulator.Call("GetQueueMetrics"));

        Assert.Equal("GetQueueMetrics", ex.ToolName);
        Assert.Equal(2, ex.CallNumber);
        Assert.Equal(1, ex.ScriptedResponses);
        Assert.Equal(2, simulator.Transcript.CallCount("GetQueueMetrics"));
        Assert.IsType<ToolCallOutcome.ScriptExhausted>(simulator.Transcript.Calls[^1].Outcome);
    }

    [Fact]
    public void Allowed_but_unscripted_tool_is_exhausted_on_the_first_call()
    {
        var simulator = new DeterministicToolSimulator(Scenario());

        var ex = Assert.Throws<ScriptExhaustedException>(() => simulator.Call("GetServiceHealth"));

        Assert.Equal(1, ex.CallNumber);
        Assert.Equal(0, ex.ScriptedResponses);
    }

    [Fact]
    public void Transcript_records_sequence_and_injected_time()
    {
        var scenario = Scenario([
            new ToolScript("GetQueueMetrics", [
                new ScriptedResponse.Success("a"),
                new ScriptedResponse.Success("b"),
            ]),
        ]);
        var simulator = new DeterministicToolSimulator(scenario, new FixedTimeProvider(T0));

        simulator.Call("GetQueueMetrics");
        simulator.Call("RedriveDeadLetterQueue");
        simulator.Call("GetQueueMetrics");

        Assert.Equal([1, 2, 3], simulator.Transcript.Calls.Select(c => c.Sequence));
        Assert.All(simulator.Transcript.Calls, c => Assert.Equal(T0, c.At));
        Assert.Equal(2, simulator.Transcript.CallCount("GetQueueMetrics"));
    }

    [Fact]
    public void Timeout_responses_do_not_actually_sleep()
    {
        var scenario = Scenario([
            new ToolScript("GetQueueMetrics", [new ScriptedResponse.Timeout(TimeSpan.FromMinutes(10))]),
        ]);
        var simulator = new DeterministicToolSimulator(scenario);

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        simulator.Call("GetQueueMetrics");
        stopwatch.Stop();

        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(1));
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
