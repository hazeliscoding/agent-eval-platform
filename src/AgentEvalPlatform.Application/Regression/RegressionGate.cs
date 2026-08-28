using System.Globalization;
using AgentEvalPlatform.Application.Scoring;

namespace AgentEvalPlatform.Application.Regression;

/// <summary>
/// Compares a fresh score against a baseline under a set of thresholds and reports each
/// gate. Pure arithmetic over two <see cref="Score"/>s — no model, no IO — so a given
/// baseline, score, and thresholds always yield the same verdict.
/// </summary>
public static class RegressionGate
{
    public static RegressionReport Compare(Baseline baseline, Score current, RegressionThresholds thresholds)
    {
        var checks = new List<RegressionCheck>
        {
            CheckSuccessRate(baseline.Score, current, thresholds),
            CheckUnsafeActions(baseline.Score, current, thresholds),
            CheckCost(baseline.Score, current, thresholds),
            CheckLatency(current, thresholds),
        };

        return new RegressionReport(baseline.SuiteName, checks);
    }

    private static RegressionCheck CheckSuccessRate(Score baseline, Score current, RegressionThresholds thresholds)
    {
        var floor = baseline.SuccessRate - thresholds.MaxSuccessRateDrop;
        var passed = current.SuccessRate >= floor - Epsilon;
        return new RegressionCheck(RegressionKind.SuccessRate, passed,
            $"success rate {Pct(current.SuccessRate)} vs baseline {Pct(baseline.SuccessRate)} " +
            $"(floor {Pct(floor)}, max drop {Pct(thresholds.MaxSuccessRateDrop)})");
    }

    private static RegressionCheck CheckUnsafeActions(Score baseline, Score current, RegressionThresholds thresholds)
    {
        var passed = thresholds.AllowUnsafeIncrease || current.UnauthorizedAttempts <= baseline.UnauthorizedAttempts;
        return new RegressionCheck(RegressionKind.UnsafeActions, passed,
            $"unauthorized attempts {current.UnauthorizedAttempts} vs baseline {baseline.UnauthorizedAttempts}" +
            (thresholds.AllowUnsafeIncrease ? " (increase allowed)" : " (no increase allowed)"));
    }

    private static RegressionCheck CheckCost(Score baseline, Score current, RegressionThresholds thresholds)
    {
        var ceiling = baseline.TotalCost * (1m + (decimal)thresholds.MaxCostIncreaseFraction);
        // A baseline of zero cost (e.g. an unpriced model) can't be exceeded by a fraction;
        // any positive cost then trips the gate, which is the safe reading.
        var passed = current.TotalCost <= ceiling + (decimal)Epsilon;
        return new RegressionCheck(RegressionKind.Cost, passed,
            $"cost ${Money(current.TotalCost)} vs baseline ${Money(baseline.TotalCost)} " +
            $"(ceiling ${Money(ceiling)}, max increase {Pct(thresholds.MaxCostIncreaseFraction)})");
    }

    private static RegressionCheck CheckLatency(Score current, RegressionThresholds thresholds)
    {
        if (thresholds.LatencyBudget is not { } budget)
        {
            return new RegressionCheck(RegressionKind.Latency, true,
                $"simulated latency {Secs(current.TotalDuration)} (no budget set)");
        }

        var passed = current.TotalDuration <= budget;
        return new RegressionCheck(RegressionKind.Latency, passed,
            $"simulated latency {Secs(current.TotalDuration)} vs budget {Secs(budget)}");
    }

    private const double Epsilon = 1e-9;

    private static string Pct(double fraction) => (fraction * 100).ToString("0.#", CultureInfo.InvariantCulture) + "%";

    private static string Money(decimal value) => value.ToString("0.####", CultureInfo.InvariantCulture);

    private static string Secs(TimeSpan value) => value.TotalSeconds.ToString("0.###", CultureInfo.InvariantCulture) + "s";
}
