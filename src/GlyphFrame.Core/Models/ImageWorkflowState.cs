using GlyphFrame.Core.Enums;

namespace GlyphFrame.Core.Models;

/// <summary>
/// Tracks platform-neutral image preview validity and operation coordination.
/// Media paths, image bytes, and UI-framework objects are intentionally excluded.
/// </summary>
public sealed record ImageWorkflowState
{
    private ImageWorkflowState(
        MediaSelectionKind mediaType,
        long mediaVersion,
        long settingsVersion,
        long? previewSettingsVersion,
        long activeOperationId,
        bool hasOriginal,
        bool hasSymbolizedPreview)
    {
        MediaType = mediaType;
        MediaVersion = mediaVersion;
        SettingsVersion = settingsVersion;
        PreviewSettingsVersion = previewSettingsVersion;
        ActiveOperationId = activeOperationId;
        HasOriginal = hasOriginal;
        HasSymbolizedPreview = hasSymbolizedPreview;
    }

    public static ImageWorkflowState Empty { get; } = new(
        MediaSelectionKind.None,
        0,
        0,
        null,
        0,
        false,
        false);

    public MediaSelectionKind MediaType { get; private init; }

    public long MediaVersion { get; private init; }

    public long SettingsVersion { get; private init; }

    public long? PreviewSettingsVersion { get; private init; }

    public long ActiveOperationId { get; private init; }

    public bool HasOriginal { get; private init; }

    public bool HasSymbolizedPreview { get; private init; }

    public bool CanPreview => MediaType == MediaSelectionKind.Image && HasOriginal;

    public bool CanExport => CanPreview;

    public bool IsPreviewStale => HasSymbolizedPreview && PreviewSettingsVersion != SettingsVersion;

    public bool RequiresRenderForExport => !HasSymbolizedPreview || IsPreviewStale;

    public ImageWorkflowState SelectMedia(MediaSelectionKind mediaType, long mediaVersion, bool hasOriginal)
    {
        if (mediaVersion <= MediaVersion)
        {
            throw new ArgumentOutOfRangeException(nameof(mediaVersion), "Media versions must increase.");
        }

        bool validImage = mediaType == MediaSelectionKind.Image && hasOriginal;
        return new(
            mediaType,
            mediaVersion,
            SettingsVersion,
            null,
            0,
            validImage,
            false);
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

    public bool CanAcceptResult(long operationId, long mediaVersion, long settingsVersion) =>
        operationId != 0
        && operationId == ActiveOperationId
        && mediaVersion == MediaVersion
        && settingsVersion == SettingsVersion
        && CanPreview;

    public ImageWorkflowState CompletePreview(long operationId, long mediaVersion, long settingsVersion) =>
        CanAcceptResult(operationId, mediaVersion, settingsVersion)
            ? this with
            {
                PreviewSettingsVersion = settingsVersion,
                HasSymbolizedPreview = true
            }
            : this;

    public ImageWorkflowState EndOperation(long operationId) => operationId == ActiveOperationId
        ? this with { ActiveOperationId = 0 }
        : this;

    public ImageWorkflowState CancelActiveOperation() => this with { ActiveOperationId = 0 };

    public ImageWorkflowState ClearPreview() => this with
    {
        PreviewSettingsVersion = null,
        HasSymbolizedPreview = false
    };
}
