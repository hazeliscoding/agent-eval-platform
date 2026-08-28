namespace AgentEvalPlatform.Application.Scoring;

/// <summary>Per-million-token input/output rates, in USD.</summary>
public sealed record ModelPrice(decimal InputPerMTok, decimal OutputPerMTok);

/// <summary>
/// A small, code-resident rate table so scores can report cost. Unknown models cost
/// zero rather than throwing — a missing rate must never fail a run — and callers can
/// supply their own table for models not listed here.
/// </summary>
public sealed class ModelPricing
{
    private static readonly IReadOnlyDictionary<string, ModelPrice> Default =
        new Dictionary<string, ModelPrice>(StringComparer.Ordinal)
        {
            ["claude-fable-5"] = new(10.00m, 50.00m),
            ["claude-opus-4-8"] = new(5.00m, 25.00m),
            ["claude-sonnet-5"] = new(3.00m, 15.00m),
            ["claude-haiku-4-5"] = new(1.00m, 5.00m),
            ["claude-haiku-4-5-20251001"] = new(1.00m, 5.00m),
        };

    private readonly IReadOnlyDictionary<string, ModelPrice> _rates;

    public ModelPricing(IReadOnlyDictionary<string, ModelPrice>? rates = null) => _rates = rates ?? Default;

    /// <summary>Cost in USD for the given token split, or zero when the model has no known rate.</summary>
    public decimal CostOf(string model, long inputTokens, long outputTokens)
    {
        if (!_rates.TryGetValue(model, out var rate))
        {
            return 0m;
        }

        return (inputTokens * rate.InputPerMTok + outputTokens * rate.OutputPerMTok) / 1_000_000m;
    }

    public bool Knows(string model) => _rates.ContainsKey(model);
}
