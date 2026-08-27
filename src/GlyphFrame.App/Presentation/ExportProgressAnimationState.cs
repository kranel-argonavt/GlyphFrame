using GlyphFrame.Core.Enums;

namespace GlyphFrame.App.Presentation;

public sealed class ExportProgressAnimationState
{
    private const double LastActivePercentage = 99.9d;
    private long _generation = long.MinValue;

    public double TargetValue { get; private set; }

    public bool IsIndeterminate { get; private set; } = true;

    public void Update(
        long generation,
        double sourceValue,
        bool isIndeterminate,
        VideoExportStage stage)
    {
        if (generation != _generation)
        {
            _generation = generation;
            TargetValue = 0d;
        }

        double finiteValue = double.IsFinite(sourceValue) ? sourceValue : 0d;
        double clampedValue = Math.Clamp(finiteValue, 0d, 100d);
        if (stage != VideoExportStage.Completed)
        {
            clampedValue = Math.Min(clampedValue, LastActivePercentage);
        }

        TargetValue = Math.Max(TargetValue, clampedValue);
        IsIndeterminate = isIndeterminate;
    }
}
