using System.Globalization;

namespace AgentEvalPlatform.Cli;

/// <summary>Minimal option parser: two positionals (mode, and everything else via flags).</summary>
internal sealed class Args
{
    private readonly Dictionary<string, string> _options = new(StringComparer.Ordinal);
    private readonly HashSet<string> _flags = new(StringComparer.Ordinal);

    public string Mode { get; }
    public string ScenarioDir { get; }
    public string BaselinePath { get; }

    private Args(string mode, string scenarioDir, string baselinePath) =>
        (Mode, ScenarioDir, BaselinePath) = (mode, scenarioDir, baselinePath);

    public static Args Parse(string[] args)
    {
        if (args.Length < 3)
        {
            throw new ArgumentException("Usage: aep <record|check> <scenario-dir> <baseline.json> [options]");
        }

        var parsed = new Args(args[0], args[1], args[2]);
        for (var i = 3; i < args.Length; i++)
        {
            var token = args[i];
            if (!token.StartsWith("--", StringComparison.Ordinal))
            {
                throw new ArgumentException($"Unexpected argument '{token}'.");
            }

            var name = token[2..];
            if (i + 1 < args.Length && !args[i + 1].StartsWith("--", StringComparison.Ordinal))
            {
                parsed._options[name] = args[++i];
            }
            else
            {
                parsed._flags.Add(name);
            }
        }

        return parsed;
    }

    public string Get(string name, string fallback) => _options.GetValueOrDefault(name, fallback);

    public string? GetOrNull(string name) => _options.GetValueOrDefault(name);

    public bool Flag(string name) => _flags.Contains(name);

    public double GetDouble(string name, double fallback) =>
        _options.TryGetValue(name, out var value)
            ? double.Parse(value, CultureInfo.InvariantCulture)
            : fallback;
}
