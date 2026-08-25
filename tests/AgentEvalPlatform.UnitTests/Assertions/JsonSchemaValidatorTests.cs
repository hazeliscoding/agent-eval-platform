using AgentEvalPlatform.Application.Assertions;

namespace AgentEvalPlatform.UnitTests.Assertions;

public class JsonSchemaValidatorTests
{
    private const string DiagnosisSchema = """
        {
          "type": "object",
          "properties": {
            "diagnosis": { "type": "string" },
            "confidence": { "type": "number" }
          },
          "required": ["diagnosis"],
          "additionalProperties": false
        }
        """;

    private readonly JsonSchemaValidator _validator = new();

    [Fact]
    public void Valid_instance_passes()
    {
        var result = _validator.Validate(DiagnosisSchema, """{"diagnosis": "worker-unavailable", "confidence": 0.9}""");

        Assert.True(result.IsValid, string.Join("; ", result.Errors));
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Wrong_type_fails_with_a_located_error()
    {
        var result = _validator.Validate(DiagnosisSchema, """{"diagnosis": 42}""");

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("diagnosis"));
    }

    [Fact]
    public void Missing_required_property_fails()
    {
        var result = _validator.Validate(DiagnosisSchema, """{"confidence": 0.9}""");

        Assert.False(result.IsValid);
        Assert.NotEmpty(result.Errors);
    }

    [Fact]
    public void Non_json_output_is_reported_as_the_agents_fault()
    {
        var result = _validator.Validate(DiagnosisSchema, "I think the workers are down");

        Assert.False(result.IsValid);
        Assert.Contains("output is not valid JSON", Assert.Single(result.Errors));
    }

    [Fact]
    public void Broken_schema_is_reported_as_the_scenarios_fault()
    {
        var result = _validator.Validate("{not a schema", """{"diagnosis": "x"}""");

        Assert.False(result.IsValid);
        Assert.Contains("schema is not valid JSON", Assert.Single(result.Errors));
    }
}
