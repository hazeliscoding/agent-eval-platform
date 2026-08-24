namespace AgentEvalPlatform.Domain;

/// <summary>
/// Base for domain invariant violations. Higher layers translate these into typed
/// results rather than letting them escape as unhandled errors.
/// </summary>
public abstract class DomainException(string message) : Exception(message);

/// <summary>The supplied data is invalid regardless of current state (a validation error).</summary>
public sealed class DomainRuleException(string message) : DomainException(message);
