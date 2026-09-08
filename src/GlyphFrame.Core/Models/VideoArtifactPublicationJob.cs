namespace GlyphFrame.Core.Models;

/// <summary>The destination version observed before asking the user for overwrite permission.</summary>
public sealed record VideoPublicationDestination(string FilePath, string? ExistingVersion);

public sealed record VideoArtifactPublicationJob(
    ManagedVideoArtifact Artifact,
    string OriginalInputPath,
    VideoPublicationDestination Destination,
    bool Overwrite);
