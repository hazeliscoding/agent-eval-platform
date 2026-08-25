namespace AgentEvalPlatform.Domain.Assertions;

/// <summary>
/// Port for JSON Schema validation so the domain stays dependency-free. The
/// application layer supplies the real implementation (JsonSchema.Net); tests can
/// supply a scripted one.
/// </summary>
public interface ISchemaValidator
{
    /// <summary>
    /// Never throws: a broken schema or a non-JSON instance comes back as an invalid
    /// result whose errors say which of the two it was.
    /// </summary>
    SchemaValidationResult Validate(string schemaJson, string instanceJson);
}

public sealed record SchemaValidationResult(bool IsValid, IReadOnlyList<string> Errors)
{
    public static SchemaValidationResult Valid() => new(true, []);

    public static SchemaValidationResult Invalid(params IReadOnlyList<string> errors) => new(false, errors);
}
