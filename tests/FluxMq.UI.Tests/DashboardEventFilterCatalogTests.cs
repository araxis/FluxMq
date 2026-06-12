using FluxFlow.Engine.Components;
using FluxMq.App.Metrics;
using FluxMq.UI.Components.Workspace;
using FluxMq.UI.Models;
using FluxMq.UI.Services;
using Shouldly;

namespace FluxMq.UI.Tests;

public sealed class DashboardEventFilterCatalogTests
{
    [Fact]
    public void DashboardWidgetFormatting_UsesDedicatedWidgetChromeMetadata()
    {
        var kpiWidget = new DashboardWidgetSnapshot(
            "received",
            DashboardWidgetCatalog.KpiTileType,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["title"] = "KPI tile",
                [DashboardEventFilterCatalog.EventTypeKey] = FluxMqEventTypes.MqttMessageReceived
            });
        var eventWidget = new DashboardWidgetSnapshot(
            "published",
            DashboardWidgetCatalog.EventGaugeType,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["title"] = "Published traffic",
                [DashboardEventFilterCatalog.EventTypeKey] = FluxMqEventTypes.MqttMessagePublished,
                [DashboardEventFilterCatalog.TopicStartsWithKey] = "factory/",
                [DashboardEventFilterCatalog.TopicNotStartsWithKey] = "$SYS/",
                [DashboardEventFilterCatalog.StatusKey] = "published"
            });
        var topicWidget = new DashboardWidgetSnapshot(
            "topics",
            DashboardWidgetCatalog.TopicTreeType,
            new Dictionary<string, string>(StringComparer.Ordinal));
        var topicActivity = new DashboardWidgetSnapshot(
            "topicActivity",
            DashboardWidgetCatalog.TopicActivityType,
                new Dictionary<string, string>(StringComparer.Ordinal));

        DashboardWidgetFormatting.WidgetTitle(kpiWidget).ShouldBe("Messages");
        DashboardWidgetFormatting.WidgetSubtitle(kpiWidget).ShouldBe("Total matching events");
        DashboardWidgetFormatting.WidgetTitle(eventWidget).ShouldBe("Published traffic");
        DashboardWidgetFormatting.WidgetClass(eventWidget).ShouldBe("event-gauge");
        DashboardWidgetFormatting.WidgetSubtitle(eventWidget)
            .ShouldBe("mqtt.message.published / topic factory/ / exclude $SYS/ / status published");
        DashboardWidgetFormatting.WidgetTitle(topicActivity).ShouldBe("Topic activity");
        DashboardWidgetFormatting.WidgetClass(topicActivity).ShouldBe("topic-activity");
        DashboardWidgetFormatting.WidgetSubtitle(topicWidget).ShouldBe("Live topic map");
    }

    [Fact]
    public void DashboardWidgetFormatting_MapsGaugeMetricValueThroughConfiguredRange()
    {
        var widget = new DashboardWidgetSnapshot(
            "gauge",
            DashboardWidgetCatalog.EventGaugeType,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [DashboardWidgetCatalog.GaugeMinKey] = "20",
                [DashboardWidgetCatalog.GaugeMaxKey] = "120",
                [DashboardWidgetCatalog.GaugeTargetKey] = "90",
                [DashboardWidgetCatalog.GaugeWarningKey] = "70",
                [DashboardWidgetCatalog.GaugeCriticalKey] = "100",
                [DashboardWidgetCatalog.GaugeNormalColorKey] = "#11aa99",
                [DashboardWidgetCatalog.GaugeWarningColorKey] = "#ffaa00",
                [DashboardWidgetCatalog.GaugeCriticalColorKey] = "#ff3355"
            });
        var metric = new DashboardMetricValue("Events", 80, "events", "80");

        var state = DashboardWidgetFormatting.GaugeVisualState(widget, metric);

        state.Progress.ShouldBe(60d);
        state.TargetProgress.ShouldBe(70d);
        state.RangeLabel.ShouldBe("20 - 120");
        state.TargetLabel.ShouldBe("90");
        state.Style.ShouldContain("--gauge-progress:60%;");
        state.Style.ShouldContain("--gauge-target:70%;");
        state.Style.ShouldContain("--gauge-target-angle:252deg;");
        state.Style.ShouldContain("--gauge-fill:#ffaa00;");
    }

    [Theory]
    [InlineData("eventRateMetric", "Event Rate metric")]
    [InlineData("latestEvent2Metric", "Latest Event metric #2")]
    [InlineData("latestEventMetric2", "Latest Event metric #2")]
    [InlineData("qosRetainBreakdownMetric", "QoS Retain Breakdown metric")]
    public void DashboardWidgetFormatting_FormatsMetricIdsForEditorDisplay(
        string metricName,
        string expected)
        => DashboardWidgetFormatting.MetricDisplayName(metricName).ShouldBe(expected);

    [Fact]
    public void DashboardMetricRegistry_EvaluatesKpiMetricAsWindowedScalarValue()
    {
        var registry = new DashboardMetricRegistry();
        var snapshot = new DashboardEventSnapshot(
            60,
            LatestEvent: null,
            RecentCount: 18,
            RateWindow: TimeSpan.FromSeconds(60),
            EventsPerSecond: 0.3,
            Events: [],
            BucketCounts: [1, 2, 3],
            TopicCounts: [],
            TotalPayloadBytes: 1024,
            UniqueTopicCount: 0,
            RetainedCount: 0);

        var value = registry.Evaluate(new DashboardMetricQueryDefinition("runtimeEvents", "count", "60s"), snapshot);

        value.Label.ShouldBe("Count");
        value.Value.ShouldBe(18);
        value.Unit.ShouldBe("events");
        value.FormattedValue.ShouldBe("18");
        value.FormattedValue.ShouldNotContain("events");
    }

    [Fact]
    public void DashboardMetricRegistry_AutoFormatUsesNaturalMeasureUnit()
    {
        var registry = new DashboardMetricRegistry();
        var snapshot = new DashboardEventSnapshot(
            60,
            LatestEvent: null,
            RecentCount: 18,
            RateWindow: TimeSpan.FromSeconds(60),
            EventsPerSecond: 0.3,
            Events: [],
            BucketCounts: [1, 2, 3],
            TopicCounts: [],
            TotalPayloadBytes: 2048,
            UniqueTopicCount: 4,
            RetainedCount: 2);

        var count = registry.Evaluate(
            new DashboardMetricQueryDefinition("runtimeEvents", "count", "60s", Format: "auto"),
            snapshot);
        var payload = registry.Evaluate(
            new DashboardMetricQueryDefinition("payloadInspection", "payloadBytes", "60s", Format: "auto"),
            snapshot);

        count.FormattedValue.ShouldBe("18");
        payload.FormattedValue.ShouldBe("2 KB");
    }

    [Fact]
    public void DashboardCellStyleDraft_ExposesOnlyCellContainerFields()
    {
        var fields = DashboardCellStyleDraft.Fields
            .Select(static field => field.Key)
            .ToArray();
        fields.ShouldBe([
            "background",
            "accent",
            "borderMode",
            "borderColor",
            "borderWidth",
            "radius",
            "padding",
            "widgetFit",
            "widgetAlignment"
        ]);
        fields.ShouldNotContain("surface");
        fields.ShouldNotContain("mainText");
        fields.ShouldNotContain("secondaryText");
        fields.ShouldNotContain("text");
        fields.ShouldNotContain("mutedText");

        var css = DashboardCellStyleDraft.CssVariables(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["background"] = "#161b24",
            ["surface"] = "#222222",
            ["accent"] = "#2ed3c6",
            ["mainText"] = "#ffffff",
            ["secondaryText"] = "#9fd0ff",
            ["text"] = "#123456",
            ["mutedText"] = "#abcdef"
        });

        css.ShouldContain("--dashboard-widget-bg:#161b24;");
        css.ShouldContain("--dashboard-widget-accent:#2ed3c6;");
        css.ShouldNotContain("--dashboard-widget-padding");
        css.ShouldNotContain("--dashboard-widget-surface");
        css.ShouldNotContain("--dashboard-widget-text");
        css.ShouldNotContain("--dashboard-widget-muted");
        css.ShouldNotContain("--dashboard-widget-title");
        css.ShouldNotContain("--dashboard-widget-subtitle");
        css.ShouldNotContain("--dashboard-widget-value");
    }

    [Fact]
    public void DashboardWidgetFormatting_ReadsLegacyWidgetColorVariables()
    {
        var widget = new DashboardWidgetSnapshot(
            "kpi",
            DashboardWidgetCatalog.KpiTileType,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["style.titleColor"] = "#ffffff",
                ["style.subtitleColor"] = "#9fd0ff",
                ["style.valueColor"] = "#2ed3c6"
            });

        var style = DashboardWidgetFormatting.WidgetStyle(widget);

        style.ShouldContain("--dashboard-widget-title:#ffffff;");
        style.ShouldContain("--dashboard-widget-subtitle:#9fd0ff;");
        style.ShouldContain("--dashboard-widget-value:#2ed3c6;");
    }

    [Fact]
    public void DashboardWidgetFormatting_ReadsKpiSpecificColorAndLayoutVariables()
    {
        var widget = new DashboardWidgetSnapshot(
            "kpi",
            DashboardWidgetCatalog.KpiTileType,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [DashboardWidgetCatalog.KpiTitleColorKey] = "#112233",
                [DashboardWidgetCatalog.KpiSubtitleColorKey] = "#445566",
                [DashboardWidgetCatalog.KpiValueColorKey] = "#778899",
                [DashboardWidgetCatalog.KpiTitleAlignKey] = DashboardWidgetCatalog.KpiAlignCenter,
                [DashboardWidgetCatalog.KpiValueAlignKey] = DashboardWidgetCatalog.KpiAlignRight,
                [DashboardWidgetCatalog.KpiValuePlacementKey] = DashboardWidgetCatalog.KpiValuePlacementBottom,
                [DashboardWidgetCatalog.MetricValuePaddingKey] = "22"
            });

        var style = DashboardWidgetFormatting.WidgetStyle(widget);

        style.ShouldContain("--dashboard-widget-title:#112233;");
        style.ShouldContain("--dashboard-widget-subtitle:#445566;");
        style.ShouldContain("--dashboard-widget-value:#778899;");
        style.ShouldContain("--dashboard-kpi-title-align:center;");
        style.ShouldContain("--dashboard-kpi-title-items:center;");
        style.ShouldContain("--dashboard-kpi-value-align:right;");
        style.ShouldContain("--dashboard-kpi-value-items:flex-end;");
        style.ShouldContain("--dashboard-kpi-value-placement:flex-end;");
        style.ShouldContain("--dashboard-widget-padding:22px;");
    }

    [Fact]
    public void DashboardCellStyleDraft_WritesSharedContainerBorderSettings()
    {
        var draft = DashboardCellStyleDraft.Create(new Dictionary<string, string>(StringComparer.Ordinal));

        draft.SetValue("borderMode", "none");
        draft.SetValue("borderWidth", "3");
        draft.SetValue("radius", "14");

        var style = draft.BuildStyle();

        style["borderMode"].ShouldBe("none");
        style["borderWidth"].ShouldBe("3");
        style["radius"].ShouldBe("14");
        DashboardCellStyleDraft.Fields
            .First(static field => field.Key == "borderMode")
            .Editor
            .ShouldBe(DashboardWidgetPropertyEditorKind.Select);
    }

    [Fact]
    public void DashboardCellStyleDraft_UsesZeroBorderWidthWhenBorderless()
    {
        var cellStyle = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["borderMode"] = "none",
            ["borderWidth"] = "3",
            ["radius"] = "14",
            ["padding"] = "18"
        };

        var css = DashboardCellStyleDraft.CssVariables(cellStyle);

        css.ShouldContain("--dashboard-widget-border-width:0px;");
        css.ShouldContain("--dashboard-widget-radius:14px;");
        css.ShouldContain("--dashboard-cell-padding:18px;");
        css.ShouldNotContain("--dashboard-widget-padding");
    }

    [Fact]
    public void DashboardCellStyleDraft_WritesContentFitAlignmentVariables()
    {
        var cellStyle = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["widgetFit"] = DashboardCellStyleDraft.WidgetFitContent,
            ["widgetAlignment"] = DashboardCellStyleDraft.WidgetAlignmentBottomRight
        };

        var css = DashboardCellStyleDraft.CssVariables(cellStyle);

        css.ShouldContain("--dashboard-cell-widget-flex:0 1 auto;");
        css.ShouldContain("--dashboard-cell-widget-width:auto;");
        css.ShouldContain("--dashboard-cell-widget-height:auto;");
        css.ShouldContain("--dashboard-cell-widget-max-width:100%;");
        css.ShouldContain("--dashboard-cell-widget-max-height:100%;");
        css.ShouldContain("--dashboard-cell-widget-justify:flex-end;");
        css.ShouldContain("--dashboard-cell-widget-align:flex-end;");
    }

    [Fact]
    public void DashboardWidgetSettingsProfiles_ExposeDedicatedSettingsShape()
    {
        var gauge = DashboardWidgetSettingsProfiles.For(DashboardWidgetCatalog.EventGaugeType);
        var kpi = DashboardWidgetSettingsProfiles.For(DashboardWidgetCatalog.KpiTileType);
        var rate = DashboardWidgetSettingsProfiles.For(DashboardWidgetCatalog.EventRateType);
        var latest = DashboardWidgetSettingsProfiles.For(DashboardWidgetCatalog.LatestEventType);
        var chart = DashboardWidgetSettingsProfiles.For(DashboardWidgetCatalog.LineChartType);
        var table = DashboardWidgetSettingsProfiles.For(DashboardWidgetCatalog.EventTableType);
        var tree = DashboardWidgetSettingsProfiles.For(DashboardWidgetCatalog.TopicTreeType);

        kpi.UsesMetricQuery.ShouldBeTrue();
        kpi.UsesMetricVisualization.ShouldBeTrue();
        kpi.UsesVisualMetrics.ShouldBeFalse();
        kpi.UsesMetricAggregation.ShouldBeTrue();
        kpi.UsesMetricWindow.ShouldBeTrue();
        kpi.UsesSubtitle.ShouldBeTrue();
        gauge.IsEventWidget.ShouldBeTrue();
        gauge.UsesMetricQuery.ShouldBeTrue();
        gauge.UsesVisualMetrics.ShouldBeFalse();
        gauge.UsesGaugeStyle.ShouldBeTrue();
        gauge.UsesChartType.ShouldBeFalse();
        gauge.SupportsMetricSlots.ShouldBeFalse();
        gauge.UsesMetricWindow.ShouldBeFalse();
        chart.SupportsMetricSlots.ShouldBeFalse();
        chart.UsesMetricWindow.ShouldBeTrue();
        table.IsEventWidget.ShouldBeTrue();
        table.UsesVisualMetrics.ShouldBeFalse();
        table.SupportsMetricSlots.ShouldBeFalse();
        table.UsesMetricWindow.ShouldBeFalse();
        tree.IsEventWidget.ShouldBeFalse();
        tree.IsTopicTreeWidget.ShouldBeTrue();
        tree.UsesMetricQuery.ShouldBeFalse();
        tree.UsesEventFilters.ShouldBeFalse();
        tree.UsesMetricWindow.ShouldBeFalse();

        rate.InspectorLabels.DataGroup.ShouldBe("Rate source");
        rate.UsesMetricVisualization.ShouldBeFalse();
        kpi.InspectorLabels.DataGroup.ShouldBe("KPI source");
        kpi.InspectorLabels.TimeWindowGroup.ShouldBe("Metric query");
        rate.UsesMetricWindow.ShouldBeFalse();
        rate.InspectorLabels.TimeWindowGroup.ShouldBe("Rate window");
        rate.InspectorLabels.FilterGroup.ShouldBe("Traffic filter");
        gauge.InspectorLabels.DataGroup.ShouldBe("Gauge source");
        gauge.InspectorLabels.MetricRow.ShouldBe("Gauge metric");
        gauge.InspectorLabels.GaugeRow.ShouldBe("Shape");
        latest.InspectorLabels.DataGroup.ShouldBe("Event source");
        latest.InspectorLabels.FilterGroup.ShouldBe("Match rules");
        table.InspectorLabels.DataGroup.ShouldBe("Table source");
        table.InspectorLabels.FilterGroup.ShouldBe("Row filter");
        tree.InspectorLabels.DisplayGroup.ShouldBe("Topic tree");
        tree.InspectorLabels.TopicSystemRow.ShouldBe("System topics");
        chart.InspectorLabels.SeriesGroup.ShouldBe("Chart series");
        chart.InspectorLabels.TimeWindowGroup.ShouldBe("Chart window");
    }

    [Fact]
    public void GaugeStyleOptions_ExposeOnlyImplementedShapes()
    {
        DashboardWidgetCatalog.NormalizeGaugeStyle(DashboardWidgetCatalog.GaugeStyleRing)
            .ShouldBe(DashboardWidgetCatalog.GaugeStyleRing);
        DashboardWidgetCatalog.NormalizeGaugeStyle(DashboardWidgetCatalog.GaugeStyleMeter)
            .ShouldBe(DashboardWidgetCatalog.GaugeStyleMeter);
        DashboardWidgetCatalog.NormalizeGaugeStyle("tiles")
            .ShouldBe(DashboardWidgetCatalog.GaugeStyleRing);

        var root = FindRepositoryRoot();
        var displayRows = File.ReadAllText(Path.Combine(
            root,
            "src",
            "FluxMq.UI",
            "Components",
            "Workspace",
            "DashboardInspectorDisplayModeRows.razor"));
        var dialog = File.ReadAllText(Path.Combine(
            root,
            "src",
            "FluxMq.UI",
            "Components",
            "Workspace",
            "Dialogs",
            "DashboardWidgetEditorDialog.razor"));

        displayRows.ShouldContain("GaugeStyleRing");
        displayRows.ShouldContain("GaugeStyleMeter");
        displayRows.ShouldNotContain("GaugeStyleTiles");
        displayRows.ShouldNotContain(">Tiles</button>");
        dialog.ShouldContain("GaugeStyleRing");
        dialog.ShouldContain("GaugeStyleMeter");
        dialog.ShouldNotContain("GaugeStyleTiles");
        dialog.ShouldNotContain("Text=\"Tiles\"");
    }

    [Fact]
    public void DashboardWidgetSettingsDraft_BuildsEventConfigurationForActiveFieldsOnly()
    {
        var catalog = new DashboardEventFilterCatalog();
        var widget = new DashboardWidgetSnapshot(
            "latest",
            DashboardWidgetCatalog.LatestEventType,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["title"] = "  ",
                [DashboardEventFilterCatalog.EventTypeKey] = FluxMqEventTypes.MqttMessageReceived,
                [DashboardEventFilterCatalog.TopicStartsWithKey] = "old/",
                [DashboardEventFilterCatalog.SubjectStartsWithKey] = "stale/",
                [DashboardEventFilterCatalog.StatusKey] = "received",
            });

        var draft = DashboardWidgetSettingsDraft.Create(widget, catalog);
        draft.SetFilterValue(DashboardEventFilterCatalog.TopicStartsWithKey, "factory/");
        draft.SetFilterValue(DashboardEventFilterCatalog.AttributeFilterKey("qos"), "1");
        draft.SetFilterValue(DashboardEventFilterCatalog.AttributeFilterKey("retain"), "false");

        var configuration = draft.BuildConfiguration();

        configuration["title"].ShouldBe("Latest event");
        configuration[DashboardEventFilterCatalog.EventTypeKey].ShouldBe(FluxMqEventTypes.MqttMessageReceived);
        configuration[DashboardEventFilterCatalog.TopicStartsWithKey].ShouldBe("factory/");
        configuration[DashboardEventFilterCatalog.SubjectStartsWithKey].ShouldBe(string.Empty);
        configuration[DashboardEventFilterCatalog.AttributeFilterKey("qos")].ShouldBe("1");
        configuration[DashboardEventFilterCatalog.AttributeFilterKey("retain")].ShouldBe("false");
        configuration.ContainsKey(DashboardWidgetCatalog.PrimaryMetricKey).ShouldBeFalse();
        configuration.ContainsKey(DashboardWidgetCatalog.GaugeStyleKey).ShouldBeFalse();
    }

    [Fact]
    public void DashboardWidgetSettingsDraft_WritesEventGaugeAsAppMetricConfiguration()
    {
        var draft = DashboardWidgetSettingsDraft.Create(
            new DashboardWidgetSnapshot(
                "gauge",
                DashboardWidgetCatalog.EventGaugeType,
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["title"] = "Factory load",
                    ["metric"] = "factoryLoadMetric",
                    [DashboardEventFilterCatalog.EventTypeKey] = FluxMqEventTypes.MqttMessageReceived,
                    [DashboardEventFilterCatalog.TopicStartsWithKey] = "factory/",
                    [DashboardEventFilterCatalog.StatusKey] = "received",
                    [DashboardWidgetCatalog.PrimaryMetricKey] = DashboardWidgetCatalog.MetricCurrentRate,
                    [DashboardWidgetCatalog.DisplayMetricsKey] = "messages,currentRate",
                    [DashboardWidgetCatalog.MetricCardColumnsKey] = "3",
                    [DashboardWidgetCatalog.GaugeStyleKey] = DashboardWidgetCatalog.GaugeStyleMeter,
                    [DashboardWidgetCatalog.GaugeMinKey] = "10",
                    [DashboardWidgetCatalog.GaugeMaxKey] = "250",
                    [DashboardWidgetCatalog.GaugeTargetKey] = "200",
                    [DashboardWidgetCatalog.GaugeWarningKey] = "150",
                    [DashboardWidgetCatalog.GaugeCriticalKey] = "225",
                    [DashboardWidgetCatalog.GaugeNormalColorKey] = "#123456",
                    [DashboardWidgetCatalog.GaugeWarningColorKey] = "#abcdef",
                    [DashboardWidgetCatalog.GaugeCriticalColorKey] = "#fedcba"
                }),
            new DashboardEventFilterCatalog());

        draft.UsesMetricQueryBuilder.ShouldBeTrue();
        var configuration = draft.BuildConfiguration();

        configuration["title"].ShouldBe("Factory load");
        configuration["metric"].ShouldBe("factoryLoadMetric");
        configuration[DashboardWidgetCatalog.GaugeStyleKey].ShouldBe(DashboardWidgetCatalog.GaugeStyleMeter);
        configuration[DashboardWidgetCatalog.GaugeMinKey].ShouldBe("10");
        configuration[DashboardWidgetCatalog.GaugeMaxKey].ShouldBe("250");
        configuration[DashboardWidgetCatalog.GaugeTargetKey].ShouldBe("200");
        configuration[DashboardWidgetCatalog.GaugeWarningKey].ShouldBe("150");
        configuration[DashboardWidgetCatalog.GaugeCriticalKey].ShouldBe("225");
        configuration[DashboardWidgetCatalog.GaugeNormalColorKey].ShouldBe("#123456");
        configuration[DashboardWidgetCatalog.GaugeWarningColorKey].ShouldBe("#abcdef");
        configuration[DashboardWidgetCatalog.GaugeCriticalColorKey].ShouldBe("#fedcba");
        configuration.ContainsKey(DashboardEventFilterCatalog.EventTypeKey).ShouldBeFalse();
        configuration.ContainsKey(DashboardEventFilterCatalog.TopicStartsWithKey).ShouldBeFalse();
        configuration.ContainsKey(DashboardEventFilterCatalog.StatusKey).ShouldBeFalse();
        configuration.ContainsKey(DashboardWidgetCatalog.PrimaryMetricKey).ShouldBeFalse();
        configuration.ContainsKey(DashboardWidgetCatalog.DisplayMetricsKey).ShouldBeFalse();
        configuration.ContainsKey(DashboardWidgetCatalog.MetricCardColumnsKey).ShouldBeFalse();
    }

    [Fact]
    public void DashboardWidgetSettingsDraft_BuildsTopicTreeConfiguration()
    {
        var draft = DashboardWidgetSettingsDraft.Create(
            new DashboardWidgetSnapshot(
                "topics",
                DashboardWidgetCatalog.TopicTreeType,
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["title"] = "Topics",
                    [DashboardWidgetCatalog.ExcludeSystemTopicsKey] = "false"
                }),
            new DashboardEventFilterCatalog());

        draft.Title = "Broker topics";
        draft.ExcludeSystemTopics = true;

        var configuration = draft.BuildConfiguration();

        configuration.Keys.ShouldBe(["title", DashboardWidgetCatalog.ExcludeSystemTopicsKey], ignoreOrder: true);
        configuration["title"].ShouldBe("Broker topics");
        configuration[DashboardWidgetCatalog.ExcludeSystemTopicsKey].ShouldBe("true");
    }

    [Fact]
    public void DashboardWidgetSettingsDraft_WritesStatusValueAsAppMetricConfiguration()
    {
        var draft = DashboardWidgetSettingsDraft.Create(
            new DashboardWidgetSnapshot(
                "status",
                DashboardWidgetCatalog.StatusValueType,
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["title"] = "Factory issues",
                    ["metric"] = "factoryIssuesMetric",
                    [DashboardEventFilterCatalog.EventTypeKey] = FluxMqEventTypes.MqttMessagePublished,
                    [DashboardEventFilterCatalog.TopicStartsWithKey] = "factory/",
                    [DashboardEventFilterCatalog.StatusKey] = "published",
                    [DashboardWidgetCatalog.PrimaryMetricKey] = DashboardWidgetCatalog.MetricRecent,
                    [DashboardWidgetCatalog.DisplayMetricsKey] = "messages,recent"
                }),
            new DashboardEventFilterCatalog());

        draft.UsesMetricQueryBuilder.ShouldBeTrue();
        var configuration = draft.BuildConfiguration();

        configuration["title"].ShouldBe("Factory issues");
        configuration["metric"].ShouldBe("factoryIssuesMetric");
        configuration.ContainsKey(DashboardEventFilterCatalog.EventTypeKey).ShouldBeFalse();
        configuration.ContainsKey(DashboardEventFilterCatalog.TopicStartsWithKey).ShouldBeFalse();
        configuration.ContainsKey(DashboardEventFilterCatalog.StatusKey).ShouldBeFalse();
        configuration.ContainsKey(DashboardWidgetCatalog.PrimaryMetricKey).ShouldBeFalse();
        configuration.ContainsKey(DashboardWidgetCatalog.DisplayMetricsKey).ShouldBeFalse();
        configuration.ContainsKey(DashboardWidgetCatalog.MetricCardColumnsKey).ShouldBeFalse();
    }

    [Fact]
    public void DashboardWidgetSettingsDraft_WritesValueVisualizationSettings()
    {
        var draft = DashboardWidgetSettingsDraft.Create(
            new DashboardWidgetSnapshot(
                "kpi",
                DashboardWidgetCatalog.KpiTileType,
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    [DashboardWidgetCatalog.PrimaryMetricKey] = DashboardWidgetCatalog.MetricMessages
                }),
            new DashboardEventFilterCatalog());

        draft.MetricVisualizationId = DashboardMetricVisualizationIds.Value;
        draft.MetricVisualization.ValueTitle = "Messages";
        draft.MetricVisualization.ValueSubtitle = "Total matching events";
        draft.MetricVisualization.ValueShowUnit = false;
        draft.MetricVisualization.ValueUnitText = "messages";
        draft.MetricVisualization.ValueTitleColor = "#112233";
        draft.MetricVisualization.ValueSubtitleColor = "#445566";
        draft.MetricVisualization.ValueValueColor = "#778899";
        draft.MetricVisualization.ValueUnitColor = "#99aabb";
        draft.MetricVisualization.ValueTitleAlign = DashboardWidgetCatalog.KpiAlignCenter;
        draft.MetricVisualization.ValueValueAlign = DashboardWidgetCatalog.KpiAlignRight;
        draft.MetricVisualization.ValueValuePlacement = DashboardWidgetCatalog.KpiValuePlacementMiddle;
        draft.MetricVisualization.ValuePadding = 22;

        var configuration = draft.BuildConfiguration();

        configuration[DashboardWidgetCatalog.MetricVisualizationKey].ShouldBe(DashboardMetricVisualizationIds.Value);
        configuration.ContainsKey(DashboardWidgetCatalog.PrimaryMetricKey).ShouldBeFalse();
        configuration[DashboardWidgetCatalog.MetricValueTitleKey].ShouldBe("Messages");
        configuration[DashboardWidgetCatalog.MetricValueSubtitleKey].ShouldBe("Total matching events");
        configuration[DashboardWidgetCatalog.MetricValueShowUnitKey].ShouldBe("false");
        configuration[DashboardWidgetCatalog.MetricValueUnitTextKey].ShouldBe("messages");
        configuration[DashboardWidgetCatalog.MetricValueTitleColorKey].ShouldBe("#112233");
        configuration[DashboardWidgetCatalog.MetricValueSubtitleColorKey].ShouldBe("#445566");
        configuration[DashboardWidgetCatalog.MetricValueValueColorKey].ShouldBe("#778899");
        configuration[DashboardWidgetCatalog.MetricValueUnitColorKey].ShouldBe("#99aabb");
        configuration[DashboardWidgetCatalog.MetricValueTitleAlignKey].ShouldBe(DashboardWidgetCatalog.KpiAlignCenter);
        configuration[DashboardWidgetCatalog.MetricValueValueAlignKey].ShouldBe(DashboardWidgetCatalog.KpiAlignRight);
        configuration[DashboardWidgetCatalog.MetricValueValuePlacementKey].ShouldBe(DashboardWidgetCatalog.KpiValuePlacementMiddle);
        configuration[DashboardWidgetCatalog.MetricValuePaddingKey].ShouldBe("22");
        configuration.ContainsKey("title").ShouldBeFalse();
        configuration.ContainsKey("subtitle").ShouldBeFalse();
        configuration.ContainsKey(DashboardWidgetCatalog.KpiTitleColorKey).ShouldBeFalse();
        configuration.ContainsKey(DashboardWidgetCatalog.KpiSubtitleColorKey).ShouldBeFalse();
        configuration.ContainsKey(DashboardWidgetCatalog.KpiValueColorKey).ShouldBeFalse();
        configuration.ContainsKey(DashboardWidgetCatalog.MetricDigitalStyleKey).ShouldBeFalse();
        configuration.ContainsKey(DashboardWidgetCatalog.MetricDigitalGlowKey).ShouldBeFalse();
        configuration.ContainsKey(DashboardWidgetCatalog.DisplayMetricsKey).ShouldBeFalse();
        configuration.ContainsKey(DashboardWidgetCatalog.MetricCardColumnsKey).ShouldBeFalse();
        configuration.Keys.ShouldNotContain("style.titleColor");
    }

    [Fact]
    public void DashboardWidgetSettingsDraft_WritesDigitalVisualizationSettingsOnlyWhenSelected()
    {
        var draft = DashboardWidgetSettingsDraft.Create(
            new DashboardWidgetSnapshot(
                "kpi",
                DashboardWidgetCatalog.KpiTileType,
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    [DashboardWidgetCatalog.MetricVisualizationKey] = DashboardMetricVisualizationIds.Digital,
                    [DashboardWidgetCatalog.MetricDigitalStyleKey] = DashboardWidgetCatalog.MetricDigitalStyleTerminal,
                    [DashboardWidgetCatalog.MetricDigitalGlowKey] = DashboardWidgetCatalog.MetricDigitalGlowStrong,
                    [DashboardWidgetCatalog.MetricDigitalBackgroundColorKey] = "#01020344",
                    [DashboardWidgetCatalog.MetricDigitalSegmentColorKey] = "#aabbcc80",
                    [DashboardWidgetCatalog.MetricDigitalInactiveSegmentColorKey] = "#112233",
                    [DashboardWidgetCatalog.MetricDigitalLabelColorKey] = "#ddeeff",
                    [DashboardWidgetCatalog.MetricDigitalDigitsKey] = "6"
                }),
            new DashboardEventFilterCatalog());

        draft.MetricVisualizationId.ShouldBe(DashboardMetricVisualizationIds.Digital);
        draft.MetricVisualization.DigitalStyle.ShouldBe(DashboardWidgetCatalog.MetricDigitalStyleTerminal);
        draft.MetricVisualization.DigitalGlow.ShouldBe(DashboardWidgetCatalog.MetricDigitalGlowStrong);
        draft.MetricVisualization.DigitalBackgroundColor.ShouldBe("#01020344");
        draft.MetricVisualization.DigitalSegmentColor.ShouldBe("#aabbcc80");
        draft.MetricVisualization.DigitalInactiveSegmentColor.ShouldBe("#112233");
        draft.MetricVisualization.DigitalLabelColor.ShouldBe("#ddeeff");
        draft.MetricVisualization.DigitalDigits.ShouldBe(6);

        var configuration = draft.BuildConfiguration();

        configuration[DashboardWidgetCatalog.MetricVisualizationKey]
            .ShouldBe(DashboardMetricVisualizationIds.Digital);
        configuration[DashboardWidgetCatalog.MetricDigitalStyleKey]
            .ShouldBe(DashboardWidgetCatalog.MetricDigitalStyleTerminal);
        configuration[DashboardWidgetCatalog.MetricDigitalGlowKey]
            .ShouldBe(DashboardWidgetCatalog.MetricDigitalGlowStrong);
        configuration[DashboardWidgetCatalog.MetricDigitalBackgroundColorKey].ShouldBe("#01020344");
        configuration[DashboardWidgetCatalog.MetricDigitalSegmentColorKey].ShouldBe("#aabbcc80");
        configuration[DashboardWidgetCatalog.MetricDigitalInactiveSegmentColorKey].ShouldBe("#112233");
        configuration[DashboardWidgetCatalog.MetricDigitalLabelColorKey].ShouldBe("#ddeeff");
        configuration[DashboardWidgetCatalog.MetricDigitalDigitsKey].ShouldBe("6");
        configuration.Keys.ShouldAllBe(key =>
            string.Equals(key, DashboardWidgetCatalog.MetricVisualizationKey, StringComparison.Ordinal) ||
            key.StartsWith("metric.digital.", StringComparison.Ordinal));
    }

    [Fact]
    public void DashboardWidgetSettingsDraft_WritesEventCounterAsMetricQueryConfiguration()
    {
        var draft = DashboardWidgetSettingsDraft.Create(
            new DashboardWidgetSnapshot(
                "counter",
                DashboardWidgetCatalog.EventCounterType,
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["title"] = "Factory events",
                    ["metric"] = "factoryEventsMetric",
                    [DashboardEventFilterCatalog.EventTypeKey] = FluxMqEventTypes.MqttMessagePublished,
                    [DashboardEventFilterCatalog.TopicStartsWithKey] = "factory/",
                    [DashboardEventFilterCatalog.StatusKey] = "published",
                    [DashboardWidgetCatalog.PrimaryMetricKey] = DashboardWidgetCatalog.MetricPayloadBytes
                }),
            new DashboardEventFilterCatalog());

        draft.UsesMetricQueryBuilder.ShouldBeTrue();
        var configuration = draft.BuildConfiguration();

        configuration["title"].ShouldBe("Factory events");
        configuration["metric"].ShouldBe("factoryEventsMetric");
        configuration.ContainsKey(DashboardEventFilterCatalog.EventTypeKey).ShouldBeFalse();
        configuration.ContainsKey(DashboardEventFilterCatalog.TopicStartsWithKey).ShouldBeFalse();
        configuration.ContainsKey(DashboardEventFilterCatalog.StatusKey).ShouldBeFalse();
        configuration.ContainsKey(DashboardWidgetCatalog.PrimaryMetricKey).ShouldBeFalse();
    }

    [Fact]
    public void DashboardWidgetSettingsDraft_WritesEventRateAsMetricQueryConfiguration()
    {
        var draft = DashboardWidgetSettingsDraft.Create(
            new DashboardWidgetSnapshot(
                "rate",
                DashboardWidgetCatalog.EventRateType,
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["title"] = "Factory rate",
                    ["metric"] = "factoryRateMetric",
                    [DashboardEventFilterCatalog.EventTypeKey] = FluxMqEventTypes.MqttMessagePublished,
                    [DashboardEventFilterCatalog.TopicStartsWithKey] = "factory/",
                    [DashboardEventFilterCatalog.StatusKey] = "published",
                    [DashboardWidgetCatalog.PrimaryMetricKey] = DashboardWidgetCatalog.MetricCurrentRate
                }),
            new DashboardEventFilterCatalog());

        draft.UsesMetricQueryBuilder.ShouldBeTrue();
        var configuration = draft.BuildConfiguration();

        configuration["title"].ShouldBe("Factory rate");
        configuration["metric"].ShouldBe("factoryRateMetric");
        configuration.ContainsKey(DashboardEventFilterCatalog.EventTypeKey).ShouldBeFalse();
        configuration.ContainsKey(DashboardEventFilterCatalog.TopicStartsWithKey).ShouldBeFalse();
        configuration.ContainsKey(DashboardEventFilterCatalog.StatusKey).ShouldBeFalse();
        configuration.ContainsKey(DashboardWidgetCatalog.PrimaryMetricKey).ShouldBeFalse();
    }

    [Fact]
    public void DashboardWidgetSettingsDraft_WritesRateTileAsAppMetricConfiguration()
    {
        var draft = DashboardWidgetSettingsDraft.Create(
            new DashboardWidgetSnapshot(
                "rateTile",
                DashboardWidgetCatalog.RateTileType,
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["title"] = "Factory rate",
                    ["metric"] = "factoryRateMetric",
                    [DashboardEventFilterCatalog.EventTypeKey] = FluxMqEventTypes.MqttMessagePublished,
                    [DashboardEventFilterCatalog.TopicStartsWithKey] = "factory/",
                    [DashboardEventFilterCatalog.StatusKey] = "published",
                    [DashboardWidgetCatalog.PrimaryMetricKey] = DashboardWidgetCatalog.MetricCurrentRate
                }),
            new DashboardEventFilterCatalog());

        draft.UsesMetricQueryBuilder.ShouldBeTrue();
        var configuration = draft.BuildConfiguration();

        configuration["title"].ShouldBe("Factory rate");
        configuration["metric"].ShouldBe("factoryRateMetric");
        configuration.ContainsKey(DashboardEventFilterCatalog.EventTypeKey).ShouldBeFalse();
        configuration.ContainsKey(DashboardEventFilterCatalog.TopicStartsWithKey).ShouldBeFalse();
        configuration.ContainsKey(DashboardEventFilterCatalog.StatusKey).ShouldBeFalse();
        configuration.ContainsKey(DashboardWidgetCatalog.PrimaryMetricKey).ShouldBeFalse();
    }

    [Fact]
    public void DashboardInspectorMetricBindingState_InitializesSingleMetricWithoutSlots()
    {
        var metrics = DashboardInspectorMetricBindingState.Initialize(
            ["secondary", "primary"],
            "primary",
            supportsSlots: false);

        metrics.ShouldBe(["primary"]);
    }

    [Fact]
    public void DashboardInspectorMetricBindingState_KeepsPrimaryFirstWithSlots()
    {
        var metrics = DashboardInspectorMetricBindingState.Initialize(
            ["secondary", "secondary", "third"],
            "primary",
            supportsSlots: true);

        metrics.ShouldBe(["primary", "secondary", "third"]);
    }

    [Fact]
    public void DashboardInspectorMetricBindingState_CurrentUsesFallbackWhenEmpty()
    {
        var metrics = DashboardInspectorMetricBindingState.Current(
            supportsSlots: true,
            metrics: [],
            primaryMetric: null,
            fallbackMetric: "fallback");

        metrics.ShouldBe(["fallback"]);
    }

    [Fact]
    public void DashboardInspectorMetricBindingState_EnsuresPrimaryForSlotMode()
    {
        var metrics = DashboardInspectorMetricBindingState.EnsurePrimary(
            ["secondary"],
            "primary",
            supportsSlots: true);

        metrics.ShouldBe(["primary", "secondary"]);
    }

    [Fact]
    public void DashboardInspectorMetricBindingState_AddRemoveAndMoveMutateBindingList()
    {
        var metrics = new List<string> { "primary", "secondary" };

        DashboardInspectorMetricBindingState.TryAdd(metrics, "third").ShouldBeTrue();
        DashboardInspectorMetricBindingState.TryAdd(metrics, "third").ShouldBeFalse();
        DashboardInspectorMetricBindingState.TryMove(metrics, "third", -2).ShouldBeTrue();
        var primary = DashboardInspectorMetricBindingState.Remove(metrics, "third", "third");

        metrics.ShouldBe(["primary", "secondary"]);
        primary.ShouldBe("primary");
    }

    [Fact]
    public void DashboardWidgetSettingsDraft_ResetToDefaultConfiguration_RestoresKpiDefaults()
    {
        var draft = DashboardWidgetSettingsDraft.Create(
            new DashboardWidgetSnapshot(
                "kpi",
                DashboardWidgetCatalog.KpiTileType,
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["title"] = "Custom title",
                    ["subtitle"] = "Custom subtitle",
                    ["metric"] = "receivedMetric",
                    [DashboardEventFilterCatalog.EventTypeKey] = FluxMqEventTypes.MqttMessageReceived,
                    [DashboardEventFilterCatalog.TopicStartsWithKey] = "factory/",
                    [DashboardEventFilterCatalog.StatusKey] = "received",
                    [DashboardWidgetCatalog.PrimaryMetricKey] = DashboardWidgetCatalog.MetricPayloadBytes,
                    [DashboardWidgetCatalog.KpiTitleColorKey] = "#112233",
                    [DashboardWidgetCatalog.KpiSubtitleColorKey] = "#445566",
                    [DashboardWidgetCatalog.KpiValueColorKey] = "#778899",
                    [DashboardWidgetCatalog.KpiTitleAlignKey] = DashboardWidgetCatalog.KpiAlignCenter,
                    [DashboardWidgetCatalog.KpiValueAlignKey] = DashboardWidgetCatalog.KpiAlignRight,
                    [DashboardWidgetCatalog.KpiValuePlacementKey] = DashboardWidgetCatalog.KpiValuePlacementBottom
                }),
            new DashboardEventFilterCatalog());

        var defaults = DashboardWidgetModuleCatalog
            .Find(DashboardWidgetCatalog.KpiTileType)!
            .DefaultConfiguration;

        draft.ResetToDefaultConfiguration(defaults);

        var configuration = draft.BuildConfiguration();

        configuration["metric"].ShouldBe("receivedMetric");
        configuration[DashboardWidgetCatalog.MetricVisualizationKey].ShouldBe(DashboardMetricVisualizationIds.Value);
        configuration.ContainsKey(DashboardEventFilterCatalog.EventTypeKey).ShouldBeFalse();
        configuration.ContainsKey(DashboardEventFilterCatalog.TopicStartsWithKey).ShouldBeFalse();
        configuration.ContainsKey(DashboardEventFilterCatalog.StatusKey).ShouldBeFalse();
        configuration.ContainsKey(DashboardWidgetCatalog.PrimaryMetricKey).ShouldBeFalse();
        configuration[DashboardWidgetCatalog.MetricValueTitleKey].ShouldBe("Messages");
        configuration[DashboardWidgetCatalog.MetricValueSubtitleKey].ShouldBe("Total matching events");
        configuration[DashboardWidgetCatalog.MetricValueTitleColorKey].ShouldBe(DashboardWidgetCatalog.KpiDefaultTitleColor);
        configuration[DashboardWidgetCatalog.MetricValueSubtitleColorKey].ShouldBe(DashboardWidgetCatalog.KpiDefaultSubtitleColor);
        configuration[DashboardWidgetCatalog.MetricValueValueColorKey].ShouldBe(DashboardWidgetCatalog.KpiDefaultValueColor);
        configuration[DashboardWidgetCatalog.MetricValueTitleAlignKey].ShouldBe(DashboardWidgetCatalog.KpiAlignLeft);
        configuration[DashboardWidgetCatalog.MetricValueValueAlignKey].ShouldBe(DashboardWidgetCatalog.KpiAlignLeft);
        configuration[DashboardWidgetCatalog.MetricValueValuePlacementKey].ShouldBe(DashboardWidgetCatalog.KpiValuePlacementTop);
        configuration.ContainsKey("title").ShouldBeFalse();
        configuration.ContainsKey("subtitle").ShouldBeFalse();
        configuration.ContainsKey(DashboardWidgetCatalog.KpiTitleColorKey).ShouldBeFalse();
    }

    [Fact]
    public void FluxMetricQueryDraft_UsesMetricFiltersBeforeLegacyWidgetFilters()
    {
        var metric = new FluxMetricDefinition(
            "publishedMetric",
            FluxMetricCatalog.RuntimeEventsSource,
            FluxMetricCatalog.MeasureRate,
            "300s",
            eventType: FluxMqEventTypes.MqttMessagePublished,
            topicStartsWith: "metric/",
            status: "published",
            format: FluxMetricCatalog.FormatNumber);
        var legacyFilters = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [FluxMetricCatalog.TopicStartsWithKey] = "legacy/",
                [FluxMetricCatalog.AttributeFilterKey("qos")] = "1",
                [FluxMetricCatalog.AttributeFilterKey("retain")] = "false"
            };

        var draft = FluxMetricQueryDraft.Create(
            metric,
            legacyFilters,
            DashboardWidgetCatalog.KpiTileType);
        var query = draft.BuildDefinition("publishedMetric");

        query.Measure.ShouldBe(FluxMetricCatalog.MeasureRate);
        query.Window.ShouldBe("300s");
        query.EventType.ShouldBe(FluxMqEventTypes.MqttMessagePublished);
        query.TopicStartsWith.ShouldBe("metric/");
        query.Status.ShouldBeNull();
        query.AdditionalFilters[FluxMetricCatalog.AttributeFilterKey("qos")].ShouldBe("1");
        query.AdditionalFilters[FluxMetricCatalog.AttributeFilterKey("retain")].ShouldBe("false");
    }

    [Fact]
    public void FluxMetricCatalog_ExposesMeasureMetadataForAllAggregations()
    {
        var registry = new DashboardMetricRegistry();
        var catalog = FluxMetricCatalog.Shared;

        foreach (var aggregation in registry.Aggregations)
        {
            var measure = catalog.FindMeasure(aggregation.Id);

            measure.Id.ShouldBe(aggregation.Id);
            measure.Label.ShouldNotBeNullOrWhiteSpace();
            measure.Description.ShouldNotBeNullOrWhiteSpace();
            measure.Explanation.ShouldNotBeNullOrWhiteSpace();
            measure.Calculation.ShouldNotBeNullOrWhiteSpace();
            measure.BestFor.ShouldNotBeNullOrWhiteSpace();
            measure.CompatibleSources.ShouldNotBeEmpty();
            measure.CompatibleSources.ShouldContain(measure.DefaultSource);
            measure.DefaultFormat.ShouldNotBeNullOrWhiteSpace();
        }
    }

    [Fact]
    public void FluxMetricQueryDraft_AppliesMeasureDefaultsUntilCustomized()
    {
        var draft = CreateKpiQueryDraft();

        draft.SetAggregation(FluxMetricCatalog.MeasurePayloadBytes);
        draft.Source.ShouldBe(FluxMetricCatalog.PayloadInspectionSource);
        draft.Format.ShouldBe(FluxMetricCatalog.FormatBytes);

        draft.SetSource(FluxMetricCatalog.RuntimeEventsSource);
        draft.SetFormat(FluxMetricCatalog.FormatNumber);
        draft.SetAggregation(FluxMetricCatalog.MeasureTopics);

        draft.Source.ShouldBe(FluxMetricCatalog.RuntimeEventsSource);
        draft.Format.ShouldBe(FluxMetricCatalog.FormatNumber);
    }

    [Fact]
    public void FluxMetricQueryDraft_DisallowsUnsupportedSourceSelection()
    {
        var draft = CreateKpiQueryDraft();

        draft.SetSource(FluxMetricCatalog.PayloadInspectionSource);

        draft.Source.ShouldBe(FluxMetricCatalog.RuntimeEventsSource);
        FluxMetricCatalog.Shared
            .IsSourceCompatible(draft.Aggregation, FluxMetricCatalog.PayloadInspectionSource)
            .ShouldBeFalse();
    }

    [Fact]
    public void FluxMetricQueryDraft_ClearsActiveFilterChipFields()
    {
        var draft = CreateKpiQueryDraft();
        draft.SetEventType(FluxMqEventTypes.MqttMessagePublished);
        draft.Status = "published";
        draft.SetFilterValue(FluxMetricCatalog.TopicStartsWithKey, "factory/");
        draft.SetFilterValue(FluxMetricCatalog.AttributeFilterKey("qos"), "1");
        draft.SetFilterValue(FluxMetricCatalog.AttributeFilterKey("retain"), "false");

        var chips = FluxMetricQuerySummary.ActiveFilterChips(draft.BuildDefinition());
        chips.Select(static chip => chip.Key).ShouldContain(FluxMetricCatalog.TopicStartsWithKey);
        chips.Select(static chip => chip.Key).ShouldContain(FluxMetricCatalog.AttributeFilterKey("qos"));
        chips.Select(static chip => chip.Key).ShouldContain(FluxMetricCatalog.AttributeFilterKey("retain"));

        draft.ClearFilter(FluxMetricCatalog.TopicStartsWithKey);
        draft.ClearFilter(FluxMetricCatalog.AttributeFilterKey("qos"));
        draft.ClearFilter(FluxMetricCatalog.AttributeFilterKey("retain"));
        draft.ClearFilter(FluxMetricCatalog.StatusKey);

        var query = draft.BuildDefinition();
        query.TopicStartsWith.ShouldBeNull();
        query.Status.ShouldBeNull();
        query.AdditionalFilters.ContainsKey(FluxMetricCatalog.AttributeFilterKey("qos")).ShouldBeFalse();
        query.AdditionalFilters.ContainsKey(FluxMetricCatalog.AttributeFilterKey("retain")).ShouldBeFalse();
    }

    [Fact]
    public void FluxMetricQueryDraft_ClearsRedundantStatusForSingleStatusEvents()
    {
        var draft = CreateKpiQueryDraft();
        draft.SetEventType(FluxMqEventTypes.MqttMessagePublished);
        draft.Status = "published";

        var query = draft.BuildDefinition();

        query.Status.ShouldBeNull();
    }

    [Fact]
    public void FluxMetricQueryDraft_KeepsMeaningfulStatusForMultiStatusEvents()
    {
        var draft = CreateKpiQueryDraft();
        draft.SetEventType(FluxMqEventTypes.JsonSchemaValidated);
        draft.Status = "invalid";

        var query = draft.BuildDefinition();

        query.Status.ShouldBe("invalid");
    }

    [Theory]
    [InlineData("30s", true, "30s")]
    [InlineData("1m", true, "1m")]
    [InlineData("2h", true, "2h")]
    [InlineData("0s", false, "")]
    [InlineData("1d", false, "")]
    [InlineData("90000s", false, "")]
    public void FluxMetricQueryDraft_ValidatesCustomWindows(
        string value,
        bool expectedValid,
        string expectedNormalized)
    {
        FluxMetricQueryDraft.TryNormalizeWindow(value, out var normalized).ShouldBe(expectedValid);
        normalized.ShouldBe(expectedNormalized);
    }

    [Theory]
    [InlineData("60s", "1m")]
    [InlineData("300s", "5m")]
    [InlineData("900s", "15m")]
    [InlineData("2h", "2h")]
    public void DashboardMetricQuerySummary_UsesHumanWindowLabels(
        string value,
        string expectedLabel)
        => DashboardMetricQuerySummary.WindowLabel(value).ShouldBe(expectedLabel);

    [Fact]
    public void DashboardMetricQuerySummary_DescribesMeasureWindowAndFilters()
    {
        var query = new DashboardMetricQueryDefinition(
            "runtimeEvents",
            "count",
            "60s",
            EventType: FluxMqEventTypes.MqttMessagePublished,
            TopicStartsWith: "factory/",
            TopicNotStartsWith: "$SYS/",
            Status: "published",
            AdditionalFilters: new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [DashboardEventFilterCatalog.AttributeFilterKey("qos")] = "1"
            });

        var summary = DashboardMetricQuerySummary.Describe(
            query,
            new DashboardEventFilterCatalog(),
            new DashboardMetricRegistry());
        var sentence = DashboardMetricQuerySummary.DescribeSentence(
            query,
            new DashboardEventFilterCatalog(),
            new DashboardMetricRegistry());

        summary.ShouldContain("Count of MQTT published messages during last 1m");
        sentence.ShouldBe("Show Count of MQTT published messages during last 1m where topic starts factory/, exclude $SYS/, QoS 1");
        summary.ShouldContain("topic starts factory/");
        summary.ShouldContain("exclude $SYS/");
        summary.ShouldNotContain("status published");
        summary.ShouldContain("QoS 1");
    }

    [Fact]
    public void DashboardMetricQuerySummary_DescribesNoFiltersAsSentence()
    {
        var sentence = DashboardMetricQuerySummary.DescribeSentence(
            new DashboardMetricQueryDefinition("runtimeEvents", "count", "60s"),
            new DashboardEventFilterCatalog(),
            new DashboardMetricRegistry());

        sentence.ShouldBe("Show Count of all runtime events during last 1m where all events match");
    }

    [Fact]
    public void DashboardMetricQuerySummary_IgnoresUnsupportedStatusForAllRuntimeEvents()
    {
        var sentence = DashboardMetricQuerySummary.DescribeSentence(
            new DashboardMetricQueryDefinition(
                "runtimeEvents",
                "count",
                "60s",
                Status: "published"),
            new DashboardEventFilterCatalog(),
            new DashboardMetricRegistry());

        sentence.ShouldBe("Show Count of all runtime events during last 1m where all events match");
        DashboardMetricQuerySummary
            .ActiveFilters(
                new DashboardMetricQueryDefinition("runtimeEvents", "count", "60s", Status: "published"),
                new DashboardEventFilterCatalog())
            .ShouldBeEmpty();
    }

    [Fact]
    public void DashboardMetricQuerySummary_LabelsAssertionFilterAsAssertion()
    {
        var sentence = DashboardMetricQuerySummary.DescribeSentence(
            new DashboardMetricQueryDefinition(
                "runtimeEvents",
                "count",
                "60s",
                EventType: FluxMqEventTypes.AssertionEvaluated,
                Status: "passed",
                AdditionalFilters: new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    [DashboardEventFilterCatalog.SubjectStartsWithKey] = "QoS at least once"
                }),
            new DashboardEventFilterCatalog(),
            new DashboardMetricRegistry());

        sentence.ShouldBe("Show Count of assertions during last 1m where status passed, assertion QoS at least once");
    }

    [Fact]
    public void DashboardMetricQuerySummary_LabelsFileFilterAsFilePath()
    {
        var sentence = DashboardMetricQuerySummary.DescribeSentence(
            new DashboardMetricQueryDefinition(
                "runtimeEvents",
                "count",
                "60s",
                EventType: FluxMqEventTypes.FileWritten,
                AdditionalFilters: new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    [DashboardEventFilterCatalog.SubjectStartsWithKey] = "logs/"
                }),
            new DashboardEventFilterCatalog(),
            new DashboardMetricRegistry());

        sentence.ShouldBe("Show Count of file writes during last 1m where file path logs/");
    }

    [Fact]
    public void DashboardMetricQueryMapper_RoundTripsDashboardMetricJsonShape()
    {
        var query = new DashboardMetricQueryDefinition(
            "runtimeEvents",
            "rate",
            "300s",
            GroupBy: "topic",
            EventType: FluxMqEventTypes.MqttMessageReceived,
            TopicStartsWith: "factory/",
            TopicNotStartsWith: "$SYS/",
            Status: "received",
            AdditionalFilters: new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [DashboardEventFilterCatalog.AttributeFilterKey("qos")] = "1",
                [DashboardEventFilterCatalog.AttributeFilterKey("retain")] = "false"
            });

        var metric = DashboardMetricQueryMapper.ToFluxMetricDefinition("receivedMetric", query);
        var roundTrip = DashboardMetricQueryMapper.ToDashboardQuery(metric);

        metric.Name.ShouldBe("receivedMetric");
        metric.Mode.ShouldBe(MetricDefinitionMode.Builder);
        metric.ExportPolicy.Enabled.ShouldBeFalse();
        roundTrip.Source.ShouldBe(query.Source);
        roundTrip.Aggregation.ShouldBe(query.Aggregation);
        roundTrip.Window.ShouldBe(query.Window);
        roundTrip.GroupBy.ShouldBe(query.GroupBy);
        roundTrip.EventType.ShouldBe(query.EventType);
        roundTrip.TopicStartsWith.ShouldBe(query.TopicStartsWith);
        roundTrip.TopicNotStartsWith.ShouldBe(query.TopicNotStartsWith);
        roundTrip.Status.ShouldBe(query.Status);
        roundTrip.AdditionalFilters.ContainsKey(DashboardEventFilterCatalog.AttributeFilterKey("qos")).ShouldBeTrue();
        roundTrip.AdditionalFilters.ContainsKey(DashboardEventFilterCatalog.AttributeFilterKey("retain")).ShouldBeTrue();
    }

    [Fact]
    public void DashboardMetricQueryMapper_CreatesDashboardMetricSnapshot()
    {
        var query = new DashboardMetricQueryDefinition(
            "runtimeEvents",
            "count",
            "60s",
            GroupBy: "topic",
            EventType: FluxMqEventTypes.MqttMessagePublished,
            TopicStartsWith: "factory/",
            TopicNotStartsWith: "$SYS/",
            Status: "published",
            Format: "bytes",
            AdditionalFilters: new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [DashboardEventFilterCatalog.AttributeFilterKey("qos")] = "1"
            });

        var metric = DashboardMetricQueryMapper.ToDashboardMetricSnapshot("publishedMetric", query);

        metric.Name.ShouldBe("publishedMetric");
        metric.Source.ShouldBe(query.Source);
        metric.Aggregation.ShouldBe(query.Aggregation);
        metric.Window.ShouldBe(query.Window);
        metric.GroupBy.ShouldBe(query.GroupBy);
        metric.ReadFilter(DashboardEventFilterCatalog.EventTypeKey).ShouldBe(FluxMqEventTypes.MqttMessagePublished);
        metric.ReadFilter(DashboardEventFilterCatalog.TopicStartsWithKey).ShouldBe("factory/");
        metric.ReadFilter(DashboardEventFilterCatalog.TopicNotStartsWithKey).ShouldBe("$SYS/");
        metric.ReadFilter(DashboardEventFilterCatalog.StatusKey).ShouldBe("published");
        metric.ReadFilter(DashboardEventFilterCatalog.AttributeFilterKey("qos")).ShouldBe("1");
        metric.ReadFormat("unit").ShouldBe("bytes");
    }

    [Fact]
    public void DashboardInspector_DelegatesMetricSnapshotMapping()
    {
        var root = FindRepositoryRoot();
        var inspector = File.ReadAllText(Path.Combine(
            root,
            "src",
            "FluxMq.UI",
            "Components",
            "Workspace",
            "DashboardInspector.razor"));
        var mapper = File.ReadAllText(Path.Combine(
            root,
            "src",
            "FluxMq.UI",
            "Services",
            "DashboardMetricQueryMapper.cs"));

        inspector.ShouldNotContain("DashboardMetricSnapshot ToMetricSnapshot");
        inspector.ShouldNotContain("private static void AddIfPresent");
        inspector.ShouldNotContain("new FluxMetricResolver");
        inspector.ShouldNotContain("Project.GetMetricArtifact(");
        inspector.ShouldContain("DashboardMetricQueryMapper.ToDashboardMetricSnapshot");
        mapper.ShouldContain("ToDashboardMetricSnapshot");
    }

    [Fact]
    public void DashboardMetricReferenceResolver_ResolvesAppMetricParameters()
    {
        var project = new FlowWorkspaceService(new FlowDefinitionComposer());
        project.AddMetric("publishedMetric");
        project.UpdateMetric(
            "publishedMetric",
            new FluxMetricArtifactDefinition
            {
                DisplayName = "Published metric",
                Definition = new FluxMetricDefinition(
                    "Published metric",
                    FluxMetricCatalog.RuntimeEventsSource,
                    FluxMetricCatalog.MeasureCount,
                    "60s",
                    eventType: FluxMqEventTypes.MqttMessagePublished,
                    topicStartsWith: "default/",
                    status: "published"),
                Parameters =
                [
                    new FluxMetricParameterDefinition
                    {
                        Id = "topic",
                        Label = "Topic",
                        Target = FluxMetricParameterTargets.TopicStartsWith,
                        DefaultValue = "default/"
                    }
                ]
            });
        var parameterValues = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["topic"] = "factory/line-a/"
        };

        var definition = DashboardMetricReferenceResolver.ResolveAppMetricDefinition(
            project,
            "publishedMetric",
            parameterValues);
        var snapshot = DashboardMetricReferenceResolver.ResolveAppMetricSnapshot(
            project,
            "publishedMetric",
            parameterValues);

        definition.ShouldNotBeNull();
        definition.TopicStartsWith.ShouldBe("factory/line-a/");
        snapshot.ShouldNotBeNull();
        snapshot.ReadFilter(DashboardEventFilterCatalog.TopicStartsWithKey).ShouldBe("factory/line-a/");
        snapshot.ReadFilter(DashboardEventFilterCatalog.EventTypeKey).ShouldBe(FluxMqEventTypes.MqttMessagePublished);
    }

    [Fact]
    public void DashboardMetricQueryPreviewFactory_UsesSampleWhenNoLiveEventsMatch()
    {
        var service = new FlowWorkspaceService(new FlowDefinitionComposer());
        var widget = new DashboardWidgetSnapshot(
            "kpi",
            DashboardWidgetCatalog.KpiTileType,
            new Dictionary<string, string>(StringComparer.Ordinal));
        var query = new DashboardMetricQueryDefinition(
            "runtimeEvents",
            "count",
            "60s",
            EventType: FluxMqEventTypes.MqttMessagePublished,
            TopicStartsWith: "factory/",
            Status: "published");

        var preview = DashboardMetricQueryPreviewFactory.Create(
            query,
            widget,
            service,
            new DashboardMetricRegistry(),
            new DashboardEventFilterCatalog());

        preview.IsLive.ShouldBeFalse();
        preview.SourceLabel.ShouldBe("Sample");
        preview.WindowEventCount.ShouldBeGreaterThan(0);
        preview.TotalMatchCount.ShouldBeGreaterThanOrEqualTo(preview.WindowEventCount);
        preview.MatchingEventCount.ShouldBe(preview.WindowEventCount);
        preview.EmptyReason.ShouldBe("No live events match this query in the selected window. Showing generated sample data.");
    }

    [Fact]
    public void DashboardMetricQueryPreviewFactory_UsesLiveEventsWhenPresent()
    {
        var service = new FlowWorkspaceService(new FlowDefinitionComposer());
        service.RecordManualMqttPublish("factory/one", """{"hello":"fluxmq"}""", 0, retain: false, "local-broker");
        service.RecordManualMqttPublish("other/two", """{"hello":"fluxmq"}""", 0, retain: false, "local-broker");
        var widget = new DashboardWidgetSnapshot(
            "kpi",
            DashboardWidgetCatalog.KpiTileType,
            new Dictionary<string, string>(StringComparer.Ordinal));
        var query = new DashboardMetricQueryDefinition(
            "runtimeEvents",
            "count",
            "60s",
            EventType: FluxMqEventTypes.MqttMessagePublished,
            TopicStartsWith: "factory/",
            Status: "published");

        var preview = DashboardMetricQueryPreviewFactory.Create(
            query,
            widget,
            service,
            new DashboardMetricRegistry(),
            new DashboardEventFilterCatalog());

        preview.IsLive.ShouldBeTrue();
        preview.SourceLabel.ShouldBe("Live");
        preview.WindowEventCount.ShouldBe(1);
        preview.TotalMatchCount.ShouldBe(1);
        preview.MatchingEventCount.ShouldBe(preview.WindowEventCount);
        preview.Value.Value.ShouldBe(1);
        preview.EmptyReason.ShouldBeEmpty();
    }

    [Fact]
    public void DashboardMetricQueryBuilder_UsesMudSelectControls()
    {
        var path = Path.Combine(
            FindRepositoryRoot(),
            "src",
            "FluxMq.UI",
            "Components",
            "Workspace",
            "DashboardMetricQueryBuilder.razor");
        var markup = File.ReadAllText(path);

        markup.ShouldNotContain("<select", Case.Insensitive);
        markup.ShouldContain("Metric sentence");
        markup.ShouldContain("<MudSwitch T=\"bool\"");
        markup.ShouldContain("ShowHelpChanged");
        markup.ShouldContain("SetShowHelpAsync");
        markup.ShouldContain("dashboard-query-builder-sentence-line");
        markup.ShouldContain("dashboard-query-builder-sentence-token");
        markup.ShouldContain("FocusSentencePart");
        markup.ShouldContain("dashboard-query-builder-section focused");
        markup.ShouldContain("<MudToggleGroup T=\"string\"");
        markup.ShouldContain("<MudToggleItem T=\"string\"");
        markup.ShouldContain("dashboard-query-builder-measure-summary");
        markup.ShouldContain("dashboard-query-builder-measure-copy");
        markup.ShouldContain("dashboard-query-builder-measure-meta");
        markup.ShouldContain("Selected");
        markup.ShouldContain("@if (ShowHelp)");
        markup.ShouldContain("Calculates");
        markup.ShouldContain("Good for");
        markup.ShouldContain("Source");
        markup.ShouldContain("@CurrentMeasureDefaultSourceLabel");
        markup.ShouldContain("Unit");
        markup.ShouldNotContain("Default source");
        markup.ShouldNotContain("dashboard-query-builder-measure-detail");
        markup.ShouldNotContain("dashboard-query-builder-measure-title");
        markup.ShouldNotContain("dashboard-query-builder-measure-facts");
        markup.ShouldContain("Selected source");
        markup.ShouldContain("Choose one compatible projection");
        markup.ShouldContain("Available sources");
        markup.ShouldContain("Recommended default for");
        markup.ShouldContain("Compatible with");
        markup.ShouldContain("MetricCatalog.IsSourceCompatible(Draft.Aggregation, source.Id)");
        markup.ShouldContain("FluxMetricQueryDraft");
        markup.ShouldContain("FluxMetricCatalog.Shared");
        markup.ShouldContain("FluxMetricQuerySummary");
        markup.ShouldNotContain("DashboardMetricQueryBuilderDraft");
        markup.ShouldNotContain("DashboardMetricQueryBuilderCatalog");
        markup.ShouldNotContain("Disabled=\"@option.Disabled\"");
        markup.ShouldNotContain("does not use this source");
        markup.ShouldContain("Preset window");
        markup.ShouldContain("Custom duration");
        markup.ShouldContain("CustomWindowInputValue");
        markup.ShouldContain("CurrentWindowHasPreset");
        markup.ShouldContain("No custom duration active.");
        markup.ShouldContain("new(\"1m\", \"1m\")");
        markup.ShouldContain("new(\"5m\", \"5m\")");
        markup.ShouldNotContain("new(\"60s\", \"1m\")");
        markup.ShouldContain("Current window");
        markup.ShouldContain("Pick a preset or type a custom duration.");
        markup.ShouldContain("Maximum 24h.");
        markup.ShouldContain("dashboard-query-builder-format-toggle");
        markup.ShouldContain("dashboard-query-builder-format-detail");
        markup.ShouldContain("Selected format");
        markup.ShouldContain("Auto will format this measure");
        markup.ShouldContain("Number format");
        markup.ShouldContain("Preview updates immediately with the current query.");
        markup.ShouldContain("All runtime events");
        markup.ShouldContain("EmptyFilterSentenceToken");
        markup.ShouldContain("dashboard-query-builder-match-controls");
        markup.ShouldContain("dashboard-query-builder-match-details");
        markup.ShouldContain("dashboard-query-builder-match-chips");
        markup.ShouldContain("Filters");
        markup.ShouldContain("Event");
        markup.ShouldContain("ShouldShowStatus");
        markup.ShouldContain("Clear all");
        markup.ShouldContain("ClearAllFiltersAsync");
        markup.ShouldNotContain("dashboard-query-builder-match-summary");
        markup.ShouldNotContain("Current match");
        markup.ShouldNotContain("MatchSummary");
        markup.ShouldNotContain("All runtime events are included.");
        markup.ShouldNotContain("This is the broadest query mode");
        markup.ShouldNotContain("EmptyFilterLabel");
        markup.ShouldNotContain("dashboard-query-builder-active-filter-heading");
        markup.ShouldNotContain("EventGateHelp");
        markup.ShouldNotContain("CurrentEventDetailHelp");
        markup.ShouldContain("dashboard-query-builder-detail-panels");
        markup.ShouldContain("dashboard-query-builder-detail-panel");
        markup.ShouldContain("RenderMqttDetailFilters");
        markup.ShouldContain("Topic match");
        markup.ShouldContain("MQTT delivery");
        markup.ShouldContain("dashboard-query-builder-mqtt-details");
        markup.ShouldContain("dashboard-query-builder-filter-toggle");
        markup.ShouldContain("QosFilterOptions");
        markup.ShouldContain("RetainFilterOptions");
        markup.ShouldContain("RenderSchemaDetailFilters");
        markup.ShouldContain("Message match");
        markup.ShouldContain("Schema match");
        markup.ShouldContain("dashboard-query-builder-schema-details");
        markup.ShouldContain("IsSchemaIdFilter");
        markup.ShouldContain("RenderFileDetailFilters");
        markup.ShouldContain("File match");
        markup.ShouldContain("dashboard-query-builder-file-details");
        markup.ShouldContain("IsSubjectFilter");
        markup.ShouldContain("RenderAssertionDetailFilters");
        markup.ShouldContain("Assertion match");
        markup.ShouldContain("Any assertion");
        markup.ShouldContain("AssertionNameOptions");
        markup.ShouldContain("dashboard-query-builder-assertion-details");
        markup.ShouldNotContain("dashboard-query-builder-match-operator");
        markup.ShouldNotContain("Rule logic");
        markup.ShouldNotContain("Rule summary");
        markup.ShouldNotContain("All rules");
        markup.ShouldNotContain("Event gate");
        markup.ShouldNotContain("Event details");
        markup.ShouldNotContain("Advanced match");
        markup.ShouldNotContain("AND/OR/NOT");
        markup.ShouldNotContain("Active rules");
        markup.ShouldNotContain("Every active rule is combined with AND.");
        markup.ShouldContain("<MudSelect T=\"string\"");
        markup.ShouldContain("<MudSelectItem T=\"string\"");
        markup.ShouldContain("<MudTextField T=\"string\"");
        markup.ShouldNotContain("OptionCardClass");
        markup.ShouldNotContain("SetWindowFromEventAsync");
        markup.ShouldNotContain("dashboard-query-builder-choice-row");
        markup.ShouldNotContain("DashboardQueryBuilderSelect");
    }

    [Fact]
    public void DashboardMetricQueryBuilder_KeepsSentenceStickyDuringScroll()
    {
        var path = Path.Combine(
            FindRepositoryRoot(),
            "src",
            "FluxMq.UI",
            "Components",
            "Workspace",
            "DashboardMetricQueryBuilder.razor.css");
        var css = File.ReadAllText(path);

        css.ShouldContain(".dashboard-query-builder-sentence");
        css.ShouldContain("position: sticky;");
        css.ShouldContain("top: -7px;");
        css.ShouldContain("z-index: 4;");
        css.ShouldNotContain("dashboard-query-builder-source-item.disabled");
        css.ShouldNotContain("dashboard-query-builder-match-operator");
        css.ShouldNotContain("dashboard-query-builder-advanced-match");
        css.ShouldNotContain("dashboard-query-builder-match-mode");
        css.ShouldNotContain("dashboard-query-builder-active-rule-heading");
        css.ShouldNotContain("dashboard-query-builder-clear-rules");
        css.ShouldNotContain("dashboard-query-builder-match-summary");
        css.ShouldNotContain("dashboard-query-builder-filter-groups");
        css.ShouldNotContain("dashboard-query-builder-filter-group-heading");
        css.ShouldNotContain("dashboard-query-builder-active-filter-heading");
        css.ShouldNotContain("dashboard-query-builder-match-overview");
        css.ShouldNotContain("dashboard-query-builder-empty-chip");
        css.ShouldContain(".dashboard-query-builder-measure-summary");
        css.ShouldContain(".dashboard-query-builder-measure-copy");
        css.ShouldContain(".dashboard-query-builder-measure-meta");
        css.ShouldNotContain("dashboard-query-builder-measure-detail");
        css.ShouldNotContain("dashboard-query-builder-measure-title");
        css.ShouldNotContain("dashboard-query-builder-measure-facts");
        css.ShouldContain(".dashboard-query-builder-result-note");
        css.ShouldContain("var(--mud-palette-primary) 6%");
        css.ShouldNotContain("var(--mud-palette-warning) 84%");
    }

    [Fact]
    public void DashboardEditorPreferenceService_PersistsQueryBuilderHelpOutsideProjectJson()
    {
        var directory = Path.Combine(Path.GetTempPath(), "FluxMqEditorPreferenceTests", Guid.NewGuid().ToString("N"));
        var path = Path.Combine(directory, "editor-preferences.json");
        try
        {
            var preferences = new DashboardEditorPreferenceService(path);
            preferences.ShowQueryBuilderHelp.ShouldBeTrue();

            preferences.SetShowQueryBuilderHelp(false);

            var reloaded = new DashboardEditorPreferenceService(path);
            reloaded.ShowQueryBuilderHelp.ShouldBeFalse();
            File.ReadAllText(path).ShouldContain("showQueryBuilderHelp");
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Fact]
    public void DashboardMetricQueryBuilderDialog_UsesCenteredMudDialogChrome()
    {
        var path = Path.Combine(
            FindRepositoryRoot(),
            "src",
            "FluxMq.UI",
            "Components",
            "Workspace",
            "Dialogs",
            "DashboardMetricQueryBuilderDialog.razor");
        var markup = File.ReadAllText(path);

        markup.ShouldContain("<MudDialog");
        markup.ShouldContain("Class=\"dashboard-query-dialog-modal\"");
        markup.ShouldContain("<TitleContent>");
        markup.ShouldContain("<DialogActions>");
        markup.ShouldContain("@DialogTitle");
        markup.ShouldContain("dashboard-query-dialog-titlebar");
        markup.ShouldContain("dashboard-query-dialog-badge");
        markup.ShouldContain("dashboard-query-dialog-close");
        markup.ShouldContain("DashboardMetricQueryBuilder");
        markup.ShouldContain("DashboardEditorPreferenceService");
        markup.ShouldContain("DialogTitle { get; set; } = \"Metric query\"");
        markup.ShouldContain("ApplyNote { get; set; } = \"Draft query. Apply updates this metric only.\"");
        markup.ShouldContain("AllowedMeasures { get; set; } = []");
        markup.ShouldContain("NormalizeAllowedMeasure");
        markup.ShouldContain("ShowHelp=\"@EditorPreferences.ShowQueryBuilderHelp\"");
        markup.ShouldContain("ShowHelpChanged=\"@SetShowHelpAsync\"");
        markup.ShouldContain("MaxWidth = MaxWidth.Large");
        markup.ShouldContain("FullWidth = true");
        markup.ShouldNotContain("DashboardMovableDialogShell");
        markup.ShouldNotContain("NoHeader = true");
        markup.ShouldNotContain("Build KPI metric");
    }

    [Fact]
    public void DashboardQueryDialog_UsesFluxThemeScopeAndBackdrop()
    {
        var root = FindRepositoryRoot();
        var layoutMarkup = File.ReadAllText(Path.Combine(
            root,
            "src",
            "FluxMq.UI",
            "Components",
            "Layout",
            "MainLayout.razor"));
        var appCss = File.ReadAllText(Path.Combine(
            root,
            "src",
            "FluxMq.UI",
            "wwwroot",
            "app.css"));

        layoutMarkup.ShouldContain("ThemeScopeClass");
        layoutMarkup.ShouldContain("flux-theme-scope");
        layoutMarkup.ShouldContain("<MudDialogProvider BackdropClick=\"false\" />");
        layoutMarkup.IndexOf("<MudDialogProvider BackdropClick=\"false\" />", StringComparison.Ordinal)
            .ShouldBeLessThan(layoutMarkup.IndexOf("<div class=\"@ShellClass\"", StringComparison.Ordinal));

        appCss.ShouldContain(".flux-theme-scope");
        appCss.ShouldContain(".flux-theme-dark .mud-overlay");
        appCss.ShouldContain(".flux-theme-light .mud-overlay");
        appCss.ShouldContain(".mud-dialog.dashboard-query-dialog-modal");
        appCss.ShouldContain("var(--flux-shadow-pop)");
        appCss.ShouldContain("var(--flux-accent");
    }

    [Fact]
    public void DashboardMetricQueryBuilder_UsesPreviewFrameWithRealWidgetView()
    {
        var path = Path.Combine(
            FindRepositoryRoot(),
            "src",
            "FluxMq.UI",
            "Components",
            "Workspace",
            "DashboardMetricQueryBuilder.razor");
        var markup = File.ReadAllText(path);

        markup.ShouldContain("DashboardQueryPreviewFrame");
        markup.ShouldContain("ShowHelp=\"@ShowHelp\"");
        markup.ShouldContain("AllowedMeasures");
        markup.ShouldContain("MeasureOptions =>");
        markup.ShouldContain("DashboardWidgetView");
        markup.ShouldContain("MetricValueOverride=\"@Preview.Value\"");
        markup.ShouldContain("RefreshSample=\"@RefreshSampleAsync\"");
        markup.ShouldContain("dashboard-query-builder-result-main");
        markup.ShouldContain("dashboard-query-builder-result-explain");
        markup.ShouldContain("PreviewMatchLabel");
        markup.ShouldContain("PreviewRetainedMatchLabel");
        markup.ShouldContain("Preview.TotalMatchCount != Preview.WindowEventCount");
        markup.ShouldContain("PreviewFilterEmptyLabel");
        markup.ShouldContain("<span>Result</span>");
        markup.ShouldContain("<span>Retained</span>");
        markup.ShouldContain("<span>Query</span>");
        markup.ShouldNotContain("dashboard-query-builder-result-row");
        markup.ShouldNotContain("Query result");
        markup.ShouldNotContain("Active filters");
    }

    [Fact]
    public void DashboardEventCounterModuleView_UsesFocusedMetricValueRenderPath()
    {
        var root = FindRepositoryRoot();
        var markup = File.ReadAllText(Path.Combine(
            root,
            "src",
            "FluxMq.UI",
            "Components",
            "Workspace",
            "DashboardWidgets",
            "DashboardEventCounterModuleView.razor"));
        var css = File.ReadAllText(Path.Combine(
            root,
            "src",
            "FluxMq.UI",
            "wwwroot",
            "dashboard-widgets.css"));

        markup.ShouldContain("DashboardMetricValueVisualizationView");
        markup.ShouldNotContain("DashboardEventCounterWidget");
        markup.ShouldNotContain("Context.Snapshot");
        css.ShouldContain(".dashboard-metric-value-layout");
        css.ShouldContain(".dashboard-metric-value-unit");
        css.ShouldContain("--dashboard-metric-value-unit");

        var valueMarkup = File.ReadAllText(Path.Combine(
            root,
            "src",
            "FluxMq.UI",
            "Components",
            "Workspace",
            "DashboardWidgets",
            "DashboardMetricValueVisualizationView.razor"));

        valueMarkup.ShouldContain("MetricValueShowUnitKey");
        valueMarkup.ShouldContain("MetricValueUnitTextKey");
        valueMarkup.ShouldContain("MetricValueUnitColorKey");
        valueMarkup.ShouldContain("DisplayUnitText");
    }

    [Fact]
    public void DashboardInspector_UsesCompactSingleLineMetricQueryRow()
    {
        var root = FindRepositoryRoot();
        var markup = File.ReadAllText(Path.Combine(
            root,
            "src",
            "FluxMq.UI",
            "Components",
            "Workspace",
            "DashboardInspectorMetricQueryRows.razor"));
        var css = File.ReadAllText(Path.Combine(
            root,
            "src",
            "FluxMq.UI",
            "Components",
            "Workspace",
            "DashboardInspector.razor.css"));

        css.ShouldContain("grid-template-columns: minmax(0, 1fr) 32px;");
        css.ShouldContain(".dashboard-inspector-query-preview");
        css.ShouldContain("height: 100%;");
        css.ShouldContain("width: 32px;");
        css.ShouldContain("white-space: nowrap;");
        css.ShouldContain(".dashboard-inspector-query-edit");
        css.ShouldContain("background: transparent;");
        css.ShouldContain("border: 0;");
        markup.ShouldContain("dashboard-inspector-query-row");
        markup.ShouldContain("dashboard-inspector-query-preview");
        markup.ShouldContain("dashboard-inspector-query-edit");
        markup.ShouldContain("Edit.InvokeAsync()");
        markup.ShouldNotContain("MetricQueryPreviewValue");
    }

    [Fact]
    public void DashboardInspector_UsesAppMetricsForFocusedMetricWidgets()
    {
        var root = FindRepositoryRoot();
        var inspector = File.ReadAllText(Path.Combine(
            root,
            "src",
            "FluxMq.UI",
            "Components",
            "Workspace",
            "DashboardInspector.razor"));
        var queryRows = File.ReadAllText(Path.Combine(
            root,
            "src",
            "FluxMq.UI",
            "Components",
            "Workspace",
            "DashboardInspectorMetricQueryRows.razor"));

        inspector.ShouldContain("IsMetricQueryBuilderWidget");
        inspector.ShouldContain("!IsMetricQueryBuilderWidget");
        inspector.ShouldContain("DashboardInspectorAppMetricRows");
        inspector.ShouldContain("DashboardInspectorMetricQueryRows");
        inspector.ShouldContain("OpenMetricBuilderAsync");
        inspector.ShouldContain("DashboardWidgetCatalog.KpiTileType");
        inspector.ShouldContain("DashboardWidgetCatalog.StatusValueType");
        inspector.ShouldContain("DashboardWidgetCatalog.EventGaugeType");
        inspector.ShouldContain("DashboardWidgetCatalog.RateTileType");
        inspector.ShouldContain("DashboardWidgetCatalog.EventCounterType");
        inspector.ShouldContain("DashboardWidgetCatalog.EventRateType");
        inspector.ShouldContain("FluxMetricCatalog.MeasureCount");
        inspector.ShouldContain("FluxMetricCatalog.MeasureRate");
        inspector.ShouldContain("[nameof(DashboardMetricQueryBuilderDialog.AllowedMeasures)] = MetricQueryAllowedMeasures");
        inspector.ShouldNotContain("Event counter query");
        inspector.ShouldNotContain("Event rate query");
        queryRows.ShouldContain("dashboard-inspector-query-row");
        queryRows.ShouldContain("dashboard-inspector-query-edit");
        inspector.ShouldNotContain("OpenKpiMetricBuilderAsync");
        inspector.ShouldNotContain("ApplyKpiMetricQueryAsync");
    }

    [Fact]
    public void DashboardInspector_ExposesKpiVisualizationFromCatalog()
    {
        var root = FindRepositoryRoot();
        var inspector = File.ReadAllText(Path.Combine(
            root,
            "src",
            "FluxMq.UI",
            "Components",
            "Workspace",
            "DashboardInspector.razor"));
        var visualRows = File.ReadAllText(Path.Combine(
            root,
            "src",
            "FluxMq.UI",
            "Components",
            "Workspace",
            "DashboardInspectorMetricVisualizationRows.razor"));

        inspector.ShouldContain("draft.Profile.UsesMetricVisualization");
        inspector.ShouldContain("DashboardInspectorMetricVisualizationRows");
        inspector.ShouldContain("MetricVisualizationOptions");
        inspector.ShouldContain("DashboardMetricVisualizationCatalog.CreateModules()");
        inspector.ShouldContain("SetMetricVisualizationAsync");
        inspector.ShouldContain("MetricVisualization.SetVisualization(value, applyDefaults: true)");
        inspector.ShouldContain("CurrentMetricVisualizationModule");
        inspector.ShouldContain("MetricVisualizationPropertyCount");
        inspector.ShouldContain("SetMetricVisualizationValueAsync");
        visualRows.ShouldContain("PropertyGridRow Name=\"Visualization\"");
        visualRows.ShouldContain("PropertyGridColorPicker");
        visualRows.ShouldContain("PropertyGridIconSegment");
        visualRows.ShouldContain("PropertyChanged");
        inspector.ShouldNotContain("PropertyGridRow Name=\"Visual settings\"");
        inspector.ShouldNotContain("OpenMetricVisualizationEditorAsync");
        inspector.ShouldNotContain("DashboardMetricVisualizationEditorDialog");
        inspector.ShouldNotContain("SetMetricDigitalStyleAsync");
        inspector.ShouldNotContain("SetKpiTitleColorAsync");
    }

    [Fact]
    public void DashboardWidgetModuleCatalog_ProvidesFocusedPropertyDefinitionsForAllPaletteWidgets()
    {
        var modules = DashboardWidgetModuleCatalog.CreateModules();

        modules.Select(static module => module.Type).ShouldContain(DashboardWidgetCatalog.QosBreakdownType);
        modules.Select(static module => module.Type).ShouldContain(DashboardWidgetCatalog.RetainBreakdownType);
        modules.Select(static module => module.Type).ShouldContain(DashboardWidgetCatalog.StatusValueType);
        modules.Select(static module => module.Type).ShouldNotContain(DashboardWidgetCatalog.StatusStripType);
        modules.Select(static module => module.Type).ShouldNotContain(DashboardWidgetCatalog.QosRetainBreakdownType);
        modules.All(static module => module.PropertyGroups.Count > 0).ShouldBeTrue();
        modules.All(static module => module.EditCellComponent is not null && module.LiveComponent is not null).ShouldBeTrue();
        modules
            .Single(static module => module.Type == DashboardWidgetCatalog.EventCounterType)
            .DefaultConfiguration
            .Keys
            .ShouldBe(["title"]);
        modules
            .Single(static module => module.Type == DashboardWidgetCatalog.EventRateType)
            .DefaultConfiguration
            .Keys
            .ShouldBe(["title"]);
        modules
            .Single(static module => module.Type == DashboardWidgetCatalog.StatusValueType)
            .DefaultConfiguration
            .Keys
            .ShouldBe(["title"]);
        modules
            .Single(static module => module.Type == DashboardWidgetCatalog.EventGaugeType)
            .DefaultConfiguration
            .Keys
            .ShouldBe([
                "title",
                DashboardWidgetCatalog.GaugeStyleKey,
                DashboardWidgetCatalog.GaugeMinKey,
                DashboardWidgetCatalog.GaugeMaxKey,
                DashboardWidgetCatalog.GaugeTargetKey,
                DashboardWidgetCatalog.GaugeWarningKey,
                DashboardWidgetCatalog.GaugeCriticalKey,
                DashboardWidgetCatalog.GaugeNormalColorKey,
                DashboardWidgetCatalog.GaugeWarningColorKey,
                DashboardWidgetCatalog.GaugeCriticalColorKey
            ], ignoreOrder: true);
        modules
            .Single(static module => module.Type == DashboardWidgetCatalog.EventGaugeType)
            .DefaultConfiguration[DashboardWidgetCatalog.GaugeStyleKey]
            .ShouldBe(DashboardWidgetCatalog.GaugeStyleRing);
        modules
            .Single(static module => module.Type == DashboardWidgetCatalog.EventGaugeType)
            .DefaultConfiguration[DashboardWidgetCatalog.GaugeMaxKey]
            .ShouldBe(DashboardWidgetCatalog.GaugeDefaultMax);
        modules
            .Where(static module =>
                string.Equals(module.Type, DashboardWidgetCatalog.KpiTileType, StringComparison.Ordinal) ||
                string.Equals(module.Type, DashboardWidgetCatalog.StatusValueType, StringComparison.Ordinal) ||
                string.Equals(module.Type, DashboardWidgetCatalog.EventCounterType, StringComparison.Ordinal) ||
                string.Equals(module.Type, DashboardWidgetCatalog.EventRateType, StringComparison.Ordinal) ||
                string.Equals(module.Type, DashboardWidgetCatalog.RateTileType, StringComparison.Ordinal))
            .All(static module => string.Equals(module.MetricVisualizationId, DashboardMetricVisualizationIds.Value, StringComparison.Ordinal))
            .ShouldBeTrue();
        modules
            .Single(static module => module.Type == DashboardWidgetCatalog.KpiTileType)
            .DefaultConfiguration[DashboardWidgetCatalog.MetricVisualizationKey]
            .ShouldBe(DashboardMetricVisualizationIds.Value);
        modules
            .Single(static module => module.Type == DashboardWidgetCatalog.KpiTileType)
            .PropertyGroups
            .SelectMany(static group => group.Properties)
            .Select(static property => property.Key)
            .ShouldNotContain(DashboardWidgetCatalog.PrimaryMetricKey);
        modules
            .Single(static module => module.Type == DashboardWidgetCatalog.KpiTileType)
            .PropertyGroups
            .SelectMany(static group => group.Properties)
            .Single(static property => property.Key == DashboardWidgetCatalog.MetricVisualizationKey)
            .DefaultValue
            .ShouldBe(DashboardMetricVisualizationIds.Value);
    }

    [Fact]
    public void FlowDashboardDefinitionFactory_CreatesWidgetDefaultsFromModuleCatalog()
    {
        var modules = DashboardWidgetModuleCatalog.CreateModules();
        foreach (var module in modules)
        {
            ShouldMatchConfiguration(
                FlowDashboardDefinitionFactory.CreateWidgetConfiguration(module.Type),
                module.DefaultConfiguration);
        }

        ShouldMatchConfiguration(
            FlowDashboardDefinitionFactory.CreateWidgetConfiguration(DashboardWidgetCatalog.StatusStripType),
            modules.Single(static module => module.Type == DashboardWidgetCatalog.StatusValueType).DefaultConfiguration);
        ShouldMatchConfiguration(
            FlowDashboardDefinitionFactory.CreateWidgetConfiguration(DashboardWidgetCatalog.EventChartType),
            modules.Single(static module => module.Type == DashboardWidgetCatalog.BarChartType).DefaultConfiguration);
        ShouldMatchConfiguration(
            FlowDashboardDefinitionFactory.CreateWidgetConfiguration(DashboardWidgetCatalog.QosRetainBreakdownType),
            modules.Single(static module => module.Type == DashboardWidgetCatalog.QosBreakdownType).DefaultConfiguration);
        FlowDashboardDefinitionFactory.CreateWidgetConfiguration("custom.widget").Count.ShouldBe(0);
    }

    [Fact]
    public void DashboardWidgetModuleCatalog_OwnsInstanceNamePrefixes()
    {
        var expected = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [DashboardWidgetCatalog.KpiTileType] = "kpiTile",
            [DashboardWidgetCatalog.StatusValueType] = "statusValue",
            [DashboardWidgetCatalog.RateTileType] = "rateTile",
            [DashboardWidgetCatalog.EventCounterType] = "eventCounter",
            [DashboardWidgetCatalog.LatestEventType] = "latestEvent",
            [DashboardWidgetCatalog.EventRateType] = "eventRate",
            [DashboardWidgetCatalog.EventGaugeType] = "eventGauge",
            [DashboardWidgetCatalog.EventTableType] = "eventTable",
            [DashboardWidgetCatalog.LineChartType] = "lineChart",
            [DashboardWidgetCatalog.AreaChartType] = "areaChart",
            [DashboardWidgetCatalog.BarChartType] = "barChart",
            [DashboardWidgetCatalog.DonutChartType] = "donutChart",
            [DashboardWidgetCatalog.TopicActivityType] = "topicActivity",
            [DashboardWidgetCatalog.TopicTreeType] = "topicTree",
            [DashboardWidgetCatalog.PayloadDistributionType] = "payloadDistribution",
            [DashboardWidgetCatalog.QosBreakdownType] = "qosBreakdown",
            [DashboardWidgetCatalog.RetainBreakdownType] = "retainBreakdown"
        };

        foreach (var module in DashboardWidgetModuleCatalog.CreateModules())
        {
            DashboardWidgetModuleCatalog.InstanceNamePrefixFor(module.Type)
                .ShouldBe(expected[module.Type]);
        }

        DashboardWidgetModuleCatalog.InstanceNamePrefixFor(DashboardWidgetCatalog.StatusStripType)
            .ShouldBe("statusValue");
        DashboardWidgetModuleCatalog.InstanceNamePrefixFor(DashboardWidgetCatalog.EventChartType)
            .ShouldBe("barChart");
        DashboardWidgetModuleCatalog.InstanceNamePrefixFor(DashboardWidgetCatalog.QosRetainBreakdownType)
            .ShouldBe("qosBreakdown");
        DashboardWidgetModuleCatalog.InstanceNamePrefixFor("custom.widget").ShouldBe("widget");
    }

    [Fact]
    public void DashboardWidgetModuleCatalog_ComposesCategoryProviderModules()
    {
        var providers = DashboardWidgetModuleCatalog.CreateProviders();
        var secondProviders = DashboardWidgetModuleCatalog.CreateProviders();
        var providerModules = providers.SelectMany(static provider => provider.CreateModules()).ToArray();
        var catalogModules = DashboardWidgetModuleCatalog.CreateModules();
        var secondCatalogModules = DashboardWidgetModuleCatalog.CreateModules();

        providers.Select(static provider => provider.Id).ShouldBe([
            "metrics",
            "events",
            "charts",
            "mqtt-ops",
            "topics"
        ]);
        secondProviders.ShouldBeSameAs(providers);
        secondCatalogModules.ShouldBeSameAs(catalogModules);
        providers.Select(static provider => provider.Id).Distinct(StringComparer.Ordinal).Count()
            .ShouldBe(providers.Count);
        providerModules.Select(static module => module.Type)
            .ShouldBe(catalogModules.Select(static module => module.Type));
        providerModules.Select(static module => module.Type).Distinct(StringComparer.Ordinal).Count()
            .ShouldBe(providerModules.Length);
    }

    [Fact]
    public void DashboardWidgetRegistry_ExposesFocusedDescriptorsAndKeepsCompatibilityLookup()
    {
        var registry = new DashboardWidgetRegistry();

        var widgetTypes = registry.Widgets.Select(static widget => widget.Type).ToArray();
        var categories = registry.Widgets.Select(static widget => widget.Category).ToArray();

        widgetTypes.Contains(DashboardWidgetCatalog.StatusStripType, StringComparer.Ordinal).ShouldBeFalse();
        widgetTypes.Contains(DashboardWidgetCatalog.EventChartType, StringComparer.Ordinal).ShouldBeFalse();
        widgetTypes.Contains(DashboardWidgetCatalog.QosRetainBreakdownType, StringComparer.Ordinal).ShouldBeFalse();
        categories.Contains("Compatibility", StringComparer.Ordinal).ShouldBeFalse();

        registry.Find(DashboardWidgetCatalog.StatusStripType)!.Type
            .ShouldBe(DashboardWidgetCatalog.StatusValueType);
        registry.Find(DashboardWidgetCatalog.EventChartType)!.Type
            .ShouldBe(DashboardWidgetCatalog.LineChartType);
        registry.Find(DashboardWidgetCatalog.QosRetainBreakdownType)!.Type
            .ShouldBe(DashboardWidgetCatalog.QosBreakdownType);
        registry.Find("custom.widget").ShouldBeNull();
    }

    [Fact]
    public void DashboardMetricWidgetModuleProvider_OwnsMetricWidgetDefinitions()
    {
        var modules = new DashboardMetricWidgetModuleProvider().CreateModules();

        modules.Select(static module => module.Type).ShouldBe([
            DashboardWidgetCatalog.KpiTileType,
            DashboardWidgetCatalog.StatusValueType,
            DashboardWidgetCatalog.RateTileType
        ]);
        modules.All(static module => string.Equals(module.Descriptor.Category, "Metrics", StringComparison.Ordinal))
            .ShouldBeTrue();
        modules
            .Single(static module => module.Type == DashboardWidgetCatalog.KpiTileType)
            .DefaultConfiguration
            .Keys
            .ShouldContain(DashboardWidgetCatalog.MetricVisualizationKey);
        modules
            .Single(static module => module.Type == DashboardWidgetCatalog.KpiTileType)
            .DefaultConfiguration
            .Keys
            .ShouldNotContain(DashboardWidgetCatalog.PrimaryMetricKey);
        modules
            .Where(static module => module.Type is DashboardWidgetCatalog.StatusValueType or DashboardWidgetCatalog.RateTileType)
            .All(static module => !module.DefaultConfiguration.ContainsKey(DashboardWidgetCatalog.PrimaryMetricKey))
            .ShouldBeTrue();
    }

    [Fact]
    public void DashboardEventWidgetModuleProvider_OwnsEventWidgetDefinitions()
    {
        var modules = new DashboardEventWidgetModuleProvider().CreateModules();

        modules.Select(static module => module.Type).ShouldBe([
            DashboardWidgetCatalog.EventCounterType,
            DashboardWidgetCatalog.LatestEventType,
            DashboardWidgetCatalog.EventRateType,
            DashboardWidgetCatalog.EventGaugeType,
            DashboardWidgetCatalog.EventTableType
        ]);
        modules.All(static module => string.Equals(module.Descriptor.Category, "Events", StringComparison.Ordinal))
            .ShouldBeTrue();
        modules
            .Single(static module => module.Type == DashboardWidgetCatalog.EventCounterType)
            .DefaultConfiguration
            .Keys
            .ShouldBe(["title"]);
        modules
            .Single(static module => module.Type == DashboardWidgetCatalog.LatestEventType)
            .DefaultConfiguration
            .Keys
            .ShouldContain(DashboardEventFilterCatalog.EventTypeKey);
        modules
            .Single(static module => module.Type == DashboardWidgetCatalog.EventGaugeType)
            .DefaultConfiguration[DashboardWidgetCatalog.GaugeStyleKey]
            .ShouldBe(DashboardWidgetCatalog.GaugeStyleRing);
        modules
            .Single(static module => module.Type == DashboardWidgetCatalog.EventTableType)
            .Layout
            .PreferredRowSpan
            .ShouldBe(2);
    }

    [Fact]
    public void DashboardChartWidgetModuleProvider_OwnsChartWidgetDefinitions()
    {
        var modules = new DashboardChartWidgetModuleProvider().CreateModules();

        modules.Select(static module => module.Type).ShouldBe([
            DashboardWidgetCatalog.LineChartType,
            DashboardWidgetCatalog.AreaChartType,
            DashboardWidgetCatalog.BarChartType,
            DashboardWidgetCatalog.DonutChartType
        ]);
        modules.All(static module => string.Equals(module.Descriptor.Category, "Charts", StringComparison.Ordinal))
            .ShouldBeTrue();
        modules
            .Single(static module => module.Type == DashboardWidgetCatalog.LineChartType)
            .DefaultConfiguration[DashboardWidgetCatalog.ChartTypeKey]
            .ShouldBe(DashboardWidgetCatalog.ChartTypeLine);
        modules
            .Single(static module => module.Type == DashboardWidgetCatalog.AreaChartType)
            .DefaultConfiguration[DashboardWidgetCatalog.ChartTypeKey]
            .ShouldBe(DashboardWidgetCatalog.ChartTypeArea);
        modules
            .Single(static module => module.Type == DashboardWidgetCatalog.BarChartType)
            .DefaultConfiguration[DashboardWidgetCatalog.ChartTypeKey]
            .ShouldBe(DashboardWidgetCatalog.ChartTypeBars);
        modules
            .Single(static module => module.Type == DashboardWidgetCatalog.LineChartType)
            .CompatibilityTypeIds
            .ShouldContain(DashboardWidgetCatalog.EventChartType);
        modules
            .Single(static module => module.Type == DashboardWidgetCatalog.DonutChartType)
            .Layout
            .PreferredColumnSpan
            .ShouldBe(2);
    }

    [Fact]
    public void DashboardMqttOpsWidgetModuleProvider_OwnsMqttOpsWidgetDefinitions()
    {
        var modules = new DashboardMqttOpsWidgetModuleProvider().CreateModules();

        modules.Select(static module => module.Type).ShouldBe([
            DashboardWidgetCatalog.PayloadDistributionType,
            DashboardWidgetCatalog.QosBreakdownType,
            DashboardWidgetCatalog.RetainBreakdownType
        ]);
        modules.All(static module => string.Equals(module.Descriptor.Category, "MQTT Ops", StringComparison.Ordinal))
            .ShouldBeTrue();
        modules
            .Single(static module => module.Type == DashboardWidgetCatalog.PayloadDistributionType)
            .Descriptor
            .RendererKind
            .ShouldBe(DashboardWidgetRendererKind.PayloadDistribution);
        modules
            .Single(static module => module.Type == DashboardWidgetCatalog.PayloadDistributionType)
            .PropertyGroups
            .Select(static group => group.Id)
            .ShouldBe(["source", "buckets"]);
        modules
            .Single(static module => module.Type == DashboardWidgetCatalog.QosBreakdownType)
            .CompatibilityTypeIds
            .ShouldContain(DashboardWidgetCatalog.QosRetainBreakdownType);
        modules
            .Where(static module => module.Type is DashboardWidgetCatalog.QosBreakdownType or DashboardWidgetCatalog.RetainBreakdownType)
            .All(static module => module.PropertyGroups.Select(static group => group.Id).SequenceEqual(["source", "breakdown"]))
            .ShouldBeTrue();
    }

    [Fact]
    public void DashboardTopicWidgetModuleProvider_OwnsTopicWidgetDefinitions()
    {
        var modules = new DashboardTopicWidgetModuleProvider().CreateModules();

        modules.Select(static module => module.Type).ShouldBe([
            DashboardWidgetCatalog.TopicActivityType,
            DashboardWidgetCatalog.TopicTreeType
        ]);
        modules.All(static module => string.Equals(module.Descriptor.Category, "Topics", StringComparison.Ordinal))
            .ShouldBeTrue();
        modules
            .Single(static module => module.Type == DashboardWidgetCatalog.TopicActivityType)
            .Descriptor
            .RendererKind
            .ShouldBe(DashboardWidgetRendererKind.TopicActivity);
        modules
            .Single(static module => module.Type == DashboardWidgetCatalog.TopicActivityType)
            .PropertyGroups
            .Select(static group => group.Id)
            .ShouldBe(["topic-metric", "top-topics"]);
        modules
            .Single(static module => module.Type == DashboardWidgetCatalog.TopicTreeType)
            .PropertyGroups
            .Select(static group => group.Id)
            .ShouldBe(["topic-tree"]);
        modules
            .Single(static module => module.Type == DashboardWidgetCatalog.TopicTreeType)
            .DefaultConfiguration[DashboardWidgetCatalog.ExcludeSystemTopicsKey]
            .ShouldBe("true");
        modules
            .Single(static module => module.Type == DashboardWidgetCatalog.TopicTreeType)
            .Layout
            .PreferredRowSpan
            .ShouldBe(2);
    }

    [Fact]
    public void DashboardMetricVisualizationCatalog_ProvidesMetricValueFoundation()
    {
        var modules = DashboardMetricVisualizationCatalog.CreateModules();
        var value = modules.Single(static module => module.Id == DashboardMetricVisualizationIds.Value);
        var digital = modules.Single(static module => module.Id == DashboardMetricVisualizationIds.Digital);

        value.DisplayName.ShouldBe("Value");
        value.EditCellComponent.ShouldBe(typeof(DashboardMetricValueVisualizationView));
        value.LiveComponent.ShouldBe(typeof(DashboardMetricValueVisualizationView));
        value.DefaultConfiguration["visualization"].ShouldBe(DashboardMetricVisualizationIds.Value);
        value.DefaultConfiguration[DashboardWidgetCatalog.MetricValueTitleKey].ShouldBe(DashboardWidgetCatalog.MetricValueDefaultTitle);
        value.DefaultConfiguration[DashboardWidgetCatalog.MetricValueSubtitleKey].ShouldBe(DashboardWidgetCatalog.MetricValueDefaultSubtitle);
        value.DefaultConfiguration[DashboardWidgetCatalog.MetricValueShowUnitKey].ShouldBe("true");
        value.DefaultConfiguration[DashboardWidgetCatalog.MetricValueUnitTextKey].ShouldBe(DashboardWidgetCatalog.MetricValueDefaultUnitText);
        value.DefaultConfiguration[DashboardWidgetCatalog.MetricValueTitleColorKey].ShouldBe(DashboardWidgetCatalog.KpiDefaultTitleColor);
        value.DefaultConfiguration[DashboardWidgetCatalog.MetricValueSubtitleColorKey].ShouldBe(DashboardWidgetCatalog.KpiDefaultSubtitleColor);
        value.DefaultConfiguration[DashboardWidgetCatalog.MetricValueValueColorKey].ShouldBe(DashboardWidgetCatalog.KpiDefaultValueColor);
        value.DefaultConfiguration[DashboardWidgetCatalog.MetricValueUnitColorKey].ShouldBe(DashboardWidgetCatalog.KpiDefaultSubtitleColor);
        value.DefaultConfiguration[DashboardWidgetCatalog.MetricValuePaddingKey]
            .ShouldBe(DashboardWidgetCatalog.MetricValueDefaultPadding.ToString(System.Globalization.CultureInfo.InvariantCulture));
        value.DefaultConfiguration.ContainsKey(DashboardWidgetCatalog.KpiTitleColorKey).ShouldBeFalse();
        value.DefaultConfiguration.ContainsKey(DashboardWidgetCatalog.KpiSubtitleColorKey).ShouldBeFalse();
        value.DefaultConfiguration.ContainsKey(DashboardWidgetCatalog.KpiValueColorKey).ShouldBeFalse();
        value.SupportedValueKinds.ShouldContain(DashboardMetricValueKinds.Number);
        value.SupportedValueKinds.ShouldContain(DashboardMetricValueKinds.Rate);
        value.SupportedValueKinds.ShouldContain(DashboardMetricValueKinds.Bytes);
        value.SupportedValueKinds.ShouldContain(DashboardMetricValueKinds.Percent);
        value.PropertyGroups.ShouldNotBeEmpty();
        value.PropertyGroups
            .SelectMany(static group => group.Properties)
            .Select(static property => property.Key)
            .ShouldBe([
                DashboardWidgetCatalog.MetricValueTitleKey,
                DashboardWidgetCatalog.MetricValueShowTitleKey,
                DashboardWidgetCatalog.MetricValueSubtitleKey,
                DashboardWidgetCatalog.MetricValueShowSubtitleKey,
                DashboardWidgetCatalog.MetricValueShowUnitKey,
                DashboardWidgetCatalog.MetricValueUnitTextKey,
                DashboardWidgetCatalog.MetricValueTitleColorKey,
                DashboardWidgetCatalog.MetricValueSubtitleColorKey,
                DashboardWidgetCatalog.MetricValueValueColorKey,
                DashboardWidgetCatalog.MetricValueUnitColorKey,
                DashboardWidgetCatalog.MetricValueTitleAlignKey,
                DashboardWidgetCatalog.MetricValueValueAlignKey,
                DashboardWidgetCatalog.MetricValueValuePlacementKey,
                DashboardWidgetCatalog.MetricValuePaddingKey
            ]);

        digital.DisplayName.ShouldBe("Digital");
        digital.EditCellComponent.ShouldBe(typeof(DashboardMetricDigitalVisualizationView));
        digital.LiveComponent.ShouldBe(typeof(DashboardMetricDigitalVisualizationView));
        digital.DefaultConfiguration[DashboardWidgetCatalog.MetricVisualizationKey]
            .ShouldBe(DashboardMetricVisualizationIds.Digital);
        digital.DefaultConfiguration[DashboardWidgetCatalog.MetricDigitalStyleKey]
            .ShouldBe(DashboardWidgetCatalog.MetricDigitalStylePanel);
        digital.DefaultConfiguration[DashboardWidgetCatalog.MetricDigitalGlowKey]
            .ShouldBe(DashboardWidgetCatalog.MetricDigitalGlowSoft);
        digital.DefaultConfiguration[DashboardWidgetCatalog.MetricDigitalBackgroundColorKey]
            .ShouldBe(DashboardWidgetCatalog.MetricDigitalDefaultBackgroundColor);
        digital.DefaultConfiguration[DashboardWidgetCatalog.MetricDigitalSegmentColorKey]
            .ShouldBe(DashboardWidgetCatalog.MetricDigitalDefaultSegmentColor);
        digital.DefaultConfiguration[DashboardWidgetCatalog.MetricDigitalInactiveSegmentColorKey]
            .ShouldBe(DashboardWidgetCatalog.MetricDigitalDefaultInactiveSegmentColor);
        digital.DefaultConfiguration[DashboardWidgetCatalog.MetricDigitalLabelColorKey]
            .ShouldBe(DashboardWidgetCatalog.MetricDigitalDefaultLabelColor);
        digital.DefaultConfiguration[DashboardWidgetCatalog.MetricDigitalDigitsKey]
            .ShouldBe(DashboardWidgetCatalog.MetricDigitalDefaultDigits.ToString(System.Globalization.CultureInfo.InvariantCulture));
        digital.DefaultConfiguration[DashboardWidgetCatalog.MetricDigitalBorderColorKey]
            .ShouldBe(DashboardWidgetCatalog.MetricDigitalDefaultBorderColor);
        digital.DefaultConfiguration[DashboardWidgetCatalog.MetricDigitalBorderWidthKey]
            .ShouldBe(DashboardWidgetCatalog.MetricDigitalDefaultBorderWidth.ToString(System.Globalization.CultureInfo.InvariantCulture));
        digital.DefaultConfiguration[DashboardWidgetCatalog.MetricDigitalRadiusKey]
            .ShouldBe(DashboardWidgetCatalog.MetricDigitalDefaultRadius.ToString(System.Globalization.CultureInfo.InvariantCulture));
        digital.DefaultConfiguration[DashboardWidgetCatalog.MetricDigitalPaddingKey]
            .ShouldBe(DashboardWidgetCatalog.MetricDigitalDefaultPadding.ToString(System.Globalization.CultureInfo.InvariantCulture));
        digital.DefaultConfiguration[DashboardWidgetCatalog.MetricDigitalFitModeKey]
            .ShouldBe(DashboardWidgetCatalog.MetricDigitalFitCompact);
        digital.SupportedValueKinds.ShouldContain(DashboardMetricValueKinds.Number);
        digital.SupportedValueKinds.ShouldContain(DashboardMetricValueKinds.Rate);
        var digitalPropertyKeys = digital.PropertyGroups
            .SelectMany(static group => group.Properties)
            .Select(static property => property.Key)
            .ToArray();
        digitalPropertyKeys.ShouldContain(DashboardWidgetCatalog.MetricDigitalStyleKey);
        digitalPropertyKeys.ShouldContain(DashboardWidgetCatalog.MetricDigitalGlowKey);
        digitalPropertyKeys.ShouldContain(DashboardWidgetCatalog.MetricDigitalBackgroundColorKey);
        digitalPropertyKeys.ShouldContain(DashboardWidgetCatalog.MetricDigitalSegmentColorKey);
        digitalPropertyKeys.ShouldContain(DashboardWidgetCatalog.MetricDigitalInactiveSegmentColorKey);
        digitalPropertyKeys.ShouldContain(DashboardWidgetCatalog.MetricDigitalLabelColorKey);
        digitalPropertyKeys.ShouldContain(DashboardWidgetCatalog.MetricDigitalDigitsKey);
        digitalPropertyKeys.ShouldContain(DashboardWidgetCatalog.MetricDigitalBorderColorKey);
        digitalPropertyKeys.ShouldContain(DashboardWidgetCatalog.MetricDigitalBorderWidthKey);
        digitalPropertyKeys.ShouldContain(DashboardWidgetCatalog.MetricDigitalRadiusKey);
        digitalPropertyKeys.ShouldContain(DashboardWidgetCatalog.MetricDigitalPaddingKey);
        digitalPropertyKeys.ShouldContain(DashboardWidgetCatalog.MetricDigitalFitModeKey);
        digitalPropertyKeys.ShouldNotContain(DashboardWidgetCatalog.KpiValueColorKey);
        digitalPropertyKeys.ShouldNotContain(DashboardWidgetCatalog.MetricValueValueColorKey);
    }

    [Fact]
    public void DashboardMetricVisualizationCatalog_ComposesExplicitProviderModules()
    {
        var providers = DashboardMetricVisualizationCatalog.CreateProviders();
        var secondProviders = DashboardMetricVisualizationCatalog.CreateProviders();
        var modules = providers.Select(static provider => provider.CreateModule()).ToArray();
        var catalogModules = DashboardMetricVisualizationCatalog.CreateModules();
        var secondCatalogModules = DashboardMetricVisualizationCatalog.CreateModules();

        providers.Select(static provider => provider.Id).ShouldBe([
            DashboardMetricVisualizationIds.Value,
            DashboardMetricVisualizationIds.Digital
        ]);
        secondProviders.ShouldBeSameAs(providers);
        secondCatalogModules.ShouldBeSameAs(catalogModules);
        providers.Select(static provider => provider.Id).Distinct(StringComparer.Ordinal).Count()
            .ShouldBe(providers.Count);
        modules.Select(static module => module.Id).ShouldBe(providers.Select(static provider => provider.Id));
        modules.Select(static module => module.Id).ShouldBe(catalogModules.Select(static module => module.Id));
        foreach (var provider in providers)
        {
            provider.CreateModule().Id.ShouldBe(provider.Id);
        }
    }

    [Fact]
    public void DashboardMetricDigitalVisualization_UsesReusableReadoutComponent()
    {
        var root = FindRepositoryRoot();
        var widgetsPath = Path.Combine(root, "src", "FluxMq.UI", "Components", "Workspace", "DashboardWidgets");
        var digitalView = File.ReadAllText(Path.Combine(widgetsPath, "DashboardMetricDigitalVisualizationView.razor"));
        var readout = File.ReadAllText(Path.Combine(widgetsPath, "DashboardDigitalReadout.razor"));

        digitalView.ShouldContain("<DashboardDigitalReadout");
        digitalView.ShouldContain("Value=\"@DigitalValue(metric.Value)\"");
        digitalView.ShouldContain("Label=\"@label\"");
        digitalView.ShouldContain("AccentColor=");
        digitalView.ShouldContain("BackgroundColor=");
        digitalView.ShouldContain("InactiveSegmentColor=");
        digitalView.ShouldContain("LabelColor=");
        digitalView.ShouldContain("MinimumDigits=");
        digitalView.ShouldNotContain("<svg");

        readout.ShouldContain("[Parameter]");
        readout.ShouldContain("public string? Value");
        readout.ShouldContain("public string? Label");
        readout.ShouldContain("public string? AccentColor");
        readout.ShouldContain("public string? BackgroundColor");
        readout.ShouldContain("public string? InactiveSegmentColor");
        readout.ShouldContain("public string? LabelColor");
        readout.ShouldContain("public int MinimumDigits");
        readout.ShouldContain("viewBox=");
        readout.ShouldContain("SegmentPath");
        readout.ShouldContain("foreach (var segment in new[] { 'a', 'b', 'c', 'd', 'e', 'f', 'g' })");
        readout.ShouldContain("if (segments.Contains('.', StringComparison.Ordinal))");
    }

    [Fact]
    public void PropertyGridColorPicker_UsesAlphaCapableFrameworkPicker()
    {
        var root = FindRepositoryRoot();
        var path = Path.Combine(
            root,
            "src",
            "FluxMq.UI",
            "Components",
            "Workspace",
            "PropertyGridColorPicker.razor");
        var markup = File.ReadAllText(path);

        markup.ShouldContain("<MudColorPicker");
        markup.ShouldContain("property-grid-color-picker");
        markup.ShouldContain("property-grid-color-swatch");
        markup.ShouldContain("<MudOverlay");
        markup.ShouldContain("AutoClose=\"true\"");
        markup.ShouldContain("OnClosed=\"@ClosePicker\"");
        markup.ShouldContain("<MudPopover");
        markup.ShouldContain("ShowAlpha=\"true\"");
        markup.ShouldContain("ShowColorField=\"true\"");
        markup.ShouldContain("ShowInputs=\"true\"");
        markup.ShouldContain("ShowModeSwitch=\"true\"");
        markup.ShouldContain("ColorPickerMode=\"ColorPickerMode.HEX\"");
        markup.ShouldContain("PickerVariant=\"PickerVariant.Static\"");
        markup.ShouldContain("private void ClosePicker()");
        markup.ShouldContain("#00000000");
        markup.ShouldContain("MudColor.TryParse");
        markup.ShouldContain("ToPersistedColor");
        markup.ShouldContain("SwatchCssColor");
        markup.ShouldContain("rgba(");
        markup.ShouldNotContain("property-grid-mud-color-picker");
        markup.ShouldNotContain("property-grid-color-alpha");

        var css = File.ReadAllText(Path.Combine(
            root,
            "src",
            "FluxMq.UI",
            "Components",
            "Workspace",
            "PropertyGridColorPicker.razor.css"));
        css.ShouldContain("grid-template-columns: 36px minmax(0, 1fr) 28px;");
        css.ShouldContain("background-color: var(--property-grid-color-value);");
        css.ShouldContain("font-size: 17px;");
    }

    [Fact]
    public void DashboardDesigner_EditPreviewKeepsMetricValuePlacement()
    {
        var root = FindRepositoryRoot();
        var path = Path.Combine(
            root,
            "src",
            "FluxMq.UI",
            "Components",
            "Workspace",
            "DashboardDesigner.razor.css");
        var css = File.ReadAllText(path);

        css.ShouldContain(".dashboard-cell-widget-preview ::deep .dashboard-metric-value-layout");
        css.ShouldContain("justify-content: var(--dashboard-kpi-value-placement, flex-start);");
        css.ShouldNotContain("justify-content: space-between;");
    }

    [Fact]
    public void DashboardDesigner_AppliesCellWidgetAlignmentToEditAndLiveViews()
    {
        var root = FindRepositoryRoot();
        var css = File.ReadAllText(Path.Combine(
            root,
            "src",
            "FluxMq.UI",
            "Components",
            "Workspace",
            "DashboardDesigner.razor.css"));

        css.ShouldContain(".dashboard-cell-widget-preview");
        css.ShouldContain("align-items: var(--dashboard-cell-widget-align, stretch);");
        css.ShouldContain("justify-content: var(--dashboard-cell-widget-justify, flex-start);");
        css.ShouldContain("flex: var(--dashboard-cell-widget-flex, 1 1 auto);");
        css.ShouldContain("width: var(--dashboard-cell-widget-width, 100%);");
        css.ShouldContain("padding: var(--dashboard-cell-padding, 0);");
        css.ShouldContain(".dashboard-live-cell");
        css.ShouldContain(".dashboard-live-cell ::deep .dashboard-widget");
    }

    [Fact]
    public void DashboardDesigner_EmitsResponsiveCellVariables()
    {
        var root = FindRepositoryRoot();
        var markup = File.ReadAllText(Path.Combine(
            root,
            "src",
            "FluxMq.UI",
            "Components",
            "Workspace",
            "DashboardDesigner.razor"));

        markup.ShouldContain("CellResponsiveVariables");
        markup.ShouldContain("--dashboard-cell-column-span");
        markup.ShouldContain("--dashboard-cell-row-span");
        markup.ShouldContain("--dashboard-cell-tablet-span");
        markup.ShouldContain("--dashboard-cell-mobile-span");
        markup.ShouldContain("--dashboard-cell-responsive-min-height");
        markup.ShouldContain("CssTemplate(_layout.Columns, ColumnAxis)");
        markup.ShouldContain("CssTemplate(_layout.Rows, RowAxis)");
    }

    [Fact]
    public void DashboardDesigner_UsesContainerResponsiveGridForEditAndLive()
    {
        var root = FindRepositoryRoot();
        var css = File.ReadAllText(Path.Combine(
            root,
            "src",
            "FluxMq.UI",
            "Components",
            "Workspace",
            "DashboardDesigner.razor.css"));

        css.ShouldContain("container-name: dashboard-layout;");
        css.ShouldContain("@container dashboard-layout (max-width: 980px)");
        css.ShouldContain("@container dashboard-layout (max-width: 620px)");
        css.ShouldContain("grid-template-columns: repeat(2, minmax(var(--dashboard-grid-column-min, 156px), 1fr)) !important;");
        css.ShouldContain("grid-column: span var(--dashboard-cell-tablet-span, 1) !important;");
        css.ShouldContain("grid-column: span var(--dashboard-cell-mobile-span, 1) !important;");
        css.ShouldContain(".dashboard-live-grid");
        css.ShouldContain("grid-auto-rows: minmax(var(--dashboard-grid-row-min, 136px), 1fr);");
    }

    [Fact]
    public void DashboardWidgets_UseContainerResponsiveValueAndDigitalSizing()
    {
        var root = FindRepositoryRoot();
        var css = File.ReadAllText(Path.Combine(
            root,
            "src",
            "FluxMq.UI",
            "wwwroot",
            "dashboard-widgets.css"));

        css.ShouldContain("font-size: clamp(28px, 8cqw, 46px);");
        css.ShouldContain("font-size: clamp(30px, 10cqw, 52px);");
        css.ShouldContain("height: clamp(34px, 42cqh, 74px);");
        css.ShouldContain("@container (max-width: 240px)");
        css.ShouldContain("@container (max-height: 150px)");
        css.ShouldContain(".dashboard-digital-readout-display");
    }

    [Fact]
    public void DashboardInspector_RendersCellWidgetAlignmentPad()
    {
        var root = FindRepositoryRoot();
        var inspector = File.ReadAllText(Path.Combine(
            root,
            "src",
            "FluxMq.UI",
            "Components",
            "Workspace",
            "DashboardInspector.razor"));
        var styleRows = File.ReadAllText(Path.Combine(
            root,
            "src",
            "FluxMq.UI",
            "Components",
            "Workspace",
            "DashboardInspectorCellStyleRows.razor"));

        inspector.ShouldContain("DashboardInspectorCellStyleRows");
        inspector.ShouldContain("SetCellWidgetAlignmentAsync");
        inspector.ShouldContain("DashboardCellStyleDraft.WidgetFitContent");
        styleRows.ShouldContain("<PropertyGridAlignmentPad");
    }

    [Fact]
    public void DashboardInspector_UsesFocusedLayoutAndStyleRowComponents()
    {
        var root = FindRepositoryRoot();
        var inspector = File.ReadAllText(Path.Combine(
            root,
            "src",
            "FluxMq.UI",
            "Components",
            "Workspace",
            "DashboardInspector.razor"));
        var layoutRows = File.ReadAllText(Path.Combine(
            root,
            "src",
            "FluxMq.UI",
            "Components",
            "Workspace",
            "DashboardInspectorLayoutRows.razor"));
        var styleRows = File.ReadAllText(Path.Combine(
            root,
            "src",
            "FluxMq.UI",
            "Components",
            "Workspace",
            "DashboardInspectorCellStyleRows.razor"));

        inspector.ShouldContain("DashboardInspectorLayoutRows");
        inspector.ShouldContain("DashboardInspectorCellStyleRows");
        inspector.ShouldNotContain("private string LayoutSelectionName");
        inspector.ShouldNotContain("StyleInputType(DashboardStyleField");
        layoutRows.ShouldContain("DuplicateWidget");
        layoutRows.ShouldContain("DeleteWidget");
        styleRows.ShouldContain("Reset cell style");
    }

    [Fact]
    public void DashboardInspector_UsesFocusedMetricDataRowComponents()
    {
        var root = FindRepositoryRoot();
        var inspector = File.ReadAllText(Path.Combine(
            root,
            "src",
            "FluxMq.UI",
            "Components",
            "Workspace",
            "DashboardInspector.razor"));
        var appRows = File.ReadAllText(Path.Combine(
            root,
            "src",
            "FluxMq.UI",
            "Components",
            "Workspace",
            "DashboardInspectorAppMetricRows.razor"));
        var bindingRows = File.ReadAllText(Path.Combine(
            root,
            "src",
            "FluxMq.UI",
            "Components",
            "Workspace",
            "DashboardInspectorMetricBindingRows.razor"));
        var queryRows = File.ReadAllText(Path.Combine(
            root,
            "src",
            "FluxMq.UI",
            "Components",
            "Workspace",
            "DashboardInspectorMetricQueryRows.razor"));

        inspector.ShouldContain("DashboardInspectorAppMetricRows");
        inspector.ShouldContain("DashboardInspectorMetricBindingRows");
        inspector.ShouldContain("DashboardInspectorMetricQueryRows");
        inspector.ShouldContain("DashboardInspectorMetricBindingState.Initialize");
        inspector.ShouldContain("DashboardInspectorMetricBindingState.Current");
        inspector.ShouldContain("DashboardInspectorMetricBindingState.TryAdd");
        inspector.ShouldContain("DashboardInspectorMetricBindingState.Remove");
        inspector.ShouldContain("DashboardInspectorMetricBindingState.TryMove");
        inspector.ShouldNotContain("<PropertyGridRow Name=\"Metric query\">");
        inspector.ShouldNotContain("private RenderFragment RenderMetricParameterField");
        inspector.ShouldNotContain("CurrentBindingMetrics(");
        appRows.ShouldContain("Open metric");
        appRows.ShouldContain("ParameterChanged");
        bindingRows.ShouldContain("PrimaryMetricChanged");
        bindingRows.ShouldContain("AddMetric");
        bindingRows.ShouldContain("DashboardInspectorMetricMove");
        queryRows.ShouldContain("dashboard-inspector-query-edit");
    }

    [Fact]
    public void DashboardInspector_UsesFocusedMetricQueryOptionRowComponent()
    {
        var root = FindRepositoryRoot();
        var inspector = File.ReadAllText(Path.Combine(
            root,
            "src",
            "FluxMq.UI",
            "Components",
            "Workspace",
            "DashboardInspector.razor"));
        var optionRows = File.ReadAllText(Path.Combine(
            root,
            "src",
            "FluxMq.UI",
            "Components",
            "Workspace",
            "DashboardInspectorMetricQueryOptionRows.razor"));

        inspector.ShouldContain("DashboardInspectorMetricQueryOptionRows");
        inspector.ShouldNotContain("PropertyGridRow Name=\"Aggregate\"");
        inspector.ShouldNotContain("PropertyGridRow Name=\"@InspectorLabels.WindowRow\"");
        optionRows.ShouldContain("PropertyGridRow Name=\"Aggregate\"");
        optionRows.ShouldContain("PropertyGridRow Name=\"@Labels.WindowRow\"");
        optionRows.ShouldContain("AggregationChanged");
        optionRows.ShouldContain("WindowChanged");
    }

    [Fact]
    public void DashboardInspector_UsesFocusedEventFilterRowComponent()
    {
        var root = FindRepositoryRoot();
        var inspector = File.ReadAllText(Path.Combine(
            root,
            "src",
            "FluxMq.UI",
            "Components",
            "Workspace",
            "DashboardInspector.razor"));
        var filterRows = File.ReadAllText(Path.Combine(
            root,
            "src",
            "FluxMq.UI",
            "Components",
            "Workspace",
            "DashboardInspectorEventFilterRows.razor"));

        inspector.ShouldContain("DashboardInspectorEventFilterRows");
        inspector.ShouldNotContain("private RenderFragment RenderFilterField");
        inspector.ShouldNotContain("QosFilterOptions");
        inspector.ShouldNotContain("RetainFilterOptions");
        filterRows.ShouldContain("PropertyGridSelect Value=\"@Draft.EventType\"");
        filterRows.ShouldContain("PropertyGridSelect Value=\"@Draft.Status\"");
        filterRows.ShouldContain("FilterChanged");
        filterRows.ShouldContain("QosFilterOptions");
        filterRows.ShouldContain("RetainFilterOptions");
        filterRows.ShouldContain("DashboardEventFilterCatalog.AttributeFilterKey(\"qos\")");
        filterRows.ShouldContain("DashboardEventFilterCatalog.AttributeFilterKey(\"retain\")");
    }

    [Fact]
    public void DashboardInspector_UsesFocusedMetricVisualizationRowComponent()
    {
        var root = FindRepositoryRoot();
        var inspector = File.ReadAllText(Path.Combine(
            root,
            "src",
            "FluxMq.UI",
            "Components",
            "Workspace",
            "DashboardInspector.razor"));
        var visualRows = File.ReadAllText(Path.Combine(
            root,
            "src",
            "FluxMq.UI",
            "Components",
            "Workspace",
            "DashboardInspectorMetricVisualizationRows.razor"));

        inspector.ShouldContain("DashboardInspectorMetricVisualizationRows");
        inspector.ShouldNotContain("private RenderFragment RenderMetricVisualizationProperty");
        inspector.ShouldNotContain("HorizontalAlignmentOptions");
        inspector.ShouldNotContain("MetricVisualizationSegmentOptions");
        visualRows.ShouldContain("<PropertyGridColorPicker");
        visualRows.ShouldContain("<PropertyGridIconSegment");
        visualRows.ShouldContain("HorizontalAlignmentOptions");
        visualRows.ShouldContain("ValuePlacementOptions");
        visualRows.ShouldContain("PropertyChanged");
    }

    [Fact]
    public void DashboardInspector_UsesFocusedVisualMetricRowComponent()
    {
        var root = FindRepositoryRoot();
        var inspector = File.ReadAllText(Path.Combine(
            root,
            "src",
            "FluxMq.UI",
            "Components",
            "Workspace",
            "DashboardInspector.razor"));
        var visualRows = File.ReadAllText(Path.Combine(
            root,
            "src",
            "FluxMq.UI",
            "Components",
            "Workspace",
            "DashboardInspectorVisualMetricRows.razor"));

        inspector.ShouldContain("DashboardInspectorVisualMetricRows");
        inspector.ShouldNotContain("SetMetricCardColumnsFromEventAsync");
        inspector.ShouldNotContain("private static string VisualMetricLabel");
        inspector.ShouldNotContain("PropertyGridRow Name=\"@InspectorLabels.PrimaryCardRow\"");
        visualRows.ShouldContain("PropertyGridRow Name=\"@Labels.PrimaryCardRow\"");
        visualRows.ShouldContain("PropertyGridRow Name=\"@Labels.AddCardRow\"");
        visualRows.ShouldContain("PropertyGridRow Name=\"@Labels.ColumnsRow\"");
        visualRows.ShouldContain("DashboardInspectorMetricMove");
        visualRows.ShouldContain("VisualMetricLabel");
        visualRows.ShouldContain("CardColumnsChanged");
    }

    [Fact]
    public void DashboardInspector_UsesFocusedDisplayModeRowComponent()
    {
        var root = FindRepositoryRoot();
        var inspector = File.ReadAllText(Path.Combine(
            root,
            "src",
            "FluxMq.UI",
            "Components",
            "Workspace",
            "DashboardInspector.razor"));
        var displayRows = File.ReadAllText(Path.Combine(
            root,
            "src",
            "FluxMq.UI",
            "Components",
            "Workspace",
            "DashboardInspectorDisplayModeRows.razor"));

        inspector.ShouldContain("DashboardInspectorDisplayModeRows");
        inspector.ShouldNotContain("private static string ChoiceClass");
        inspector.ShouldNotContain("PropertyGridRow Name=\"@InspectorLabels.GaugeRow\"");
        inspector.ShouldNotContain("PropertyGridRow Name=\"@InspectorLabels.ChartRow\"");
        inspector.ShouldNotContain("PropertyGridRow Name=\"@InspectorLabels.TopicSystemRow\"");
        displayRows.ShouldContain("PropertyGridRow Name=\"@Labels.GaugeRow\"");
        displayRows.ShouldContain("PropertyGridRow Name=\"Min\"");
        displayRows.ShouldContain("PropertyGridColorPicker");
        displayRows.ShouldContain("GaugePropertyChanged");
        displayRows.ShouldContain("PropertyGridRow Name=\"@Labels.ChartRow\"");
        displayRows.ShouldContain("PropertyGridRow Name=\"@Labels.TopicSystemRow\"");
        displayRows.ShouldContain("GaugeStyleChanged");
        displayRows.ShouldContain("ChartTypeChanged");
        displayRows.ShouldContain("ExcludeSystemTopicsChanged");
    }

    [Fact]
    public void DashboardInspector_SplitsDraftLoadingLifecycle()
    {
        var root = FindRepositoryRoot();
        var inspector = File.ReadAllText(Path.Combine(
            root,
            "src",
            "FluxMq.UI",
            "Components",
            "Workspace",
            "DashboardInspector.razor"));

        inspector.ShouldContain("LoadSelectedCellDraft();");
        inspector.ShouldContain("ClearWidgetDraftState();");
        inspector.ShouldContain("LoadWidgetDraftState(Widget);");
        inspector.ShouldContain("private void LoadMetricDraftState");
        inspector.ShouldContain("private DashboardMetricSnapshot? ResolveMetricSnapshot");
        inspector.ShouldContain("private DashboardMetricSnapshot? CreateLegacyMetricSnapshot");
        inspector.ShouldNotContain("protected override void OnParametersSet()\r\n    {\r\n        var selectedCell");
    }

    [Fact]
    public void DashboardDesigner_EmptyCellLabelsStayBoundedWhenGridShrinks()
    {
        var root = FindRepositoryRoot();
        var markup = File.ReadAllText(Path.Combine(
            root,
            "src",
            "FluxMq.UI",
            "Components",
            "Workspace",
            "DashboardDesigner.razor"));
        var css = File.ReadAllText(Path.Combine(
            root,
            "src",
            "FluxMq.UI",
            "Components",
            "Workspace",
            "DashboardDesigner.razor.css"));

        markup.ShouldContain("dashboard-cell-placeholder");
        css.ShouldContain(".dashboard-cell-placeholder");
        css.ShouldContain("align-items: flex-start;");
        css.ShouldContain("max-width: 100%;");
        css.ShouldContain(".dashboard-cell-span");
        css.ShouldContain("text-overflow: ellipsis;");
    }

    [Fact]
    public void DashboardMetricValueWidgets_UseSharedVisualizationView()
    {
        var root = FindRepositoryRoot();
        var widgetsPath = Path.Combine(root, "src", "FluxMq.UI", "Components", "Workspace", "DashboardWidgets");
        var widgetView = File.ReadAllText(Path.Combine(root, "src", "FluxMq.UI", "Components", "Workspace", "DashboardWidgetView.razor"));
        var kpi = File.ReadAllText(Path.Combine(widgetsPath, "DashboardKpiTileModuleView.razor"));
        var counter = File.ReadAllText(Path.Combine(widgetsPath, "DashboardEventCounterModuleView.razor"));
        var eventRate = File.ReadAllText(Path.Combine(widgetsPath, "DashboardEventRateModuleView.razor"));
        var rateTile = File.ReadAllText(Path.Combine(widgetsPath, "DashboardRateTileModuleView.razor"));
        var statusValue = File.ReadAllText(Path.Combine(widgetsPath, "DashboardStatusValueModuleView.razor"));
        var eventGaugeModule = File.ReadAllText(Path.Combine(widgetsPath, "DashboardEventGaugeModuleView.razor"));
        var eventGauge = File.ReadAllText(Path.Combine(widgetsPath, "DashboardEventGaugeWidget.razor"));

        kpi.ShouldContain("DashboardMetricVisualizationHost");
        counter.ShouldContain("DashboardMetricValueVisualizationView");
        eventRate.ShouldContain("DashboardMetricValueVisualizationView");
        rateTile.ShouldContain("DashboardMetricValueVisualizationView");
        statusValue.ShouldContain("DashboardMetricValueVisualizationView");
        statusValue.ShouldNotContain("DashboardMetricTile");
        eventGaugeModule.ShouldContain("Metric=\"@Context.MetricValue\"");
        eventGaugeModule.ShouldNotContain("Context.Snapshot");
        eventGauge.ShouldContain("DashboardMetricValue Metric");
        eventGauge.ShouldContain("Metric.FormattedValue");
        eventGauge.ShouldNotContain("PrimaryMetricCard");
        eventGauge.ShouldNotContain("DashboardEventSnapshot Snapshot");
        eventRate.ShouldNotContain("DashboardEventRateWidget");
        eventRate.ShouldNotContain("Context.Snapshot");
        widgetView.ShouldContain("DashboardWidgetCatalog.EventCounterType");
        widgetView.ShouldContain("DashboardWidgetCatalog.EventRateType");
        widgetView.ShouldContain("DashboardWidgetCatalog.RateTileType");
        widgetView.ShouldContain("DashboardWidgetCatalog.StatusValueType");
    }

    [Fact]
    public void FlowWorkspaceService_DelegatesDashboardMetricResolutionToBridge()
    {
        var root = FindRepositoryRoot();
        var service = File.ReadAllText(Path.Combine(root, "src", "FluxMq.UI", "Services", "FlowWorkspaceService.cs"));
        var bridge = File.ReadAllText(Path.Combine(root, "src", "FluxMq.UI", "Services", "DashboardMetricValueBridge.cs"));

        service.ShouldContain("DashboardMetricValueBridge");
        service.ShouldNotContain("DashboardMetricRegistry _dashboardMetricRegistry");
        service.ShouldNotContain("FluxMetricResolver _metricResolver");
        bridge.ShouldContain("ResolveMetricWidget");
        bridge.ShouldContain("GetMetricValue");
        bridge.ShouldContain("TryGetMetricReading");
    }

    [Fact]
    public void DashboardPreviewSampleFactory_CreatesDesignOnlyTrafficFromWidgetFilters()
    {
        var widget = new DashboardWidgetSnapshot(
            "published",
            DashboardWidgetCatalog.LineChartType,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [DashboardEventFilterCatalog.EventTypeKey] = FluxMqEventTypes.MqttMessageReceived,
                [DashboardEventFilterCatalog.TopicStartsWithKey] = "factory/line-b",
                [DashboardEventFilterCatalog.StatusKey] = "received",
                [DashboardEventFilterCatalog.AttributeFilterKey("qos")] = "2",
                [DashboardEventFilterCatalog.AttributeFilterKey("retain")] = "true"
            });

        var sample = DashboardPreviewSampleFactory.Create(widget);

        sample.Snapshot.Count.ShouldBeGreaterThan(0);
        sample.Snapshot.BucketCounts.Count.ShouldBe(12);
        sample.Snapshot.TopicCounts.ShouldNotBeEmpty();
        foreach (var flowEvent in sample.Snapshot.Events)
        {
            flowEvent.Type.ShouldBe(FluxMqEventTypes.MqttMessageReceived);
            flowEvent.Status.ShouldBe("received");
            flowEvent.Channel.ShouldNotBeNull();
            flowEvent.Channel.StartsWith("factory/line-b/", StringComparison.Ordinal).ShouldBeTrue();
            flowEvent.GetAttribute("qos").ShouldBe("2");
            string.Equals(flowEvent.GetAttribute("retain"), bool.TrueString, StringComparison.OrdinalIgnoreCase)
                .ShouldBeTrue();
        }

        sample.TopicMessages.ShouldNotBeEmpty();
    }

    [Fact]
    public void DashboardPreviewSampleFactory_UsesTopicTreeDefaults()
    {
        var widget = new DashboardWidgetSnapshot(
            "topics",
            DashboardWidgetCatalog.TopicTreeType,
            new Dictionary<string, string>(StringComparer.Ordinal));

        var sample = DashboardPreviewSampleFactory.Create(widget);

        sample.TopicMessages.ShouldNotBeEmpty();
        sample.Snapshot.UniqueTopicCount.ShouldBeGreaterThan(0);
    }

    [Fact]
    public void Find_ReturnsEventSpecificFieldDescriptors()
    {
        var catalog = new DashboardEventFilterCatalog();

        var any = catalog.Find(string.Empty);
        var fileWritten = catalog.Find(FluxMqEventTypes.FileWritten);
        var schemaValidated = catalog.Find(FluxMqEventTypes.JsonSchemaValidated);
        var assertion = catalog.Find(FluxMqEventTypes.AssertionEvaluated);

        any.Fields.ShouldBeEmpty();
        any.Label.ShouldBe("All runtime events");
        any.StatusOptions.Select(static option => option.Value).ShouldBe([""]);
        DashboardEventFilterCatalog.ShouldExposeStatus(any).ShouldBeFalse();

        var mqttReceived = catalog.Find(FluxMqEventTypes.MqttMessageReceived);
        mqttReceived.Fields.Select(static field => field.Key).ShouldBe([
            DashboardEventFilterCatalog.TopicStartsWithKey,
            DashboardEventFilterCatalog.TopicNotStartsWithKey,
            DashboardEventFilterCatalog.AttributeFilterKey("qos"),
            DashboardEventFilterCatalog.AttributeFilterKey("retain")
        ]);
        DashboardEventFilterCatalog.ShouldExposeStatus(mqttReceived).ShouldBeFalse();

        var fileField = fileWritten.Fields.ShouldHaveSingleItem();
        fileField.Key.ShouldBe(DashboardEventFilterCatalog.SubjectStartsWithKey);
        fileField.Label.ShouldBe("File path");
        fileField.Placeholder.ShouldBe("logs/");
        DashboardEventFilterCatalog.ShouldExposeStatus(fileWritten).ShouldBeFalse();

        schemaValidated.Fields.Select(static field => field.Key).ShouldBe([
            DashboardEventFilterCatalog.TopicStartsWithKey,
            DashboardEventFilterCatalog.AttributeFilterKey("schemaId")
        ]);
        schemaValidated.Fields[1].AttributeName.ShouldBe("schemaId");
        DashboardEventFilterCatalog.ShouldExposeStatus(schemaValidated).ShouldBeTrue();

        assertion.Fields.Select(static field => field.Key).ShouldBe([
            DashboardEventFilterCatalog.SubjectStartsWithKey
        ]);
        assertion.Fields[0].Label.ShouldBe("Assertion");
        assertion.StatusOptions.Select(static option => option.Value).ShouldBe(["", "passed", "failed"]);
        DashboardEventFilterCatalog.ShouldExposeStatus(assertion).ShouldBeTrue();
    }

    [Fact]
    public void Matches_UsesSubjectPrefixForFileWrittenEvents()
    {
        var catalog = new DashboardEventFilterCatalog();
        var widget = new DashboardWidgetSnapshot(
            "written",
            DashboardWidgetCatalog.EventCounterType,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [DashboardEventFilterCatalog.EventTypeKey] = FluxMqEventTypes.FileWritten,
                [DashboardEventFilterCatalog.SubjectStartsWithKey] = "logs/",
                [DashboardEventFilterCatalog.TopicStartsWithKey] = string.Empty,
                [DashboardEventFilterCatalog.StatusKey] = "written"
            });

        catalog.Matches(widget, Event(FluxMqEventTypes.FileWritten, subject: "logs/a.json", status: "written")).ShouldBeTrue();
        catalog.Matches(widget, Event(FluxMqEventTypes.FileWritten, subject: "archive/a.json", status: "written")).ShouldBeFalse();
        catalog.Matches(widget, Event(FluxMqEventTypes.FileWritten, topic: "logs/a.json", subject: "archive/a.json", status: "written")).ShouldBeFalse();
    }

    [Fact]
    public void Matches_UsesAttributeFieldForJsonSchemaEvents()
    {
        var catalog = new DashboardEventFilterCatalog();
        var widget = new DashboardWidgetSnapshot(
            "schema",
            DashboardWidgetCatalog.EventCounterType,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [DashboardEventFilterCatalog.EventTypeKey] = FluxMqEventTypes.JsonSchemaValidated,
                [DashboardEventFilterCatalog.TopicStartsWithKey] = "factory/",
                [DashboardEventFilterCatalog.AttributeFilterKey("schemaId")] = "temperature",
                [DashboardEventFilterCatalog.StatusKey] = "valid"
            });

        catalog.Matches(
            widget,
            Event(
                FluxMqEventTypes.JsonSchemaValidated,
                topic: "factory/line-a",
                status: "valid",
                attributes: new Dictionary<string, string> { ["schemaId"] = "temperature" })).ShouldBeTrue();
        catalog.Matches(
            widget,
            Event(
                FluxMqEventTypes.JsonSchemaValidated,
                topic: "factory/line-a",
                status: "valid",
                attributes: new Dictionary<string, string> { ["schemaId"] = "pressure" })).ShouldBeFalse();
    }

    [Fact]
    public void Matches_UsesMqttQosAndRetainAttributes()
    {
        var catalog = new DashboardEventFilterCatalog();
        var widget = new DashboardWidgetSnapshot(
            "published",
            DashboardWidgetCatalog.EventCounterType,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [DashboardEventFilterCatalog.EventTypeKey] = FluxMqEventTypes.MqttMessagePublished,
                [DashboardEventFilterCatalog.TopicStartsWithKey] = "test",
                [DashboardEventFilterCatalog.AttributeFilterKey("qos")] = "1",
                [DashboardEventFilterCatalog.AttributeFilterKey("retain")] = "false",
                [DashboardEventFilterCatalog.StatusKey] = "published"
            });

        catalog.Matches(
            widget,
            Event(
                FluxMqEventTypes.MqttMessagePublished,
                topic: "test",
                status: "published",
                attributes: new Dictionary<string, string>
                {
                    ["qos"] = "1",
                    ["retain"] = "False"
                })).ShouldBeTrue();
        catalog.Matches(
            widget,
            Event(
                FluxMqEventTypes.MqttMessagePublished,
                topic: "test",
                status: "published",
                attributes: new Dictionary<string, string>
                {
                    ["qos"] = "1",
                    ["retain"] = "True"
                })).ShouldBeFalse();
    }

    [Fact]
    public void Matches_ExcludesMqttTopicPrefix()
    {
        var catalog = new DashboardEventFilterCatalog();
        var widget = new DashboardWidgetSnapshot(
            "received",
            DashboardWidgetCatalog.EventCounterType,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [DashboardEventFilterCatalog.EventTypeKey] = FluxMqEventTypes.MqttMessageReceived,
                [DashboardEventFilterCatalog.TopicStartsWithKey] = string.Empty,
                [DashboardEventFilterCatalog.TopicNotStartsWithKey] = "$SYS/",
                [DashboardEventFilterCatalog.StatusKey] = "received"
            });

        catalog.Matches(
            widget,
            Event(
                FluxMqEventTypes.MqttMessageReceived,
                topic: "test",
                status: "received")).ShouldBeTrue();
        catalog.Matches(
            widget,
            Event(
                FluxMqEventTypes.MqttMessageReceived,
                topic: "$SYS/broker/bytes/sent",
                status: "received")).ShouldBeFalse();
    }

    [Fact]
    public void Matches_UsesAssertionNameSubjectForAssertionEvents()
    {
        var catalog = new DashboardEventFilterCatalog();
        var widget = new DashboardWidgetSnapshot(
            "assertions",
            DashboardWidgetCatalog.EventCounterType,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [DashboardEventFilterCatalog.EventTypeKey] = FluxMqEventTypes.AssertionEvaluated,
                [DashboardEventFilterCatalog.SubjectStartsWithKey] = "QoS at least once",
                [DashboardEventFilterCatalog.StatusKey] = "passed"
            });

        catalog.Matches(
            widget,
            Event(
                FluxMqEventTypes.AssertionEvaluated,
                topic: "factory/line-a",
                subject: "QoS at least once",
                status: "passed")).ShouldBeTrue();
        catalog.Matches(
            widget,
            Event(
                FluxMqEventTypes.AssertionEvaluated,
                topic: "QoS at least once",
                subject: "Payload has id",
                status: "passed")).ShouldBeFalse();
    }

    private static FlowEvent Event(
        string type,
        string? topic = null,
        string? subject = null,
        string? status = null,
        IReadOnlyDictionary<string, string>? attributes = null)
        => new()
        {
            Timestamp = DateTimeOffset.Parse("2026-05-25T10:00:00Z"),
            Type = type,
            Source = "test",
            Channel = topic,
            Subject = subject,
            Status = status,
            Attributes = attributes ?? new Dictionary<string, string>(StringComparer.Ordinal)
        };

    private static FluxMetricQueryDraft CreateKpiQueryDraft()
        => FluxMetricQueryDraft.Create(
            metric: null,
            metricName: DashboardWidgetCatalog.KpiTileType);

    private static IReadOnlyDictionary<string, string> ToConfigurationDictionary(
        System.Text.Json.Nodes.JsonObject configuration)
        => configuration.ToDictionary(
            static item => item.Key,
            static item => item.Value?.GetValue<string>() ?? string.Empty,
            StringComparer.Ordinal);

    private static void ShouldMatchConfiguration(
        System.Text.Json.Nodes.JsonObject configuration,
        IReadOnlyDictionary<string, string> expected)
    {
        var actual = ToConfigurationDictionary(configuration);
        actual.Keys.ShouldBe(expected.Keys, ignoreOrder: true);
        foreach (var (key, value) in expected)
        {
            actual[key].ShouldBe(value);
        }
    }

    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "FluxMq.sln")))
        {
            current = current.Parent;
        }

        current.ShouldNotBeNull("Could not locate FluxMq.sln from the test output directory.");
        return current.FullName;
    }
}
