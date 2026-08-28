using AgentEvalPlatform.Application.Regression;
using AgentEvalPlatform.Application.Scoring;
using AgentEvalPlatform.Infrastructure;

namespace AgentEvalPlatform.IntegrationTests;

public class JsonFileBaselineStoreTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "aep-baseline-" + Guid.NewGuid().ToString("N"));

    private string BaselinePath => Path.Combine(_dir, "nested", "baseline.json");

    [Fact]
    public async Task Round_trips_a_baseline_including_decimal_and_timespan()
    {
        var score = new Score(
            ScenarioCount: 10, PassedScenarios: 9, SuccessRate: 0.9, AssertionPassRate: 0.95,
            TotalToolCalls: 42, UnauthorizedAttempts: 1, TotalTokens: 123456,
            TotalDuration: TimeSpan.FromSeconds(35.5), TotalCost: 1.2345m);
        var recordedAt = new DateTimeOffset(2026, 8, 28, 9, 30, 0, TimeSpan.Zero);
        var baseline = new Baseline("suite", "opus", "claude-opus-4-8", score, recordedAt);

        var store = new JsonFileBaselineStore(BaselinePath);
        await store.SaveAsync(baseline);          // also creates the nested directory
        var loaded = await store.LoadAsync();

        Assert.Equal(baseline, loaded);           // record equality covers every field
        Assert.Equal(1.2345m, loaded!.Score.TotalCost);
        Assert.Equal(TimeSpan.FromSeconds(35.5), loaded.Score.TotalDuration);
    }

    [Fact]
    public async Task Loading_a_missing_baseline_returns_null()
    {
        var store = new JsonFileBaselineStore(BaselinePath);
        Assert.Null(await store.LoadAsync());
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }
    }
}
