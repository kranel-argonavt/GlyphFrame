using GlyphFrame.Core.Enums;

namespace GlyphFrame.Core.Models;

public sealed record UiPresentationState
{
    private UiPresentationState(MediaSelectionKind mediaType, bool isExportActive)
    {
        MediaType = mediaType;
        IsExportActive = isExportActive;
    }

    public static UiPresentationState Empty { get; } = new(MediaSelectionKind.None, false);

    public MediaSelectionKind MediaType { get; }

    public bool IsExportActive { get; }

    public bool IsNoMediaSelected => MediaType == MediaSelectionKind.None;

    public bool IsMediaSelected => !IsNoMediaSelected;

    public bool IsImageSelected => MediaType == MediaSelectionKind.Image;

    public bool IsVideoSelected => MediaType == MediaSelectionKind.Video;

    public bool CanUseMediaActions => IsMediaSelected && !IsExportActive;

    public string InputLabel => MediaType switch
    {
        MediaSelectionKind.Image => "Image File",
        MediaSelectionKind.Video => "Video File",
        _ => "Media File"
    };

    public string PreviewActionLabel => MediaType switch
    {
        MediaSelectionKind.Image => "Preview Symbolized Image",
        MediaSelectionKind.Video => "Preview Symbolized Frame",
        _ => "Preview Symbolized"
    };

    public string ExportActionLabel => MediaType switch
    {
        MediaSelectionKind.Image => "Export Image",
        MediaSelectionKind.Video => "Export Video",
        _ => "Export"
    };

    public static UiPresentationState ForMedia(MediaSelectionKind mediaType)
    {
        return mediaType == MediaSelectionKind.None
            ? Empty
            : new UiPresentationState(mediaType, false);
    }

    public UiPresentationState BeginExport()
    {
        return IsMediaSelected ? new UiPresentationState(MediaType, true) : this;
    }

    public UiPresentationState EndExport()
    {
        return IsExportActive ? new UiPresentationState(MediaType, false) : this;
    }
}
