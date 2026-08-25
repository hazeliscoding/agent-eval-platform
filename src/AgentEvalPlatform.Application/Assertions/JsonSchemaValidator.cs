using System.Text.Json;
using System.Text.Json.Nodes;
using AgentEvalPlatform.Domain.Assertions;
using Json.Schema;

namespace AgentEvalPlatform.Application.Assertions;

/// <summary>
/// <see cref="ISchemaValidator"/> backed by JsonSchema.Net. Both failure modes an
/// eval can hit — the scenario author wrote a broken schema, or the agent produced
/// non-JSON output — surface as invalid results that say which side is at fault.
/// </summary>
public sealed class JsonSchemaValidator : ISchemaValidator
{
    public SchemaValidationResult Validate(string schemaJson, string instanceJson)
    {
        JsonSchema schema;
        try
        {
            schema = JsonSchema.FromText(schemaJson);
        }
        catch (JsonException ex)
        {
            return SchemaValidationResult.Invalid($"The assertion's schema is not valid JSON: {ex.Message}");
        }

        JsonDocument instance;
        try
        {
            instance = JsonDocument.Parse(instanceJson);
        }
        catch (JsonException ex)
        {
            return SchemaValidationResult.Invalid($"The output is not valid JSON: {ex.Message}");
        }

        using (instance)
        {
            var results = schema.Evaluate(instance.RootElement, new EvaluationOptions { OutputFormat = OutputFormat.List });
            if (results.IsValid)
            {
                return SchemaValidationResult.Valid();
            }

            var errors = (results.Details ?? [])
                .Where(d => d.Errors is { Count: > 0 })
                .SelectMany(d => d.Errors!.Select(e => $"{Location(d)}: {e.Value}"))
                .Distinct()
                .ToList();

            return SchemaValidationResult.Invalid(
                errors.Count > 0 ? errors : ["The output does not satisfy the schema."]);
        }

        static string Location(EvaluationResults detail)
        {
            var pointer = detail.InstanceLocation.ToString();
            return pointer.Length == 0 ? "$" : $"${pointer.Replace('/', '.')}";
        }
    }
}
