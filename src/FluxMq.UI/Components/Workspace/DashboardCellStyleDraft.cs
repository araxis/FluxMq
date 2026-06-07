using FluxMq.UI.Models;
using System.Globalization;

namespace FluxMq.UI.Components.Workspace;

public sealed class DashboardCellStyleDraft
{
    public const string WidgetFitKey = "widgetFit";
    public const string WidgetAlignmentKey = "widgetAlignment";
    public const string WidgetFitFill = "fill";
    public const string WidgetFitContent = "content";
    public const string WidgetAlignmentTopLeft = "top-left";
    public const string WidgetAlignmentTopCenter = "top-center";
    public const string WidgetAlignmentTopRight = "top-right";
    public const string WidgetAlignmentMiddleLeft = "middle-left";
    public const string WidgetAlignmentMiddleCenter = "middle-center";
    public const string WidgetAlignmentMiddleRight = "middle-right";
    public const string WidgetAlignmentBottomLeft = "bottom-left";
    public const string WidgetAlignmentBottomCenter = "bottom-center";
    public const string WidgetAlignmentBottomRight = "bottom-right";

    private readonly Dictionary<string, string> _values = new(StringComparer.Ordinal);

    public static IReadOnlyList<DashboardStyleField> Fields { get; } =
    [
        new("background", "Background", "#101720", Editor: DashboardWidgetPropertyEditorKind.Color),
        new("accent", "Accent", "#2ed3c6", Editor: DashboardWidgetPropertyEditorKind.Color),
        new("borderMode", "Border", "visible", Editor: DashboardWidgetPropertyEditorKind.Select, Options: [
            new("visible", "On"),
            new("none", "Off")
        ]),
        new("borderColor", "Border color", "#223042", Editor: DashboardWidgetPropertyEditorKind.Color),
        new("borderWidth", "Border width", "1", "px", DashboardWidgetPropertyEditorKind.Number),
        new("radius", "Radius", "8", "px", DashboardWidgetPropertyEditorKind.Number),
        new("padding", "Padding", "16", "px", DashboardWidgetPropertyEditorKind.Number),
        new(WidgetFitKey, "Widget fit", WidgetFitFill, Editor: DashboardWidgetPropertyEditorKind.Select, Options: [
            new(WidgetFitFill, "Fill"),
            new(WidgetFitContent, "Content")
        ]),
        new(WidgetAlignmentKey, "Widget align", WidgetAlignmentMiddleCenter, Editor: DashboardWidgetPropertyEditorKind.Segmented)
    ];

    private DashboardCellStyleDraft(IReadOnlyDictionary<string, string> style)
    {
        foreach (var field in Fields)
        {
            _values[field.Key] = ReadStyleValue(style, field) ?? field.DefaultValue;
        }
    }

    public static DashboardCellStyleDraft Create(IReadOnlyDictionary<string, string>? style)
        => new(style ?? new Dictionary<string, string>(StringComparer.Ordinal));

    public string GetValue(string key)
        => _values.TryGetValue(key, out var value) ? value : string.Empty;

    public void SetValue(string key, string? value)
    {
        if (!Fields.Any(field => string.Equals(field.Key, key, StringComparison.Ordinal)))
        {
            return;
        }

        _values[key] = Normalize(value);
    }

