namespace GlyphFrame.App.Services;

public interface IVideoPlaybackCoordinator
{
    void PauseAll();

    void PlayOriginal();

    void RestartOriginal();

    void PlayRendered();

    void RestartRendered();
}

/// <summary>Owns only the single-audio transport rule. Each player retains its
/// independent source, position, volume, mute, timer, and native lifetime.</summary>
public sealed class VideoPlaybackCoordinator(
    IOriginalVideoPlayer original,
    IRenderedVideoPlayer rendered) : IVideoPlaybackCoordinator
{
    private readonly IOriginalVideoPlayer _original = original ?? throw new ArgumentNullException(nameof(original));
    private readonly IRenderedVideoPlayer _rendered = rendered ?? throw new ArgumentNullException(nameof(rendered));

    public void PauseAll()
    {
        _original.Pause();
        _rendered.Pause();
    }

    public void PlayOriginal()
    {
        _rendered.Pause();
        _original.Play();
    }

    public void RestartOriginal()
    {
        _rendered.Pause();
        _original.Restart();
    }

    public void PlayRendered()
    {
        _original.Pause();
        _rendered.Play();
    }

    public void RestartRendered()
    {
        _original.Pause();
        _rendered.Restart();
    }
}
