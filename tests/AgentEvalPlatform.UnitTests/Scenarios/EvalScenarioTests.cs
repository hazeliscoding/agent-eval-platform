using AgentEvalPlatform.Domain;
using AgentEvalPlatform.Domain.Scenarios;

namespace AgentEvalPlatform.UnitTests.Scenarios;

public class EvalScenarioTests
{
    private static readonly IReadOnlyDictionary<string, string> NoState = new Dictionary<string, string>();

    [Fact]
    public void Rejects_blank_name()
    {
        var ex = Assert.Throws<DomainRuleException>(() =>
            new EvalScenario("  ", NoState, null, [], [], []));
        Assert.Contains("name", ex.Message);
    }

    [Fact]
    public void Rejects_tool_that_is_both_allowed_and_forbidden()
    {
        var ex = Assert.Throws<DomainRuleException>(() =>
            new EvalScenario("s", NoState, null, ["GetQueueMetrics"], ["GetQueueMetrics"], []));
        Assert.Contains("GetQueueMetrics", ex.Message);
    }

    [Fact]
    public void Rejects_two_scripts_for_the_same_tool()
    {
        var script = new ToolScript("GetQueueMetrics", [new ScriptedResponse.Success("{}")]);
        Assert.Throws<DomainRuleException>(() =>
            new EvalScenario("s", NoState, null, ["GetQueueMetrics"], [], [script, script]));
    }

    [Fact]
    public void Rejects_script_for_a_tool_that_is_not_allowed()
    {
        var script = new ToolScript("NotAllowed", [new ScriptedResponse.Success("{}")]);
        var ex = Assert.Throws<DomainRuleException>(() =>
            new EvalScenario("s", NoState, null, ["GetQueueMetrics"], [], [script]));
        Assert.Contains("NotAllowed", ex.Message);
    }

    [Fact]
    public void Tool_script_requires_a_name_and_at_least_one_response()
    {
        Assert.Throws<DomainRuleException>(() => new ToolScript(" ", [new ScriptedResponse.Success("{}")]));
        Assert.Throws<DomainRuleException>(() => new ToolScript("GetQueueMetrics", []));
    }

    [Fact]
    public void Valid_scenario_exposes_its_parts()
    {
        var script = new ToolScript("GetQueueMetrics", [new ScriptedResponse.Success("{}")]);
        var scenario = new EvalScenario(
            "queue-backlog",
            new Dictionary<string, string> { ["queueDepth"] = "50000" },
            "worker-unavailable",
            ["GetQueueMetrics", "GetServiceHealth"],
            ["RedriveDeadLetterQueue"],
            [script]);

        Assert.Equal("queue-backlog", scenario.Name);
        Assert.Equal("50000", scenario.InitialState["queueDepth"]);
        Assert.Equal("worker-unavailable", scenario.ExpectedDiagnosis);
        Assert.Contains("GetServiceHealth", scenario.AllowedTools);
        Assert.Contains("RedriveDeadLetterQueue", scenario.ForbiddenTools);
        Assert.Same(script, scenario.ToolScripts["GetQueueMetrics"]);
    }
}
