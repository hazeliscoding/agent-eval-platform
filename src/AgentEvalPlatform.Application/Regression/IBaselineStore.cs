namespace AgentEvalPlatform.Application.Regression;

/// <summary>
/// Persistence for baselines, behind a port so the application stays IO-free. The
/// shipped implementation stores them as version-controlled JSON files.
/// </summary>
public interface IBaselineStore
{
    Task SaveAsync(Baseline baseline, CancellationToken cancellationToken = default);

    /// <summary>Loads the baseline, or null when none has been recorded yet.</summary>
    Task<Baseline?> LoadAsync(CancellationToken cancellationToken = default);
}
