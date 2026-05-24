using System.Text.Json;

namespace FluxMq.Pipeline.Definitions;

public sealed record DashboardDefinition
{
    public DashboardLayoutDefinition Layout { get; init; } = new();
    public Dictionary<string, DashboardWidgetDefinition> Widgets { get; init; } = [];
}

public sealed record DashboardLayoutDefinition
{
    public List<DashboardGridTrackDefinition> Columns { get; init; } = [DashboardGridTrackDefinition.Star()];
    public List<DashboardGridTrackDefinition> Rows { get; init; } = [DashboardGridTrackDefinition.Star()];
    public Dictionary<string, DashboardCellDefinition> Cells { get; init; } = [];
}

public sealed record DashboardGridTrackDefinition
{
    public DashboardGridTrackUnit Unit { get; init; } = DashboardGridTrackUnit.Star;
    public double Value { get; init; } = 1;

    public static DashboardGridTrackDefinition Fixed(double value)
        => new()
        {
            Unit = DashboardGridTrackUnit.Fixed,
            Value = value
        };

    public static DashboardGridTrackDefinition Percent(double value)
        => new()
        {
            Unit = DashboardGridTrackUnit.Percent,
            Value = value
        };

    public static DashboardGridTrackDefinition Star(double value = 1)
        => new()
        {
            Unit = DashboardGridTrackUnit.Star,
            Value = value
        };

    public static DashboardGridTrackDefinition Parse(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new FormatException("Dashboard grid track size cannot be empty.");
        }

        var trimmed = value.Trim();
        if (trimmed.EndsWith('*'))
        {
            var coefficient = trimmed[..^1].Trim();
            return Star(ParsePositiveDouble(string.IsNullOrWhiteSpace(coefficient) ? "1" : coefficient, "star track"));
        }

        if (trimmed.EndsWith('%'))
        {
            return Percent(ParsePositiveDouble(trimmed[..^1], "percent track"));
        }

        if (trimmed.EndsWith("px", StringComparison.OrdinalIgnoreCase))
        {
            return Fixed(ParsePositiveDouble(trimmed[..^2], "fixed track"));
        }

        return Fixed(ParsePositiveDouble(trimmed, "fixed track"));
    }

    public string ToSizeString()
        => Unit switch
        {
            DashboardGridTrackUnit.Fixed => Value.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture),
            DashboardGridTrackUnit.Percent => $"{Value.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)}%",
            DashboardGridTrackUnit.Star => Value == 1 ? "*" : $"{Value.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)}*",
            _ => throw new InvalidOperationException($"Unsupported dashboard grid track unit '{Unit}'.")
        };

    private static double ParsePositiveDouble(string value, string label)
    {
        if (!double.TryParse(
                value,
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture,
                out var parsed) ||
            parsed <= 0 ||
            double.IsNaN(parsed) ||
            double.IsInfinity(parsed))
        {
            throw new FormatException($"Dashboard grid {label} size must be a positive finite number.");
        }

        return parsed;
    }
}

public enum DashboardGridTrackUnit
{
    Fixed,
    Percent,
    Star
}

public sealed record DashboardCellDefinition
{
    public int Row { get; init; }
    public int Column { get; init; }
    public int RowSpan { get; init; } = 1;
    public int ColumnSpan { get; init; } = 1;
    public string? Widget { get; init; }
}

public sealed record DashboardWidgetDefinition
{
    public string Type { get; init; } = string.Empty;
    public Dictionary<string, JsonElement> Configuration { get; init; } = [];
}
