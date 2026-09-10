using System.Windows.Media;

namespace GlyphFrame.App.Services;

public sealed class MediaPlaybackOpenedEventArgs(long generation) : EventArgs
{
    public long Generation { get; } = generation;
}

public sealed class MediaPlaybackEndedEventArgs(long generation) : EventArgs
{
    public long Generation { get; } = generation;
}

public sealed class MediaPlaybackFailedEventArgs(
    long generation,
    string? userMessage = null,
    string? technicalDetail = null) : EventArgs
{
    public long Generation { get; } = generation;

    public string? UserMessage { get; } = userMessage;

    public string? TechnicalDetail { get; } = technicalDetail;
}

public interface IMediaPlaybackBackend : IDisposable
{
    event EventHandler<MediaPlaybackOpenedEventArgs>? MediaOpened;

    event EventHandler<MediaPlaybackEndedEventArgs>? MediaEnded;

    event EventHandler<MediaPlaybackFailedEventArgs>? MediaFailed;

    ImageSource VisualSource { get; }

    TimeSpan Position { get; set; }

    TimeSpan? Duration { get; }

    int NaturalVideoWidth { get; }

    int NaturalVideoHeight { get; }

    double Volume { get; set; }

    bool IsMuted { get; set; }

    void Open(string filePath, long generation);

    void Play();

    void Pause();

    void Stop();

    void Close();
}

public interface IMediaPlaybackPositionTimer : IDisposable
{
    event EventHandler? Tick;

    bool IsRunning { get; }

    void Start();

    void Stop();
}

public interface IMediaPlaybackOpenDeadline
{
    Task WaitAsync(Task operation, TimeSpan timeout, CancellationToken cancellationToken);
}

public sealed class SystemMediaPlaybackOpenDeadline : IMediaPlaybackOpenDeadline
{
    public Task WaitAsync(Task operation, TimeSpan timeout, CancellationToken cancellationToken) =>
        operation.WaitAsync(timeout, cancellationToken);
}
