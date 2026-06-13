using FluxMq.App.Definitions;
using FluxMq.App.Metrics;
using FluxMq.UI.Models;
using System.Text.Json.Nodes;

namespace FluxMq.UI.Services;

public sealed partial class FlowDefinitionComposer
{
    /// <summary>Adds an empty dashboard artifact with the given name if it does not already exist.</summary>
    public string AddDashboard(string json, string name)
    {
        var root = ParseOrCreate(json);
        var flowApplication = GetFlowApplication(root);
        var dashboards = GetOrCreateObject(flowApplication, "dashboards");
        if (!dashboards.ContainsKey(name))
        {
            dashboards[name] = FlowDashboardDefinitionFactory.CreateDashboard();
        }

        return root.ToJsonString(Options);
    }

    /// <summary>Removes a dashboard by name, leaving the definition unchanged if it doesn't exist.</summary>
    public string RemoveDashboard(string json, string name)
    {
        var root = ParseOrCreate(json);
        var flowApplication = GetFlowApplication(root);
        if (flowApplication["dashboards"] is JsonObject dashboards)
        {
            dashboards.Remove(name);
        }

        return root.ToJsonString(Options);
    }

    public DashboardLayoutSnapshot? GetDashboardLayout(string json, string dashboardName)
    {
        if (string.IsNullOrWhiteSpace(dashboardName))
        {
            return null;
        }

        var root = ParseOrCreate(json);
        var flowApplication = GetFlowApplication(root);
        if (flowApplication["dashboards"] is not JsonObject dashboards ||
            dashboards[dashboardName] is not JsonObject dashboard)
        {
            return null;
        }

        var layout = dashboard["layout"] as JsonObject ?? new JsonObject();
        var cells = layout["cells"] as JsonObject ?? new JsonObject();
        var widgets = dashboard["widgets"] as JsonObject ?? new JsonObject();
        var appMetrics = flowApplication["metrics"] as JsonObject ?? new JsonObject();
        var metrics = dashboard["metrics"] as JsonObject ?? new JsonObject();
        var bindings = dashboard["bindings"] as JsonObject ?? new JsonObject();

        var columns = ReadTrackStrings(layout, "columns", ["*"]);
        var rows = ReadTrackStrings(layout, "rows", ["*"]);

        return new DashboardLayoutSnapshot(
            columns,
            rows,
            NormalizePaddingValues(ReadPaddingValues(layout, "columnPadding"), columns.Count),
            NormalizePaddingValues(ReadPaddingValues(layout, "rowPadding"), rows.Count),
            ReadDashboardCells(cells),
            ReadDashboardWidgets(widgets),
            ReadDashboardMetrics(appMetrics, metrics),
            ReadDashboardBindings(bindings));
    }

    public string AddDashboardWidget(string json, string dashboardName, string widgetType, string? cellName = null)
    {
        if (string.IsNullOrWhiteSpace(dashboardName))
        {
            return json;
        }

        var normalizedType = string.IsNullOrWhiteSpace(widgetType)
            ? DashboardWidgetCatalog.EventCounterType
            : DashboardWidgetCatalog.NormalizeWidgetTypeForAdd(widgetType);
        var root = ParseOrCreate(json);
        var dashboard = GetOrCreateDashboardObject(GetFlowApplication(root), dashboardName);
        var layout = GetOrCreateObject(dashboard, "layout");
        var cells = GetOrCreateObject(layout, "cells");
        var widgets = GetOrCreateObject(dashboard, "widgets");

        var widgetName = MakeUniqueDashboardWidgetName(widgets, DashboardWidgetModuleCatalog.InstanceNamePrefixFor(normalizedType));
        widgets[widgetName] = FlowDashboardDefinitionFactory.CreateWidget(normalizedType);
        AssignWidgetToDashboardCell(layout, cells, widgetName, cellName);

        FluxMqApplicationDefinitionMigrator.MigrateRoot(root);
        return root.ToJsonString(Options);
    }

    public string UpdateDashboardWidgetConfiguration(
        string json,
        string dashboardName,
        string widgetName,
        IReadOnlyDictionary<string, string> configuration)
    {
        if (string.IsNullOrWhiteSpace(dashboardName) ||
            string.IsNullOrWhiteSpace(widgetName))
        {
            return json;
        }

        var root = ParseOrCreate(json);
        var flowApplication = GetFlowApplication(root);
        if (flowApplication["dashboards"] is not JsonObject dashboards ||
            dashboards[dashboardName] is not JsonObject dashboard ||
            dashboard["widgets"] is not JsonObject widgets ||
            widgets[widgetName] is not JsonObject widget)
        {
            return json;
        }

        widget["configuration"] = FlowDashboardDefinitionFactory.CreateConfiguration(configuration);
        return root.ToJsonString(Options);
    }

    public string UpdateDashboardCellStyle(
        string json,
        string dashboardName,
        string cellName,
        IReadOnlyDictionary<string, string> style)
    {
        if (string.IsNullOrWhiteSpace(dashboardName) ||
            string.IsNullOrWhiteSpace(cellName))
        {
            return json;
        }

        var root = ParseOrCreate(json);
        var dashboard = GetOrCreateDashboardObject(GetFlowApplication(root), dashboardName);
        var layout = GetOrCreateObject(dashboard, "layout");
        var cells = GetOrCreateObject(layout, "cells");
        if (!TryFindOrCreateDashboardCell(layout, cells, cellName, out _, out var cell))
        {
            return json;
        }

        if (style.Count == 0)
        {
            cell.Remove("style");
        }
        else
        {
            cell["style"] = FlowDashboardDefinitionFactory.CreateConfiguration(style);
        }

        return root.ToJsonString(Options);
    }

    public string UpdateDashboardWidgetBinding(
        string json,
        string dashboardName,
        string widgetName,
        string? primaryMetric,
        IEnumerable<string> metrics)
        => UpdateDashboardWidgetBinding(json, dashboardName, widgetName, primaryMetric, metrics, null);

    public string UpdateDashboardWidgetBinding(
        string json,
        string dashboardName,
        string widgetName,
        string? primaryMetric,
        IEnumerable<string> metrics,
        IReadOnlyDictionary<string, string>? options)
    {
        if (string.IsNullOrWhiteSpace(dashboardName) ||
            string.IsNullOrWhiteSpace(widgetName))
        {
            return json;
        }

        var metricNames = NormalizeMetricNames(metrics).ToArray();
        var normalizedPrimary = string.IsNullOrWhiteSpace(primaryMetric)
            ? metricNames.FirstOrDefault()
            : primaryMetric.Trim();
        if (metricNames.Length == 0 && !string.IsNullOrWhiteSpace(normalizedPrimary))
        {
            metricNames = [normalizedPrimary];
        }

        var root = ParseOrCreate(json);
        var dashboard = GetOrCreateDashboardObject(GetFlowApplication(root), dashboardName);
        var bindings = GetOrCreateObject(dashboard, "bindings");
        bindings[widgetName.Trim()] = new JsonObject
        {
            ["primaryMetric"] = normalizedPrimary ?? string.Empty,
            ["metrics"] = CreateStringArray(metricNames),
            ["options"] = FlowDashboardDefinitionFactory.CreateConfiguration(options ?? new Dictionary<string, string>(StringComparer.Ordinal))
        };

        FluxMqApplicationDefinitionMigrator.MigrateRoot(root);
        return root.ToJsonString(Options);
    }

