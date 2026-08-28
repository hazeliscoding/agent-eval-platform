using AgentEvalPlatform.Domain;
using AgentEvalPlatform.Domain.Assertions;
using AgentEvalPlatform.Domain.Injections;
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
    private static readonly string[] ResponseKinds =
        ["success", "timeout", "malformed", "exception", "partial", "slow", "duplicate", "stale", "unauthorized", "injected"];

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
        var assertions = ParseAssertions(dto.Assertions, errors);
        var descriptionInjections = ParseToolDescriptionInjections(dto.ToolDescriptions, errors);

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
                scripts,
                assertions,
                descriptionInjections));
        }
        catch (DomainRuleException ex)
        {
            return ScenarioLoadResult.Invalid(new ScenarioValidationError("(scenario)", ex.Message));
        }
    }

    private static List<ToolScript> ParseScripts(
        Dictionary<string, List<Dictionary<string, object>>>? toolScripts,
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

                var (kind, rawValue) = entries[i].Single();
                var value = rawValue as string;
                switch (kind)
                {
                    case "success":
                        responses.Add(new ScriptedResponse.Success(value ?? string.Empty));
                        break;
                    case "malformed":
                        responses.Add(new ScriptedResponse.Malformed(value ?? string.Empty));
                        break;
                    case "exception":
                        responses.Add(new ScriptedResponse.Exception(value ?? string.Empty));
                        break;
                    case "partial":
                        responses.Add(new ScriptedResponse.Partial(value ?? string.Empty));
                        break;
                    case "duplicate":
                        responses.Add(new ScriptedResponse.Duplicate(value ?? string.Empty));
                        break;
                    case "unauthorized":
                        responses.Add(new ScriptedResponse.Unauthorized(value ?? string.Empty));
                        break;
                    case "timeout" when TryParseDuration(value, out var after):
                        responses.Add(new ScriptedResponse.Timeout(after));
                        break;
                    case "timeout":
                        errors.Add(new ScenarioValidationError(
                            path, $"Cannot parse timeout duration '{value}'. Use e.g. '5s', '250ms', or a number of seconds."));
                        break;
                    case "slow" when TryParseTimedPayload(rawValue, "latency", path, errors, out var slow):
                        responses.Add(new ScriptedResponse.Slow(slow.Duration, slow.Payload));
                        break;
                    case "stale" when TryParseTimedPayload(rawValue, "age", path, errors, out var stale):
                        responses.Add(new ScriptedResponse.Stale(stale.Duration, stale.Payload));
                        break;
                    case "slow" or "stale":
                        break; // TryParseTimedPayload already reported the specifics
                    case "injected" when TryParseInjection(rawValue, requireSurface: true, path, errors, out var injection):
                        responses.Add(new ScriptedResponse.Injected(injection, InjectionBenign(rawValue)));
                        break;
                    case "injected":
                        break; // TryParseInjection reported the specifics
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

    /// <summary>
    /// Parses the <c>assertions</c> list into the typed union. Each entry is a flat map
    /// with a snake_case <c>type</c> discriminator (matching the scenario-format
    /// examples in the plan) and exactly the fields that type requires — extra keys are
    /// errors so a typo like <c>tools:</c> can't silently weaken a scenario.
    /// </summary>
    private static List<Assertion> ParseAssertions(
        List<Dictionary<string, string>>? entries,
        List<ScenarioValidationError> errors)
    {
        var assertions = new List<Assertion>();
        if (entries is null)
        {
            return assertions;
        }

        for (var i = 0; i < entries.Count; i++)
        {
            var path = $"assertions[{i}]";
            var entry = entries[i];
            if (!entry.TryGetValue("type", out var type))
            {
                errors.Add(new ScenarioValidationError(path, "Each assertion requires a 'type' field."));
                continue;
            }

            var before = errors.Count;
            Assertion? assertion = type switch
            {
                "tool_called" => Fields(entry, path, errors, "tool") is [var tool]
                    ? new Assertion.ToolCalled(tool) : null,
                "tool_not_called" => Fields(entry, path, errors, "tool") is [var tool]
                    ? new Assertion.ToolNotCalled(tool) : null,
                "tool_call_count" => Fields(entry, path, errors, "tool", "count") is [var tool, var count]
                    ? ParseCount(tool, count, path, errors) : null,
                "output_contains" => Fields(entry, path, errors, "text") is [var text]
                    ? new Assertion.OutputContains(text) : null,
                "output_matches_schema" => Fields(entry, path, errors, "schema") is [var schema]
                    ? new Assertion.OutputMatchesSchema(schema) : null,
                "workflow_reached_state" => Fields(entry, path, errors, "state") is [var state]
                    ? new Assertion.WorkflowReachedState(state) : null,
                "no_unauthorized_actions" => Fields(entry, path, errors) is not null
                    ? new Assertion.NoUnauthorizedActions() : null,
                "maximum_token_usage" => Fields(entry, path, errors, "tokens") is [var tokens]
                    ? ParseTokens(tokens, path, errors) : null,
                "maximum_execution_time" => Fields(entry, path, errors, "duration") is [var duration]
                    ? ParseMaxDuration(duration, path, errors) : null,
                "injection_resisted" => Fields(entry, path, errors) is not null
                    ? new Assertion.InjectionResisted() : null,
                _ => AddError(errors, path, $"Unknown assertion type '{type}'."),
            };

            if (assertion is not null && errors.Count == before)
            {
                assertions.Add(assertion);
            }
        }

        return assertions;
    }

    /// <summary>
    /// Returns the values of <paramref name="required"/> in order, or null after
    /// reporting missing/unexpected keys. 'type' is always permitted.
    /// </summary>
    private static string[]? Fields(
        Dictionary<string, string> entry,
        string path,
        List<ScenarioValidationError> errors,
        params string[] required)
    {
        var ok = true;
        foreach (var key in required.Where(k => !entry.ContainsKey(k)))
        {
            errors.Add(new ScenarioValidationError(path, $"Assertion is missing required field '{key}'."));
            ok = false;
        }

        foreach (var key in entry.Keys.Where(k => k != "type" && !required.Contains(k, StringComparer.Ordinal)))
        {
            errors.Add(new ScenarioValidationError(path, $"Assertion has unexpected field '{key}'."));
            ok = false;
        }

        return ok ? required.Select(k => entry[k]).ToArray() : null;
    }

    private static Assertion? ParseCount(string tool, string count, string path, List<ScenarioValidationError> errors) =>
        int.TryParse(count, out var n) && n >= 0
            ? new Assertion.ToolCallCount(tool, n)
            : AddError(errors, path, $"'count' must be a non-negative integer, got '{count}'.");

    private static Assertion? ParseTokens(string tokens, string path, List<ScenarioValidationError> errors) =>
        long.TryParse(tokens, out var n) && n >= 0
            ? new Assertion.MaximumTokenUsage(n)
            : AddError(errors, path, $"'tokens' must be a non-negative integer, got '{tokens}'.");

    private static Assertion? ParseMaxDuration(string duration, string path, List<ScenarioValidationError> errors) =>
        TryParseDuration(duration, out var d)
            ? new Assertion.MaximumExecutionTime(d)
            : AddError(errors, path, $"Cannot parse duration '{duration}'. Use e.g. '30s', '500ms', or a number of seconds.");

    private static Assertion? AddError(List<ScenarioValidationError> errors, string path, string message)
    {
        errors.Add(new ScenarioValidationError(path, message));
        return null;
    }

    private static readonly Dictionary<string, InjectionSurface> Surfaces = new(StringComparer.Ordinal)
    {
        ["log_stream"] = InjectionSurface.LogStream,
        ["document"] = InjectionSurface.Document,
        ["email"] = InjectionSurface.Email,
        ["issue_comment"] = InjectionSurface.IssueComment,
        ["tool_description"] = InjectionSurface.ToolDescription,
    };

    /// <summary>
    /// Parses the <c>toolDescriptions</c> map — one injection per tool, always on the
    /// <c>tool_description</c> surface, with the description text as the benign content.
    /// </summary>
    private static Dictionary<string, InjectedToolDescription> ParseToolDescriptionInjections(
        Dictionary<string, Dictionary<object, object>>? toolDescriptions,
        List<ScenarioValidationError> errors)
    {
        var result = new Dictionary<string, InjectedToolDescription>(StringComparer.Ordinal);
        if (toolDescriptions is null)
        {
            return result;
        }

        foreach (var (tool, map) in toolDescriptions)
        {
            var path = $"toolDescriptions.{tool}";
            if (TryParseInjection(map, requireSurface: false, path, errors, out var injection))
            {
                if (injection.Surface != InjectionSurface.ToolDescription)
                {
                    errors.Add(new ScenarioValidationError(
                        path, "A toolDescriptions injection is always on the 'tool_description' surface; omit 'surface'."));
                    continue;
                }

                result[tool] = new InjectedToolDescription(InjectionBenign(map), injection);
            }
        }

        return result;
    }

    /// <summary>
    /// Parses an injection map: <c>template</c> XOR <c>text</c> for the adversarial
    /// string, optional <c>demandedTool</c>, optional <c>surface</c> (required for
    /// response injections, fixed for tool descriptions). Reports its own errors; the
    /// caller's <c>when</c> guard skips the add on failure.
    /// </summary>
    private static bool TryParseInjection(
        object? rawValue,
        bool requireSurface,
        string path,
        List<ScenarioValidationError> errors,
        out Injection injection)
    {
        injection = null!;
        if (rawValue is not Dictionary<object, object> map)
        {
            errors.Add(new ScenarioValidationError(path, "Expected an injection map (surface/template-or-text/demandedTool/benign)."));
            return false;
        }

        var fields = map.ToDictionary(kv => kv.Key.ToString() ?? string.Empty, kv => kv.Value?.ToString());
        var demandedTool = fields.GetValueOrDefault("demandedTool");

        var surface = InjectionSurface.ToolDescription;
        if (fields.TryGetValue("surface", out var surfaceName))
        {
            if (surfaceName is null || !Surfaces.TryGetValue(surfaceName, out surface))
            {
                errors.Add(new ScenarioValidationError(
                    path, $"Unknown surface '{surfaceName}'. Expected one of: {string.Join(", ", Surfaces.Keys)}."));
                return false;
            }
        }
        else if (requireSurface)
        {
            errors.Add(new ScenarioValidationError(path, "An injected response requires a 'surface'."));
            return false;
        }

        var hasTemplate = fields.TryGetValue("template", out var templateName);
        var hasText = fields.TryGetValue("text", out var literalText);
        if (hasTemplate == hasText)
        {
            errors.Add(new ScenarioValidationError(path, "An injection needs exactly one of 'template' or 'text'."));
            return false;
        }

        string adversarial;
        if (hasTemplate)
        {
            if (templateName is null || !InjectionTemplates.TryResolve(templateName, demandedTool, out adversarial))
            {
                errors.Add(new ScenarioValidationError(
                    path, $"Unknown injection template '{templateName}'. Known: {string.Join(", ", InjectionTemplates.Names)}."));
                return false;
            }
        }
        else
        {
            adversarial = literalText ?? string.Empty;
        }

        try
        {
            injection = new Injection(surface, adversarial, demandedTool);
            return true;
        }
        catch (DomainRuleException ex)
        {
            errors.Add(new ScenarioValidationError(path, ex.Message));
            return false;
        }
    }

    private static string InjectionBenign(object? rawValue) =>
        rawValue is Dictionary<object, object> map && map.TryGetValue("benign", out var benign)
            ? benign?.ToString() ?? string.Empty
            : string.Empty;

    /// <summary>
    /// Parses <c>slow</c>/<c>stale</c> entries, whose value is a nested map of a
    /// duration (keyed <paramref name="durationKey"/>) plus a <c>payload</c>.
    /// Reports its own errors; the caller's <c>when</c> guard just skips the add.
    /// </summary>
    private static bool TryParseTimedPayload(
        object? rawValue,
        string durationKey,
        string path,
        List<ScenarioValidationError> errors,
        out (TimeSpan Duration, string Payload) parsed)
    {
        parsed = default;
        if (rawValue is not Dictionary<object, object> map)
        {
            errors.Add(new ScenarioValidationError(
                path, $"Expected a map with '{durationKey}' and 'payload' keys."));
            return false;
        }

        var keys = map.Keys.Select(k => k.ToString()).ToList();
        if (!keys.Contains(durationKey) || !keys.Contains("payload") || keys.Count != 2)
        {
            errors.Add(new ScenarioValidationError(
                path, $"Expected exactly the keys '{durationKey}' and 'payload', got: {string.Join(", ", keys)}."));
            return false;
        }

        var durationText = map.Single(kv => kv.Key.ToString() == durationKey).Value?.ToString();
        if (!TryParseDuration(durationText, out var duration))
        {
            errors.Add(new ScenarioValidationError(
                path, $"Cannot parse {durationKey} '{durationText}'. Use e.g. '30s', '250ms', '10m', '2h', or a number of seconds."));
            return false;
        }

        parsed = (duration, map.Single(kv => kv.Key.ToString() == "payload").Value?.ToString() ?? string.Empty);
        return true;
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
            _ when text.EndsWith('m') => (text[..^1], TimeSpan.TicksPerMinute),
            _ when text.EndsWith('h') => (text[..^1], TimeSpan.TicksPerHour),
            _ => (text, TimeSpan.TicksPerSecond),
        };

        if (!double.TryParse(numberPart, System.Globalization.CultureInfo.InvariantCulture, out var number) || number < 0)
        {
            return false;
        }

        duration = TimeSpan.FromTicks((long)(number * factor));
        return true;
    }

    /// <summary>The wire shape of a scenario file.</summary>
    private sealed class ScenarioDto
    {
        public string? Name { get; set; }
        public Dictionary<string, string>? InitialState { get; set; }
        public ExpectedDto? Expected { get; set; }
        public List<string>? AllowedTools { get; set; }
        public List<string>? ForbiddenTools { get; set; }
        public Dictionary<string, List<Dictionary<string, object>>>? ToolScripts { get; set; }
        public Dictionary<string, Dictionary<object, object>>? ToolDescriptions { get; set; }
        public List<Dictionary<string, string>>? Assertions { get; set; }
    }

    private sealed class ExpectedDto
    {
        public string? Diagnosis { get; set; }
    }
}
