using FluxMq.UI.Models;
using System.Text.Json.Nodes;

namespace FluxMq.UI.Services;

public static class FlowDashboardDefinitionFactory
{
    public static JsonObject CreateDashboard()
        => new()
        {
            ["layout"] = new JsonObject
            {
                ["columns"] = new JsonArray("320", "*"),
                ["rows"] = new JsonArray("180", "*"),
                ["columnPadding"] = new JsonArray(0, 0),
                ["rowPadding"] = new JsonArray(0, 0),
                ["cells"] = new JsonObject()
            },
            ["widgets"] = new JsonObject()
        };

    public static JsonObject CreateCell(DashboardCellSnapshot cell)
    {
        var result = new JsonObject
        {
            ["row"] = cell.Row,
            ["column"] = cell.Column,
            ["rowSpan"] = cell.RowSpan,
            ["columnSpan"] = cell.ColumnSpan
        };

        if (!string.IsNullOrWhiteSpace(cell.Widget))
        {
            result["widget"] = cell.Widget;
        }

        if (cell.Style.Count > 0)
        {
            result["style"] = CreateConfiguration(cell.Style);
        }

        return result;
    }

    public static JsonObject CreateWidget(string widgetType)
        => new()
        {
            ["type"] = DashboardWidgetCatalog.NormalizeWidgetTypeForAdd(widgetType),
            ["configuration"] = CreateWidgetConfiguration(widgetType)
        };

    public static JsonObject CreateWidgetConfiguration(string widgetType)
    {
        var normalizedType = DashboardWidgetCatalog.NormalizeWidgetTypeForAdd(widgetType);
        var module = DashboardWidgetModuleCatalog.Find(normalizedType);
        return module is null
            ? new JsonObject()
            : CreateConfiguration(module.DefaultConfiguration);
    }

    public static JsonObject CreateConfiguration(IReadOnlyDictionary<string, string> configuration)
    {
        var result = new JsonObject();
        foreach (var (key, value) in configuration.OrderBy(static pair => pair.Key, StringComparer.Ordinal))
        {
            if (!string.IsNullOrWhiteSpace(key))
            {
                result[key] = value ?? string.Empty;
            }
        }

        return result;
    }

}