    public string RemoveDashboardMetricIfUnused(
        string json,
        string dashboardName,
        string metricName)
    {
        if (string.IsNullOrWhiteSpace(dashboardName) ||
            string.IsNullOrWhiteSpace(metricName))
        {
            return json;
        }

        var root = ParseOrCreate(json);
        var flowApplication = GetFlowApplication(root);
        if (flowApplication["dashboards"] is not JsonObject dashboards ||
            dashboards[dashboardName] is not JsonObject dashboard ||
            dashboard["bindings"] is not JsonObject bindings)
        {
            return json;
        }

        var localMetricName = FluxMetricNaming.RemoveDashboardScope(dashboardName, metricName);
        var scopedMetricName = FluxMetricNaming.ToDashboardScopedId(dashboardName, metricName);
        var referenceCandidates = new[]
            {
                metricName.Trim(),
                FluxMetricNaming.ToArtifactId(metricName),
                localMetricName,
                scopedMetricName
            }
            .Where(static candidate => !string.IsNullOrWhiteSpace(candidate))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var isUsed = bindings
            .Select(binding => binding.Value as JsonObject)
            .Where(static binding => binding is not null)
            .Cast<JsonObject>()
            .Any(binding => referenceCandidates.Any(candidate => BindingUsesMetric(binding, candidate)));
        if (!isUsed)
        {
            if (dashboard["metrics"] is JsonObject metrics)
            {
                metrics.Remove(localMetricName);
                metrics.Remove(scopedMetricName);
            }

            if (flowApplication["metrics"] is JsonObject appMetrics &&
                appMetrics[scopedMetricName] is JsonObject metricArtifact &&
                IsDashboardPromotedMetric(metricArtifact))
            {
                appMetrics.Remove(scopedMetricName);
            }
        }

        return root.ToJsonString(Options);
    }

    public string DuplicateDashboardWidget(
        string json,
        string dashboardName,
        string widgetName,
        string? targetCellName = null)
    {
        if (string.IsNullOrWhiteSpace(dashboardName) ||
            string.IsNullOrWhiteSpace(widgetName))
        {
            return json;
        }

        var root = ParseOrCreate(json);
        var flowApplication = GetFlowApplication(root);
        if (flowApplication["dashboards"] is not JsonObject dashboards ||
            dashboards[dashboardName] is not JsonObject dashboard ||
            dashboard["widgets"] is not JsonObject widgets ||
            widgets[widgetName] is not JsonObject widget)
        {
            return json;
        }

        var layout = GetOrCreateObject(dashboard, "layout");
        var cells = GetOrCreateObject(layout, "cells");
        var bindings = GetOrCreateObject(dashboard, "bindings");
        var newName = MakeUniqueDashboardWidgetName(widgets, $"{NormalizeDashboardIdentifier(widgetName)}Copy");
        widgets[newName] = widget.DeepClone();
        if (bindings[widgetName] is JsonObject binding)
        {
            bindings[newName] = binding.DeepClone();
        }

        AssignWidgetToDashboardCell(layout, cells, newName, targetCellName);
        FluxMqApplicationDefinitionMigrator.MigrateRoot(root);
        return root.ToJsonString(Options);
    }

    public string ReplaceDashboardWidgetType(
        string json,
        string dashboardName,
        string widgetName,
        string widgetType)
    {
        if (string.IsNullOrWhiteSpace(dashboardName) ||
            string.IsNullOrWhiteSpace(widgetName) ||
            string.IsNullOrWhiteSpace(widgetType))
        {
            return json;
        }

        var root = ParseOrCreate(json);
        var flowApplication = GetFlowApplication(root);
        if (flowApplication["dashboards"] is not JsonObject dashboards ||
            dashboards[dashboardName] is not JsonObject dashboard ||
            dashboard["widgets"] is not JsonObject widgets ||
            widgets[widgetName] is not JsonObject widget)
        {
            return json;
        }

        var configuration = widget["configuration"] as JsonObject ?? new JsonObject();
        var title = ReadString(configuration, "title");
        widget["type"] = widgetType.Trim();
        var nextConfiguration = FlowDashboardDefinitionFactory.CreateWidgetConfiguration(widgetType);
        if (!string.IsNullOrWhiteSpace(title))
        {
            nextConfiguration["title"] = title;
        }

        widget["configuration"] = nextConfiguration;
        FluxMqApplicationDefinitionMigrator.MigrateRoot(root);
        return root.ToJsonString(Options);
    }

    public string RemoveDashboardWidget(string json, string dashboardName, string widgetName)
    {
        if (string.IsNullOrWhiteSpace(dashboardName) ||
            string.IsNullOrWhiteSpace(widgetName))
        {
            return json;
        }

        var root = ParseOrCreate(json);
        var flowApplication = GetFlowApplication(root);
        if (flowApplication["dashboards"] is not JsonObject dashboards ||
            dashboards[dashboardName] is not JsonObject dashboard)
        {
            return json;
        }

        var changed = false;
        if (dashboard["widgets"] is JsonObject widgets)
        {
            changed = widgets.Remove(widgetName);
        }

        if (dashboard["layout"] is JsonObject layout &&
            layout["cells"] is JsonObject cells)
        {
            foreach (var (_, cell) in cells.ToArray())
            {
                if (cell is JsonObject cellObject &&
                    string.Equals(ReadString(cellObject, "widget"), widgetName, StringComparison.Ordinal))
                {
                    cellObject.Remove("widget");
                    changed = true;
                }
            }
        }

        return changed ? root.ToJsonString(Options) : json;
    }

