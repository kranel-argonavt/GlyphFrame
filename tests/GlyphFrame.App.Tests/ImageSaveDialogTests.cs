using System.IO;
using System.Runtime.ExceptionServices;
using GlyphFrame.App.Services;
using GlyphFrame.Core.Enums;

namespace GlyphFrame.App.Tests;

public sealed class ImageSaveDialogTests
{
    [Theory]
    [InlineData(ImageExportFormat.Png, 1, ".png", 2, ImageExportFormat.Jpeg)]
    [InlineData(ImageExportFormat.Jpeg, 2, ".jpg", 1, ImageExportFormat.Png)]
    public void Dialog_OffersBothExportFormatsAndReturnsAcceptedFilterAfterSwitch(
        ImageExportFormat initial, int initialIndex, string extension, int finalIndex, ImageExportFormat accepted)
    {
        ExceptionDispatchInfo? failure = null;
        Thread thread = new(() =>
        {
            try
            {
                var dialog = ImageSaveDialogService.CreateImageDialog(@"C:\media\source.png", initial);
                Assert.Equal("PNG (*.png)|*.png|JPEG (*.jpg;*.jpeg)|*.jpg;*.jpeg", dialog.Filter);
                Assert.Equal(initialIndex, dialog.FilterIndex);
                Assert.Equal("source_symbolized" + extension, dialog.FileName);
                Assert.False(dialog.AddExtension); Assert.False(dialog.OverwritePrompt);
                Assert.True(dialog.CheckPathExists); Assert.True(dialog.ValidateNames);
                // The filename retains its initial extension while the filter
                // changes. Caller normalization must use the accepted filter.
                dialog.FileName = Path.Combine(Path.GetTempPath(), "source_symbolized" + extension);
                dialog.FilterIndex = finalIndex;
                ImageSaveDialogResult result = ImageSaveDialogService.AcceptedImageSelection(dialog);
                Assert.Equal(accepted, result.Format); Assert.Equal(dialog.FileName, result.FilePath);
            }
            catch (Exception ex) { failure = ExceptionDispatchInfo.Capture(ex); }
        });
        thread.SetApartmentState(ApartmentState.STA); thread.Start(); thread.Join(); failure?.Throw();
    }
}
