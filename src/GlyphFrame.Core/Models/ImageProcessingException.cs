using GlyphFrame.Core.Enums;

namespace GlyphFrame.Core.Models;

public sealed class ImageProcessingException : Exception
{
    public ImageProcessingException(
        ImageProcessingErrorCategory category,
        string userMessage,
        string? technicalDetail = null,
        Exception? innerException = null)
        : base(technicalDetail ?? userMessage, innerException)
    {
        Category = category;
        UserMessage = userMessage;
        TechnicalDetail = technicalDetail;
    }

    public ImageProcessingErrorCategory Category { get; }

    public string UserMessage { get; }

    public string? TechnicalDetail { get; }
}
