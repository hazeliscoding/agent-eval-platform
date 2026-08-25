using AgentEvalPlatform.Application.Scenarios;
using AgentEvalPlatform.Domain.Assertions;
using AgentEvalPlatform.Domain.Scenarios;

namespace AgentEvalPlatform.UnitTests.Scenarios;

public class ScenarioLoaderTests
{
    private readonly ScenarioLoader _loader = new();

    private static string Fixture(string file) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", file));

    [Fact]
    public void Loads_the_plan_example_with_typed_assertions()
    {
        var result = _loader.Load(Fixture("plan-example.yaml"));

        Assert.True(result.IsValid, string.Join("; ", result.Errors));
        var scenario = result.Scenario!;
        Assert.Equal("queue-backlog-correct-diagnosis", scenario.Name);
        Assert.Equal("50000", scenario.InitialState["queueDepth"]);
        Assert.Equal("0", scenario.InitialState["workerCount"]);
        Assert.Equal("worker-unavailable", scenario.ExpectedDiagnosis);
        Assert.Equal(["GetQueueMetrics", "GetServiceHealth"], scenario.AllowedTools.Order());
        Assert.Equal(["RedriveDeadLetterQueue"], scenario.ForbiddenTools);
        Assert.Empty(scenario.ToolScripts);
        Assert.Equal(
            [new Assertion.ToolCalled("GetQueueMetrics"), new Assertion.ToolNotCalled("RedriveDeadLetterQueue")],
            scenario.Assertions);
    }

    [Fact]
    public void Loads_every_assertion_type()
    {
        var result = _loader.Load(
            """
            name: s
            allowedTools: [T]
            assertions:
              - type: tool_called
                tool: T
              - type: tool_not_called
                tool: U
              - type: tool_call_count
                tool: T
                count: 2
              - type: output_contains
                text: worker-unavailable
              - type: output_matches_schema
                schema: '{"type": "object"}'
              - type: workflow_reached_state
                state: Resolved
              - type: no_unauthorized_actions
              - type: maximum_token_usage
                tokens: 20000
              - type: maximum_execution_time
                duration: 30s
            """);

        Assert.True(result.IsValid, string.Join("; ", result.Errors));
        Assert.Equal(
            [
                new Assertion.ToolCalled("T"),
                new Assertion.ToolNotCalled("U"),
                new Assertion.ToolCallCount("T", 2),
                new Assertion.OutputContains("worker-unavailable"),
                new Assertion.OutputMatchesSchema("""{"type": "object"}"""),
                new Assertion.WorkflowReachedState("Resolved"),
                new Assertion.NoUnauthorizedActions(),
                new Assertion.MaximumTokenUsage(20000),
                new Assertion.MaximumExecutionTime(TimeSpan.FromSeconds(30)),
            ],
            result.Scenario!.Assertions);
    }

    [Fact]
    public void Unknown_assertion_type_is_an_error()
    {
        var result = _loader.Load(
            """
            name: s
            assertions:
              - type: tool_summoned
                tool: T
            """);

        Assert.False(result.IsValid);
        var error = Assert.Single(result.Errors);
        Assert.Equal("assertions[0]", error.Path);
        Assert.Contains("tool_summoned", error.Message);
    }

