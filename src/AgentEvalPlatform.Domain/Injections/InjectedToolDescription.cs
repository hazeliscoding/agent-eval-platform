namespace AgentEvalPlatform.Domain.Injections;

/// <summary>
/// A tool whose description carries an injection. The benign description is what the
/// tool legitimately does; <see cref="Composed"/> is what the agent actually reads —
/// the benign text with the adversarial instruction embedded.
/// </summary>
public sealed record InjectedToolDescription(string BenignDescription, Injection Injection)
{
    public string Composed => Injection.Embed(BenignDescription);
}
