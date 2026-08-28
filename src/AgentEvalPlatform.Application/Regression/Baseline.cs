using AgentEvalPlatform.Application.Scoring;

namespace AgentEvalPlatform.Application.Regression;

/// <summary>
/// A recorded score for a suite under one configuration — the reference a later run is
/// gated against. Stored as JSON and version-controlled, so a change to the numbers
/// shows up in a pull request diff. <see cref="RecordedAt"/> is supplied by the caller
/// (the domain never reads the clock), so it stays out of equality-sensitive logic.
/// </summary>
public sealed record Baseline(
    string SuiteName,
    string ConfigurationLabel,
    string Model,
    Score Score,
    DateTimeOffset RecordedAt);
