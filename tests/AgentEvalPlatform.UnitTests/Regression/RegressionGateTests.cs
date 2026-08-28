using AgentEvalPlatform.Application.Regression;
using AgentEvalPlatform.Application.Scoring;

namespace AgentEvalPlatform.UnitTests.Regression;

public class RegressionGateTests
{
    private static readonly DateTimeOffset When = new(2026, 8, 28, 12, 0, 0, TimeSpan.Zero);

    private static Score Score(
        double successRate = 1.0,
        int unauthorized = 0,
        decimal cost = 0.10m,
        double durationSeconds = 10) =>
        new(
            ScenarioCount: 10,
            PassedScenarios: (int)(successRate * 10),
            SuccessRate: successRate,
            AssertionPassRate: successRate,
            TotalToolCalls: 20,
            UnauthorizedAttempts: unauthorized,
            TotalTokens: 5000,
            TotalDuration: TimeSpan.FromSeconds(durationSeconds),
            TotalCost: cost);

    private static Baseline Baseline(Score score) => new("suite", "baseline", "claude-opus-4-8", score, When);

    private static RegressionCheck Check(RegressionReport report, RegressionKind kind) =>
        report.Checks.Single(c => c.Kind == kind);

    [Fact]
    public void An_identical_run_passes_every_gate()
    {
        var score = Score();
        var report = RegressionGate.Compare(Baseline(score), score, new RegressionThresholds());

        Assert.True(report.Passed);
        Assert.Empty(report.Failures);
    }

    [Fact]
    public void A_success_rate_drop_beyond_tolerance_fails()
    {
        var baseline = Baseline(Score(successRate: 1.0));
        var current = Score(successRate: 0.8);

        var strict = RegressionGate.Compare(baseline, current, new RegressionThresholds(maxSuccessRateDrop: 0.1));
        Assert.False(strict.Passed);
        Assert.False(Check(strict, RegressionKind.SuccessRate).Passed);

        var lenient = RegressionGate.Compare(baseline, current, new RegressionThresholds(maxSuccessRateDrop: 0.2));
        Assert.True(Check(lenient, RegressionKind.SuccessRate).Passed);
    }

    [Fact]
    public void More_unsafe_actions_fail_unless_explicitly_allowed()
    {
        var baseline = Baseline(Score(unauthorized: 0));
        var current = Score(unauthorized: 1);

        Assert.False(Check(RegressionGate.Compare(baseline, current, new RegressionThresholds()), RegressionKind.UnsafeActions).Passed);
        Assert.True(Check(
            RegressionGate.Compare(baseline, current, new RegressionThresholds(0, allowUnsafeIncrease: true)),
            RegressionKind.UnsafeActions).Passed);
    }

    [Fact]
    public void Cost_over_the_allowed_increase_fails()
    {
        var baseline = Baseline(Score(cost: 1.00m));
        var current = Score(cost: 1.20m);

        var strict = RegressionGate.Compare(baseline, current, new RegressionThresholds(0, maxCostIncreaseFraction: 0.10));
        Assert.False(Check(strict, RegressionKind.Cost).Passed);

        var lenient = RegressionGate.Compare(baseline, current, new RegressionThresholds(0, maxCostIncreaseFraction: 0.25));
        Assert.True(Check(lenient, RegressionKind.Cost).Passed);
    }

    [Fact]
    public void Latency_over_budget_fails_and_no_budget_never_gates()
    {
        var baseline = Baseline(Score(durationSeconds: 10));
        var current = Score(durationSeconds: 40);

        var budgeted = RegressionGate.Compare(baseline, current,
            new RegressionThresholds(0, latencyBudget: TimeSpan.FromSeconds(30)));
        Assert.False(Check(budgeted, RegressionKind.Latency).Passed);

        var unbudgeted = RegressionGate.Compare(baseline, current, new RegressionThresholds());
        Assert.True(Check(unbudgeted, RegressionKind.Latency).Passed);
    }

    [Fact]
    public void Every_gate_is_reported_even_when_all_pass()
    {
        var score = Score();
        var report = RegressionGate.Compare(Baseline(score), score, new RegressionThresholds());

        Assert.Equal(
            [RegressionKind.SuccessRate, RegressionKind.UnsafeActions, RegressionKind.Cost, RegressionKind.Latency],
            report.Checks.Select(c => c.Kind));
    }
}
