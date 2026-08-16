using GlyphFrame.Core.Models;

namespace GlyphFrame.Core.Interfaces;

/// <summary>
/// Runs a local process whose standard output is binary data.
/// </summary>
public interface IBinaryProcessRunner
{
    Task<BinaryProcessExecutionResult> RunAsync(
        BinaryProcessExecutionRequest request,
        CancellationToken cancellationToken = default);
}
