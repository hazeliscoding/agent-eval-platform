namespace AgentEvalPlatform.Cli;

/// <summary>Process exit codes. Non-zero on regression is the CI gate.</summary>
internal static class ExitCode
{
    public const int Ok = 0;
    public const int Regressed = 1;
    public const int Usage = 2;
    public const int BadInput = 3;
}