    [Fact]
    public void Missing_and_unexpected_assertion_fields_are_both_reported()
    {
        var result = _loader.Load(
            """
            name: s
            assertions:
              - type: tool_called
                tools: T
            """);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Message.Contains("missing required field 'tool'"));
        Assert.Contains(result.Errors, e => e.Message.Contains("unexpected field 'tools'"));
    }

    [Fact]
    public void Non_numeric_count_is_an_error()
    {
        var result = _loader.Load(
            """
            name: s
            assertions:
              - type: tool_call_count
                tool: T
                count: many
            """);

        Assert.False(result.IsValid);
        Assert.Contains("many", Assert.Single(result.Errors).Message);
    }

    [Fact]
    public void Assertion_without_a_type_is_an_error()
    {
        var result = _loader.Load(
            """
            name: s
            assertions:
              - tool: T
            """);

        Assert.False(result.IsValid);
        Assert.Contains("requires a 'type' field", Assert.Single(result.Errors).Message);
    }

    [Fact]
    public void Loads_tool_scripts_with_every_response_kind_and_duration_format()
    {
        var result = _loader.Load(Fixture("scripted-tools.yaml"));

        Assert.True(result.IsValid, string.Join("; ", result.Errors));
        var metrics = result.Scenario!.ToolScripts["GetQueueMetrics"].Responses;
        Assert.Equal(new ScriptedResponse.Success("""{"depth": 50000, "inFlight": 0}"""), metrics[0]);
        Assert.Equal(new ScriptedResponse.Timeout(TimeSpan.FromSeconds(5)), metrics[1]);
        Assert.Equal(new ScriptedResponse.Malformed("<<<not json at all"), metrics[2]);

        var health = result.Scenario.ToolScripts["GetServiceHealth"].Responses;
        Assert.Equal(new ScriptedResponse.Timeout(TimeSpan.FromMilliseconds(250)), health[0]);
    }

    [Fact]
    public void Bare_number_timeouts_parse_as_seconds()
    {
        var result = _loader.Load(
            """
            name: s
            allowedTools: [T]
            toolScripts:
              T:
                - timeout: 3
            """);

        Assert.True(result.IsValid, string.Join("; ", result.Errors));
        Assert.Equal(
            new ScriptedResponse.Timeout(TimeSpan.FromSeconds(3)),
            result.Scenario!.ToolScripts["T"].Responses[0]);
    }

    [Fact]
    public void Unknown_response_kind_names_the_path()
    {
        var result = _loader.Load(
            """
            name: s
            allowedTools: [T]
            toolScripts:
              T:
                - explode: boom
            """);

        Assert.False(result.IsValid);
        var error = Assert.Single(result.Errors);
        Assert.Equal("toolScripts.T[0]", error.Path);
        Assert.Contains("explode", error.Message);
    }

    [Fact]
    public void Unparsable_timeout_duration_is_an_error()
    {
        var result = _loader.Load(
            """
            name: s
            allowedTools: [T]
            toolScripts:
              T:
                - timeout: eventually
            """);

        Assert.False(result.IsValid);
        Assert.Contains("eventually", Assert.Single(result.Errors).Message);
    }

    [Fact]
    public void Missing_name_is_an_error()
    {
        var result = _loader.Load("allowedTools: [T]");

        Assert.False(result.IsValid);
        Assert.Equal("name", Assert.Single(result.Errors).Path);
    }

    [Fact]
    public void Empty_document_is_an_error_not_an_exception()
    {
        var result = _loader.Load("");

        Assert.False(result.IsValid);
        Assert.Single(result.Errors);
    }

    [Fact]
    public void Broken_yaml_reports_the_line_instead_of_throwing()
    {
        var result = _loader.Load("name: [unclosed");

        Assert.False(result.IsValid);
        Assert.StartsWith("line ", Assert.Single(result.Errors).Path);
    }

    [Fact]
    public void Domain_invariants_surface_as_validation_errors()
    {
        var result = _loader.Load(
            """
            name: s
            allowedTools: [T]
            forbiddenTools: [T]
            """);

        Assert.False(result.IsValid);
        Assert.Contains("both allowed and forbidden", Assert.Single(result.Errors).Message);
    }

    [Fact]
    public void Script_for_a_tool_missing_from_allowedTools_is_an_error()
    {
        var result = _loader.Load(
            """
            name: s
            allowedTools: [T]
            toolScripts:
              Other:
                - success: ok
            """);

        Assert.False(result.IsValid);
        Assert.Contains("Other", Assert.Single(result.Errors).Message);
    }
}
