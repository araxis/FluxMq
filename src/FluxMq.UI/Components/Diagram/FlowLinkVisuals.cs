using System.Text;

namespace FluxMq.UI.Components.Diagram;

public static class FlowLinkVisuals
{
    public const string DefaultColor = "#2DD4BF";
    public const string ErrorColor = "#F87171";
    public const string ConditionalColor = "#FBBF24";
    public const string SelectedColor = "#A78BFA";
    public const double DefaultWidth = 2d;
    public const double ConditionalWidth = 3d;
    public const double SelectedWidth = 4d;

    private const int MaxConditionLabelLength = 72;

    public static string ColorFor(bool hasCondition, bool isError)
        => hasCondition ? ConditionalColor : isError ? ErrorColor : DefaultColor;

    public static double WidthFor(bool hasCondition)
        => hasCondition ? ConditionalWidth : DefaultWidth;

    public static string? ConditionLabel(string? condition)
    {
        var compact = Compact(condition);
        if (compact.Length == 0)
        {
            return null;
        }

        var text = compact.Length <= MaxConditionLabelLength
            ? compact
            : $"{compact[..(MaxConditionLabelLength - 3)]}...";
        return $"when: {text}";
    }

    private static string Compact(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(value.Length);
        var previousWasWhitespace = false;
        foreach (var character in value.Trim())
        {
            if (char.IsWhiteSpace(character))
            {
                if (!previousWasWhitespace)
                {
                    builder.Append(' ');
                    previousWasWhitespace = true;
                }

                continue;
            }

            builder.Append(character);
            previousWasWhitespace = false;
        }

        return builder.ToString();
    }
}
