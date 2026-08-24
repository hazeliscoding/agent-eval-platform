using AgentEvalPlatform.Domain;
using AgentEvalPlatform.Domain.Scenarios;
using YamlDotNet.Core;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace AgentEvalPlatform.Application.Scenarios;

/// <summary>
/// Parses the YAML scenario format into the validated domain model. Structural
/// problems (bad YAML, wrong shapes, unknown response kinds) come back as
/// <see cref="ScenarioValidationError"/>s with the offending path; domain invariants
/// stay in <see cref="EvalScenario"/> and are translated, not duplicated, here.
/// </summary>
public sealed class ScenarioLoader
{
    private static readonly string[] ResponseKinds = ["success", "timeout", "malformed"];

    private readonly IDeserializer _deserializer = new DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .Build();

    public ScenarioLoadResult Load(string yaml)
    {
        ScenarioDto? dto;
        try
        {
            dto = _deserializer.Deserialize<ScenarioDto?>(yaml);
        }
        catch (YamlException ex)
        {
            var detail = ex.InnerException?.Message ?? ex.Message;
            return ScenarioLoadResult.Invalid(
                new ScenarioValidationError($"line {ex.Start.Line}", detail));
        }

        if (dto is null)
        {
            return ScenarioLoadResult.Invalid(new ScenarioValidationError("(document)", "The scenario file is empty."));
        }

        var errors = new List<ScenarioValidationError>();

        if (string.IsNullOrWhiteSpace(dto.Name))
        {
            errors.Add(new ScenarioValidationError("name", "A scenario requires a name."));
        }

        var scripts = ParseScripts(dto.ToolScripts, errors);

        if (errors.Count > 0)
        {
            return ScenarioLoadResult.Invalid(errors);
        }

        try
        {
            return ScenarioLoadResult.Valid(new EvalScenario(
                dto.Name!,
                dto.InitialState ?? new Dictionary<string, string>(),
                dto.Expected?.Diagnosis,
                dto.AllowedTools ?? [],
                dto.ForbiddenTools ?? [],
                scripts));
        }
        catch (DomainRuleException ex)
        {
            return ScenarioLoadResult.Invalid(new ScenarioValidationError("(scenario)", ex.Message));
        }
    }

    private static List<ToolScript> ParseScripts(
        Dictionary<string, List<Dictionary<string, string>>>? toolScripts,
        List<ScenarioValidationError> errors)
    {
        var scripts = new List<ToolScript>();
        if (toolScripts is null)
        {
            return scripts;
        }

        foreach (var (toolName, entries) in toolScripts)
        {
            var responses = new List<ScriptedResponse>();
            for (var i = 0; i < entries.Count; i++)
            {
                var path = $"toolScripts.{toolName}[{i}]";
                if (entries[i].Count != 1)
                {
                    errors.Add(new ScenarioValidationError(
                        path, $"Each response must be a single-key map of one of: {string.Join(", ", ResponseKinds)}."));
                    continue;
                }

                var (kind, value) = entries[i].Single();
                switch (kind)
                {
                    case "success":
                        responses.Add(new ScriptedResponse.Success(value ?? string.Empty));
                        break;
                    case "malformed":
                        responses.Add(new ScriptedResponse.Malformed(value ?? string.Empty));
                        break;
                    case "timeout" when TryParseDuration(value, out var after):
                        responses.Add(new ScriptedResponse.Timeout(after));
                        break;
                    case "timeout":
                        errors.Add(new ScenarioValidationError(
                            path, $"Cannot parse timeout duration '{value}'. Use e.g. '5s', '250ms', or a number of seconds."));
                        break;
                    default:
                        errors.Add(new ScenarioValidationError(
                            path, $"Unknown response kind '{kind}'. Expected one of: {string.Join(", ", ResponseKinds)}."));
                        break;
                }
            }

            if (responses.Count == 0)
            {
                // Only an authored-empty list earns its own error; entries that failed
                // to parse have already been reported individually.
                if (entries.Count == 0)
                {
                    errors.Add(new ScenarioValidationError(
                        $"toolScripts.{toolName}", "Tool script has no responses."));
                }

                continue;
            }

            try
            {
                scripts.Add(new ToolScript(toolName, responses));
            }
            catch (DomainRuleException ex)
            {
                errors.Add(new ScenarioValidationError($"toolScripts.{toolName}", ex.Message));
            }
        }

        return scripts;
    }

    private static bool TryParseDuration(string? value, out TimeSpan duration)
    {
        duration = default;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var text = value.Trim();
        var (numberPart, factor) = text switch
        {
            _ when text.EndsWith("ms", StringComparison.Ordinal) => (text[..^2], TimeSpan.TicksPerMillisecond),
            _ when text.EndsWith('s') => (text[..^1], TimeSpan.TicksPerSecond),
            _ => (text, TimeSpan.TicksPerSecond),
        };

        if (!double.TryParse(numberPart, System.Globalization.CultureInfo.InvariantCulture, out var number) || number < 0)
        {
            return false;
        }

        duration = TimeSpan.FromTicks((long)(number * factor));
        return true;
    }

    /// <summary>
    /// The wire shape of a scenario file. <c>Assertions</c> is accepted but unread —
    /// Phase 2 gives it meaning; rejecting it now would break forward-written scenarios.
    /// </summary>
    private sealed class ScenarioDto
    {
        public string? Name { get; set; }
        public Dictionary<string, string>? InitialState { get; set; }
        public ExpectedDto? Expected { get; set; }
        public List<string>? AllowedTools { get; set; }
        public List<string>? ForbiddenTools { get; set; }
        public Dictionary<string, List<Dictionary<string, string>>>? ToolScripts { get; set; }
        public List<object>? Assertions { get; set; }
    }

    private sealed class ExpectedDto
    {
        public string? Diagnosis { get; set; }
    }
}
