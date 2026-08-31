namespace GlyphFrame.Core.Enums;


/// Describes the durable Render -&gt; Review -&gt; Export workflow state.
/// Video-frame preview states remain distinct from a future full-video render artifact.

public enum RenderWorkflowStatus
{
    Empty,
    MediaReady,
    PreviewingFrame,
    FramePreviewReady,
    Rendering,
    RenderReady,
    RenderStale,
    Exporting,
    Exported,
    Cancelling,
    Cancelled,
    Failed
}
