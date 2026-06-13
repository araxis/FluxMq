using System.Globalization;

namespace FluxMq.App.Metrics;

/// <summary>
/// Formatting helpers for numeric metric values. Each metric declares its own format token (see
/// <see cref="MetricDescriptor.Format"/>); dashboards and previews use these helpers to render a reading.
/// </summary>
public static class MetricFormats
{
    public const string Number = "number";
    public const string Bytes = "bytes";
    public const string Percent = "percent";

    public static string Format(double value, string? format)
        => (string.IsNullOrWhiteSpace(format) ? Number : format.Trim()) switch
        {
            Bytes => FormatBytes(value),
            Percent => $"{value:0.#}%",
            _ => value.ToString(value >= 100 ? "0" : "0.##", CultureInfo.InvariantCulture)
        };

    private static string FormatBytes(double value)
    {
        string[] units = ["B", "KB", "MB", "GB"];
        var unitIndex = 0;
        while (value >= 1024 && unitIndex < units.Length - 1)
        {
            value /= 1024;
            unitIndex++;
        }

        return $"{value:0.#} {units[unitIndex]}";
    }
}