    public string MoveDashboardWidget(string json, string dashboardName, string widgetName, string targetCellName)
    {
        if (string.IsNullOrWhiteSpace(dashboardName) ||
            string.IsNullOrWhiteSpace(widgetName) ||
            string.IsNullOrWhiteSpace(targetCellName))
        {
            return json;
        }

        var root = ParseOrCreate(json);
        var flowApplication = GetFlowApplication(root);
        if (flowApplication["dashboards"] is not JsonObject dashboards ||
            dashboards[dashboardName] is not JsonObject dashboard ||
            dashboard["widgets"] is not JsonObject widgets ||
            !widgets.ContainsKey(widgetName))
        {
            return json;
        }

        var layout = GetOrCreateObject(dashboard, "layout");
        var cells = GetOrCreateObject(layout, "cells");
        if (!TryFindOrCreateDashboardCell(layout, cells, targetCellName, out var targetName, out var targetCell))
        {
            return json;
        }

        var sourceCells = ReadDashboardCells(cells)
            .Where(cell => string.Equals(cell.Widget, widgetName, StringComparison.Ordinal))
            .ToArray();
        if (sourceCells.Any(cell => string.Equals(cell.Name, targetName, StringComparison.Ordinal)))
        {
            return json;
        }

        var replacedWidget = ReadString(targetCell, "widget");
        targetCell["widget"] = widgetName;

        var firstSourceCell = sourceCells.FirstOrDefault();
        foreach (var sourceCell in sourceCells)
        {
            if (cells[sourceCell.Name] is JsonObject sourceCellObject)
            {
                sourceCellObject.Remove("widget");
            }
        }

        if (firstSourceCell is not null &&
            !string.IsNullOrWhiteSpace(replacedWidget) &&
            !string.Equals(replacedWidget, widgetName, StringComparison.Ordinal) &&
            cells[firstSourceCell.Name] is JsonObject firstSourceCellObject)
        {
            firstSourceCellObject["widget"] = replacedWidget;
        }

        return root.ToJsonString(Options);
    }

    public string UpdateDashboardTrack(
        string json,
        string dashboardName,
        string axis,
        int index,
        string size,
        double padding)
    {
        if (string.IsNullOrWhiteSpace(dashboardName) || index < 0)
        {
            return json;
        }

        if (padding < 0 || double.IsNaN(padding) || double.IsInfinity(padding))
        {
            throw new FormatException("Dashboard track padding must be a non-negative finite size.");
        }

        var normalizedSize = NormalizeTrackString(size, axis);
        var isRow = string.Equals(axis, "row", StringComparison.OrdinalIgnoreCase);
        var trackProperty = isRow ? "rows" : "columns";
        var paddingProperty = isRow ? "rowPadding" : "columnPadding";

        var root = ParseOrCreate(json);
        var dashboard = GetOrCreateDashboardObject(GetFlowApplication(root), dashboardName);
        var layout = GetOrCreateObject(dashboard, "layout");
        var tracks = ReadTrackStrings(layout, trackProperty, ["*"]).ToList();
        if (index >= tracks.Count)
        {
            return json;
        }

        tracks[index] = normalizedSize;
        layout[trackProperty] = CreateTrackArray(tracks);

        var paddingValues = NormalizePaddingValues(ReadPaddingValues(layout, paddingProperty), tracks.Count).ToList();
        paddingValues[index] = padding;
        layout[paddingProperty] = CreateNumberArray(paddingValues);
        GetOrCreateObject(layout, "cells");
        GetOrCreateObject(dashboard, "widgets");

        return root.ToJsonString(Options);
    }

    public string UpdateDashboardGridTracks(
        string json,
        string dashboardName,
        IEnumerable<string> columns,
        IEnumerable<string> rows)
    {
        if (string.IsNullOrWhiteSpace(dashboardName))
        {
            return json;
        }

        var normalizedColumns = NormalizeTrackStrings(columns, "column");
        var normalizedRows = NormalizeTrackStrings(rows, "row");

        var root = ParseOrCreate(json);
        var dashboard = GetOrCreateDashboardObject(GetFlowApplication(root), dashboardName);
        var layout = GetOrCreateObject(dashboard, "layout");
        layout["columns"] = CreateTrackArray(normalizedColumns);
        layout["rows"] = CreateTrackArray(normalizedRows);
        layout["columnPadding"] = CreateNumberArray(NormalizePaddingValues(ReadPaddingValues(layout, "columnPadding"), normalizedColumns.Count));
        layout["rowPadding"] = CreateNumberArray(NormalizePaddingValues(ReadPaddingValues(layout, "rowPadding"), normalizedRows.Count));
        GetOrCreateObject(layout, "cells");
        GetOrCreateObject(dashboard, "widgets");

        return root.ToJsonString(Options);
    }

    public string ResizeDashboardGrid(string json, string dashboardName, int rowCount, int columnCount)
    {
        if (string.IsNullOrWhiteSpace(dashboardName))
        {
            return json;
        }

        rowCount = Math.Clamp(rowCount, 1, 12);
        columnCount = Math.Clamp(columnCount, 1, 12);

        var root = ParseOrCreate(json);
        var dashboard = GetOrCreateDashboardObject(GetFlowApplication(root), dashboardName);
        var layout = GetOrCreateObject(dashboard, "layout");
        var columns = ResizeTrackStrings(ReadTrackStrings(layout, "columns", ["*"]), columnCount);
        var rows = ResizeTrackStrings(ReadTrackStrings(layout, "rows", ["*"]), rowCount);
        layout["columns"] = CreateTrackArray(columns);
        layout["rows"] = CreateTrackArray(rows);
        layout["columnPadding"] = CreateNumberArray(NormalizePaddingValues(ReadPaddingValues(layout, "columnPadding"), columnCount));
        layout["rowPadding"] = CreateNumberArray(NormalizePaddingValues(ReadPaddingValues(layout, "rowPadding"), rowCount));

        var cells = GetOrCreateObject(layout, "cells");
        RemoveCellsOutsideGrid(cells, rowCount, columnCount);
        GetOrCreateObject(dashboard, "widgets");

        return root.ToJsonString(Options);
    }

    public string AddDashboardRow(string json, string dashboardName)
    {
        var layout = GetDashboardLayout(json, dashboardName);
        return layout is null
            ? json
            : ResizeDashboardGrid(json, dashboardName, layout.Rows.Count + 1, layout.Columns.Count);
    }

    public string RemoveDashboardRow(string json, string dashboardName)
    {
        var layout = GetDashboardLayout(json, dashboardName);
        return layout is null
            ? json
            : ResizeDashboardGrid(json, dashboardName, Math.Max(1, layout.Rows.Count - 1), layout.Columns.Count);
    }

    public string AddDashboardColumn(string json, string dashboardName)
    {
        var layout = GetDashboardLayout(json, dashboardName);
        return layout is null
            ? json
            : ResizeDashboardGrid(json, dashboardName, layout.Rows.Count, layout.Columns.Count + 1);
    }

    public string RemoveDashboardColumn(string json, string dashboardName)
    {
        var layout = GetDashboardLayout(json, dashboardName);
        return layout is null
            ? json
            : ResizeDashboardGrid(json, dashboardName, layout.Rows.Count, Math.Max(1, layout.Columns.Count - 1));
    }

