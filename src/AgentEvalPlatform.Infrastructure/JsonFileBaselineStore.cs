using System.Text.Json;
using System.Text.Json.Serialization;
using AgentEvalPlatform.Application.Regression;

namespace AgentEvalPlatform.Infrastructure;

/// <summary>
/// Stores a baseline as a single, human-readable JSON file — meant to be committed, so a
/// score change surfaces in a pull-request diff. Indented and stably ordered for clean
/// diffs; the score's <see cref="System.TimeSpan"/> and <see cref="decimal"/> round-trip
/// exactly.
/// </summary>
public sealed class JsonFileBaselineStore(string path) : IBaselineStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    };

    public async Task SaveAsync(Baseline baseline, CancellationToken cancellationToken = default)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, baseline, Options, cancellationToken);
    }

    public async Task<Baseline?> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        await using var stream = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<Baseline>(stream, Options, cancellationToken);
    }
}
