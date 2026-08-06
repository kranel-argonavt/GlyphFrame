using GlyphFrame.Core.Enums;

namespace GlyphFrame.Core.Models;

public sealed record CharacterPresetOption(
    CharacterPresetKind Kind,
    string Name,
    string Characters,
    bool IsUnicode = false)
{
    public static IReadOnlyList<CharacterPresetOption> BuiltIn { get; } = Array.AsReadOnly(
    new CharacterPresetOption[]
    {
        new(CharacterPresetKind.SimpleAscii, "Simple ASCII", " .:-=+*#%@"),
        new(
            CharacterPresetKind.DetailedAscii,
            "Detailed ASCII",
            " .'`^\",:;Il!i~+_-?][}{1)(|\\/tfjrxnuvczXYUJCLQ0OZmwqpdbkhao*#MW&8%B@$"),
        new(CharacterPresetKind.Blocks, "Blocks", " ░▒▓█", true)
    });

    public static CharacterPresetOption Default => BuiltIn[1];

    public override string ToString() => Name;
}
