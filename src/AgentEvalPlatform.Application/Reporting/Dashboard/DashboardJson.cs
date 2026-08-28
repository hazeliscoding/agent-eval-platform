using System.Text.Encodings.Web;
using System.Text.Json;

namespace AgentEvalPlatform.Application.Reporting.Dashboard;

/// <summary>Serializes the dashboard dataset with one stable option set (camelCase, indented).</summary>
public static class DashboardJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        // The dataset is a committed artifact people read and diff — keep quotes and
        // apostrophes literal instead of the default \u00XX escapes.
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static string Serialize(DashboardDataset dataset) => JsonSerializer.Serialize(dataset, Options);

    public static DashboardDataset Deserialize(string json) =>
        JsonSerializer.Deserialize<DashboardDataset>(json, Options)
        ?? throw new JsonException("The dashboard dataset JSON was null.");
}