    public string AddDashboardCell(string json, string dashboardName)
    {
        if (string.IsNullOrWhiteSpace(dashboardName))
        {
            return json;
        }

        var root = ParseOrCreate(json);
        var dashboard = GetOrCreateDashboardObject(GetFlowApplication(root), dashboardName);
        var layout = GetOrCreateObject(dashboard, "layout");
        var columns = ReadTrackStrings(layout, "columns", ["*"]).ToList();
        var rows = ReadTrackStrings(layout, "rows", ["*"]).ToList();

        if (columns.Count == 0)
        {
            columns.Add("*");
        }

        if (rows.Count == 0)
        {
            rows.Add("*");
        }

        layout["columns"] = CreateTrackArray(columns);
        layout["rows"] = CreateTrackArray(rows);
        layout["columnPadding"] = CreateNumberArray(NormalizePaddingValues(ReadPaddingValues(layout, "columnPadding"), columns.Count));
        layout["rowPadding"] = CreateNumberArray(NormalizePaddingValues(ReadPaddingValues(layout, "rowPadding"), rows.Count));

        var cells = GetOrCreateObject(layout, "cells");
        var existingCells = ReadDashboardCells(cells);
        var position = FindFirstOpenDashboardCell(columns.Count, rows.Count, existingCells);
        if (position is null)
        {
            rows.Add("*");
            layout["rows"] = CreateTrackArray(rows);
            layout["rowPadding"] = CreateNumberArray(NormalizePaddingValues(ReadPaddingValues(layout, "rowPadding"), rows.Count));
            position = (rows.Count - 1, 0);
        }

        var cellName = MakeUniqueDashboardCellName(cells, "cell");
        cells[cellName] = new JsonObject
        {
            ["row"] = position.Value.Row,
            ["column"] = position.Value.Column,
            ["rowSpan"] = 1,
            ["columnSpan"] = 1
        };
        GetOrCreateObject(dashboard, "widgets");

        return root.ToJsonString(Options);
    }

    public string MergeDashboardCells(
        string json,
        string dashboardName,
        IEnumerable<DashboardCellSnapshot> selectedCells)
    {
        var selection = selectedCells.ToArray();
        if (string.IsNullOrWhiteSpace(dashboardName) || !TryGetSelectionBounds(selection, out var bounds))
        {
            return json;
        }

        var root = ParseOrCreate(json);
        var dashboard = GetOrCreateDashboardObject(GetFlowApplication(root), dashboardName);
        var layout = GetOrCreateObject(dashboard, "layout");
        var cells = GetOrCreateObject(layout, "cells");
        var selectedNames = selection
            .Where(static cell => cell.IsExplicit)
            .Select(static cell => cell.Name)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var selectedName in selectedNames)
        {
            cells.Remove(selectedName);
        }

        var cellName = selectedNames.Count == 1
            ? selectedNames.Single()
            : MakeUniqueDashboardCellName(cells, "cell");
        var widgets = selection
            .Where(static cell => cell.IsExplicit && !string.IsNullOrWhiteSpace(cell.Widget))
            .Select(static cell => cell.Widget)
            .Distinct(StringComparer.Ordinal)
            .Take(2)
            .ToArray();
        var widget = widgets.Length == 1 ? widgets[0] : null;

        cells[cellName] = new JsonObject
        {
            ["row"] = bounds.MinRow,
            ["column"] = bounds.MinColumn,
            ["rowSpan"] = bounds.MaxRow - bounds.MinRow + 1,
            ["columnSpan"] = bounds.MaxColumn - bounds.MinColumn + 1
        };

        if (!string.IsNullOrWhiteSpace(widget))
        {
            ((JsonObject)cells[cellName]!)["widget"] = widget;
        }

