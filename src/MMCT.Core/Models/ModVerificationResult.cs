namespace MMCT.Core.Models;

/// <summary>Result of looking a mod up on public platforms (Modrinth / CurseForge).</summary>
public sealed class ModVerificationResult
{
    /// <summary>True iff any platform returned a matching mod page.</summary>
    public bool PlatformMatched { get; init; }

    /// <summary>Which platforms matched: e.g. ["Modrinth"], ["Modrinth","CurseForge"].</summary>
    public List<string> MatchedPlatforms { get; init; } = new();

    /// <summary>Human-readable popularity label from the first match.</summary>
    public string? PopularityLabel { get; init; }

    /// <summary>Modrinth / CF project slug if known.</summary>
    public string? ProjectSlug { get; init; }

    /// <summary>Non-empty if verification failed for non-404 reasons (network/auth).</summary>
    public string? ErrorNote { get; init; }
}
