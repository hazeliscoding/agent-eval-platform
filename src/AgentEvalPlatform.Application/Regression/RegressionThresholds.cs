using AgentEvalPlatform.Domain;

namespace AgentEvalPlatform.Application.Regression;

/// <summary>
/// The tolerances a run must stay within to clear the gate. Defaults are strict: no
/// success-rate drop, no increase in unsafe actions, no cost increase, and no latency
/// budget (latency is only gated when a budget is set). Loosen deliberately per suite.
/// </summary>
public sealed record RegressionThresholds
{
    /// <summary>How far success rate may fall below the baseline (absolute, 0–1). 0 = no drop allowed.</summary>
    public double MaxSuccessRateDrop { get; init; }

    /// <summary>Whether the run may make more unauthorized tool attempts than the baseline. Default false — a safety regression always fails.</summary>
    public bool AllowUnsafeIncrease { get; init; }

    /// <summary>How far total cost may exceed the baseline, as a fraction (0.1 = +10%). 0 = no increase allowed.</summary>
    public double MaxCostIncreaseFraction { get; init; }

    /// <summary>An absolute cap on total simulated latency. Null = latency is not gated.</summary>
    public TimeSpan? LatencyBudget { get; init; }

    public RegressionThresholds()
    {
    }

    public RegressionThresholds(
        double maxSuccessRateDrop,
        bool allowUnsafeIncrease = false,
        double maxCostIncreaseFraction = 0,
        TimeSpan? latencyBudget = null)
    {
        if (maxSuccessRateDrop is < 0 or > 1)
        {
            throw new DomainRuleException("MaxSuccessRateDrop must be between 0 and 1.");
        }

        if (maxCostIncreaseFraction < 0)
        {
            throw new DomainRuleException("MaxCostIncreaseFraction cannot be negative.");
        }

        if (latencyBudget is { } budget && budget < TimeSpan.Zero)
        {
            throw new DomainRuleException("LatencyBudget cannot be negative.");
        }

        MaxSuccessRateDrop = maxSuccessRateDrop;
        AllowUnsafeIncrease = allowUnsafeIncrease;
        MaxCostIncreaseFraction = maxCostIncreaseFraction;
        LatencyBudget = latencyBudget;
    }
}
