namespace GlyphFrame.Core.Models;

public sealed record SettingOption<T>(T Value, string DisplayName)
    where T : notnull
{
    public override string ToString() => DisplayName;
}
