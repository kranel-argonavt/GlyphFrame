using GlyphFrame.Infrastructure.Ffmpeg;

namespace GlyphFrame.Tests.TestInfrastructure;

internal sealed class FakeStreamingProcessFactory : IStreamingProcessFactory
{
    public FakeStreamingProcessSession Session { get; init; } = new();

    public Exception? StartException { get; init; }

    public int StartCalls { get; private set; }

    public string? ExecutablePath { get; private set; }

    public IReadOnlyList<string>? Arguments { get; private set; }

    public bool RedirectStandardInput { get; private set; }

    public bool RedirectStandardOutput { get; private set; }

    public IStreamingProcessSession Start(
        string executablePath,
        IReadOnlyList<string> arguments,
        bool redirectStandardInput,
        bool redirectStandardOutput)
    {
        StartCalls++;
        ExecutablePath = executablePath;
        Arguments = arguments.ToArray();
        RedirectStandardInput = redirectStandardInput;
        RedirectStandardOutput = redirectStandardOutput;
        if (StartException is not null)
        {
            throw StartException;
        }

        return Session;
    }
}

internal sealed class FakeStreamingProcessSession : IStreamingProcessSession
{
    public Stream StandardInput { get; init; } = new TrackingWriteStream();

    public Stream StandardOutput { get; init; } = new ChunkedReadStream([]);

    public TextReader StandardError { get; init; } = new TrackingStringReader(string.Empty);

    public bool HasExited { get; set; }

    public int ExitCode { get; set; }

    public bool ExitOnKill { get; init; } = true;

    public Func<CancellationToken, Task>? WaitForExitHandler { get; init; }

    public int CompleteStandardInputCalls { get; private set; }

    public int WaitForExitCalls { get; private set; }

    public int KillCalls { get; private set; }

    public int DisposeCalls { get; private set; }

    public void CompleteStandardInput() => CompleteStandardInputCalls++;

    public async Task WaitForExitAsync(CancellationToken cancellationToken)
    {
        WaitForExitCalls++;
        if (WaitForExitHandler is not null)
        {
            await WaitForExitHandler(cancellationToken);
        }

        HasExited = true;
    }

    public void KillEntireProcessTree()
    {
        KillCalls++;
        if (ExitOnKill)
        {
            HasExited = true;
        }
    }

    public ValueTask DisposeAsync()
    {
        DisposeCalls++;
        StandardInput.Dispose();
        StandardOutput.Dispose();
        StandardError.Dispose();
        return ValueTask.CompletedTask;
    }
}

internal sealed class TrackingWriteStream : Stream
{
    private readonly List<byte> _bytes = [];

    public Func<ReadOnlyMemory<byte>, CancellationToken, Task>? WriteHandler { get; init; }

    public IReadOnlyList<byte> Bytes => _bytes;

    public int WriteCalls { get; private set; }

    public int FlushCalls { get; private set; }

    public int DisposeCalls { get; private set; }

    public override bool CanRead => false;

    public override bool CanSeek => false;

    public override bool CanWrite => true;

    public override long Length => _bytes.Count;

    public override long Position
    {
        get => _bytes.Count;
        set => throw new NotSupportedException();
    }

    public override void Flush() => FlushCalls++;

    public override Task FlushAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        FlushCalls++;
        return Task.CompletedTask;
    }

    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) =>
        Write(buffer.AsSpan(offset, count));

    public override void Write(ReadOnlySpan<byte> buffer)
    {
        WriteCalls++;
        _bytes.AddRange(buffer.ToArray());
    }

    public override async ValueTask WriteAsync(
        ReadOnlyMemory<byte> buffer,
        CancellationToken cancellationToken = default)
    {
        WriteCalls++;
        if (WriteHandler is not null)
        {
            await WriteHandler(buffer, cancellationToken);
        }

        _bytes.AddRange(buffer.ToArray());
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            DisposeCalls++;
        }

        base.Dispose(disposing);
    }
}

internal sealed class ChunkedReadStream(byte[] bytes, int maximumChunkSize = int.MaxValue) : Stream
{
    private int _offset;

    public int DisposeCalls { get; private set; }

    public override bool CanRead => true;

    public override bool CanSeek => false;

    public override bool CanWrite => false;

    public override long Length => bytes.Length;

    public override long Position
    {
        get => _offset;
        set => throw new NotSupportedException();
    }

    public override void Flush()
    {
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        int read = Math.Min(Math.Min(count, maximumChunkSize), bytes.Length - _offset);
        if (read <= 0)
        {
            return 0;
        }

        bytes.AsSpan(_offset, read).CopyTo(buffer.AsSpan(offset, read));
        _offset += read;
        return read;
    }

    public override ValueTask<int> ReadAsync(
        Memory<byte> buffer,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        int read = Math.Min(Math.Min(buffer.Length, maximumChunkSize), bytes.Length - _offset);
        if (read <= 0)
        {
            return ValueTask.FromResult(0);
        }

        bytes.AsMemory(_offset, read).CopyTo(buffer);
        _offset += read;
        return ValueTask.FromResult(read);
    }

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            DisposeCalls++;
        }

        base.Dispose(disposing);
    }
}

internal sealed class BlockingReadStream : Stream
{
    public int DisposeCalls { get; private set; }

    public override bool CanRead => true;

    public override bool CanSeek => false;

    public override bool CanWrite => false;

    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override void Flush()
    {
    }

    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    public override async ValueTask<int> ReadAsync(
        Memory<byte> buffer,
        CancellationToken cancellationToken = default)
    {
        TaskCompletionSource<int> completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        return await completion.Task.WaitAsync(cancellationToken);
    }

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            DisposeCalls++;
        }

        base.Dispose(disposing);
    }
}

internal sealed class TrackingStringReader(string value) : StringReader(value)
{
    public int DisposeCalls { get; private set; }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            DisposeCalls++;
        }

        base.Dispose(disposing);
    }
}
