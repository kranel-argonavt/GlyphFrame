using GlyphFrame.Core.Enums;

namespace GlyphFrame.Core.Models;

/// <summary>
/// Tracks platform-neutral still-frame preview validity and operation coordination.
/// A source version identifies either a decoded image or one decoded video frame.
/// Media paths, pixel bytes, and UI-framework objects are intentionally excluded.
/// </summary>
public sealed record ImageWorkflowState
{
    private ImageWorkflowState(
        MediaSelectionKind mediaType,
        long mediaVersion,
        long sourceVersion,
        long settingsVersion,
        PreviewIdentity? symbolizedPreviewIdentity,
        long activeOperationId,
        bool hasOriginal,
        bool hasSymbolizedPreview)
    {
        MediaType = mediaType;
        MediaVersion = mediaVersion;
        SourceVersion = sourceVersion;
        SettingsVersion = settingsVersion;
        SymbolizedPreviewIdentity = symbolizedPreviewIdentity;
        ActiveOperationId = activeOperationId;
        HasOriginal = hasOriginal;
        HasSymbolizedPreview = hasSymbolizedPreview;
    }

    public static ImageWorkflowState Empty { get; } = new(
        MediaSelectionKind.None,
        0,
        0,
        0,
        null,
        0,
        false,
        false);

    public MediaSelectionKind MediaType { get; private init; }

    public long MediaVersion { get; private init; }

    public long SourceVersion { get; private init; }

    public long SettingsVersion { get; private init; }

    public PreviewIdentity? SymbolizedPreviewIdentity { get; private init; }

    public long? PreviewSettingsVersion => SymbolizedPreviewIdentity?.SettingsVersion;

    public long ActiveOperationId { get; private init; }

    public bool HasOriginal { get; private init; }

    public bool HasSymbolizedPreview { get; private init; }

    public PreviewIdentity? CurrentIdentity => HasOriginal
        ? new PreviewIdentity(MediaType, MediaVersion, SourceVersion, SettingsVersion)
        : null;

    public bool CanPreview => MediaType is MediaSelectionKind.Image or MediaSelectionKind.Video && HasOriginal;

    public bool CanExport => MediaType == MediaSelectionKind.Image && HasOriginal;

    public bool IsPreviewStale => HasSymbolizedPreview && SymbolizedPreviewIdentity != CurrentIdentity;

    public bool RequiresRenderForExport => !HasSymbolizedPreview || IsPreviewStale;

    public ImageWorkflowState SelectMedia(MediaSelectionKind mediaType, long mediaVersion, bool hasOriginal)
    {
        if (mediaVersion <= MediaVersion)
        {
            throw new ArgumentOutOfRangeException(nameof(mediaVersion), "Media versions must increase.");
        }

        bool validOriginal = mediaType is MediaSelectionKind.Image or MediaSelectionKind.Video && hasOriginal;
        return new(
            mediaType,
            mediaVersion,
            validOriginal ? 1 : 0,
            SettingsVersion,
            null,
            0,
            validOriginal,
            false);
    }

    public ImageWorkflowState SourceChanged()
    {
        if (MediaType == MediaSelectionKind.None)
        {
            return this;
        }

        return this with { SourceVersion = checked(SourceVersion + 1) };
    }

    public ImageWorkflowState OriginalAvailable(long mediaVersion)
    {
        if (mediaVersion != MediaVersion || MediaType == MediaSelectionKind.None)
        {
            return this;
        }

        long sourceVersion = SourceVersion == 0 ? 1 : SourceVersion;
        return this with { SourceVersion = sourceVersion, HasOriginal = true };
    }

    public ImageWorkflowState RenderingSettingChanged(ImageRenderSetting setting)
    {
        if (!Enum.IsDefined(setting))
        {
            throw new ArgumentOutOfRangeException(nameof(setting));
        }

        return this with { SettingsVersion = checked(SettingsVersion + 1) };
    }

    public ImageWorkflowState OutputFormatChanged() => this;

    public ImageWorkflowState BeginOperation(long operationId)
    {
        if (operationId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(operationId));
        }

        return this with { ActiveOperationId = operationId };
    }

    public bool CanAcceptResult(long operationId, PreviewIdentity identity) =>
        operationId != 0
        && operationId == ActiveOperationId
        && identity == CurrentIdentity
        && CanPreview;

    public bool CanAcceptResult(long operationId, long mediaVersion, long settingsVersion) =>
        CurrentIdentity is PreviewIdentity identity
        && identity.MediaVersion == mediaVersion
        && identity.SettingsVersion == settingsVersion
        && CanAcceptResult(operationId, identity);

    public ImageWorkflowState CompletePreview(long operationId, PreviewIdentity identity) =>
        CanAcceptResult(operationId, identity)
            ? this with
            {
                SymbolizedPreviewIdentity = identity,
                HasSymbolizedPreview = true
            }
            : this;

    public ImageWorkflowState CompletePreview(long operationId, long mediaVersion, long settingsVersion) =>
        CurrentIdentity is PreviewIdentity identity
        && identity.MediaVersion == mediaVersion
        && identity.SettingsVersion == settingsVersion
            ? CompletePreview(operationId, identity)
            : this;

    public ImageWorkflowState EndOperation(long operationId) => operationId == ActiveOperationId
        ? this with { ActiveOperationId = 0 }
        : this;

    public ImageWorkflowState CancelActiveOperation() => this with { ActiveOperationId = 0 };

    public ImageWorkflowState ClearPreview() => this with
    {
        SymbolizedPreviewIdentity = null,
        HasSymbolizedPreview = false
    };
}
