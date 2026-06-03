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

        var columns = ReadTrackStrings(layout, "columns", ["*"]);
        var rows = ReadTrackStrings(layout, "rows", ["*"]);

        return new DashboardLayoutSnapshot(
            columns,
            rows,
            NormalizePaddingValues(ReadPaddingValues(layout, "columnPadding"), columns.Count),
            NormalizePaddingValues(ReadPaddingValues(layout, "rowPadding"), rows.Count),
            ReadDashboardCells(cells),
            ReadDashboardWidgets(widgets));
    }

    public string AddDashboardWidget(string json, string dashboardName, string widgetType, string? cellName = null)
    {
        if (string.IsNullOrWhiteSpace(dashboardName))
        {
            return json;
        }

        var normalizedType = string.IsNullOrWhiteSpace(widgetType)
            ? DashboardWidgetCatalog.EventCounterType
            : widgetType.Trim();
        var root = ParseOrCreate(json);
        var dashboard = GetOrCreateDashboardObject(GetFlowApplication(root), dashboardName);
        var layout = GetOrCreateObject(dashboard, "layout");
        var cells = GetOrCreateObject(layout, "cells");
        var widgets = GetOrCreateObject(dashboard, "widgets");

        var widgetName = MakeUniqueDashboardWidgetName(widgets, FlowDashboardDefinitionFactory.WidgetNamePrefix(normalizedType));
        widgets[widgetName] = FlowDashboardDefinitionFactory.CreateWidget(normalizedType);
        AssignWidgetToDashboardCell(layout, cells, widgetName, cellName);

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
}