        return root.ToJsonString(Options);
    }

    public string SplitDashboardCell(string json, string dashboardName, string cellName)
    {
        if (string.IsNullOrWhiteSpace(dashboardName) || string.IsNullOrWhiteSpace(cellName))
        {
            return json;
        }

        var root = ParseOrCreate(json);
        var flowApplication = GetFlowApplication(root);
        if (flowApplication["dashboards"] is not JsonObject dashboards ||
            dashboards[dashboardName] is not JsonObject dashboard ||
            dashboard["layout"] is not JsonObject layout ||
            layout["cells"] is not JsonObject cells ||
            cells[cellName] is not JsonObject cellObject)
        {
            return json;
        }

        var cell = new DashboardCellSnapshot(
            cellName,
            ReadInt(cellObject, "row"),
            ReadInt(cellObject, "column"),
            Math.Max(1, ReadInt(cellObject, "rowSpan", 1)),
            Math.Max(1, ReadInt(cellObject, "columnSpan", 1)),
            ReadString(cellObject, "widget"));

        if (!cell.IsMerged)
        {
            return json;
        }

        cells.Remove(cellName);
        foreach (var coordinate in cell.CoveredCoordinates())
        {
            var name = MakeUniqueDashboardCellName(cells, "cell");
            cells[name] = new JsonObject
            {
                ["row"] = coordinate.Row,
                ["column"] = coordinate.Column,
                ["rowSpan"] = 1,
                ["columnSpan"] = 1
            };
        }

        return root.ToJsonString(Options);
    }

    public string SubdivideDashboardCell(
        string json,
        string dashboardName,
        DashboardCellSnapshot selectedCell,
        int rowParts,
        int columnParts)
    {
        rowParts = Math.Clamp(rowParts, 1, 6);
        columnParts = Math.Clamp(columnParts, 1, 6);
        if (string.IsNullOrWhiteSpace(dashboardName) || rowParts == 1 && columnParts == 1)
        {
            return json;
        }

        var root = ParseOrCreate(json);
        var dashboard = GetOrCreateDashboardObject(GetFlowApplication(root), dashboardName);
        var layout = GetOrCreateObject(dashboard, "layout");
        var columns = ReadTrackStrings(layout, "columns", ["*"]).ToArray();
        var rows = ReadTrackStrings(layout, "rows", ["*"]).ToArray();
        var columnPadding = NormalizePaddingValues(ReadPaddingValues(layout, "columnPadding"), columns.Length);
        var rowPadding = NormalizePaddingValues(ReadPaddingValues(layout, "rowPadding"), rows.Length);
        var rowInsertCount = Math.Max(0, rowParts - selectedCell.RowSpan);
        var columnInsertCount = Math.Max(0, columnParts - selectedCell.ColumnSpan);

        if (rows.Length + rowInsertCount > 12 || columns.Length + columnInsertCount > 12)
        {
            return json;
        }

        layout["rows"] = CreateTrackArray(SubdivideTrackStrings(rows, selectedCell.Row, selectedCell.RowSpan, rowParts));
        layout["columns"] = CreateTrackArray(SubdivideTrackStrings(columns, selectedCell.Column, selectedCell.ColumnSpan, columnParts));
        layout["rowPadding"] = CreateNumberArray(SubdividePaddingValues(rowPadding, selectedCell.Row, selectedCell.RowSpan, rowParts));
        layout["columnPadding"] = CreateNumberArray(SubdividePaddingValues(columnPadding, selectedCell.Column, selectedCell.ColumnSpan, columnParts));

        var cells = GetOrCreateObject(layout, "cells");
        var existingCells = ReadDashboardCells(cells)
            .Where(cell => !selectedCell.IsExplicit || !string.Equals(cell.Name, selectedCell.Name, StringComparison.Ordinal))
            .Select(cell => TransformDashboardCell(cell, selectedCell, rowInsertCount, columnInsertCount))
            .ToArray();

        var nextCells = new JsonObject();
        foreach (var cell in existingCells)
        {
            nextCells[cell.Name] = FlowDashboardDefinitionFactory.CreateCell(cell);
        }

        foreach (var child in CreateSubdivisionCells(selectedCell, rowParts, columnParts))
        {
            var name = MakeUniqueDashboardCellName(nextCells, "cell");
            nextCells[name] = FlowDashboardDefinitionFactory.CreateCell(child with { Name = name, IsExplicit = true });
        }

        layout["cells"] = nextCells;
        GetOrCreateObject(dashboard, "widgets");

        return root.ToJsonString(Options);
    }

    public string RemoveDashboardCell(string json, string dashboardName, string cellName)
    {
        if (string.IsNullOrWhiteSpace(dashboardName) || string.IsNullOrWhiteSpace(cellName))
        {
            return json;
        }

        var root = ParseOrCreate(json);
        var flowApplication = GetFlowApplication(root);
        if (flowApplication["dashboards"] is JsonObject dashboards &&
            dashboards[dashboardName] is JsonObject dashboard &&
            dashboard["layout"] is JsonObject layout &&
            layout["cells"] is JsonObject cells)
        {
            cells.Remove(cellName);
        }

        return root.ToJsonString(Options);
    }
    private static IReadOnlyList<string> NormalizeTrackStrings(IEnumerable<string> tracks, string axis)
    {
        var normalized = tracks
            .Select(track => string.IsNullOrWhiteSpace(track) ? string.Empty : NormalizeTrackString(track, axis))
            .Where(static track => !string.IsNullOrWhiteSpace(track))
            .ToArray();

        if (normalized.Length == 0)
        {
            throw new FormatException($"Dashboard {axis} tracks must include at least one size.");
        }

        return normalized;
    }

    private static string NormalizeTrackString(string value, string axis)
    {
        var track = DashboardGridTrackDefinition.Parse(value);
        if (track.Unit == DashboardGridTrackUnit.Percent && track.Value > 100)
        {
            throw new FormatException($"Dashboard {axis} percent track cannot exceed 100%.");
        }

        return track.ToSizeString();
    }

    private static IReadOnlyList<string> ReadTrackStrings(
        JsonObject layout,
        string propertyName,
        IReadOnlyList<string> fallback)
    {
        if (layout[propertyName] is not JsonArray tracks || tracks.Count == 0)
        {
            return fallback;
        }

        var result = new List<string>();
        foreach (var track in tracks)
        {
            try
            {
                var size = ReadTrackString(track);
                if (!string.IsNullOrWhiteSpace(size))
                {
                    result.Add(size);
                }
            }
            catch
            {
                if (track is JsonValue value &&
                    value.TryGetValue<string>(out var raw) &&
                    !string.IsNullOrWhiteSpace(raw))
                {
                    result.Add(raw.Trim());
                }
            }
        }

        return result.Count > 0 ? result : fallback;
    }

    private static string ReadTrackString(JsonNode? node)
    {
        if (node is null)
        {
            return "*";
        }

        if (node is JsonValue value)
        {
            if (value.TryGetValue<string>(out var text))
            {
                return text.Trim();
            }

            if (value.TryGetValue<double>(out var number))
            {
                return DashboardGridTrackDefinition.Fixed(number).ToSizeString();
            }
        }

        if (node is JsonObject obj)
        {
            if (obj["size"] is JsonValue sizeValue &&
                sizeValue.TryGetValue<string>(out var size) &&
                !string.IsNullOrWhiteSpace(size))
            {
                return size.Trim();
            }

            if (obj["unit"] is JsonValue unitValue &&
                unitValue.TryGetValue<string>(out var unit) &&
                obj["value"] is JsonValue numericValue &&
                numericValue.TryGetValue<double>(out var numeric))
            {
                return unit.Trim().ToLowerInvariant() switch
                {
                    "fixed" => DashboardGridTrackDefinition.Fixed(numeric).ToSizeString(),
                    "percent" => DashboardGridTrackDefinition.Percent(numeric).ToSizeString(),
                    "star" => DashboardGridTrackDefinition.Star(numeric).ToSizeString(),
                    _ => "*"
                };
            }
        }

        return "*";
    }

    private static IReadOnlyList<double> ReadPaddingValues(JsonObject layout, string propertyName)
    {
        if (layout[propertyName] is not JsonArray values)
        {
            return [];
        }

        var result = new List<double>();
        foreach (var item in values)
        {
            if (item is JsonValue value &&
                value.TryGetValue<double>(out var number) &&
                number >= 0 &&
                double.IsFinite(number))
            {
                result.Add(number);
            }
            else
            {
                result.Add(0);
            }
        }

        return result;
    }

    private static IReadOnlyList<double> NormalizePaddingValues(IReadOnlyList<double> values, int count)
    {
        var normalized = values
            .Select(static value => value >= 0 && double.IsFinite(value) ? value : 0)
            .Take(count)
            .ToList();

        while (normalized.Count < count)
        {
            normalized.Add(0);
        }

        return normalized;
    }

    private static IReadOnlyList<DashboardCellSnapshot> ReadDashboardCells(JsonObject cells)
    {
        var result = new List<DashboardCellSnapshot>();
        foreach (var cell in cells)
        {
            if (cell.Value is not JsonObject cellObject)
            {
                continue;
            }

            result.Add(new DashboardCellSnapshot(
                cell.Key,
                ReadInt(cellObject, "row"),
                ReadInt(cellObject, "column"),
                Math.Max(1, ReadInt(cellObject, "rowSpan", 1)),
                Math.Max(1, ReadInt(cellObject, "columnSpan", 1)),
                ReadString(cellObject, "widget"),
                Style: ReadConfigurationStrings(cellObject["style"] as JsonObject ?? new JsonObject())));
        }

        return result
            .OrderBy(static cell => cell.Row)
            .ThenBy(static cell => cell.Column)
            .ThenBy(static cell => cell.Name, StringComparer.Ordinal)
            .ToArray();
    }

    private static IReadOnlyDictionary<string, DashboardWidgetSnapshot> ReadDashboardWidgets(JsonObject widgets)
    {
        var result = new Dictionary<string, DashboardWidgetSnapshot>(StringComparer.Ordinal);
        foreach (var widget in widgets)
        {
            if (widget.Value is not JsonObject widgetObject)
            {
                continue;
            }

            var configuration = widgetObject["configuration"] as JsonObject ?? new JsonObject();
            result[widget.Key] = new DashboardWidgetSnapshot(
                widget.Key,
                ReadString(widgetObject, "type") ?? string.Empty,
                ReadConfigurationStrings(configuration));
        }

        return result;
    }

    private static IReadOnlyDictionary<string, DashboardMetricSnapshot> ReadDashboardMetrics(
        JsonObject appMetrics,
        JsonObject dashboardMetrics)
    {
        var result = new Dictionary<string, DashboardMetricSnapshot>(StringComparer.Ordinal);
        foreach (var metric in ReadAppDashboardMetrics(appMetrics))
        {
            result[metric.Key] = metric.Value;
        }

        foreach (var metric in ReadLegacyDashboardMetrics(dashboardMetrics))
        {
            result[metric.Key] = metric.Value;
        }

        return result;
    }

    private static readonly IFluxMetricTypeRegistry DashboardMetricTypes = FluxMetricTypeRegistry.CreateDefault();

    private static IReadOnlyDictionary<string, DashboardMetricSnapshot> ReadAppDashboardMetrics(JsonObject metrics)
    {
        var result = new Dictionary<string, DashboardMetricSnapshot>(StringComparer.Ordinal);
        foreach (var (metricName, resource) in ReadMetricResources(metrics))
        {
            result[metricName] = ResourceToDashboardMetricSnapshot(metricName, resource);
        }

        return result;
    }

    internal static DashboardMetricSnapshot ResourceToDashboardMetricSnapshot(
        string metricName,
        FluxMetricResourceDefinition resource)
    {
        var filters = new Dictionary<string, string>(StringComparer.Ordinal);
        AddIfPresent(filters, DashboardEventFilterCatalog.EventTypeKey, resource.GetParameter(MetricParameterKeys.EventType));
        AddIfPresent(filters, DashboardEventFilterCatalog.TopicStartsWithKey, resource.GetParameter(MetricParameterKeys.TopicStartsWith));
        AddIfPresent(filters, DashboardEventFilterCatalog.TopicNotStartsWithKey, resource.GetParameter(MetricParameterKeys.TopicNotStartsWith));
        AddIfPresent(filters, DashboardEventFilterCatalog.StatusKey, resource.GetParameter(MetricParameterKeys.Status));
        AddIfPresent(filters, DashboardEventFilterCatalog.AttributeFilterKey("qos"), resource.GetParameter(MetricParameterKeys.Qos));
        AddIfPresent(filters, DashboardEventFilterCatalog.AttributeFilterKey("retain"), resource.GetParameter(MetricParameterKeys.Retain));

        var typeFormat = DashboardMetricTypes.TryGetNumberType(resource.TypeId, out var type)
            ? type.Format
            : MetricFormats.Number;
        var unit = resource.GetParameter("format", typeFormat);
        var groupBy = resource.GetParameter("groupBy");

        return new DashboardMetricSnapshot(
            metricName,
            "runtimeEvents",
            DashboardMetricRegistry.MeasureForType(resource.TypeId),
            resource.GetParameter(MetricParameterKeys.Window, "60s"),
            string.IsNullOrWhiteSpace(groupBy) ? null : groupBy,
            filters,
            new Dictionary<string, string>(StringComparer.Ordinal) { ["unit"] = unit });
    }

    private static IReadOnlyDictionary<string, DashboardMetricSnapshot> ReadLegacyDashboardMetrics(JsonObject metrics)
    {
        var result = new Dictionary<string, DashboardMetricSnapshot>(StringComparer.Ordinal);
        foreach (var metric in metrics)
        {
            if (metric.Value is not JsonObject metricObject)
            {
                continue;
            }

            var filters = metricObject["filters"] as JsonObject ?? new JsonObject();
            var format = metricObject["format"] as JsonObject ?? new JsonObject();
            result[metric.Key] = new DashboardMetricSnapshot(
                metric.Key,
                ReadString(metricObject, "source") ?? "runtimeEvents",
                ReadString(metricObject, "aggregation") ?? "count",
                ReadString(metricObject, "window") ?? "60s",
                ReadString(metricObject, "groupBy"),
                ReadConfigurationStrings(filters),
                ReadConfigurationStrings(format));
        }

        return result;
    }

    private static void AddIfPresent(IDictionary<string, string> target, string key, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            target[key] = value.Trim();
        }
    }

    private static IReadOnlyDictionary<string, DashboardWidgetBindingSnapshot> ReadDashboardBindings(JsonObject bindings)
    {
        var result = new Dictionary<string, DashboardWidgetBindingSnapshot>(StringComparer.Ordinal);
        foreach (var binding in bindings)
        {
            if (binding.Value is not JsonObject bindingObject)
            {
                continue;
            }

            var metrics = bindingObject["metrics"] as JsonArray;
            result[binding.Key] = new DashboardWidgetBindingSnapshot(
                binding.Key,
                ReadString(bindingObject, "primaryMetric"),
                ReadStringArray(metrics),
                ReadConfigurationStrings(bindingObject["options"] as JsonObject ?? new JsonObject()));
        }

        return result;
    }

    private static IReadOnlyDictionary<string, string> ReadConfigurationStrings(JsonObject configuration)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var property in configuration)
        {
            if (string.Equals(property.Key, "attributes", StringComparison.Ordinal) &&
                property.Value is JsonObject attributes)
            {
                foreach (var attribute in attributes)
                {
                    if (TryReadConfigurationString(attribute.Value, out var attributeValue))
                    {
                        result[DashboardEventFilterCatalog.AttributeFilterKey(attribute.Key)] = attributeValue;
                    }
                }

                continue;
            }

            if (TryReadConfigurationString(property.Value, out var configurationValue))
            {
                result[property.Key] = configurationValue;
            }
        }

        return result;
    }

    private static bool TryReadConfigurationString(JsonNode? node, out string value)
    {
        if (node is JsonValue jsonValue)
        {
            if (jsonValue.TryGetValue<string>(out var text))
            {
                value = text;
                return true;
            }

            if (jsonValue.TryGetValue<bool>(out var boolean))
            {
                value = boolean ? "true" : "false";
                return true;
            }

            if (jsonValue.TryGetValue<double>(out var number))
            {
                value = number.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
                return true;
            }
        }

        value = node?.ToJsonString() ?? string.Empty;
        return true;
    }

    private static IReadOnlyList<string> ReadStringArray(JsonArray? values)
    {
        if (values is null)
        {
            return [];
        }

        return values
            .Select(static value => value is JsonValue jsonValue && jsonValue.TryGetValue<string>(out var text) ? text : null)
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Select(static value => value!.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private static IEnumerable<string> NormalizeMetricNames(IEnumerable<string>? metrics)
    {
        if (metrics is null)
        {
            yield break;
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var metric in metrics)
        {
            if (string.IsNullOrWhiteSpace(metric))
            {
                continue;
            }

            var normalized = metric.Trim();
            if (seen.Add(normalized))
            {
                yield return normalized;
            }
        }
    }

    private static JsonArray CreateStringArray(IEnumerable<string> values)
    {
        var array = new JsonArray();
        foreach (var value in values)
        {
            array.Add(value);
        }

        return array;
    }

    private static string NormalizeDashboardIdentifier(string value)
    {
        var chars = value
            .Where(char.IsLetterOrDigit)
            .ToArray();
        if (chars.Length == 0)
        {
            return "widget";
        }

        return char.ToLowerInvariant(chars[0]) + new string(chars[1..]);
    }

    private static bool BindingUsesMetric(JsonObject binding, string metricName)
    {
        if (string.Equals(ReadString(binding, "primaryMetric"), metricName, StringComparison.Ordinal))
        {
            return true;
        }

        return binding["metrics"] is JsonArray metrics &&
            ReadStringArray(metrics).Contains(metricName, StringComparer.Ordinal);
    }

    private static bool IsDashboardPromotedMetric(JsonObject metricArtifact)
        => metricArtifact["labels"] is JsonObject labels &&
           !string.IsNullOrWhiteSpace(ReadString(labels, "promotedFrom"));

    private static int ReadInt(JsonObject obj, string propertyName, int fallback = 0)
    {
        if (obj[propertyName] is not JsonValue value)
        {
            return fallback;
        }

        if (value.TryGetValue<int>(out var integer))
        {
            return integer;
        }

        return value.TryGetValue<double>(out var number) ? (int)number : fallback;
    }

    private static string? ReadString(JsonObject obj, string propertyName)
        => obj[propertyName] is JsonValue value &&
           value.TryGetValue<string>(out var text) &&
           !string.IsNullOrWhiteSpace(text)
            ? text
            : null;

    private static (int Row, int Column)? FindFirstOpenDashboardCell(
        int columnCount,
        int rowCount,
        IReadOnlyList<DashboardCellSnapshot> cells)
    {
        for (var row = 0; row < rowCount; row++)
        {
            for (var column = 0; column < columnCount; column++)
            {
                if (!cells.Any(cell => CoversDashboardSlot(cell, row, column)))
                {
                    return (row, column);
                }
            }
        }

        return null;
    }

    private static void AssignWidgetToDashboardCell(JsonObject layout, JsonObject cells, string widgetName, string? requestedCellName)
    {
        var columns = ReadTrackStrings(layout, "columns", ["*"]).ToList();
        var rows = ReadTrackStrings(layout, "rows", ["*"]).ToList();
        var existingCells = ReadDashboardCells(cells);

        if (!string.IsNullOrWhiteSpace(requestedCellName))
        {
            if (cells[requestedCellName] is JsonObject existingCell)
            {
                existingCell["widget"] = widgetName;
                return;
            }

            if (TryParseSlotCellName(requestedCellName, out var requestedRow, out var requestedColumn) &&
                requestedRow >= 0 &&
                requestedColumn >= 0 &&
                requestedRow < rows.Count &&
                requestedColumn < columns.Count)
            {
                var coveringCell = existingCells.FirstOrDefault(cell => CoversDashboardSlot(cell, requestedRow, requestedColumn));
                if (coveringCell is not null && cells[coveringCell.Name] is JsonObject coveringCellObject)
                {
                    coveringCellObject["widget"] = widgetName;
                    return;
                }

                var cellName = MakeUniqueDashboardCellName(cells, "cell");
                cells[cellName] = FlowDashboardDefinitionFactory.CreateCell(new DashboardCellSnapshot(
                    cellName,
                    requestedRow,
                    requestedColumn,
                    1,
                    1,
                    widgetName));
                return;
            }
        }

        var emptyCell = existingCells.FirstOrDefault(static cell => string.IsNullOrWhiteSpace(cell.Widget));
        if (emptyCell is not null && cells[emptyCell.Name] is JsonObject emptyCellObject)
        {
            emptyCellObject["widget"] = widgetName;
            return;
        }

        var openPosition = FindFirstOpenDashboardCell(columns.Count, rows.Count, existingCells);
        if (openPosition is null)
        {
            rows.Add("*");
            var rowPadding = NormalizePaddingValues(ReadPaddingValues(layout, "rowPadding"), rows.Count - 1).ToList();
            rowPadding.Add(0);
            layout["rows"] = CreateTrackArray(rows);
            layout["rowPadding"] = CreateNumberArray(rowPadding);
            openPosition = (rows.Count - 1, 0);
        }

        var name = MakeUniqueDashboardCellName(cells, "cell");
        cells[name] = FlowDashboardDefinitionFactory.CreateCell(new DashboardCellSnapshot(
            name,
            openPosition.Value.Row,
            openPosition.Value.Column,
            1,
            1,
            widgetName));
    }

    private static bool TryFindOrCreateDashboardCell(
        JsonObject layout,
        JsonObject cells,
        string requestedCellName,
        out string cellName,
        out JsonObject cellObject)
    {
        if (cells[requestedCellName] is JsonObject existingCell)
        {
            cellName = requestedCellName;
            cellObject = existingCell;
            return true;
        }

        if (!TryParseSlotCellName(requestedCellName, out var requestedRow, out var requestedColumn))
        {
            cellName = string.Empty;
            cellObject = new JsonObject();
            return false;
        }

        var columns = ReadTrackStrings(layout, "columns", ["*"]).ToList();
        var rows = ReadTrackStrings(layout, "rows", ["*"]).ToList();
        if (requestedRow < 0 ||
            requestedColumn < 0 ||
            requestedRow >= rows.Count ||
            requestedColumn >= columns.Count)
        {
            cellName = string.Empty;
            cellObject = new JsonObject();
            return false;
        }

        var coveringCell = ReadDashboardCells(cells)
            .FirstOrDefault(cell => CoversDashboardSlot(cell, requestedRow, requestedColumn));
        if (coveringCell is not null && cells[coveringCell.Name] is JsonObject coveringCellObject)
        {
            cellName = coveringCell.Name;
            cellObject = coveringCellObject;
            return true;
        }

        cellName = MakeUniqueDashboardCellName(cells, "cell");
        cellObject = FlowDashboardDefinitionFactory.CreateCell(new DashboardCellSnapshot(
            cellName,
            requestedRow,
            requestedColumn,
            1,
            1));
        cells[cellName] = cellObject;
        return true;
    }

    private static bool TryParseSlotCellName(string name, out int row, out int column)
    {
        row = 0;
        column = 0;
        var parts = name.Split(':');
        return parts.Length == 3 &&
               string.Equals(parts[0], "slot", StringComparison.Ordinal) &&
               int.TryParse(parts[1], out row) &&
               int.TryParse(parts[2], out column);
    }

    private static bool TryGetSelectionBounds(
        IReadOnlyList<DashboardCellSnapshot> selectedCells,
        out (int MinRow, int MaxRow, int MinColumn, int MaxColumn) bounds)
    {
        bounds = default;
        if (selectedCells.Count < 2)
        {
            return false;
        }

        var coordinates = selectedCells.SelectMany(static cell => cell.CoveredCoordinates()).ToArray();
        var uniqueCoordinates = coordinates.ToHashSet();
        if (uniqueCoordinates.Count != coordinates.Length)
        {
            return false;
        }

        var minRow = uniqueCoordinates.Min(static coordinate => coordinate.Row);
        var maxRow = uniqueCoordinates.Max(static coordinate => coordinate.Row);
        var minColumn = uniqueCoordinates.Min(static coordinate => coordinate.Column);
        var maxColumn = uniqueCoordinates.Max(static coordinate => coordinate.Column);
        var expectedArea = (maxRow - minRow + 1) * (maxColumn - minColumn + 1);
        if (uniqueCoordinates.Count != expectedArea)
        {
            return false;
        }

        bounds = (minRow, maxRow, minColumn, maxColumn);
        return true;
    }

    private static bool CoversDashboardSlot(DashboardCellSnapshot cell, int row, int column)
        => row >= cell.Row &&
           row < cell.Row + cell.RowSpan &&
           column >= cell.Column &&
           column < cell.Column + cell.ColumnSpan;

    private static IReadOnlyList<string> ResizeTrackStrings(IReadOnlyList<string> tracks, int count)
    {
        var result = tracks.Take(count).ToList();
        while (result.Count < count)
        {
            result.Add("*");
        }

        return result;
    }

    private static IReadOnlyList<string> SubdivideTrackStrings(
        IReadOnlyList<string> tracks,
        int start,
        int span,
        int parts)
    {
        if (parts <= span)
        {
            return tracks;
        }

        var selected = tracks.Skip(start).Take(Math.Max(1, span)).ToArray();
        var children = CreateSubdividedTrackTokens(selected, parts);

        return tracks.Take(start)
            .Concat(children)
            .Concat(tracks.Skip(start + Math.Max(1, span)))
            .ToArray();
    }

    private static IReadOnlyList<double> SubdividePaddingValues(
        IReadOnlyList<double> values,
        int start,
        int span,
        int parts)
    {
        if (parts <= span)
        {
            return values;
        }

        var selected = values.Skip(start).Take(Math.Max(1, span)).ToArray();
        var child = selected.Length == 0 ? 0 : selected.Average();

        return values.Take(start)
            .Concat(Enumerable.Repeat(child, parts))
            .Concat(values.Skip(start + Math.Max(1, span)))
            .ToArray();
    }

    private static IReadOnlyList<string> CreateSubdividedTrackTokens(IReadOnlyList<string> selectedTokens, int parts)
    {
        var parsed = selectedTokens
            .Select(token =>
            {
                try
                {
                    return DashboardGridTrackDefinition.Parse(token);
                }
                catch
                {
                    return DashboardGridTrackDefinition.Star();
                }
            })
            .ToArray();

        if (parsed.Length == 0)
        {
            return Enumerable.Repeat("*", parts).ToArray();
        }

        var first = parsed[0];
        if (parsed.Any(track => track.Unit != first.Unit))
        {
            return Enumerable.Repeat("*", parts).ToArray();
        }

        var child = first with { Value = parsed.Sum(static track => track.Value) / parts };
        return Enumerable.Repeat(child.ToSizeString(), parts).ToArray();
    }

    private static DashboardCellSnapshot TransformDashboardCell(
        DashboardCellSnapshot cell,
        DashboardCellSnapshot selected,
        int rowInsertCount,
        int columnInsertCount)
    {
        var row = TransformDashboardDimension(cell.Row, cell.RowSpan, selected.Row, selected.RowSpan, rowInsertCount);
        var column = TransformDashboardDimension(cell.Column, cell.ColumnSpan, selected.Column, selected.ColumnSpan, columnInsertCount);

        return cell with
        {
            Row = row.Start,
            RowSpan = row.Span,
            Column = column.Start,
            ColumnSpan = column.Span
        };
    }

    private static (int Start, int Span) TransformDashboardDimension(
        int start,
        int span,
        int selectedStart,
        int selectedSpan,
        int insertCount)
    {
        if (insertCount == 0)
        {
            return (start, span);
        }

        var end = start + span;
        var selectedEnd = selectedStart + selectedSpan;

        if (start >= selectedEnd)
        {
            return (start + insertCount, span);
        }

        if (end <= selectedStart)
        {
            return (start, span);
        }

        return (start, span + insertCount);
    }

    private static IReadOnlyList<DashboardCellSnapshot> CreateSubdivisionCells(
        DashboardCellSnapshot selected,
        int rowParts,
        int columnParts)
    {
        var rowSpan = selected.RowSpan + Math.Max(0, rowParts - selected.RowSpan);
        var columnSpan = selected.ColumnSpan + Math.Max(0, columnParts - selected.ColumnSpan);
        var rowBands = CreateDashboardBands(selected.Row, rowSpan, rowParts);
        var columnBands = CreateDashboardBands(selected.Column, columnSpan, columnParts);

        return rowBands
            .SelectMany(rowBand => columnBands.Select(columnBand => new DashboardCellSnapshot(
                "cell",
                rowBand.Start,
                columnBand.Start,
                rowBand.Span,
                columnBand.Span)))
            .ToArray();
    }

    private static IReadOnlyList<(int Start, int Span)> CreateDashboardBands(int start, int span, int parts)
    {
        var baseSpan = span / parts;
        var remainder = span % parts;
        var bands = new List<(int Start, int Span)>(parts);
        var cursor = start;

        for (var index = 0; index < parts; index++)
        {
            var bandSpan = baseSpan + (index < remainder ? 1 : 0);
            bands.Add((cursor, bandSpan));
            cursor += bandSpan;
        }

        return bands;
    }

    private static void RemoveCellsOutsideGrid(JsonObject cells, int rowCount, int columnCount)
    {
        foreach (var cell in ReadDashboardCells(cells))
        {
            if (cell.Row < 0 ||
                cell.Column < 0 ||
                cell.Row + cell.RowSpan > rowCount ||
                cell.Column + cell.ColumnSpan > columnCount)
            {
                cells.Remove(cell.Name);
            }
        }
    }

    private static JsonArray CreateTrackArray(IEnumerable<string> tracks)
    {
        var array = new JsonArray();
        foreach (var track in tracks)
        {
            array.Add(track);
        }

        return array;
    }

    private static JsonArray CreateNumberArray(IEnumerable<double> values)
    {
        var array = new JsonArray();
        foreach (var value in values)
        {
            array.Add(value);
        }

        return array;
    }

    private static JsonObject GetOrCreateDashboardObject(JsonObject flowApplication, string dashboardName)
    {
        var dashboards = GetOrCreateObject(flowApplication, "dashboards");
        if (dashboards[dashboardName] is JsonObject dashboard)
        {
            return dashboard;
        }

        dashboard = FlowDashboardDefinitionFactory.CreateDashboard();
        dashboards[dashboardName] = dashboard;
        return dashboard;
    }

    private static string MakeUniqueDashboardCellName(JsonObject cells, string preferred)
    {
        if (!cells.ContainsKey(preferred))
        {
            return preferred;
        }

        var index = 2;
        while (cells.ContainsKey($"{preferred}{index}"))
        {
            index++;
        }

        return $"{preferred}{index}";
    }

    private static string MakeUniqueDashboardWidgetName(JsonObject widgets, string preferred)
    {
        if (!widgets.ContainsKey(preferred))
        {
            return preferred;
        }

        var index = 2;
        while (widgets.ContainsKey($"{preferred}{index}"))
        {
            index++;
        }

        return $"{preferred}{index}";
    }

}
