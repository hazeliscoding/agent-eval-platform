namespace AgentEvalPlatform.Domain.Assertions;

/// <summary>
/// One checkable expectation about an agent run. A closed union: the evaluator
/// switches exhaustively over the concrete types, so a new assertion kind is a new
/// variant here plus one evaluator branch — never a stringly-typed rule engine.
/// The application-side loader is the validation boundary for field values; these
/// records carry data.
/// </summary>
public abstract record Assertion
{
    private Assertion() { }

    /// <summary>The tool was called at least once.</summary>
    public sealed record ToolCalled(string Tool) : Assertion;

    /// <summary>The tool was never called.</summary>
    public sealed record ToolNotCalled(string Tool) : Assertion;

    /// <summary>The tool was called exactly <paramref name="Count"/> times.</summary>
    public sealed record ToolCallCount(string Tool, int Count) : Assertion;

    /// <summary>The final output contains <paramref name="Text"/> (ordinal, case-sensitive).</summary>
    public sealed record OutputContains(string Text) : Assertion;

    /// <summary>The final output is JSON that validates against <paramref name="SchemaJson"/>.</summary>
    public sealed record OutputMatchesSchema(string SchemaJson) : Assertion;

    /// <summary>The workflow passed through <paramref name="State"/> at some point.</summary>
    public sealed record WorkflowReachedState(string State) : Assertion;

    /// <summary>The transcript contains no refused (forbidden or unknown) tool calls.</summary>
    public sealed record NoUnauthorizedActions : Assertion;

    /// <summary>The run used at most <paramref name="MaxTokens"/> tokens.</summary>
    public sealed record MaximumTokenUsage(long MaxTokens) : Assertion;

    /// <summary>The run finished within <paramref name="Maximum"/>.</summary>
    public sealed record MaximumExecutionTime(TimeSpan Maximum) : Assertion;

    /// <summary>
    /// The agent resisted every prompt injection it was exposed to — it did not call an
    /// injection's demanded tool after receiving the payload carrying that demand.
    /// Injections that demand no specific tool are treated as resisted (nothing to do).
    /// </summary>
    public sealed record InjectionResisted : Assertion;
}

/// <summary>
/// The verdict for one assertion. <see cref="Message"/> always states what was
/// observed — a failing eval someone can't read from the report alone is noise.
/// </summary>
public sealed record AssertionResult(Assertion Assertion, bool Passed, string Message);
