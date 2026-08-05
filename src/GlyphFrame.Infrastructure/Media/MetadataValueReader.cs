using System.Globalization;
using MetadataDirectory = MetadataExtractor.Directory;

namespace GlyphFrame.Infrastructure.Media;

internal static class MetadataValueReader
{
    public static int? FindPositiveInt(
        IEnumerable<MetadataDirectory> directories,
        Func<MetadataDirectory, bool> directoryPredicate,
        params string[] tagNames)
    {
        double? value = FindDouble(directories, directoryPredicate, tagNames);
        return value is > 0 and <= int.MaxValue ? Convert.ToInt32(Math.Round(value.Value)) : null;
    }

    public static long? FindPositiveLong(
        IEnumerable<MetadataDirectory> directories,
        Func<MetadataDirectory, bool> directoryPredicate,
        params string[] tagNames)
    {
        double? value = FindDouble(directories, directoryPredicate, tagNames);
        return value is > 0 and <= long.MaxValue ? Convert.ToInt64(Math.Round(value.Value)) : null;
    }

    public static double? FindDouble(
        IEnumerable<MetadataDirectory> directories,
        Func<MetadataDirectory, bool> directoryPredicate,
        params string[] tagNames)
    {
        foreach (MetadataDirectory directory in directories.Where(directoryPredicate))
        {
            foreach (MetadataExtractor.Tag tag in directory.Tags)
            {
                if (!tagNames.Contains(tag.Name, StringComparer.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (TryConvertDouble(directory.GetObject(tag.Type), out double value))
                {
                    return value;
                }

                if (double.TryParse(
                        tag.Description,
                        NumberStyles.Float,
                        CultureInfo.InvariantCulture,
                        out value))
                {
                    return value;
                }
            }
        }

        return null;
    }

    public static string? FindDescription(
        IEnumerable<MetadataDirectory> directories,
        Func<MetadataDirectory, bool> directoryPredicate,
        params string[] tagNames)
    {
        foreach (MetadataDirectory directory in directories.Where(directoryPredicate))
        {
            MetadataExtractor.Tag? tag = directory.Tags.FirstOrDefault(
                candidate => tagNames.Contains(candidate.Name, StringComparer.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(tag?.Description))
            {
                return tag.Description;
            }
        }

        return null;
    }

    public static TimeSpan? FindDuration(IEnumerable<MetadataDirectory> directories)
    {
        foreach (MetadataDirectory directory in directories)
        {
            MetadataExtractor.Tag? durationTag = directory.Tags.FirstOrDefault(
                tag => tag.Name.Equals("Duration", StringComparison.OrdinalIgnoreCase));
            if (durationTag is null)
            {
                continue;
            }

            object? value = directory.GetObject(durationTag.Type);
            if (value is TimeSpan duration && duration > TimeSpan.Zero)
            {
                return duration;
            }

            if (TimeSpan.TryParse(durationTag.Description, CultureInfo.InvariantCulture, out duration)
                && duration > TimeSpan.Zero)
            {
                return duration;
            }

            double? rawDuration = TryConvertDouble(value, out double numericDuration)
                ? numericDuration
                : null;
            double? timeScale = FindDouble([directory], _ => true, "Time Scale", "Timescale");
            if (rawDuration > 0 && timeScale > 0)
            {
                return TimeSpan.FromSeconds(rawDuration.Value / timeScale.Value);
            }
        }

        return null;
    }

    private static bool TryConvertDouble(object? value, out double result)
    {
        switch (value)
        {
            case byte number:
                result = number;
                return true;
            case sbyte number:
                result = number;
                return true;
            case short number:
                result = number;
                return true;
            case ushort number:
                result = number;
                return true;
            case int number:
                result = number;
                return true;
            case uint number:
                result = number;
                return true;
            case long number:
                result = number;
                return true;
            case ulong number when number <= long.MaxValue:
                result = number;
                return true;
            case float number:
                result = number;
                return true;
            case double number:
                result = number;
                return true;
            case decimal number:
                result = (double)number;
                return true;
            case MetadataExtractor.Rational rational:
                result = rational.ToDouble();
                return true;
            default:
                result = 0;
                return false;
        }
    }
}
