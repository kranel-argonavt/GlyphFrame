using GlyphFrame.Core.Models;

namespace GlyphFrame.Core.Interfaces;

public interface IProcessRunner
{
    Task<ProcessExecutionResult> RunAsync(
        ProcessExecutionRequest request,
        CancellationToken cancellationToken = default);
}