    public IReadOnlyDictionary<string, string> BuildStyle()
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var field in Fields)
        {
            var value = Normalize(GetValue(field.Key));
            if (!string.IsNullOrWhiteSpace(value) &&
                !string.Equals(value, field.DefaultValue, StringComparison.OrdinalIgnoreCase))
            {
                result[field.Key] = value;
            }
        }

        return result;
    }

    public static string CssVariables(IReadOnlyDictionary<string, string>? style)
    {
        if (style is null || style.Count == 0)
        {
            return string.Empty;
        }

        var parts = new List<string>();
        AddCssVariable(parts, "--dashboard-widget-bg", ReadStyleValue(style, "background"), IsSafeCssToken);
        AddCssVariable(parts, "--dashboard-widget-accent", ReadStyleValue(style, "accent"), IsSafeCssToken);
        AddCssVariable(parts, "--dashboard-widget-border", ReadStyleValue(style, "borderColor"), IsSafeCssToken);
        AddCssVariable(parts, "--dashboard-widget-border-width", BorderWidthValue(style), static value => value.EndsWith("px", StringComparison.Ordinal));
        AddCssVariable(parts, "--dashboard-widget-radius", PixelValue(ReadStyleValue(style, "radius")), static value => value.EndsWith("px", StringComparison.Ordinal));
        AddCssVariable(parts, "--dashboard-cell-padding", PixelValue(ReadStyleValue(style, "padding")), static value => value.EndsWith("px", StringComparison.Ordinal));
        if (string.Equals(NormalizeWidgetFit(ReadStyleValue(style, WidgetFitKey)), WidgetFitContent, StringComparison.Ordinal))
        {
            var alignment = NormalizeWidgetAlignment(ReadStyleValue(style, WidgetAlignmentKey));
            AddCssVariable(parts, "--dashboard-cell-widget-flex", "0 1 auto", static _ => true);
            AddCssVariable(parts, "--dashboard-cell-widget-width", "auto", static _ => true);
            AddCssVariable(parts, "--dashboard-cell-widget-height", "auto", static _ => true);
            AddCssVariable(parts, "--dashboard-cell-widget-max-width", "100%", static _ => true);
            AddCssVariable(parts, "--dashboard-cell-widget-max-height", "100%", static _ => true);
            AddCssVariable(parts, "--dashboard-cell-widget-justify", HorizontalAlignmentCss(alignment), static _ => true);
            AddCssVariable(parts, "--dashboard-cell-widget-align", VerticalAlignmentCss(alignment), static _ => true);
        }

        return string.Join(string.Empty, parts);
    }

    private static string? ReadStyleValue(IReadOnlyDictionary<string, string> style, DashboardStyleField field)
        => ReadStyleValue(style, field.Key) ?? LegacyStyleFallback(style, field);

    private static string? ReadStyleValue(IReadOnlyDictionary<string, string> style, string key)
        => style.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : null;

    private static string? LegacyStyleFallback(IReadOnlyDictionary<string, string> style, DashboardStyleField field)
        => field.Key switch
        {
            "borderColor" => ReadStyleValue(style, "border"),
            _ => null
        };

    private static string? BorderWidthValue(IReadOnlyDictionary<string, string> style)
    {
        if (string.Equals(ReadStyleValue(style, "borderMode"), "none", StringComparison.OrdinalIgnoreCase))
        {
            return "0px";
        }

        return PixelValue(ReadStyleValue(style, "borderWidth"));
    }

    private static string NormalizeWidgetFit(string? value)
        => string.Equals(value, WidgetFitContent, StringComparison.OrdinalIgnoreCase)
            ? WidgetFitContent
            : WidgetFitFill;

    private static string NormalizeWidgetAlignment(string? value)
    {
        var normalized = Normalize(value).ToLowerInvariant();
        return normalized switch
        {
            WidgetAlignmentTopLeft or
            WidgetAlignmentTopCenter or
            WidgetAlignmentTopRight or
            WidgetAlignmentMiddleLeft or
            WidgetAlignmentMiddleCenter or
            WidgetAlignmentMiddleRight or
            WidgetAlignmentBottomLeft or
            WidgetAlignmentBottomCenter or
            WidgetAlignmentBottomRight => normalized,
            _ => WidgetAlignmentMiddleCenter
        };
    }

    private static string HorizontalAlignmentCss(string alignment)
        => alignment.EndsWith("-right", StringComparison.Ordinal)
            ? "flex-end"
            : alignment.EndsWith("-center", StringComparison.Ordinal)
                ? "center"
                : "flex-start";

    private static string VerticalAlignmentCss(string alignment)
        => alignment.StartsWith("bottom-", StringComparison.Ordinal)
            ? "flex-end"
            : alignment.StartsWith("middle-", StringComparison.Ordinal)
                ? "center"
                : "flex-start";

    private static void AddCssVariable(List<string> parts, string name, string? value, Func<string, bool> isValid)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            var normalized = value.Trim();
            if (isValid(normalized))
            {
                parts.Add($"{name}:{normalized};");
            }
        }
    }

    private static string? PixelValue(string? value)
    {
        if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
        {
            return null;
        }

        return $"{Math.Clamp(number, 0, 64).ToString("0.###", CultureInfo.InvariantCulture)}px";
    }

    private static bool IsSafeCssToken(string value)
        => value.Length <= 64 &&
           !value.Contains(';', StringComparison.Ordinal) &&
           !value.Contains("url", StringComparison.OrdinalIgnoreCase) &&
           !value.Contains("expression", StringComparison.OrdinalIgnoreCase);

    private static string Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
}

public sealed record DashboardStyleField(
    string Key,
    string Label,
    string DefaultValue,
    string? Unit = null,
    DashboardWidgetPropertyEditorKind Editor = DashboardWidgetPropertyEditorKind.Text,
    IReadOnlyList<DashboardWidgetPropertyOption>? Options = null)
{
    public IReadOnlyList<DashboardWidgetPropertyOption> Options { get; init; } = Options ?? [];
}
