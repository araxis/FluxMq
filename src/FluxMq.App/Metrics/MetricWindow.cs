using System.Globalization;

namespace FluxMq.App.Metrics;

/// <summary>
/// Parses and normalizes the rolling-window strings used by metric types (for example "30s", "1m", "2h").
/// A window must be a positive duration between one second and twenty-four hours.
/// </summary>
public static class MetricWindow
{
    public const string Default = "60s";

    public static bool TryNormalize(string? value, out string normalized)
    {
        normalized = string.Empty;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var candidate = value.Trim().ToLowerInvariant();
        var suffix = candidate[^1];
        if (suffix is not ('s' or 'm' or 'h'))
        {
            return false;
        }

        var numberText = candidate[..^1];
        if (!double.TryParse(numberText, NumberStyles.Float, CultureInfo.InvariantCulture, out var amount) ||
            amount <= 0)
        {
            return false;
        }

        var seconds = suffix switch
        {
            'h' => amount * 3600d,
            'm' => amount * 60d,
            _ => amount
        };
        if (seconds is < 1d or > 86_400d)
        {
            return false;
        }

        var amountText = amount % 1d == 0d
            ? amount.ToString("0", CultureInfo.InvariantCulture)
            : amount.ToString("0.##", CultureInfo.InvariantCulture);
        normalized = $"{amountText}{suffix}";
        return true;
    }

    public static TimeSpan Parse(string? value)
    {
        if (!TryNormalize(value, out var normalized))
        {
            normalized = Default;
        }

        var amount = double.Parse(normalized[..^1], CultureInfo.InvariantCulture);
        return normalized[^1] switch
        {
            'h' => TimeSpan.FromHours(amount),
            'm' => TimeSpan.FromMinutes(amount),
            _ => TimeSpan.FromSeconds(amount)
        };
    }
}
