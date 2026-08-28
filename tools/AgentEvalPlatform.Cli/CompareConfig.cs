using System.Text.Json;
using System.Text.Json.Serialization;

namespace AgentEvalPlatform.Cli;

/// <summary>The JSON config `aep compare` reads: the configurations to run the suite under.</summary>
internal sealed record CompareConfig(
    [property: JsonPropertyName("suiteLabel")] string? SuiteLabel,
    [property: JsonPropertyName("configurations")] IReadOnlyList<CompareConfigEntry> Configurations);

internal sealed record CompareConfigEntry(
    [property: JsonPropertyName("label")] string Label,
    [property: JsonPropertyName("model")] string Model,
    [property: JsonPropertyName("promptFile")] string? PromptFile,
    [property: JsonPropertyName("maxTurns")] int? MaxTurns);

internal static class CompareConfigReader
{
    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

    public static CompareConfig Read(string path) =>
        JsonSerializer.Deserialize<CompareConfig>(File.ReadAllText(path), Options)
        ?? throw new InvalidOperationException($"Compare config '{path}' is empty.");
}
