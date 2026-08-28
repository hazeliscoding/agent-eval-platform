using AgentEvalPlatform.Application.Scenarios;

namespace AgentEvalPlatform.UnitTests.Scenarios;

public class SuiteLoaderTests
{
    private const string ScenarioA = """
        name: scenario-a
        allowedTools: [GetQueueMetrics]
        assertions:
          - type: tool_called
            tool: GetQueueMetrics
        """;

    private const string ScenarioB = """
        name: scenario-b
        allowedTools: [GetServiceHealth]
        """;

    [Fact]
    public void Assembles_a_suite_from_valid_documents()
    {
        var result = SuiteLoader.Load("my-suite", [("a.yaml", ScenarioA), ("b.yaml", ScenarioB)]);

        Assert.True(result.IsValid, string.Join("; ", result.Errors));
        Assert.Equal("my-suite", result.Suite!.Name);
        Assert.Equal(["scenario-a", "scenario-b"], result.Suite.Scenarios.Select(s => s.Name));
    }

    [Fact]
    public void One_bad_document_aborts_the_whole_suite_and_names_the_source()
    {
        var result = SuiteLoader.Load("my-suite", [("good.yaml", ScenarioA), ("bad.yaml", "name: [unclosed")]);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.StartsWith("bad.yaml:", StringComparison.Ordinal));
    }

    [Fact]
    public void An_empty_document_set_is_an_error()
    {
        var result = SuiteLoader.Load("empty", []);

        Assert.False(result.IsValid);
        Assert.Contains("No scenario documents", Assert.Single(result.Errors));
    }

    [Fact]
    public void Duplicate_scenario_names_are_rejected_by_the_suite()
    {
        var result = SuiteLoader.Load("dupes", [("a.yaml", ScenarioA), ("a-again.yaml", ScenarioA)]);

        Assert.False(result.IsValid);
        Assert.Contains("scenario-a", Assert.Single(result.Errors));
    }
}
