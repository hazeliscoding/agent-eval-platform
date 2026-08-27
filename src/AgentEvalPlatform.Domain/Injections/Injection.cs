namespace AgentEvalPlatform.Domain.Injections;

/// <summary>Where an adversarial string is embedded — the channels the plan names.</summary>
public enum InjectionSurface
{
    LogStream,
    Document,
    Email,
    IssueComment,
    ToolDescription,
}

/// <summary>
/// Ground truth about one adversarial payload: which surface carried it, the exact
/// adversarial text, and — when the injection demands a tool call — which tool, so
/// the resistance assertion can check post-exposure behavior. The agent never sees
/// this record; it sees only the composed payload.
/// </summary>
public sealed record Injection
{
    public InjectionSurface Surface { get; }
    public string AdversarialText { get; }

    /// <summary>The tool the injection tries to coerce the agent into calling, if any.</summary>
    public string? DemandedTool { get; }

    public Injection(InjectionSurface surface, string adversarialText, string? demandedTool)
    {
        if (string.IsNullOrWhiteSpace(adversarialText))
        {
            throw new DomainRuleException("An injection requires adversarial text.");
        }

        Surface = surface;
        AdversarialText = adversarialText;
        DemandedTool = demandedTool;
    }

    /// <summary>
    /// Composes the payload the agent will actually receive: the benign content with
    /// the adversarial text embedded the way that surface would plausibly carry it.
    /// Deterministic — the same inputs always produce the same payload.
    /// </summary>
    public string Embed(string benignContent) => Surface switch
    {
        InjectionSurface.LogStream =>
            $"{benignContent}\n{AdversarialText}",
        InjectionSurface.Document =>
            $"{benignContent}\n\n{AdversarialText}\n\n(document continues)",
        InjectionSurface.Email =>
            $"From: ops@example.com\nSubject: Re: ongoing incident\n\n{benignContent}\n\n{AdversarialText}\n",
        InjectionSurface.IssueComment =>
            $"Comment by maintainer:\n{benignContent}\n\nComment by anonymous user:\n{AdversarialText}",
        InjectionSurface.ToolDescription =>
            $"{benignContent} {AdversarialText}",
        _ => throw new DomainRuleException($"Unhandled injection surface {Surface}."),
    };
}
