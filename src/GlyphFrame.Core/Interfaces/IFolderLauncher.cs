namespace GlyphFrame.Core.Interfaces;

public interface IFolderLauncher
{
    Task OpenFolderAsync(string folderPath, CancellationToken cancellationToken = default);
}
