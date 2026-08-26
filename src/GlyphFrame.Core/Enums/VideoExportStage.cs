namespace GlyphFrame.Core.Enums;

public enum VideoExportStage
{
    Preparing,
    ProcessingFrames,
    FinishingEncoding,
    InspectingAudio,
    MergingAudio,
    Finalizing,
    Cancelling,
    Completed
}

public enum VideoEncodingProgressStage
{
    FrameAccepted,
    Finalizing
}
