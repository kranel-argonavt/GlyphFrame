using System.IO;
using System.Security;
using System.Text.Json;

namespace GlyphFrame.App.Services;

internal static class NativeTermsStartupGate
{
    internal static bool EnsureAcknowledged(string installationDirectory, string dataDirectory,
        Func<NativeTermsBundle?, string?, Action, bool> show)
    {
        NativeTermsBundle? bundle;
        try { bundle = NativeTermsBundle.Load(installationDirectory); }
        catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException or SecurityException
            or JsonException or InvalidOperationException or KeyNotFoundException or ArgumentException or FormatException)
        {
            show(null,
                "The Microsoft component scope or complete terms could not be verified. Restore or extract the complete matching package. No media work has started.",
                () => throw new InvalidOperationException("Unverified terms cannot be acknowledged."));
            return false;
        }
        if (bundle is null) { return true; }
        NativeTermsAcknowledgementStore store = new(dataDirectory);
        if (store.IsCurrent(bundle.Identity)) { return true; }
        return show(bundle, null, () => store.Save(bundle.Identity)) && store.IsCurrent(bundle.Identity);
    }
}
