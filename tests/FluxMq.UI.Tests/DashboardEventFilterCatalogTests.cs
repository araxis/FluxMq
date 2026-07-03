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
                [DashboardEventGaugeWidgetOptions.MinKey] = "20",
                [DashboardEventGaugeWidgetOptions.MaxKey] = "120",
                [DashboardEventGaugeWidgetOptions.TargetKey] = "90",
                [DashboardEventGaugeWidgetOptions.WarningKey] = "70",
                [DashboardEventGaugeWidgetOptions.CriticalKey] = "100",
                [DashboardEventGaugeWidgetOptions.NormalColorKey] = "#11aa99",
                [DashboardEventGaugeWidgetOptions.WarningColorKey] = "#ffaa00",
                [DashboardEventGaugeWidgetOptions.CriticalColorKey] = "#ff3355"
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

        var value = registry.Evaluate("event.count", snapshot);

        value.Label.ShouldBe("Event count");
        value.Value.ShouldBe(18);
        value.Unit.ShouldBe("events");
        value.FormattedValue.ShouldBe("18");
        value.FormattedValue.ShouldNotContain("events");
    }

    [Fact]
    public void DashboardMetricRegistry_MapsSnapshotAggregateAndLabelPerMetricType()
    {
        var registry = new DashboardMetricRegistry();
        var snapshot = new DashboardEventSnapshot(
            60,
            LatestEvent: null,
            RecentCount: 18,
            RateWindow: TimeSpan.FromSeconds(60),
            EventsPerSecond: 0.3,
            Events: [],
            BucketCounts: [],
            TopicCounts: [],
            TotalPayloadBytes: 2048,
            UniqueTopicCount: 4,
            RetainedCount: 2);

        var topics = registry.Evaluate(TopicCountMetric.TypeId, snapshot);
        topics.Label.ShouldBe("Topic count");
        topics.Value.ShouldBe(4);
        topics.Unit.ShouldBe("topics");

        var rate = registry.Evaluate(EventRateMetric.TypeId, snapshot);
        rate.Label.ShouldBe("Event rate");
        rate.Value.ShouldBe(0.3);
        rate.Unit.ShouldBe("/s");

        var bytes = registry.Evaluate(PayloadBytesMetric.TypeId, snapshot);
        bytes.Value.ShouldBe(2048);
        bytes.Unit.ShouldBe("bytes");

        registry.Evaluate(RetainedCountMetric.TypeId, snapshot).Value.ShouldBe(2);
        registry.Evaluate(MessageCountMetric.TypeId, snapshot).Value.ShouldBe(18);
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
                [DashboardWidgetCatalog.KpiTitleAlignKey] = DashboardMetricValueVisualizationOptions.AlignCenter,
                [DashboardWidgetCatalog.KpiValueAlignKey] = DashboardMetricValueVisualizationOptions.AlignRight,
                [DashboardWidgetCatalog.KpiValuePlacementKey] = DashboardMetricValueVisualizationOptions.ValuePlacementBottom,
                [DashboardMetricValueVisualizationOptions.PaddingKey] = "22"
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
        var statusValue = DashboardWidgetSettingsProfiles.For(DashboardWidgetCatalog.StatusValueType);
        var rate = DashboardWidgetSettingsProfiles.For(DashboardWidgetCatalog.EventRateType);
        var rateTile = DashboardWidgetSettingsProfiles.For(DashboardWidgetCatalog.RateTileType);
        var latest = DashboardWidgetSettingsProfiles.For(DashboardWidgetCatalog.LatestEventType);
        var chart = DashboardWidgetSettingsProfiles.For(DashboardWidgetCatalog.LineChartType);
        var area = DashboardWidgetSettingsProfiles.For(DashboardWidgetCatalog.AreaChartType);
        var bar = DashboardWidgetSettingsProfiles.For(DashboardWidgetCatalog.BarChartType);
        var donut = DashboardWidgetSettingsProfiles.For(DashboardWidgetCatalog.DonutChartType);
        var table = DashboardWidgetSettingsProfiles.For(DashboardWidgetCatalog.EventTableType);
        var topicActivity = DashboardWidgetSettingsProfiles.For(DashboardWidgetCatalog.TopicActivityType);
        var tree = DashboardWidgetSettingsProfiles.For(DashboardWidgetCatalog.TopicTreeType);

        kpi.UsesMetricQuery.ShouldBeTrue();
        kpi.UsesMetricVisualization.ShouldBeTrue();
        kpi.UsesVisualMetrics.ShouldBeFalse();
        kpi.UsesMetricAggregation.ShouldBeTrue();
        kpi.UsesMetricWindow.ShouldBeTrue();
        kpi.UsesSubtitle.ShouldBeTrue();
        gauge.IsEventWidget.ShouldBeTrue();
        gauge.UsesMetricQuery.ShouldBeTrue();
        gauge.UsesMetricVisualization.ShouldBeTrue();
        gauge.UsesVisualMetrics.ShouldBeFalse();
        gauge.UsesGaugeStyle.ShouldBeFalse();
        gauge.UsesChartType.ShouldBeFalse();
        gauge.SupportsMetricSlots.ShouldBeFalse();
        gauge.UsesMetricWindow.ShouldBeFalse();
        chart.SupportsMetricSlots.ShouldBeFalse();
        chart.UsesMetricWindow.ShouldBeTrue();
        chart.UsesVisualMetrics.ShouldBeFalse();
        chart.UsesChartType.ShouldBeFalse();
        chart.UsesLineChartVisual.ShouldBeTrue();
        area.SupportsMetricSlots.ShouldBeFalse();
        area.UsesMetricWindow.ShouldBeTrue();
        area.UsesVisualMetrics.ShouldBeFalse();
        area.UsesChartType.ShouldBeFalse();
        area.UsesAreaChartVisual.ShouldBeTrue();
        bar.SupportsMetricSlots.ShouldBeFalse();
        bar.UsesMetricWindow.ShouldBeTrue();
        bar.UsesVisualMetrics.ShouldBeFalse();
        bar.UsesChartType.ShouldBeFalse();
        bar.UsesBarChartVisual.ShouldBeTrue();
        donut.SupportsMetricSlots.ShouldBeFalse();
        donut.UsesMetricWindow.ShouldBeTrue();
        donut.UsesVisualMetrics.ShouldBeFalse();
        donut.UsesChartType.ShouldBeFalse();
        donut.UsesDonutChartVisual.ShouldBeTrue();
        table.IsEventWidget.ShouldBeTrue();
        table.UsesVisualMetrics.ShouldBeFalse();
        table.SupportsMetricSlots.ShouldBeFalse();
        table.UsesMetricWindow.ShouldBeFalse();
        table.UsesEventTableVisual.ShouldBeTrue();
        topicActivity.IsEventWidget.ShouldBeTrue();
        topicActivity.UsesTopicActivityVisual.ShouldBeTrue();
        topicActivity.UsesMetricVisualization.ShouldBeFalse();
        topicActivity.SupportsMetricSlots.ShouldBeFalse();
        tree.IsEventWidget.ShouldBeFalse();
        tree.IsTopicTreeWidget.ShouldBeTrue();
        tree.UsesTopicTreeVisual.ShouldBeTrue();
        tree.UsesMetricQuery.ShouldBeFalse();
        tree.UsesEventFilters.ShouldBeFalse();
        tree.UsesMetricWindow.ShouldBeFalse();

        rate.InspectorLabels.DataGroup.ShouldBe("Rate source");
        rate.UsesMetricVisualization.ShouldBeTrue();
        rateTile.InspectorLabels.DataGroup.ShouldBe("Rate source");
        rateTile.UsesMetricVisualization.ShouldBeTrue();
        statusValue.InspectorLabels.DataGroup.ShouldBe("Status source");
        statusValue.UsesMetricVisualization.ShouldBeTrue();
        kpi.InspectorLabels.DataGroup.ShouldBe("KPI source");
        kpi.InspectorLabels.TimeWindowGroup.ShouldBe("Metric query");
        rate.UsesMetricWindow.ShouldBeFalse();
        rateTile.UsesMetricWindow.ShouldBeFalse();
        rate.InspectorLabels.TimeWindowGroup.ShouldBe("Rate window");
        rate.InspectorLabels.FilterGroup.ShouldBe("Traffic filter");
        gauge.InspectorLabels.DataGroup.ShouldBe("Gauge source");
        gauge.InspectorLabels.MetricRow.ShouldBe("Gauge metric");
        gauge.InspectorLabels.GaugeRow.ShouldBe("Shape");
        latest.InspectorLabels.DataGroup.ShouldBe("Event source");
        latest.InspectorLabels.FilterGroup.ShouldBe("Match rules");
        latest.UsesLatestEventVisual.ShouldBeTrue();
        latest.UsesMetricVisualization.ShouldBeFalse();
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
        DashboardMetricGaugeVisualizationOptions.NormalizeShape(DashboardMetricGaugeVisualizationOptions.ShapeRing)
            .ShouldBe(DashboardMetricGaugeVisualizationOptions.ShapeRing);
        DashboardMetricGaugeVisualizationOptions.NormalizeShape(DashboardMetricGaugeVisualizationOptions.ShapeMeter)
            .ShouldBe(DashboardMetricGaugeVisualizationOptions.ShapeMeter);
        DashboardMetricGaugeVisualizationOptions.NormalizeShape("tiles")
            .ShouldBe(DashboardMetricGaugeVisualizationOptions.ShapeRing);
        DashboardEventGaugeWidgetOptions.NormalizeStyle(DashboardEventGaugeWidgetOptions.StyleRing)
            .ShouldBe(DashboardEventGaugeWidgetOptions.StyleRing);
        DashboardEventGaugeWidgetOptions.NormalizeStyle(DashboardEventGaugeWidgetOptions.StyleMeter)
            .ShouldBe(DashboardEventGaugeWidgetOptions.StyleMeter);
        DashboardEventGaugeWidgetOptions.NormalizeStyle("tiles")
            .ShouldBe(DashboardEventGaugeWidgetOptions.StyleRing);

        var root = FindRepositoryRoot();
        var gaugeOptions = File.ReadAllText(Path.Combine(
            root,
            "src",
            "FluxMq.UI",
            "Services",
            "DashboardMetricGaugeVisualizationOptions.cs"));

        gaugeOptions.ShouldContain("ShapeRing");
        gaugeOptions.ShouldContain("ShapeMeter");
        gaugeOptions.ShouldNotContain("GaugeStyleTiles");
        gaugeOptions.ShouldNotContain("\"tiles\"");
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
                [DashboardLatestEventVisualOptions.LegacyShowPayloadKey] = bool.FalseString,
                [DashboardLatestEventVisualOptions.LegacyTimestampFormatKey] = "hidden",
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
        configuration[DashboardLatestEventVisualOptions.HeaderKey].ShouldBe("Latest event");
        configuration[DashboardLatestEventVisualOptions.ShowPayloadKey].ShouldBe(bool.FalseString);
        configuration[DashboardLatestEventVisualOptions.ShowTimestampKey].ShouldBe(bool.FalseString);
        configuration.ContainsKey(DashboardLatestEventVisualOptions.LegacyShowPayloadKey).ShouldBeFalse();
        configuration.ContainsKey(DashboardLatestEventVisualOptions.LegacyTimestampFormatKey).ShouldBeFalse();
        configuration.ContainsKey(DashboardWidgetCatalog.PrimaryMetricKey).ShouldBeFalse();
        configuration.ContainsKey(DashboardEventGaugeWidgetOptions.StyleKey).ShouldBeFalse();
    }

    [Fact]
    public void DashboardWidgetSettingsDraft_WritesLatestEventVisualConfiguration()
    {
        var draft = DashboardWidgetSettingsDraft.Create(
            new DashboardWidgetSnapshot(
                "latest",
                DashboardWidgetCatalog.LatestEventType,
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["title"] = "Runtime event",
                    [DashboardLatestEventVisualOptions.ShowTopicKey] = bool.FalseString,
                    [DashboardLatestEventVisualOptions.HeaderColorKey] = "#112233"
                }),
            new DashboardEventFilterCatalog());

        draft.SetLatestEventVisualValue(DashboardLatestEventVisualOptions.HeaderKey, "Broker event");
        draft.SetLatestEventVisualValue(DashboardLatestEventVisualOptions.ShowStatusKey, bool.FalseString);
        draft.SetLatestEventVisualValue(DashboardLatestEventVisualOptions.ShowPayloadKey, bool.TrueString);
        draft.SetLatestEventVisualValue(DashboardLatestEventVisualOptions.EmptyTextKey, "Waiting for an event");

        var configuration = draft.BuildConfiguration();

        configuration["title"].ShouldBe("Broker event");
        configuration[DashboardLatestEventVisualOptions.HeaderKey].ShouldBe("Broker event");
        configuration[DashboardLatestEventVisualOptions.ShowTopicKey].ShouldBe(bool.FalseString);
        configuration[DashboardLatestEventVisualOptions.ShowStatusKey].ShouldBe(bool.FalseString);
        configuration[DashboardLatestEventVisualOptions.ShowPayloadKey].ShouldBe(bool.TrueString);
        configuration[DashboardLatestEventVisualOptions.EmptyTextKey].ShouldBe("Waiting for an event");
        configuration[DashboardLatestEventVisualOptions.HeaderColorKey].ShouldBe("#112233");
    }

    [Fact]
    public void DashboardWidgetSettingsDraft_WritesEventTableVisualConfiguration()
    {
        var draft = DashboardWidgetSettingsDraft.Create(
            new DashboardWidgetSnapshot(
                "table",
                DashboardWidgetCatalog.EventTableType,
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["title"] = "Runtime table",
                    [DashboardEventTableVisualOptions.LegacyRowCountKey] = "4",
                    [DashboardEventTableVisualOptions.LegacyPayloadPreviewKey] = bool.FalseString
                }),
            new DashboardEventFilterCatalog());

        draft.SetEventTableVisualValue(DashboardEventTableVisualOptions.HeaderKey, "Recent events");
        draft.SetEventTableVisualValue(DashboardEventTableVisualOptions.RowCountKey, "9");
        draft.SetEventTableVisualValue(DashboardEventTableVisualOptions.DensityKey, DashboardEventTableVisualOptions.DensityComfortable);
        draft.SetEventTableVisualValue(DashboardEventTableVisualOptions.ShowTopicKey, bool.FalseString);
        draft.SetEventTableVisualValue(DashboardEventTableVisualOptions.EmptyTextKey, "Waiting for events");

        var configuration = draft.BuildConfiguration();

        configuration["title"].ShouldBe("Recent events");
        configuration[DashboardEventTableVisualOptions.HeaderKey].ShouldBe("Recent events");
        configuration[DashboardEventTableVisualOptions.RowCountKey].ShouldBe("9");
        configuration[DashboardEventTableVisualOptions.DensityKey].ShouldBe(DashboardEventTableVisualOptions.DensityComfortable);
        configuration[DashboardEventTableVisualOptions.ShowTopicKey].ShouldBe(bool.FalseString);
        configuration[DashboardEventTableVisualOptions.ShowPayloadKey].ShouldBe(bool.FalseString);
        configuration[DashboardEventTableVisualOptions.EmptyTextKey].ShouldBe("Waiting for events");
        configuration.ContainsKey(DashboardEventTableVisualOptions.LegacyRowCountKey).ShouldBeFalse();
        configuration.ContainsKey(DashboardEventTableVisualOptions.LegacyPayloadPreviewKey).ShouldBeFalse();
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
                    [DashboardEventGaugeWidgetOptions.StyleKey] = DashboardEventGaugeWidgetOptions.StyleMeter,
                    [DashboardEventGaugeWidgetOptions.MinKey] = "10",
                    [DashboardEventGaugeWidgetOptions.MaxKey] = "250",
                    [DashboardEventGaugeWidgetOptions.TargetKey] = "200",
                    [DashboardEventGaugeWidgetOptions.WarningKey] = "150",
                    [DashboardEventGaugeWidgetOptions.CriticalKey] = "225",
                    [DashboardEventGaugeWidgetOptions.NormalColorKey] = "#123456",
                    [DashboardEventGaugeWidgetOptions.WarningColorKey] = "#abcdef",
                    [DashboardEventGaugeWidgetOptions.CriticalColorKey] = "#fedcba"
                }),
            new DashboardEventFilterCatalog());

        draft.UsesMetricQueryBuilder.ShouldBeTrue();
        var configuration = draft.BuildConfiguration();

        configuration["title"].ShouldBe("Factory load");
        configuration["metric"].ShouldBe("factoryLoadMetric");
        configuration[DashboardWidgetCatalog.MetricVisualizationKey].ShouldBe(DashboardMetricVisualizationIds.RadialGauge);
        configuration[DashboardMetricGaugeVisualizationOptions.ShapeKey].ShouldBe(DashboardMetricGaugeVisualizationOptions.ShapeMeter);
        configuration[DashboardMetricGaugeVisualizationOptions.LabelKey].ShouldBe("Factory load");
        configuration[DashboardMetricGaugeVisualizationOptions.MinKey].ShouldBe("10");
        configuration[DashboardMetricGaugeVisualizationOptions.MaxKey].ShouldBe("250");
        configuration[DashboardMetricGaugeVisualizationOptions.TargetKey].ShouldBe("200");
        configuration[DashboardMetricGaugeVisualizationOptions.WarningKey].ShouldBe("150");
        configuration[DashboardMetricGaugeVisualizationOptions.CriticalKey].ShouldBe("225");
        configuration[DashboardMetricGaugeVisualizationOptions.NormalColorKey].ShouldBe("#123456");
        configuration[DashboardMetricGaugeVisualizationOptions.WarningColorKey].ShouldBe("#abcdef");
        configuration[DashboardMetricGaugeVisualizationOptions.CriticalColorKey].ShouldBe("#fedcba");
        configuration.ContainsKey(DashboardEventFilterCatalog.EventTypeKey).ShouldBeFalse();
        configuration.ContainsKey(DashboardEventFilterCatalog.TopicStartsWithKey).ShouldBeFalse();
        configuration.ContainsKey(DashboardEventFilterCatalog.StatusKey).ShouldBeFalse();
        configuration.ContainsKey(DashboardWidgetCatalog.PrimaryMetricKey).ShouldBeFalse();
        configuration.ContainsKey(DashboardWidgetCatalog.DisplayMetricsKey).ShouldBeFalse();
        configuration.ContainsKey(DashboardWidgetCatalog.MetricCardColumnsKey).ShouldBeFalse();
        configuration.ContainsKey(DashboardEventGaugeWidgetOptions.StyleKey).ShouldBeFalse();
        configuration.ContainsKey(DashboardEventGaugeWidgetOptions.MaxKey).ShouldBeFalse();
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

        draft.SetTopicTreeVisualValue(DashboardTopicTreeVisualOptions.HeaderKey, "Broker topics");
        draft.SetTopicTreeVisualValue(DashboardTopicTreeVisualOptions.ShowSummaryKey, bool.FalseString);
        draft.SetTopicTreeVisualValue(DashboardTopicTreeVisualOptions.ShowMessageCountKey, bool.FalseString);
        draft.SetTopicTreeVisualValue(DashboardTopicTreeVisualOptions.ExcludeSystemTopicsKey, bool.TrueString);
        draft.SetTopicTreeVisualValue(DashboardTopicTreeVisualOptions.AccentColorKey, "#33ccaa");

        var configuration = draft.BuildConfiguration();

        configuration["title"].ShouldBe("Broker topics");
        configuration[DashboardTopicTreeVisualOptions.HeaderKey].ShouldBe("Broker topics");
        configuration[DashboardTopicTreeVisualOptions.ShowSummaryKey].ShouldBe(bool.FalseString);
        configuration[DashboardTopicTreeVisualOptions.ShowTopicCountKey].ShouldBe(bool.TrueString);
        configuration[DashboardTopicTreeVisualOptions.ShowMessageCountKey].ShouldBe(bool.FalseString);
        configuration[DashboardTopicTreeVisualOptions.ExcludeSystemTopicsKey].ShouldBe(bool.TrueString);
        configuration[DashboardTopicTreeVisualOptions.EmptyTextKey].ShouldBe(DashboardTopicTreeVisualOptions.DefaultEmptyText);
        configuration[DashboardTopicTreeVisualOptions.HeaderColorKey].ShouldBe(DashboardTopicTreeVisualOptions.DefaultHeaderColor);
        configuration[DashboardTopicTreeVisualOptions.TextColorKey].ShouldBe(DashboardTopicTreeVisualOptions.DefaultTextColor);
        configuration[DashboardTopicTreeVisualOptions.MutedColorKey].ShouldBe(DashboardTopicTreeVisualOptions.DefaultMutedColor);
        configuration[DashboardTopicTreeVisualOptions.AccentColorKey].ShouldBe("#33ccaa");
    }

    [Fact]
    public void DashboardWidgetSettingsDraft_BuildsTopicActivityConfiguration()
    {
        var draft = DashboardWidgetSettingsDraft.Create(
            new DashboardWidgetSnapshot(
                "activity",
                DashboardWidgetCatalog.TopicActivityType,
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["title"] = "Topics",
                    ["limit"] = "18",
                    [DashboardEventFilterCatalog.EventTypeKey] = FluxMqEventTypes.MqttMessagePublished,
                    [DashboardEventFilterCatalog.TopicStartsWithKey] = "factory/"
                }),
            new DashboardEventFilterCatalog());

        draft.SetTopicActivityVisualValue(DashboardTopicActivityVisualOptions.HeaderKey, "Hot topics");
        draft.SetTopicActivityVisualValue(DashboardTopicActivityVisualOptions.LimitKey, "3");
        draft.SetTopicActivityVisualValue(DashboardTopicActivityVisualOptions.ShowCountsKey, bool.FalseString);
        draft.SetTopicActivityVisualValue(DashboardTopicActivityVisualOptions.AccentColorKey, "#44ddbb");

        var configuration = draft.BuildConfiguration();

        configuration["title"].ShouldBe("Hot topics");
        configuration[DashboardTopicActivityVisualOptions.HeaderKey].ShouldBe("Hot topics");
        configuration[DashboardTopicActivityVisualOptions.ShowHeaderKey].ShouldBe(bool.TrueString);
        configuration[DashboardTopicActivityVisualOptions.LimitKey].ShouldBe("3");
        configuration[DashboardTopicActivityVisualOptions.ShowCountsKey].ShouldBe(bool.FalseString);
        configuration[DashboardTopicActivityVisualOptions.EmptyTextKey].ShouldBe(DashboardTopicActivityVisualOptions.DefaultEmptyText);
        configuration[DashboardTopicActivityVisualOptions.HeaderColorKey].ShouldBe(DashboardTopicActivityVisualOptions.DefaultHeaderColor);
        configuration[DashboardTopicActivityVisualOptions.TextColorKey].ShouldBe(DashboardTopicActivityVisualOptions.DefaultTextColor);
        configuration[DashboardTopicActivityVisualOptions.MutedColorKey].ShouldBe(DashboardTopicActivityVisualOptions.DefaultMutedColor);
        configuration[DashboardTopicActivityVisualOptions.AccentColorKey].ShouldBe("#44ddbb");
        configuration[DashboardEventFilterCatalog.EventTypeKey].ShouldBe(FluxMqEventTypes.MqttMessagePublished);
        configuration[DashboardEventFilterCatalog.TopicStartsWithKey].ShouldBe("factory/");
        configuration.ContainsKey("limit").ShouldBeFalse();
    }

    [Fact]
    public void DashboardWidgetSettingsDraft_BuildsLineChartVisualConfiguration()
    {
        var draft = DashboardWidgetSettingsDraft.Create(
            new DashboardWidgetSnapshot(
                "throughput",
                DashboardWidgetCatalog.LineChartType,
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["title"] = "Throughput",
                    [DashboardChartWidgetOptions.TypeKey] = DashboardChartWidgetOptions.TypeArea,
                    [DashboardLineChartVisualOptions.LegacyShowGridKey] = bool.FalseString,
                    [DashboardLineChartVisualOptions.LegacyShowPointsKey] = bool.TrueString,
                    [DashboardLineChartVisualOptions.LegacyLineColorKey] = "#112233",
                    [DashboardWidgetCatalog.PrimaryMetricKey] = DashboardWidgetCatalog.MetricMessages,
                    [DashboardEventFilterCatalog.EventTypeKey] = FluxMqEventTypes.MqttMessageReceived
                }),
            new DashboardEventFilterCatalog());

        draft.SetLineChartVisualValue(DashboardLineChartVisualOptions.HeaderKey, "Received throughput");
        draft.SetLineChartVisualValue(DashboardLineChartVisualOptions.ShowGridKey, bool.TrueString);
        draft.SetLineChartVisualValue(DashboardLineChartVisualOptions.ShowLabelsKey, bool.FalseString);
        draft.SetLineChartVisualValue(DashboardLineChartVisualOptions.LineWidthKey, "5");
        draft.SetLineChartVisualValue(DashboardLineChartVisualOptions.EmptyTextKey, "No throughput");
        draft.SetLineChartVisualValue(DashboardLineChartVisualOptions.LineColorKey, "#33ccaa");
        draft.SetLineChartVisualValue(DashboardLineChartVisualOptions.GridColorKey, "#203040");
        draft.SetLineChartVisualValue(DashboardLineChartVisualOptions.LabelColorKey, "#8899aa");

        var configuration = draft.BuildConfiguration();

        configuration["title"].ShouldBe("Received throughput");
        configuration[DashboardChartWidgetOptions.TypeKey].ShouldBe(DashboardChartWidgetOptions.TypeLine);
        configuration[DashboardLineChartVisualOptions.HeaderKey].ShouldBe("Received throughput");
        configuration[DashboardLineChartVisualOptions.ShowHeaderKey].ShouldBe(bool.TrueString);
        configuration[DashboardLineChartVisualOptions.ShowGridKey].ShouldBe(bool.TrueString);
        configuration[DashboardLineChartVisualOptions.ShowLabelsKey].ShouldBe(bool.FalseString);
        configuration[DashboardLineChartVisualOptions.ShowPointsKey].ShouldBe(bool.TrueString);
        configuration[DashboardLineChartVisualOptions.LineWidthKey].ShouldBe("5");
        configuration[DashboardLineChartVisualOptions.EmptyTextKey].ShouldBe("No throughput");
        configuration[DashboardLineChartVisualOptions.LineColorKey].ShouldBe("#33ccaa");
        configuration[DashboardLineChartVisualOptions.GridColorKey].ShouldBe("#203040");
        configuration[DashboardLineChartVisualOptions.LabelColorKey].ShouldBe("#8899aa");
        configuration[DashboardEventFilterCatalog.EventTypeKey].ShouldBe(FluxMqEventTypes.MqttMessageReceived);
        configuration.ContainsKey(DashboardWidgetCatalog.PrimaryMetricKey).ShouldBeFalse();
        configuration.ContainsKey(DashboardLineChartVisualOptions.LegacyShowGridKey).ShouldBeFalse();
        configuration.ContainsKey(DashboardLineChartVisualOptions.LegacyLineColorKey).ShouldBeFalse();
    }

    [Fact]
    public void DashboardWidgetSettingsDraft_BuildsAreaChartVisualConfiguration()
    {
        var draft = DashboardWidgetSettingsDraft.Create(
            new DashboardWidgetSnapshot(
                "area",
                DashboardWidgetCatalog.AreaChartType,
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["title"] = "Area",
                    [DashboardChartWidgetOptions.TypeKey] = DashboardChartWidgetOptions.TypeLine,
                    [DashboardAreaChartVisualOptions.LegacyShowGridKey] = bool.FalseString,
                    [DashboardAreaChartVisualOptions.LegacyShowPointsKey] = bool.TrueString,
                    [DashboardAreaChartVisualOptions.LegacyLineColorKey] = "#112233",
                    [DashboardAreaChartVisualOptions.LegacyFillColorKey] = "#445566",
                    [DashboardAreaChartVisualOptions.LegacyFillOpacityKey] = "0.5",
                    [DashboardWidgetCatalog.PrimaryMetricKey] = DashboardWidgetCatalog.MetricMessages,
                    [DashboardEventFilterCatalog.EventTypeKey] = FluxMqEventTypes.MqttMessagePublished
                }),
            new DashboardEventFilterCatalog());

        draft.SetAreaChartVisualValue(DashboardAreaChartVisualOptions.HeaderKey, "Published area");
        draft.SetAreaChartVisualValue(DashboardAreaChartVisualOptions.ShowGridKey, bool.TrueString);
        draft.SetAreaChartVisualValue(DashboardAreaChartVisualOptions.ShowLabelsKey, bool.FalseString);
        draft.SetAreaChartVisualValue(DashboardAreaChartVisualOptions.LineWidthKey, "4");
        draft.SetAreaChartVisualValue(DashboardAreaChartVisualOptions.FillOpacityKey, "42");
        draft.SetAreaChartVisualValue(DashboardAreaChartVisualOptions.EmptyTextKey, "No area data");
        draft.SetAreaChartVisualValue(DashboardAreaChartVisualOptions.LineColorKey, "#33ccaa");
        draft.SetAreaChartVisualValue(DashboardAreaChartVisualOptions.FillColorKey, "#225544");
        draft.SetAreaChartVisualValue(DashboardAreaChartVisualOptions.GridColorKey, "#203040");
        draft.SetAreaChartVisualValue(DashboardAreaChartVisualOptions.LabelColorKey, "#8899aa");

        var configuration = draft.BuildConfiguration();

        configuration["title"].ShouldBe("Published area");
        configuration[DashboardChartWidgetOptions.TypeKey].ShouldBe(DashboardChartWidgetOptions.TypeArea);
        configuration[DashboardAreaChartVisualOptions.HeaderKey].ShouldBe("Published area");
        configuration[DashboardAreaChartVisualOptions.ShowHeaderKey].ShouldBe(bool.TrueString);
        configuration[DashboardAreaChartVisualOptions.ShowGridKey].ShouldBe(bool.TrueString);
        configuration[DashboardAreaChartVisualOptions.ShowLabelsKey].ShouldBe(bool.FalseString);
        configuration[DashboardAreaChartVisualOptions.ShowPointsKey].ShouldBe(bool.TrueString);
        configuration[DashboardAreaChartVisualOptions.LineWidthKey].ShouldBe("4");
        configuration[DashboardAreaChartVisualOptions.FillOpacityKey].ShouldBe("42");
        configuration[DashboardAreaChartVisualOptions.EmptyTextKey].ShouldBe("No area data");
        configuration[DashboardAreaChartVisualOptions.LineColorKey].ShouldBe("#33ccaa");
        configuration[DashboardAreaChartVisualOptions.FillColorKey].ShouldBe("#225544");
        configuration[DashboardAreaChartVisualOptions.GridColorKey].ShouldBe("#203040");
        configuration[DashboardAreaChartVisualOptions.LabelColorKey].ShouldBe("#8899aa");
        configuration[DashboardEventFilterCatalog.EventTypeKey].ShouldBe(FluxMqEventTypes.MqttMessagePublished);
        configuration.ContainsKey(DashboardWidgetCatalog.PrimaryMetricKey).ShouldBeFalse();
        configuration.ContainsKey(DashboardAreaChartVisualOptions.LegacyFillColorKey).ShouldBeFalse();
        configuration.ContainsKey(DashboardAreaChartVisualOptions.LegacyFillOpacityKey).ShouldBeFalse();
    }

    [Fact]
    public void DashboardWidgetSettingsDraft_BuildsBarChartVisualConfiguration()
    {
        var draft = DashboardWidgetSettingsDraft.Create(
            new DashboardWidgetSnapshot(
                "bars",
                DashboardWidgetCatalog.BarChartType,
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["title"] = "Bars",
                    [DashboardChartWidgetOptions.TypeKey] = DashboardChartWidgetOptions.TypeArea,
                    [DashboardBarChartVisualOptions.LegacyShowGridKey] = bool.FalseString,
                    [DashboardBarChartVisualOptions.LegacyOrientationKey] = DashboardBarChartVisualOptions.OrientationHorizontal,
                    [DashboardBarChartVisualOptions.LegacyBarColorKey] = "#112233",
                    [DashboardWidgetCatalog.PrimaryMetricKey] = DashboardWidgetCatalog.MetricMessages,
                    [DashboardEventFilterCatalog.EventTypeKey] = FluxMqEventTypes.MqttMessageReceived
                }),
            new DashboardEventFilterCatalog());

        draft.SetBarChartVisualValue(DashboardBarChartVisualOptions.HeaderKey, "Received bars");
        draft.SetBarChartVisualValue(DashboardBarChartVisualOptions.ShowGridKey, bool.TrueString);
        draft.SetBarChartVisualValue(DashboardBarChartVisualOptions.ShowLabelsKey, bool.FalseString);
        draft.SetBarChartVisualValue(DashboardBarChartVisualOptions.OrientationKey, DashboardBarChartVisualOptions.OrientationHorizontal);
        draft.SetBarChartVisualValue(DashboardBarChartVisualOptions.BarRadiusKey, "6");
        draft.SetBarChartVisualValue(DashboardBarChartVisualOptions.EmptyTextKey, "No bar data");
        draft.SetBarChartVisualValue(DashboardBarChartVisualOptions.BarColorKey, "#33ccaa");
        draft.SetBarChartVisualValue(DashboardBarChartVisualOptions.GridColorKey, "#203040");
        draft.SetBarChartVisualValue(DashboardBarChartVisualOptions.LabelColorKey, "#8899aa");

        var configuration = draft.BuildConfiguration();

        configuration["title"].ShouldBe("Received bars");
        configuration[DashboardChartWidgetOptions.TypeKey].ShouldBe(DashboardChartWidgetOptions.TypeBars);
        configuration[DashboardBarChartVisualOptions.HeaderKey].ShouldBe("Received bars");
        configuration[DashboardBarChartVisualOptions.ShowHeaderKey].ShouldBe(bool.TrueString);
        configuration[DashboardBarChartVisualOptions.ShowGridKey].ShouldBe(bool.TrueString);
        configuration[DashboardBarChartVisualOptions.ShowLabelsKey].ShouldBe(bool.FalseString);
        configuration[DashboardBarChartVisualOptions.OrientationKey].ShouldBe(DashboardBarChartVisualOptions.OrientationHorizontal);
        configuration[DashboardBarChartVisualOptions.BarRadiusKey].ShouldBe("6");
        configuration[DashboardBarChartVisualOptions.EmptyTextKey].ShouldBe("No bar data");
        configuration[DashboardBarChartVisualOptions.BarColorKey].ShouldBe("#33ccaa");
        configuration[DashboardBarChartVisualOptions.GridColorKey].ShouldBe("#203040");
        configuration[DashboardBarChartVisualOptions.LabelColorKey].ShouldBe("#8899aa");
        configuration[DashboardEventFilterCatalog.EventTypeKey].ShouldBe(FluxMqEventTypes.MqttMessageReceived);
        configuration.ContainsKey(DashboardWidgetCatalog.PrimaryMetricKey).ShouldBeFalse();
        configuration.ContainsKey(DashboardBarChartVisualOptions.LegacyBarColorKey).ShouldBeFalse();
        configuration.ContainsKey(DashboardBarChartVisualOptions.LegacyOrientationKey).ShouldBeFalse();
    }

    [Fact]
    public void DashboardWidgetSettingsDraft_BuildsDonutChartVisualConfiguration()
    {
        var draft = DashboardWidgetSettingsDraft.Create(
            new DashboardWidgetSnapshot(
                "topics",
                DashboardWidgetCatalog.DonutChartType,
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["title"] = "Topics",
                    [DashboardChartWidgetOptions.TypeKey] = DashboardChartWidgetOptions.TypeBars,
                    [DashboardDonutChartVisualOptions.LegacyLimitKey] = "9",
                    [DashboardDonutChartVisualOptions.LegacyGroupByKey] = "topic",
                    [DashboardDonutChartVisualOptions.LegacyPaletteKey] = "cool",
                    [DashboardWidgetCatalog.PrimaryMetricKey] = DashboardWidgetCatalog.MetricMessages,
                    [DashboardEventFilterCatalog.EventTypeKey] = FluxMqEventTypes.MqttMessageReceived
                }),
            new DashboardEventFilterCatalog());

        draft.SetDonutChartVisualValue(DashboardDonutChartVisualOptions.HeaderKey, "Topic mix");
        draft.SetDonutChartVisualValue(DashboardDonutChartVisualOptions.ShowLegendKey, bool.FalseString);
        draft.SetDonutChartVisualValue(DashboardDonutChartVisualOptions.ShowTotalKey, bool.FalseString);
        draft.SetDonutChartVisualValue(DashboardDonutChartVisualOptions.LimitKey, "4");
        draft.SetDonutChartVisualValue(DashboardDonutChartVisualOptions.InnerRadiusKey, "62");
        draft.SetDonutChartVisualValue(DashboardDonutChartVisualOptions.EmptyTextKey, "No categories");
        draft.SetDonutChartVisualValue(DashboardDonutChartVisualOptions.SegmentColor1Key, "#33ccaa");
        draft.SetDonutChartVisualValue(DashboardDonutChartVisualOptions.SegmentColor2Key, "#225544");
        draft.SetDonutChartVisualValue(DashboardDonutChartVisualOptions.LabelColorKey, "#f2f6ff");
        draft.SetDonutChartVisualValue(DashboardDonutChartVisualOptions.MutedColorKey, "#8899aa");

        var configuration = draft.BuildConfiguration();

        configuration["title"].ShouldBe("Topic mix");
        configuration[DashboardChartWidgetOptions.TypeKey].ShouldBe(DashboardChartWidgetOptions.TypeTopics);
        configuration[DashboardDonutChartVisualOptions.HeaderKey].ShouldBe("Topic mix");
        configuration[DashboardDonutChartVisualOptions.ShowHeaderKey].ShouldBe(bool.TrueString);
        configuration[DashboardDonutChartVisualOptions.ShowLegendKey].ShouldBe(bool.FalseString);
        configuration[DashboardDonutChartVisualOptions.ShowTotalKey].ShouldBe(bool.FalseString);
        configuration[DashboardDonutChartVisualOptions.LimitKey].ShouldBe("4");
        configuration[DashboardDonutChartVisualOptions.InnerRadiusKey].ShouldBe("62");
        configuration[DashboardDonutChartVisualOptions.EmptyTextKey].ShouldBe("No categories");
        configuration[DashboardDonutChartVisualOptions.SegmentColor1Key].ShouldBe("#33ccaa");
        configuration[DashboardDonutChartVisualOptions.SegmentColor2Key].ShouldBe("#225544");
        configuration[DashboardDonutChartVisualOptions.LabelColorKey].ShouldBe("#f2f6ff");
        configuration[DashboardDonutChartVisualOptions.MutedColorKey].ShouldBe("#8899aa");
        configuration[DashboardEventFilterCatalog.EventTypeKey].ShouldBe(FluxMqEventTypes.MqttMessageReceived);
        configuration.ContainsKey(DashboardWidgetCatalog.PrimaryMetricKey).ShouldBeFalse();
        configuration.ContainsKey(DashboardDonutChartVisualOptions.LegacyLimitKey).ShouldBeFalse();
        configuration.ContainsKey(DashboardDonutChartVisualOptions.LegacyPaletteKey).ShouldBeFalse();
        configuration.ContainsKey(DashboardDonutChartVisualOptions.LegacyGroupByKey).ShouldBeFalse();
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
        configuration[DashboardWidgetCatalog.MetricVisualizationKey].ShouldBe(DashboardMetricVisualizationIds.Value);
        configuration[DashboardMetricValueVisualizationOptions.TitleKey].ShouldBe("Factory issues");
        configuration[DashboardMetricValueVisualizationOptions.SubtitleKey].ShouldBe(DashboardMetricValueVisualizationOptions.DefaultSubtitle);
        configuration[DashboardMetricValueVisualizationOptions.UnitTextKey].ShouldBeEmpty();
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
        draft.MetricVisualization.ValueTitleAlign = DashboardMetricValueVisualizationOptions.AlignCenter;
        draft.MetricVisualization.ValueValueAlign = DashboardMetricValueVisualizationOptions.AlignRight;
        draft.MetricVisualization.ValueValuePlacement = DashboardMetricValueVisualizationOptions.ValuePlacementMiddle;
        draft.MetricVisualization.ValuePadding = 22;
        draft.MetricVisualization.ValueFitMode = DashboardMetricValueVisualizationOptions.FitCompact;

        var configuration = draft.BuildConfiguration();

        configuration[DashboardWidgetCatalog.MetricVisualizationKey].ShouldBe(DashboardMetricVisualizationIds.Value);
        configuration.ContainsKey(DashboardWidgetCatalog.PrimaryMetricKey).ShouldBeFalse();
        configuration[DashboardMetricValueVisualizationOptions.TitleKey].ShouldBe("Messages");
        configuration[DashboardMetricValueVisualizationOptions.SubtitleKey].ShouldBe("Total matching events");
        configuration[DashboardMetricValueVisualizationOptions.ShowUnitKey].ShouldBe("false");
        configuration[DashboardMetricValueVisualizationOptions.UnitTextKey].ShouldBe("messages");
        configuration[DashboardMetricValueVisualizationOptions.TitleColorKey].ShouldBe("#112233");
        configuration[DashboardMetricValueVisualizationOptions.SubtitleColorKey].ShouldBe("#445566");
        configuration[DashboardMetricValueVisualizationOptions.ValueColorKey].ShouldBe("#778899");
        configuration[DashboardMetricValueVisualizationOptions.UnitColorKey].ShouldBe("#99aabb");
        configuration[DashboardMetricValueVisualizationOptions.TitleAlignKey].ShouldBe(DashboardMetricValueVisualizationOptions.AlignCenter);
        configuration[DashboardMetricValueVisualizationOptions.ValueAlignKey].ShouldBe(DashboardMetricValueVisualizationOptions.AlignRight);
        configuration[DashboardMetricValueVisualizationOptions.ValuePlacementKey].ShouldBe(DashboardMetricValueVisualizationOptions.ValuePlacementMiddle);
        configuration[DashboardMetricValueVisualizationOptions.PaddingKey].ShouldBe("22");
        configuration[DashboardMetricValueVisualizationOptions.FitModeKey].ShouldBe(DashboardMetricValueVisualizationOptions.FitCompact);
        configuration.ContainsKey("title").ShouldBeFalse();
        configuration.ContainsKey("subtitle").ShouldBeFalse();
        configuration.ContainsKey(DashboardWidgetCatalog.KpiTitleColorKey).ShouldBeFalse();
        configuration.ContainsKey(DashboardWidgetCatalog.KpiSubtitleColorKey).ShouldBeFalse();
        configuration.ContainsKey(DashboardWidgetCatalog.KpiValueColorKey).ShouldBeFalse();
        configuration.ContainsKey(DashboardMetricDigitalVisualizationOptions.StyleKey).ShouldBeFalse();
        configuration.ContainsKey(DashboardMetricDigitalVisualizationOptions.GlowKey).ShouldBeFalse();
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
                    [DashboardMetricDigitalVisualizationOptions.StyleKey] = DashboardMetricDigitalVisualizationOptions.StyleTerminal,
                    [DashboardMetricDigitalVisualizationOptions.GlowKey] = DashboardMetricDigitalVisualizationOptions.GlowStrong,
                    [DashboardMetricDigitalVisualizationOptions.BackgroundColorKey] = "#01020344",
                    [DashboardMetricDigitalVisualizationOptions.SegmentColorKey] = "#aabbcc80",
                    [DashboardMetricDigitalVisualizationOptions.InactiveSegmentColorKey] = "#112233",
                    [DashboardMetricDigitalVisualizationOptions.LabelColorKey] = "#ddeeff",
                    [DashboardMetricDigitalVisualizationOptions.DigitsKey] = "6",
                    [DashboardMetricDigitalVisualizationOptions.AlignKey] = DashboardMetricDigitalVisualizationOptions.AlignRight,
                    [DashboardMetricDigitalVisualizationOptions.PlacementKey] = DashboardMetricDigitalVisualizationOptions.PlacementTop
                }),
            new DashboardEventFilterCatalog());

        draft.MetricVisualizationId.ShouldBe(DashboardMetricVisualizationIds.Digital);
        draft.MetricVisualization.DigitalStyle.ShouldBe(DashboardMetricDigitalVisualizationOptions.StyleTerminal);
        draft.MetricVisualization.DigitalGlow.ShouldBe(DashboardMetricDigitalVisualizationOptions.GlowStrong);
        draft.MetricVisualization.DigitalBackgroundColor.ShouldBe("#01020344");
        draft.MetricVisualization.DigitalSegmentColor.ShouldBe("#aabbcc80");
        draft.MetricVisualization.DigitalInactiveSegmentColor.ShouldBe("#112233");
        draft.MetricVisualization.DigitalLabelColor.ShouldBe("#ddeeff");
        draft.MetricVisualization.DigitalDigits.ShouldBe(6);
        draft.MetricVisualization.DigitalAlign.ShouldBe(DashboardMetricDigitalVisualizationOptions.AlignRight);
        draft.MetricVisualization.DigitalPlacement.ShouldBe(DashboardMetricDigitalVisualizationOptions.PlacementTop);

        var configuration = draft.BuildConfiguration();

        configuration[DashboardWidgetCatalog.MetricVisualizationKey]
            .ShouldBe(DashboardMetricVisualizationIds.Digital);
        configuration[DashboardMetricDigitalVisualizationOptions.StyleKey]
            .ShouldBe(DashboardMetricDigitalVisualizationOptions.StyleTerminal);
        configuration[DashboardMetricDigitalVisualizationOptions.GlowKey]
            .ShouldBe(DashboardMetricDigitalVisualizationOptions.GlowStrong);
        configuration[DashboardMetricDigitalVisualizationOptions.BackgroundColorKey].ShouldBe("#01020344");
        configuration[DashboardMetricDigitalVisualizationOptions.SegmentColorKey].ShouldBe("#aabbcc80");
        configuration[DashboardMetricDigitalVisualizationOptions.InactiveSegmentColorKey].ShouldBe("#112233");
        configuration[DashboardMetricDigitalVisualizationOptions.LabelColorKey].ShouldBe("#ddeeff");
        configuration[DashboardMetricDigitalVisualizationOptions.DigitsKey].ShouldBe("6");
        configuration[DashboardMetricDigitalVisualizationOptions.AlignKey]
            .ShouldBe(DashboardMetricDigitalVisualizationOptions.AlignRight);
        configuration[DashboardMetricDigitalVisualizationOptions.PlacementKey]
            .ShouldBe(DashboardMetricDigitalVisualizationOptions.PlacementTop);
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
                    ["subtitle"] = "Published factory events",
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
        configuration[DashboardWidgetCatalog.MetricVisualizationKey].ShouldBe(DashboardMetricVisualizationIds.Value);
        configuration[DashboardMetricValueVisualizationOptions.TitleKey].ShouldBe("Factory events");
        configuration[DashboardMetricValueVisualizationOptions.SubtitleKey].ShouldBe("Published factory events");
        configuration.ContainsKey(DashboardEventFilterCatalog.EventTypeKey).ShouldBeFalse();
        configuration.ContainsKey(DashboardEventFilterCatalog.TopicStartsWithKey).ShouldBeFalse();
        configuration.ContainsKey(DashboardEventFilterCatalog.StatusKey).ShouldBeFalse();
        configuration.ContainsKey(DashboardWidgetCatalog.PrimaryMetricKey).ShouldBeFalse();
        configuration.ContainsKey("subtitle").ShouldBeFalse();
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
        configuration[DashboardWidgetCatalog.MetricVisualizationKey].ShouldBe(DashboardMetricVisualizationIds.Value);
        configuration[DashboardMetricValueVisualizationOptions.TitleKey].ShouldBe("Factory rate");
        configuration[DashboardMetricValueVisualizationOptions.SubtitleKey].ShouldBe(DashboardMetricValueVisualizationOptions.DefaultSubtitle);
        configuration[DashboardMetricValueVisualizationOptions.UnitTextKey].ShouldBeEmpty();
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
        configuration[DashboardWidgetCatalog.MetricVisualizationKey].ShouldBe(DashboardMetricVisualizationIds.Value);
        configuration[DashboardMetricValueVisualizationOptions.TitleKey].ShouldBe("Factory rate");
        configuration[DashboardMetricValueVisualizationOptions.SubtitleKey].ShouldBe(DashboardMetricValueVisualizationOptions.DefaultSubtitle);
        configuration[DashboardMetricValueVisualizationOptions.UnitTextKey].ShouldBeEmpty();
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
                    [DashboardWidgetCatalog.KpiTitleAlignKey] = DashboardMetricValueVisualizationOptions.AlignCenter,
                    [DashboardWidgetCatalog.KpiValueAlignKey] = DashboardMetricValueVisualizationOptions.AlignRight,
                    [DashboardWidgetCatalog.KpiValuePlacementKey] = DashboardMetricValueVisualizationOptions.ValuePlacementBottom
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
        configuration[DashboardMetricValueVisualizationOptions.TitleKey].ShouldBe("Messages");
        configuration[DashboardMetricValueVisualizationOptions.SubtitleKey].ShouldBe("Total matching events");
        configuration[DashboardMetricValueVisualizationOptions.TitleColorKey].ShouldBe(DashboardMetricValueVisualizationOptions.DefaultTitleColor);
        configuration[DashboardMetricValueVisualizationOptions.SubtitleColorKey].ShouldBe(DashboardMetricValueVisualizationOptions.DefaultSubtitleColor);
        configuration[DashboardMetricValueVisualizationOptions.ValueColorKey].ShouldBe(DashboardMetricValueVisualizationOptions.DefaultValueColor);
        configuration[DashboardMetricValueVisualizationOptions.TitleAlignKey].ShouldBe(DashboardMetricValueVisualizationOptions.AlignLeft);
        configuration[DashboardMetricValueVisualizationOptions.ValueAlignKey].ShouldBe(DashboardMetricValueVisualizationOptions.AlignLeft);
        configuration[DashboardMetricValueVisualizationOptions.ValuePlacementKey].ShouldBe(DashboardMetricValueVisualizationOptions.ValuePlacementTop);
        configuration.ContainsKey("title").ShouldBeFalse();
        configuration.ContainsKey("subtitle").ShouldBeFalse();
        configuration.ContainsKey(DashboardWidgetCatalog.KpiTitleColorKey).ShouldBeFalse();
    }

    [Fact]
    public void DashboardMetricReferenceResolver_ResolvesAppMetricParameters()
    {
        var project = new FlowWorkspaceService(new FlowDefinitionComposer());
        project.AddMetric("publishedMetric");
        project.UpdateMetric(
            "publishedMetric",
            new FluxMetricResourceDefinition
            {
                TypeId = "event.count",
                DisplayName = "Published metric",
                Parameters = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["window"] = "60s",
                    ["eventType"] = FluxMqEventTypes.MqttMessagePublished,
                    ["topicStartsWith"] = "default/",
                    ["status"] = "published"
                }
            });
        var parameterValues = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["topicStartsWith"] = "factory/line-a/"
        };

        var resource = DashboardMetricReferenceResolver.ResolveAppMetric(
            project,
            "publishedMetric",
            parameterValues);
        var snapshot = DashboardMetricReferenceResolver.ResolveAppMetricSnapshot(
            project,
            "publishedMetric",
            parameterValues);

        resource.ShouldNotBeNull();
        resource.GetParameter("topicStartsWith").ShouldBe("factory/line-a/");
        snapshot.ShouldNotBeNull();
        snapshot.ReadFilter(DashboardEventFilterCatalog.TopicStartsWithKey).ShouldBe("factory/line-a/");
        snapshot.ReadFilter(DashboardEventFilterCatalog.EventTypeKey).ShouldBe(FluxMqEventTypes.MqttMessagePublished);
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

        valueMarkup.ShouldContain("DashboardMetricValueVisualizationOptions.ShowUnitKey");
        valueMarkup.ShouldContain("DashboardMetricValueVisualizationOptions.UnitTextKey");
        valueMarkup.ShouldContain("DashboardMetricValueVisualizationOptions.UnitColorKey");
        valueMarkup.ShouldContain("DisplayUnitText");
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
        inspector.ShouldContain("DashboardInspectorAppMetricRows");
        inspector.ShouldContain("DashboardWidgetCatalog.KpiTileType");
        inspector.ShouldContain("DashboardWidgetCatalog.StatusValueType");
        inspector.ShouldContain("DashboardWidgetCatalog.EventGaugeType");
        inspector.ShouldContain("DashboardWidgetCatalog.RateTileType");
        inspector.ShouldContain("DashboardWidgetCatalog.EventCounterType");
        inspector.ShouldContain("DashboardWidgetCatalog.EventRateType");
        inspector.ShouldNotContain("IsMetricQueryBuilderWidget");
        inspector.ShouldNotContain("OpenMetricBuilderAsync");
        inspector.ShouldNotContain("DashboardMetricQueryBuilderDialog");
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
            .ShouldContain(DashboardWidgetCatalog.MetricVisualizationKey);
        modules
            .Single(static module => module.Type == DashboardWidgetCatalog.EventCounterType)
            .DefaultConfiguration[DashboardMetricValueVisualizationOptions.TitleKey]
            .ShouldBe("Events");
        modules
            .Single(static module => module.Type == DashboardWidgetCatalog.EventCounterType)
            .DefaultConfiguration[DashboardMetricValueVisualizationOptions.SubtitleKey]
            .ShouldBe("All runtime events");
        DashboardWidgetSettingsProfiles.For(DashboardWidgetCatalog.EventCounterType)
            .UsesMetricVisualization
            .ShouldBeTrue();
        modules
            .Single(static module => module.Type == DashboardWidgetCatalog.EventRateType)
            .DefaultConfiguration
            .Keys
            .ShouldContain(DashboardWidgetCatalog.MetricVisualizationKey);
        modules
            .Single(static module => module.Type == DashboardWidgetCatalog.EventRateType)
            .DefaultConfiguration[DashboardMetricValueVisualizationOptions.TitleKey]
            .ShouldBe("Event rate");
        modules
            .Single(static module => module.Type == DashboardWidgetCatalog.EventRateType)
            .DefaultConfiguration[DashboardMetricValueVisualizationOptions.SubtitleKey]
            .ShouldBe("All runtime events");
        DashboardWidgetSettingsProfiles.For(DashboardWidgetCatalog.EventRateType)
            .UsesMetricVisualization
            .ShouldBeTrue();
        modules
            .Single(static module => module.Type == DashboardWidgetCatalog.RateTileType)
            .DefaultConfiguration
            .Keys
            .ShouldContain(DashboardWidgetCatalog.MetricVisualizationKey);
        modules
            .Single(static module => module.Type == DashboardWidgetCatalog.RateTileType)
            .DefaultConfiguration[DashboardMetricValueVisualizationOptions.TitleKey]
            .ShouldBe("Rate tile");
        modules
            .Single(static module => module.Type == DashboardWidgetCatalog.RateTileType)
            .DefaultConfiguration[DashboardMetricValueVisualizationOptions.SubtitleKey]
            .ShouldBe("Selected rate metric");
        DashboardWidgetSettingsProfiles.For(DashboardWidgetCatalog.RateTileType)
            .UsesMetricVisualization
            .ShouldBeTrue();
        modules
            .Single(static module => module.Type == DashboardWidgetCatalog.StatusValueType)
            .DefaultConfiguration
            .Keys
            .ShouldContain(DashboardWidgetCatalog.MetricVisualizationKey);
        modules
            .Single(static module => module.Type == DashboardWidgetCatalog.StatusValueType)
            .DefaultConfiguration[DashboardMetricValueVisualizationOptions.TitleKey]
            .ShouldBe("Status value");
        modules
            .Single(static module => module.Type == DashboardWidgetCatalog.StatusValueType)
            .DefaultConfiguration[DashboardMetricValueVisualizationOptions.SubtitleKey]
            .ShouldBe("Selected status metric");
        DashboardWidgetSettingsProfiles.For(DashboardWidgetCatalog.StatusValueType)
            .UsesMetricVisualization
            .ShouldBeTrue();
        modules
            .Single(static module => module.Type == DashboardWidgetCatalog.EventGaugeType)
            .DefaultConfiguration
            .Keys
            .ShouldBe([
                "title",
                DashboardWidgetCatalog.MetricVisualizationKey,
                DashboardMetricGaugeVisualizationOptions.ShapeKey,
                DashboardMetricGaugeVisualizationOptions.LabelKey,
                DashboardMetricGaugeVisualizationOptions.ShowLabelKey,
                DashboardMetricGaugeVisualizationOptions.MinKey,
                DashboardMetricGaugeVisualizationOptions.MaxKey,
                DashboardMetricGaugeVisualizationOptions.TargetKey,
                DashboardMetricGaugeVisualizationOptions.WarningKey,
                DashboardMetricGaugeVisualizationOptions.CriticalKey,
                DashboardMetricGaugeVisualizationOptions.NormalColorKey,
                DashboardMetricGaugeVisualizationOptions.WarningColorKey,
                DashboardMetricGaugeVisualizationOptions.CriticalColorKey
            ], ignoreOrder: true);
        modules
            .Single(static module => module.Type == DashboardWidgetCatalog.EventGaugeType)
            .DefaultConfiguration[DashboardWidgetCatalog.MetricVisualizationKey]
            .ShouldBe(DashboardMetricVisualizationIds.RadialGauge);
        modules
            .Single(static module => module.Type == DashboardWidgetCatalog.EventGaugeType)
            .DefaultConfiguration[DashboardMetricGaugeVisualizationOptions.LabelKey]
            .ShouldBe("Event gauge");
        modules
            .Single(static module => module.Type == DashboardWidgetCatalog.EventGaugeType)
            .DefaultConfiguration[DashboardMetricGaugeVisualizationOptions.MaxKey]
            .ShouldBe(DashboardMetricGaugeVisualizationOptions.DefaultMax);
        DashboardWidgetSettingsProfiles.For(DashboardWidgetCatalog.EventGaugeType)
            .UsesMetricVisualization
            .ShouldBeTrue();
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
            .Single(static module => module.Type == DashboardWidgetCatalog.EventGaugeType)
            .MetricVisualizationId
            .ShouldBe(DashboardMetricVisualizationIds.RadialGauge);
        modules
            .Single(static module => module.Type == DashboardWidgetCatalog.EventGaugeType)
            .PropertyGroups
            .SelectMany(static group => group.Properties)
            .Select(static property => property.Key)
            .ShouldNotContain(DashboardEventGaugeWidgetOptions.StyleKey);
        modules
            .Single(static module => module.Type == DashboardWidgetCatalog.KpiTileType)
            .PropertyGroups
            .SelectMany(static group => group.Properties)
            .Select(static property => property.Key)
            .ShouldNotContain(DashboardWidgetCatalog.PrimaryMetricKey);
        modules
            .Single(static module => module.Type == DashboardWidgetCatalog.RateTileType)
            .PropertyGroups
            .SelectMany(static group => group.Properties)
            .Select(static property => property.Key)
            .ShouldNotContain(DashboardWidgetCatalog.PrimaryMetricKey);
        modules
            .Single(static module => module.Type == DashboardWidgetCatalog.StatusValueType)
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
    public void DashboardWidgetCatalog_IsStaticAndNotRegisteredAsAService()
    {
        var root = FindRepositoryRoot();
        var catalogPath = Path.Combine(root, "src", "FluxMq.UI", "Services", "DashboardWidgetCatalog.cs");
        var mauiProgramPath = Path.Combine(root, "src", "FluxMq.UI", "MauiProgram.cs");
        var workspaceComponentsPath = Path.Combine(root, "src", "FluxMq.UI", "Components", "Workspace");

        var catalogSource = File.ReadAllText(catalogPath);
        catalogSource.ShouldContain("public static class DashboardWidgetCatalog");
        catalogSource.ShouldNotContain("MetricDigital");
        catalogSource.ShouldNotContain("MetricValue");
        catalogSource.ShouldNotContain("GaugeStyleKey");
        catalogSource.ShouldNotContain("GaugeDefault");
        catalogSource.ShouldNotContain("ChartTypeKey");
        catalogSource.ShouldNotContain("ChartTypeBars");
        File.ReadAllText(mauiProgramPath).ShouldNotContain("AddSingleton<DashboardWidgetCatalog>");

        Directory
            .EnumerateFiles(workspaceComponentsPath, "*.razor", SearchOption.AllDirectories)
            .Select(File.ReadAllText)
            .Any(static source => source.Contains("@inject DashboardWidgetCatalog", StringComparison.Ordinal))
            .ShouldBeFalse();
    }

    [Fact]
    public void DashboardChartWidgetOptions_OwnsChartDefaults()
    {
        DashboardChartWidgetOptions.TypeKey.ShouldBe("chartType");
        DashboardChartWidgetOptions.TypeBars.ShouldBe("bars");
        DashboardChartWidgetOptions.TypeLine.ShouldBe("line");
        DashboardChartWidgetOptions.TypeArea.ShouldBe("area");
        DashboardChartWidgetOptions.TypeTopics.ShouldBe("topics");

        DashboardChartWidgetOptions.NormalizeType("unknown")
            .ShouldBe(DashboardChartWidgetOptions.TypeBars);
        DashboardChartWidgetOptions.NormalizeType(DashboardChartWidgetOptions.TypeLine)
            .ShouldBe(DashboardChartWidgetOptions.TypeLine);
        DashboardChartWidgetOptions.NormalizeType(DashboardChartWidgetOptions.TypeArea)
            .ShouldBe(DashboardChartWidgetOptions.TypeArea);
        DashboardChartWidgetOptions.NormalizeType(DashboardChartWidgetOptions.TypeTopics)
            .ShouldBe(DashboardChartWidgetOptions.TypeTopics);
    }

    [Fact]
    public void DashboardEventGaugeWidgetOptions_OwnsGaugeDefaults()
    {
        DashboardEventGaugeWidgetOptions.StyleKey.ShouldBe("gaugeStyle");
        DashboardEventGaugeWidgetOptions.MinKey.ShouldBe("gauge.min");
        DashboardEventGaugeWidgetOptions.MaxKey.ShouldBe("gauge.max");
        DashboardEventGaugeWidgetOptions.DefaultNormalColor.ShouldBe("#2ed3c6");
        DashboardEventGaugeWidgetOptions.DefaultWarningColor.ShouldBe("#f4b642");
        DashboardEventGaugeWidgetOptions.DefaultCriticalColor.ShouldBe("#ff5f6d");

        DashboardEventGaugeWidgetOptions.NormalizeStyle("unknown")
            .ShouldBe(DashboardEventGaugeWidgetOptions.StyleRing);
        DashboardEventGaugeWidgetOptions.NormalizeStyle(DashboardEventGaugeWidgetOptions.StyleMeter)
            .ShouldBe(DashboardEventGaugeWidgetOptions.StyleMeter);
    }

    [Fact]
    public void DashboardMetricDigitalVisualizationOptions_OwnsDigitalVisualDefaults()
    {
        DashboardMetricDigitalVisualizationOptions.LabelKey.ShouldBe("metric.digital.label");
        DashboardMetricDigitalVisualizationOptions.BackgroundColorKey.ShouldBe("metric.digital.backgroundColor");
        DashboardMetricDigitalVisualizationOptions.DefaultLabel.ShouldBe("Messages");

        DashboardMetricDigitalVisualizationOptions.NormalizeStyle("unknown")
            .ShouldBe(DashboardMetricDigitalVisualizationOptions.StylePanel);
        DashboardMetricDigitalVisualizationOptions.NormalizeStyle(DashboardMetricDigitalVisualizationOptions.StyleTerminal)
            .ShouldBe(DashboardMetricDigitalVisualizationOptions.StyleTerminal);
        DashboardMetricDigitalVisualizationOptions.NormalizeGlow("unknown")
            .ShouldBe(DashboardMetricDigitalVisualizationOptions.GlowSoft);
        DashboardMetricDigitalVisualizationOptions.NormalizeLabelPlacement("unknown")
            .ShouldBe(DashboardMetricDigitalVisualizationOptions.LabelPlacementBottom);
        DashboardMetricDigitalVisualizationOptions.NormalizeFitMode("unknown")
            .ShouldBe(DashboardMetricDigitalVisualizationOptions.FitCompact);
        DashboardMetricDigitalVisualizationOptions.NormalizeAlignment("unknown")
            .ShouldBe(DashboardMetricDigitalVisualizationOptions.AlignCenter);
        DashboardMetricDigitalVisualizationOptions.NormalizeAlignment(DashboardMetricDigitalVisualizationOptions.AlignRight)
            .ShouldBe(DashboardMetricDigitalVisualizationOptions.AlignRight);
        DashboardMetricDigitalVisualizationOptions.NormalizePlacement("unknown")
            .ShouldBe(DashboardMetricDigitalVisualizationOptions.PlacementMiddle);
        DashboardMetricDigitalVisualizationOptions.NormalizePlacement(DashboardMetricDigitalVisualizationOptions.PlacementBottom)
            .ShouldBe(DashboardMetricDigitalVisualizationOptions.PlacementBottom);
        DashboardMetricDigitalVisualizationOptions.NormalizeDigits(0)
            .ShouldBe(DashboardMetricDigitalVisualizationOptions.MinDigits);
        DashboardMetricDigitalVisualizationOptions.NormalizeDigits(99)
            .ShouldBe(DashboardMetricDigitalVisualizationOptions.MaxDigits);
        DashboardMetricDigitalVisualizationOptions.NormalizeDigits("bad")
            .ShouldBe(DashboardMetricDigitalVisualizationOptions.DefaultDigits);
    }

    [Fact]
    public void DashboardMetricValueVisualizationOptions_OwnsValueVisualDefaults()
    {
        DashboardMetricValueVisualizationOptions.TitleKey.ShouldBe("metric.value.title");
        DashboardMetricValueVisualizationOptions.ValueColorKey.ShouldBe("metric.value.valueColor");
        DashboardMetricValueVisualizationOptions.DefaultTitle.ShouldBe("Messages");
        DashboardMetricValueVisualizationOptions.DefaultUnitColor
            .ShouldBe(DashboardMetricValueVisualizationOptions.DefaultSubtitleColor);

        DashboardMetricValueVisualizationOptions.NormalizeHorizontalAlignment("unknown")
            .ShouldBe(DashboardMetricValueVisualizationOptions.AlignLeft);
        DashboardMetricValueVisualizationOptions.NormalizeHorizontalAlignment(DashboardMetricValueVisualizationOptions.AlignRight)
            .ShouldBe(DashboardMetricValueVisualizationOptions.AlignRight);
        DashboardMetricValueVisualizationOptions.NormalizeValuePlacement("unknown")
            .ShouldBe(DashboardMetricValueVisualizationOptions.ValuePlacementTop);
        DashboardMetricValueVisualizationOptions.NormalizeValuePlacement(DashboardMetricValueVisualizationOptions.ValuePlacementBottom)
            .ShouldBe(DashboardMetricValueVisualizationOptions.ValuePlacementBottom);
        DashboardMetricValueVisualizationOptions.NormalizeFitMode("unknown")
            .ShouldBe(DashboardMetricValueVisualizationOptions.FitFill);
        DashboardMetricValueVisualizationOptions.NormalizeFitMode(DashboardMetricValueVisualizationOptions.FitCompact)
            .ShouldBe(DashboardMetricValueVisualizationOptions.FitCompact);
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
            .ShouldContain(DashboardWidgetCatalog.MetricVisualizationKey);
        modules
            .Single(static module => module.Type == DashboardWidgetCatalog.EventCounterType)
            .PropertyGroups
            .SelectMany(static group => group.Properties)
            .Select(static property => property.Key)
            .ShouldNotContain("unit");
        modules
            .Single(static module => module.Type == DashboardWidgetCatalog.LatestEventType)
            .DefaultConfiguration
            .Keys
            .ShouldContain(DashboardEventFilterCatalog.EventTypeKey);
        modules
            .Single(static module => module.Type == DashboardWidgetCatalog.LatestEventType)
            .DefaultConfiguration
            .Keys
            .ShouldContain(DashboardLatestEventVisualOptions.HeaderKey);
        modules
            .Single(static module => module.Type == DashboardWidgetCatalog.LatestEventType)
            .PropertyGroups
            .SelectMany(static group => group.Properties)
            .Select(static property => property.Key)
            .ShouldNotContain(DashboardLatestEventVisualOptions.LegacyShowPayloadKey);
        modules
            .Single(static module => module.Type == DashboardWidgetCatalog.LatestEventType)
            .PropertyGroups
            .SelectMany(static group => group.Properties)
            .Select(static property => property.Key)
            .ShouldContain(DashboardLatestEventVisualOptions.ShowPayloadKey);
        modules
            .Single(static module => module.Type == DashboardWidgetCatalog.EventGaugeType)
            .DefaultConfiguration[DashboardWidgetCatalog.MetricVisualizationKey]
            .ShouldBe(DashboardMetricVisualizationIds.RadialGauge);
        modules
            .Single(static module => module.Type == DashboardWidgetCatalog.EventGaugeType)
            .DefaultConfiguration[DashboardMetricGaugeVisualizationOptions.ShapeKey]
            .ShouldBe(DashboardMetricGaugeVisualizationOptions.ShapeRing);
        modules
            .Single(static module => module.Type == DashboardWidgetCatalog.EventTableType)
            .Layout
            .PreferredRowSpan
            .ShouldBe(2);
        modules
            .Single(static module => module.Type == DashboardWidgetCatalog.EventTableType)
            .DefaultConfiguration
            .Keys
            .ShouldContain(DashboardEventTableVisualOptions.HeaderKey);
        modules
            .Single(static module => module.Type == DashboardWidgetCatalog.EventTableType)
            .PropertyGroups
            .SelectMany(static group => group.Properties)
            .Select(static property => property.Key)
            .ShouldNotContain(DashboardEventTableVisualOptions.LegacyRowCountKey);
        modules
            .Single(static module => module.Type == DashboardWidgetCatalog.EventTableType)
            .PropertyGroups
            .SelectMany(static group => group.Properties)
            .Select(static property => property.Key)
            .ShouldContain(DashboardEventTableVisualOptions.RowCountKey);
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
            .DefaultConfiguration[DashboardChartWidgetOptions.TypeKey]
            .ShouldBe(DashboardChartWidgetOptions.TypeLine);
        modules
            .Single(static module => module.Type == DashboardWidgetCatalog.LineChartType)
            .DefaultConfiguration
            .ContainsKey(DashboardWidgetCatalog.PrimaryMetricKey)
            .ShouldBeFalse();
        modules
            .Single(static module => module.Type == DashboardWidgetCatalog.LineChartType)
            .DefaultConfiguration[DashboardLineChartVisualOptions.HeaderKey]
            .ShouldBe("Line chart");
        modules
            .Single(static module => module.Type == DashboardWidgetCatalog.LineChartType)
            .PropertyGroups
            .Select(static group => group.Id)
            .ShouldBe(["line-chart"]);
        modules
            .Single(static module => module.Type == DashboardWidgetCatalog.LineChartType)
            .PropertyGroups
            .SelectMany(static group => group.Properties)
            .Select(static property => property.Key)
            .ShouldContain(DashboardLineChartVisualOptions.LineColorKey);
        modules
            .Single(static module => module.Type == DashboardWidgetCatalog.AreaChartType)
            .DefaultConfiguration[DashboardChartWidgetOptions.TypeKey]
            .ShouldBe(DashboardChartWidgetOptions.TypeArea);
        modules
            .Single(static module => module.Type == DashboardWidgetCatalog.AreaChartType)
            .DefaultConfiguration
            .ContainsKey(DashboardWidgetCatalog.PrimaryMetricKey)
            .ShouldBeFalse();
        modules
            .Single(static module => module.Type == DashboardWidgetCatalog.AreaChartType)
            .DefaultConfiguration[DashboardAreaChartVisualOptions.HeaderKey]
            .ShouldBe("Area chart");
        modules
            .Single(static module => module.Type == DashboardWidgetCatalog.AreaChartType)
            .PropertyGroups
            .Select(static group => group.Id)
            .ShouldBe(["area-chart"]);
        modules
            .Single(static module => module.Type == DashboardWidgetCatalog.AreaChartType)
            .PropertyGroups
            .SelectMany(static group => group.Properties)
            .Select(static property => property.Key)
            .ShouldContain(DashboardAreaChartVisualOptions.FillColorKey);
        modules
            .Single(static module => module.Type == DashboardWidgetCatalog.BarChartType)
            .DefaultConfiguration[DashboardChartWidgetOptions.TypeKey]
            .ShouldBe(DashboardChartWidgetOptions.TypeBars);
        modules
            .Single(static module => module.Type == DashboardWidgetCatalog.BarChartType)
            .DefaultConfiguration
            .ContainsKey(DashboardWidgetCatalog.PrimaryMetricKey)
            .ShouldBeFalse();
        modules
            .Single(static module => module.Type == DashboardWidgetCatalog.BarChartType)
            .DefaultConfiguration[DashboardBarChartVisualOptions.HeaderKey]
            .ShouldBe("Bar chart");
        modules
            .Single(static module => module.Type == DashboardWidgetCatalog.BarChartType)
            .PropertyGroups
            .Select(static group => group.Id)
            .ShouldBe(["bar-chart"]);
        modules
            .Single(static module => module.Type == DashboardWidgetCatalog.BarChartType)
            .PropertyGroups
            .SelectMany(static group => group.Properties)
            .Select(static property => property.Key)
            .ShouldContain(DashboardBarChartVisualOptions.BarColorKey);
        modules
            .Single(static module => module.Type == DashboardWidgetCatalog.DonutChartType)
            .DefaultConfiguration[DashboardChartWidgetOptions.TypeKey]
            .ShouldBe(DashboardChartWidgetOptions.TypeTopics);
        modules
            .Single(static module => module.Type == DashboardWidgetCatalog.DonutChartType)
            .DefaultConfiguration
            .ContainsKey(DashboardWidgetCatalog.PrimaryMetricKey)
            .ShouldBeFalse();
        modules
            .Single(static module => module.Type == DashboardWidgetCatalog.DonutChartType)
            .DefaultConfiguration[DashboardDonutChartVisualOptions.HeaderKey]
            .ShouldBe("Donut chart");
        modules
            .Single(static module => module.Type == DashboardWidgetCatalog.DonutChartType)
            .PropertyGroups
            .Select(static group => group.Id)
            .ShouldBe(["donut-chart"]);
        modules
            .Single(static module => module.Type == DashboardWidgetCatalog.DonutChartType)
            .PropertyGroups
            .SelectMany(static group => group.Properties)
            .Select(static property => property.Key)
            .ShouldContain(DashboardDonutChartVisualOptions.InnerRadiusKey);
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
            .ShouldBe(["topic-activity"]);
        modules
            .Single(static module => module.Type == DashboardWidgetCatalog.TopicActivityType)
            .PropertyGroups
            .Single()
            .Properties
            .Select(static property => property.Key)
            .ShouldBe([
                DashboardTopicActivityVisualOptions.HeaderKey,
                DashboardTopicActivityVisualOptions.ShowHeaderKey,
                DashboardTopicActivityVisualOptions.LimitKey,
                DashboardTopicActivityVisualOptions.ShowCountsKey,
                DashboardTopicActivityVisualOptions.EmptyTextKey,
                DashboardTopicActivityVisualOptions.HeaderColorKey,
                DashboardTopicActivityVisualOptions.TextColorKey,
                DashboardTopicActivityVisualOptions.MutedColorKey,
                DashboardTopicActivityVisualOptions.AccentColorKey
            ]);
        modules
            .Single(static module => module.Type == DashboardWidgetCatalog.TopicActivityType)
            .DefaultConfiguration[DashboardTopicActivityVisualOptions.HeaderKey]
            .ShouldBe("Topic activity");
        modules
            .Single(static module => module.Type == DashboardWidgetCatalog.TopicActivityType)
            .DefaultConfiguration[DashboardTopicActivityVisualOptions.LimitKey]
            .ShouldBe(DashboardTopicActivityVisualOptions.DefaultLimit.ToString(System.Globalization.CultureInfo.InvariantCulture));
        modules
            .Single(static module => module.Type == DashboardWidgetCatalog.TopicTreeType)
            .PropertyGroups
            .Select(static group => group.Id)
            .ShouldBe(["topic-tree"]);
        modules
            .Single(static module => module.Type == DashboardWidgetCatalog.TopicTreeType)
            .PropertyGroups
            .Single()
            .Properties
            .Select(static property => property.Key)
            .ShouldBe([
                DashboardTopicTreeVisualOptions.HeaderKey,
                DashboardTopicTreeVisualOptions.ShowHeaderKey,
                DashboardTopicTreeVisualOptions.ShowSummaryKey,
                DashboardTopicTreeVisualOptions.ShowTopicCountKey,
                DashboardTopicTreeVisualOptions.ShowMessageCountKey,
                DashboardTopicTreeVisualOptions.ExcludeSystemTopicsKey,
                DashboardTopicTreeVisualOptions.EmptyTextKey,
                DashboardTopicTreeVisualOptions.HeaderColorKey,
                DashboardTopicTreeVisualOptions.TextColorKey,
                DashboardTopicTreeVisualOptions.MutedColorKey,
                DashboardTopicTreeVisualOptions.AccentColorKey
            ]);
        modules
            .Single(static module => module.Type == DashboardWidgetCatalog.TopicTreeType)
            .DefaultConfiguration[DashboardWidgetCatalog.ExcludeSystemTopicsKey]
            .ShouldBe("true");
        modules
            .Single(static module => module.Type == DashboardWidgetCatalog.TopicTreeType)
            .DefaultConfiguration[DashboardTopicTreeVisualOptions.HeaderKey]
            .ShouldBe("Topic tree");
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
        var gauge = modules.Single(static module => module.Id == DashboardMetricVisualizationIds.RadialGauge);

        value.DisplayName.ShouldBe("Value");
        value.EditCellComponent.ShouldBe(typeof(DashboardMetricValueVisualizationView));
        value.LiveComponent.ShouldBe(typeof(DashboardMetricValueVisualizationView));
        value.DefaultConfiguration["visualization"].ShouldBe(DashboardMetricVisualizationIds.Value);
        value.DefaultConfiguration[DashboardMetricValueVisualizationOptions.TitleKey].ShouldBe(DashboardMetricValueVisualizationOptions.DefaultTitle);
        value.DefaultConfiguration[DashboardMetricValueVisualizationOptions.SubtitleKey].ShouldBe(DashboardMetricValueVisualizationOptions.DefaultSubtitle);
        value.DefaultConfiguration[DashboardMetricValueVisualizationOptions.ShowUnitKey].ShouldBe("true");
        value.DefaultConfiguration[DashboardMetricValueVisualizationOptions.UnitTextKey].ShouldBe(DashboardMetricValueVisualizationOptions.DefaultUnitText);
        value.DefaultConfiguration[DashboardMetricValueVisualizationOptions.TitleColorKey].ShouldBe(DashboardMetricValueVisualizationOptions.DefaultTitleColor);
        value.DefaultConfiguration[DashboardMetricValueVisualizationOptions.SubtitleColorKey].ShouldBe(DashboardMetricValueVisualizationOptions.DefaultSubtitleColor);
        value.DefaultConfiguration[DashboardMetricValueVisualizationOptions.ValueColorKey].ShouldBe(DashboardMetricValueVisualizationOptions.DefaultValueColor);
        value.DefaultConfiguration[DashboardMetricValueVisualizationOptions.UnitColorKey].ShouldBe(DashboardMetricValueVisualizationOptions.DefaultUnitColor);
        value.DefaultConfiguration[DashboardMetricValueVisualizationOptions.PaddingKey]
            .ShouldBe(DashboardMetricValueVisualizationOptions.DefaultPadding.ToString(System.Globalization.CultureInfo.InvariantCulture));
        value.DefaultConfiguration[DashboardMetricValueVisualizationOptions.FitModeKey]
            .ShouldBe(DashboardMetricValueVisualizationOptions.FitFill);
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
                DashboardMetricValueVisualizationOptions.TitleKey,
                DashboardMetricValueVisualizationOptions.ShowTitleKey,
                DashboardMetricValueVisualizationOptions.SubtitleKey,
                DashboardMetricValueVisualizationOptions.ShowSubtitleKey,
                DashboardMetricValueVisualizationOptions.ShowUnitKey,
                DashboardMetricValueVisualizationOptions.UnitTextKey,
                DashboardMetricValueVisualizationOptions.TitleColorKey,
                DashboardMetricValueVisualizationOptions.SubtitleColorKey,
                DashboardMetricValueVisualizationOptions.ValueColorKey,
                DashboardMetricValueVisualizationOptions.UnitColorKey,
                DashboardMetricValueVisualizationOptions.TitleAlignKey,
                DashboardMetricValueVisualizationOptions.ValueAlignKey,
                DashboardMetricValueVisualizationOptions.ValuePlacementKey,
                DashboardMetricValueVisualizationOptions.PaddingKey,
                DashboardMetricValueVisualizationOptions.FitModeKey
            ]);

        digital.DisplayName.ShouldBe("Digital");
        digital.EditCellComponent.ShouldBe(typeof(DashboardMetricDigitalVisualizationView));
        digital.LiveComponent.ShouldBe(typeof(DashboardMetricDigitalVisualizationView));
        digital.DefaultConfiguration[DashboardWidgetCatalog.MetricVisualizationKey]
            .ShouldBe(DashboardMetricVisualizationIds.Digital);
        digital.DefaultConfiguration[DashboardMetricDigitalVisualizationOptions.StyleKey]
            .ShouldBe(DashboardMetricDigitalVisualizationOptions.StylePanel);
        digital.DefaultConfiguration[DashboardMetricDigitalVisualizationOptions.GlowKey]
            .ShouldBe(DashboardMetricDigitalVisualizationOptions.GlowSoft);
        digital.DefaultConfiguration[DashboardMetricDigitalVisualizationOptions.BackgroundColorKey]
            .ShouldBe(DashboardMetricDigitalVisualizationOptions.DefaultBackgroundColor);
        digital.DefaultConfiguration[DashboardMetricDigitalVisualizationOptions.SegmentColorKey]
            .ShouldBe(DashboardMetricDigitalVisualizationOptions.DefaultSegmentColor);
        digital.DefaultConfiguration[DashboardMetricDigitalVisualizationOptions.InactiveSegmentColorKey]
            .ShouldBe(DashboardMetricDigitalVisualizationOptions.DefaultInactiveSegmentColor);
        digital.DefaultConfiguration[DashboardMetricDigitalVisualizationOptions.LabelColorKey]
            .ShouldBe(DashboardMetricDigitalVisualizationOptions.DefaultLabelColor);
        digital.DefaultConfiguration[DashboardMetricDigitalVisualizationOptions.DigitsKey]
            .ShouldBe(DashboardMetricDigitalVisualizationOptions.DefaultDigits.ToString(System.Globalization.CultureInfo.InvariantCulture));
        digital.DefaultConfiguration[DashboardMetricDigitalVisualizationOptions.BorderColorKey]
            .ShouldBe(DashboardMetricDigitalVisualizationOptions.DefaultBorderColor);
        digital.DefaultConfiguration[DashboardMetricDigitalVisualizationOptions.BorderWidthKey]
            .ShouldBe(DashboardMetricDigitalVisualizationOptions.DefaultBorderWidth.ToString(System.Globalization.CultureInfo.InvariantCulture));
        digital.DefaultConfiguration[DashboardMetricDigitalVisualizationOptions.RadiusKey]
            .ShouldBe(DashboardMetricDigitalVisualizationOptions.DefaultRadius.ToString(System.Globalization.CultureInfo.InvariantCulture));
        digital.DefaultConfiguration[DashboardMetricDigitalVisualizationOptions.PaddingKey]
            .ShouldBe(DashboardMetricDigitalVisualizationOptions.DefaultPadding.ToString(System.Globalization.CultureInfo.InvariantCulture));
        digital.DefaultConfiguration[DashboardMetricDigitalVisualizationOptions.FitModeKey]
            .ShouldBe(DashboardMetricDigitalVisualizationOptions.FitCompact);
        digital.DefaultConfiguration[DashboardMetricDigitalVisualizationOptions.AlignKey]
            .ShouldBe(DashboardMetricDigitalVisualizationOptions.AlignCenter);
        digital.DefaultConfiguration[DashboardMetricDigitalVisualizationOptions.PlacementKey]
            .ShouldBe(DashboardMetricDigitalVisualizationOptions.PlacementMiddle);
        digital.SupportedValueKinds.ShouldContain(DashboardMetricValueKinds.Number);
        digital.SupportedValueKinds.ShouldContain(DashboardMetricValueKinds.Rate);
        var digitalPropertyKeys = digital.PropertyGroups
            .SelectMany(static group => group.Properties)
            .Select(static property => property.Key)
            .ToArray();
        digitalPropertyKeys.ShouldContain(DashboardMetricDigitalVisualizationOptions.StyleKey);
        digitalPropertyKeys.ShouldContain(DashboardMetricDigitalVisualizationOptions.GlowKey);
        digitalPropertyKeys.ShouldContain(DashboardMetricDigitalVisualizationOptions.BackgroundColorKey);
        digitalPropertyKeys.ShouldContain(DashboardMetricDigitalVisualizationOptions.SegmentColorKey);
        digitalPropertyKeys.ShouldContain(DashboardMetricDigitalVisualizationOptions.InactiveSegmentColorKey);
        digitalPropertyKeys.ShouldContain(DashboardMetricDigitalVisualizationOptions.LabelColorKey);
        digitalPropertyKeys.ShouldContain(DashboardMetricDigitalVisualizationOptions.DigitsKey);
        digitalPropertyKeys.ShouldContain(DashboardMetricDigitalVisualizationOptions.BorderColorKey);
        digitalPropertyKeys.ShouldContain(DashboardMetricDigitalVisualizationOptions.BorderWidthKey);
        digitalPropertyKeys.ShouldContain(DashboardMetricDigitalVisualizationOptions.RadiusKey);
        digitalPropertyKeys.ShouldContain(DashboardMetricDigitalVisualizationOptions.PaddingKey);
        digitalPropertyKeys.ShouldContain(DashboardMetricDigitalVisualizationOptions.FitModeKey);
        digitalPropertyKeys.ShouldContain(DashboardMetricDigitalVisualizationOptions.AlignKey);
        digitalPropertyKeys.ShouldContain(DashboardMetricDigitalVisualizationOptions.PlacementKey);
        digitalPropertyKeys.ShouldNotContain(DashboardWidgetCatalog.KpiValueColorKey);
        digitalPropertyKeys.ShouldNotContain(DashboardMetricValueVisualizationOptions.ValueColorKey);

        gauge.DisplayName.ShouldBe("Gauge");
        gauge.EditCellComponent.ShouldBe(typeof(DashboardMetricGaugeVisualizationView));
        gauge.LiveComponent.ShouldBe(typeof(DashboardMetricGaugeVisualizationView));
        gauge.DefaultConfiguration[DashboardWidgetCatalog.MetricVisualizationKey]
            .ShouldBe(DashboardMetricVisualizationIds.RadialGauge);
        gauge.DefaultConfiguration[DashboardMetricGaugeVisualizationOptions.ShapeKey]
            .ShouldBe(DashboardMetricGaugeVisualizationOptions.ShapeRing);
        gauge.DefaultConfiguration[DashboardMetricGaugeVisualizationOptions.LabelKey]
            .ShouldBe(DashboardMetricGaugeVisualizationOptions.DefaultLabel);
        gauge.DefaultConfiguration[DashboardMetricGaugeVisualizationOptions.MaxKey]
            .ShouldBe(DashboardMetricGaugeVisualizationOptions.DefaultMax);
        gauge.SupportedValueKinds.ShouldContain(DashboardMetricValueKinds.Number);
        gauge.SupportedValueKinds.ShouldContain(DashboardMetricValueKinds.Rate);
        gauge.SupportedValueKinds.ShouldContain(DashboardMetricValueKinds.Bytes);
        gauge.SupportedValueKinds.ShouldContain(DashboardMetricValueKinds.Percent);
        var gaugePropertyKeys = gauge.PropertyGroups
            .SelectMany(static group => group.Properties)
            .Select(static property => property.Key)
            .ToArray();
        gaugePropertyKeys.ShouldContain(DashboardMetricGaugeVisualizationOptions.ShapeKey);
        gaugePropertyKeys.ShouldContain(DashboardMetricGaugeVisualizationOptions.LabelKey);
        gaugePropertyKeys.ShouldContain(DashboardMetricGaugeVisualizationOptions.ShowLabelKey);
        gaugePropertyKeys.ShouldContain(DashboardMetricGaugeVisualizationOptions.NormalColorKey);
        gaugePropertyKeys.ShouldContain(DashboardMetricGaugeVisualizationOptions.WarningColorKey);
        gaugePropertyKeys.ShouldContain(DashboardMetricGaugeVisualizationOptions.CriticalColorKey);
        gaugePropertyKeys.ShouldNotContain(DashboardEventGaugeWidgetOptions.StyleKey);
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
            DashboardMetricVisualizationIds.Digital,
            DashboardMetricVisualizationIds.RadialGauge
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
        digitalView.ShouldContain("style=\"@RootStyle\"");
        digitalView.ShouldContain("DashboardMetricDigitalVisualizationOptions.AlignKey");
        digitalView.ShouldContain("DashboardMetricDigitalVisualizationOptions.PlacementKey");
        digitalView.ShouldContain("--dashboard-kpi-value-placement");
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
    public void DashboardMetricValueVisualization_UsesFitClassForEditorAndLiveParity()
    {
        var root = FindRepositoryRoot();
        var valueView = File.ReadAllText(Path.Combine(
            root,
            "src",
            "FluxMq.UI",
            "Components",
            "Workspace",
            "DashboardWidgets",
            "DashboardMetricValueVisualizationView.razor"));
        var css = File.ReadAllText(Path.Combine(
            root,
            "src",
            "FluxMq.UI",
            "wwwroot",
            "dashboard-widgets.css"));

        valueView.ShouldContain("dashboard-metric-value-visual fit-");
        valueView.ShouldContain("DashboardMetricValueVisualizationOptions.FitModeKey");
        css.ShouldContain(".dashboard-metric-value-visual.fit-fill");
        css.ShouldContain(".dashboard-metric-value-visual.fit-compact");
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
        markup.ShouldContain("aria-haspopup=\"dialog\"");
        markup.ShouldContain("aria-expanded=\"@_pickerOpen\"");
        markup.ShouldContain("aria-controls=\"@_popoverId\"");
        markup.ShouldContain("id=\"@_popoverId\"");
        markup.ShouldContain("role=\"dialog\"");
        markup.ShouldContain("private readonly string _popoverId");
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
        css.ShouldContain("grid-template-columns: 26px minmax(0, 1fr) 24px;");
        css.ShouldContain("background-color: var(--property-grid-color-value);");
        css.ShouldContain("font-size: 14px;");
    }

    [Fact]
    public void DashboardInspector_UsesDensePropertyGridAndIconMetricControls()
    {
        var root = FindRepositoryRoot();
        var propertyGrid = File.ReadAllText(Path.Combine(
            root,
            "src",
            "FluxMq.UI",
            "Components",
            "Workspace",
            "PropertyGrid.razor"));
        var propertyGridCss = File.ReadAllText(Path.Combine(
            root,
            "src",
            "FluxMq.UI",
            "Components",
            "Workspace",
            "PropertyGrid.razor.css"));
        var rowMarkup = File.ReadAllText(Path.Combine(
            root,
            "src",
            "FluxMq.UI",
            "Components",
            "Workspace",
            "PropertyGridRow.razor"));
        var rowCss = File.ReadAllText(Path.Combine(
            root,
            "src",
            "FluxMq.UI",
            "Components",
            "Workspace",
            "PropertyGridRow.razor.css"));
        var selectMarkup = File.ReadAllText(Path.Combine(
            root,
            "src",
            "FluxMq.UI",
            "Components",
            "Workspace",
            "PropertyGridSelect.razor"));
        var selectCss = File.ReadAllText(Path.Combine(
            root,
            "src",
            "FluxMq.UI",
            "Components",
            "Workspace",
            "PropertyGridSelect.razor.css"));
        var iconSegmentMarkup = File.ReadAllText(Path.Combine(
            root,
            "src",
            "FluxMq.UI",
            "Components",
            "Workspace",
            "PropertyGridIconSegment.razor"));
        var iconSegmentCss = File.ReadAllText(Path.Combine(
            root,
            "src",
            "FluxMq.UI",
            "Components",
            "Workspace",
            "PropertyGridIconSegment.razor.css"));
        var colorPickerMarkup = File.ReadAllText(Path.Combine(
            root,
            "src",
            "FluxMq.UI",
            "Components",
            "Workspace",
            "PropertyGridColorPicker.razor"));
        var colorPickerCss = File.ReadAllText(Path.Combine(
            root,
            "src",
            "FluxMq.UI",
            "Components",
            "Workspace",
            "PropertyGridColorPicker.razor.css"));
        var alignmentPadCss = File.ReadAllText(Path.Combine(
            root,
            "src",
            "FluxMq.UI",
            "Components",
            "Workspace",
            "PropertyGridAlignmentPad.razor.css"));
        var inspector = File.ReadAllText(Path.Combine(
            root,
            "src",
            "FluxMq.UI",
            "Components",
            "Workspace",
            "DashboardInspector.razor"));
        var inspectorCss = File.ReadAllText(Path.Combine(
            root,
            "src",
            "FluxMq.UI",
            "Components",
            "Workspace",
            "DashboardInspector.razor.css"));
        var appMetricRows = File.ReadAllText(Path.Combine(
            root,
            "src",
            "FluxMq.UI",
            "Components",
            "Workspace",
            "DashboardInspectorAppMetricRows.razor"));
        var cellStyleRows = File.ReadAllText(Path.Combine(
            root,
            "src",
            "FluxMq.UI",
            "Components",
            "Workspace",
            "DashboardInspectorCellStyleRows.razor"));
        var layoutRows = File.ReadAllText(Path.Combine(
            root,
            "src",
            "FluxMq.UI",
            "Components",
            "Workspace",
            "DashboardInspectorLayoutRows.razor"));
        var visualMetricRows = File.ReadAllText(Path.Combine(
            root,
            "src",
            "FluxMq.UI",
            "Components",
            "Workspace",
            "DashboardInspectorVisualMetricRows.razor"));
        var inspectorControlMarkups = new[]
        {
            propertyGrid,
            rowMarkup,
            selectMarkup,
            iconSegmentMarkup,
            colorPickerMarkup,
            inspector,
            appMetricRows,
            cellStyleRows,
            layoutRows,
            visualMetricRows
        };

        propertyGrid.ShouldContain("DefaultNameColumnWidth = 116");
        inspectorControlMarkups
            .SelectMany(static markup => markup.Split('\n'))
            .Where(static line => line.Contains("<MudIcon ", StringComparison.Ordinal) &&
                !line.Contains("aria-hidden=\"true\"", StringComparison.Ordinal))
            .ToArray()
            .ShouldBeEmpty();
        propertyGrid.ShouldContain("MinNameColumnWidth = 78");
        propertyGrid.ShouldContain("MaxNameColumnWidth = 176");
        propertyGrid.ShouldContain("--property-grid-name-width: min({_nameColumnWidth.ToString(\"0\", CultureInfo.InvariantCulture)}px, 39%);");
        propertyGrid.ShouldContain("aria-label=\"@GridAriaLabel\"");
        propertyGrid.ShouldContain("private string GridAriaLabel");
        propertyGrid.ShouldContain("$\"Dashboard property editor, {Groups.Count.ToString(CultureInfo.InvariantCulture)} groups\"");
        propertyGrid.ShouldContain("title=\"@NameColumnResizeLabel\"");
        propertyGrid.ShouldContain("aria-label=\"@NameColumnResizeLabel\"");
        propertyGrid.ShouldContain("private string NameColumnResizeLabel");
        propertyGrid.ShouldContain("$\"Resize property name column, {NameColumnWidthValue} pixels\"");
        propertyGrid.ShouldNotContain("aria-label=\"Dashboard property editor\"");
        propertyGrid.ShouldNotContain("title=\"Resize property name column\"");
        propertyGrid.ShouldNotContain("aria-label=\"Resize property name column\"");
        propertyGrid.ShouldContain("aria-keyshortcuts=\"ArrowLeft ArrowRight Home End Enter\"");
        propertyGrid.ShouldContain("role=\"rowgroup\"");
        propertyGrid.ShouldContain("aria-label=\"@GroupAriaLabel(group, collapsed)\"");
        propertyGrid.ShouldContain("aria-controls=\"@GroupBodyId(group)\"");
        propertyGrid.ShouldContain("aria-label=\"@GroupHeaderAriaLabel(group, collapsed)\"");
        propertyGrid.ShouldContain("title=\"@group.Title\"");
        propertyGrid.ShouldContain("id=\"@GroupBodyId(group)\"");
        propertyGrid.ShouldContain("aria-label=\"@FormatSettingCount(group.SettingCount)\"");
        propertyGrid.ShouldContain("private static string GroupAriaLabel");
        propertyGrid.ShouldContain("private static string GroupHeaderAriaLabel");
        propertyGrid.ShouldContain("private static string GroupBodyId");
        propertyGrid.ShouldContain("private static string SanitizeIdToken");
        propertyGridCss.ShouldContain("container-name: property-grid;");
        propertyGridCss.ShouldContain("position: sticky;");
        propertyGridCss.ShouldContain("grid-template-columns: 20px minmax(0, 1fr) auto;");
        propertyGridCss.ShouldContain("min-height: 26px;");
        propertyGridCss.ShouldContain("left: calc(var(--property-grid-name-width) - 2px);");
        propertyGridCss.ShouldContain(".property-grid:hover .property-grid-column-resizer::after");
        propertyGridCss.ShouldContain(".property-grid-group:not(.collapsed) .property-grid-group-body");
        propertyGridCss.ShouldContain("@container property-grid (max-width: 280px)");
        rowMarkup.ShouldContain("class=\"@RowClass\"");
        rowMarkup.ShouldContain("title=\"@NameTitle\"");
        rowMarkup.ShouldContain("\"property-grid-row stacked\"");
        rowMarkup.ShouldContain("has-help");
        rowMarkup.ShouldContain("private string NameTitle");
        rowCss.ShouldContain("min-height: 24px;");
        rowCss.ShouldContain("min-height: 22px;");
        rowCss.ShouldContain("min-height: 20px;");
        rowCss.ShouldContain(".property-grid-row:focus-within");
        rowCss.ShouldContain(".property-grid-row.has-help .property-grid-name");
        rowCss.ShouldContain("border-right: 1px solid color-mix(in srgb, var(--flux-border) 20%, transparent);");
        rowCss.ShouldContain("padding-left: 2px;");
        rowCss.ShouldContain("width: calc(100% - 4px);");
        rowCss.ShouldContain(".property-grid-help ::deep .mud-icon-root");
        rowCss.ShouldContain("grid-template-columns: minmax(0, 1fr);");
        selectMarkup.ShouldContain("aria-haspopup=\"listbox\"");
        selectMarkup.ShouldContain("aria-expanded=\"@_isOpen\"");
        selectMarkup.ShouldContain("aria-controls=\"@_listboxId\"");
        selectMarkup.ShouldContain("id=\"@_listboxId\"");
        selectMarkup.ShouldContain("role=\"listbox\"");
        selectMarkup.ShouldContain("aria-activedescendant=\"@SelectedOptionId\"");
        selectMarkup.ShouldContain("aria-label=\"@ResolvedAriaLabel\"");
        selectMarkup.ShouldContain("id=\"@OptionId(index)\"");
        selectMarkup.ShouldContain("private readonly string _listboxId");
        selectMarkup.ShouldContain("private string? SelectedOptionId");
        selectMarkup.ShouldContain("private string OptionId(int index)");
        selectMarkup.ShouldContain("string.Equals(args.Key, \"Spacebar\", StringComparison.Ordinal)");
        selectMarkup.ShouldContain("@onclick=\"@(() => SelectOptionAsync(option))\"");
        selectMarkup.ShouldContain("@onmousedown=\"@(() => SelectOptionAsync(option))\"");
        selectCss.ShouldContain("max-height: 160px;");
        selectCss.ShouldContain("right: 5px;");
        iconSegmentCss.ShouldContain("min-height: 19px;");
        iconSegmentCss.ShouldContain("width: calc(100% - 4px);");
        iconSegmentCss.ShouldContain(".property-grid-icon-segment.show-labels .property-grid-icon-segment-button");
        iconSegmentCss.ShouldContain(".property-grid-icon-segment.show-labels .property-grid-icon-segment-label");
        colorPickerCss.ShouldContain("grid-template-columns: 26px minmax(0, 1fr) 24px;");
        colorPickerCss.ShouldContain("width: calc(100% - 4px);");
        alignmentPadCss.ShouldContain("grid-template-columns: repeat(3, 16px);");
        alignmentPadCss.ShouldContain(".property-grid-alignment-pad-button span");
        alignmentPadCss.ShouldContain("border-radius: 50%;");
        alignmentPadCss.ShouldNotContain("border-radius: 999px;");
        inspector.ShouldContain("role=\"complementary\" aria-label=\"@InspectorRegionLabel(propertyGroups)\"");
        inspector.ShouldContain("private string InspectorRegionLabel(IReadOnlyList<PropertyGridGroup> groups)");
        inspector.ShouldContain("var targetLabel = string.Equals(InspectorTitle, \"Dashboard inspector\", StringComparison.Ordinal)");
        inspector.ShouldContain("return $\"{targetLabel}, {InspectorModeLabel}, {InspectorGroupCountLabel(groups.Count)}, {InspectorPropertyCountLabel(groups)}\"");
        inspector.ShouldNotContain("role=\"complementary\" aria-label=\"Dashboard inspector\"");
        inspector.ShouldContain("var propertyGroups = PropertyGroups;");
        inspector.ShouldContain("class=\"@InspectorHeaderClass\"");
        inspector.ShouldContain("dashboard-inspector-meta-strip");
        inspector.ShouldContain("aria-label=\"@InspectorMetaSummaryLabel\"");
        inspector.ShouldContain("private string InspectorMetaSummaryLabel");
        inspector.ShouldContain("private string WidgetCommandGroupLabel");
        inspector.ShouldContain("private string ResetWidgetPropertiesLabel");
        inspector.ShouldContain("aria-label=\"@WidgetCommandGroupLabel\"");
        inspector.ShouldContain("Text=\"@ResetWidgetPropertiesLabel\"");
        inspector.ShouldContain("aria-label=\"@ResetWidgetPropertiesLabel\"");
        inspector.ShouldContain("dashboard-inspector-reset-command");
        inspector.ShouldContain("@InspectorModeLabel");
        inspector.ShouldContain("@InspectorGroupCountLabel(propertyGroups.Count)");
        inspector.ShouldContain("@InspectorPropertyCountLabel(propertyGroups)");
        inspector.ShouldContain("dashboard-inspector-property-shell");
        inspector.ShouldContain("dashboard-inspector-empty\" role=\"status\" aria-live=\"polite\"");
        inspector.ShouldContain("dashboard-inspector-empty-icon");
        inspector.ShouldContain("dashboard-inspector-empty-card");
        inspector.ShouldContain("private string InspectorModeClass");
        inspector.ShouldContain("Empty cell");
        inspector.ShouldNotContain("Cell ready");
        inspector.ShouldNotContain("dashboard-inspector-live-strip");
        inspector.ShouldNotContain("dashboard-inspector-status-chip");
        inspector.ShouldNotContain("title=\"@InspectorStatusLabel\"");
        inspector.ShouldNotContain("@InspectorStatusIcon");
        inspector.ShouldNotContain("@InspectorStatusLabel");
        inspector.ShouldNotContain("Widget edits apply immediately");
        inspector.ShouldNotContain("Cell edits apply immediately");
        inspector.ShouldNotContain("Select a target to edit");
        inspector.ShouldNotContain("aria-label=\"Inspector selection summary\"");
        inspector.ShouldNotContain("aria-label=\"Widget commands\"");
        inspector.ShouldNotContain("aria-label=\"Reset widget properties\"");
        inspector.ShouldNotContain("Text=\"Reset widget properties to defaults\"");
        inspector.ShouldContain("private static string InspectorPropertyCountLabel");
        inspectorCss.ShouldContain("flex: 0 0 324px;");
        inspectorCss.ShouldContain("grid-template-columns: 24px minmax(0, 1fr) auto;");
        inspectorCss.ShouldContain(".dashboard-inspector-header.widget");
        inspectorCss.ShouldContain(".dashboard-inspector-reset-command");
        inspectorCss.ShouldContain(".dashboard-inspector-heading");
        inspectorCss.ShouldContain("flex-wrap: nowrap;");
        inspectorCss.ShouldContain(".dashboard-inspector-meta-strip span");
        inspectorCss.ShouldContain("flex: 0 1 auto;");
        inspectorCss.ShouldNotContain(".dashboard-inspector-status-chip");
        inspectorCss.ShouldNotContain(".dashboard-inspector-live-strip");
        inspectorCss.ShouldContain(".dashboard-inspector-property-shell");
        inspectorCss.ShouldContain("overflow: hidden;");
        inspectorCss.ShouldNotContain(".dashboard-inspector-empty::before");
        inspectorCss.ShouldContain(".dashboard-inspector-empty-icon");
        inspectorCss.ShouldContain("grid-template-columns: minmax(0, min(320px, 100%));");
        inspectorCss.ShouldContain("justify-items: center;");
        inspectorCss.ShouldContain("text-align: center;");
        inspectorCss.ShouldContain("overflow-wrap: anywhere;");
        inspectorCss.ShouldContain("align-content: center;");
        inspectorCss.ShouldContain(".dashboard-inspector-meta-strip span:nth-child(n + 3)");
        inspectorCss.ShouldContain(".dashboard-inspector-reset-command span {");
        visualMetricRows.ShouldContain("KeyboardArrowUp");
        visualMetricRows.ShouldContain("KeyboardArrowDown");
        visualMetricRows.ShouldContain("Icons.Material.Filled.Close");
        visualMetricRows.ShouldContain("var currentMetricLabel = VisualMetricLabel(currentMetric);");
        visualMetricRows.ShouldContain("aria-label=\"@MetricCardCommandsLabel(currentMetricLabel)\"");
        visualMetricRows.ShouldContain("title=\"@MoveMetricUpLabel(currentMetricLabel)\"");
        visualMetricRows.ShouldContain("aria-label=\"@MoveMetricUpLabel(currentMetricLabel)\"");
        visualMetricRows.ShouldContain("title=\"@MoveMetricDownLabel(currentMetricLabel)\"");
        visualMetricRows.ShouldContain("aria-label=\"@MoveMetricDownLabel(currentMetricLabel)\"");
        visualMetricRows.ShouldContain("title=\"@RemoveMetricCardLabel(currentMetricLabel)\"");
        visualMetricRows.ShouldContain("aria-label=\"@RemoveMetricCardLabel(currentMetricLabel)\"");
        visualMetricRows.ShouldContain("private static string MetricCardCommandsLabel(string metricLabel)");
        visualMetricRows.ShouldContain("private static string MoveMetricUpLabel(string metricLabel)");
        visualMetricRows.ShouldContain("private static string MoveMetricDownLabel(string metricLabel)");
        visualMetricRows.ShouldContain("private static string RemoveMetricCardLabel(string metricLabel)");
        visualMetricRows.ShouldNotContain("@($\"Move {VisualMetricLabel(currentMetric)} up\")");
        visualMetricRows.ShouldNotContain("@($\"Move {VisualMetricLabel(currentMetric)} down\")");
        visualMetricRows.ShouldNotContain("@($\"Remove {VisualMetricLabel(currentMetric)}\")");
        visualMetricRows.ShouldNotContain("title=\"Move up\"");
        visualMetricRows.ShouldNotContain("title=\"Move down\"");
        visualMetricRows.ShouldNotContain("title=\"Remove\"");
        visualMetricRows.ShouldContain("title=\"@AddMetricCardLabel\"");
        visualMetricRows.ShouldContain("aria-label=\"@AddMetricCardLabel\"");
        visualMetricRows.ShouldContain("private string AddMetricCardLabel => string.IsNullOrWhiteSpace(MetricToAdd)");
        visualMetricRows.ShouldContain("$\"Add metric card for {VisualMetricLabel(MetricToAdd)}\"");
        visualMetricRows.ShouldNotContain("title=\"Add metric card\"");
        visualMetricRows.ShouldNotContain("aria-label=\"Add metric card\"");
    }

    [Fact]
    public void LiveInspectorPanel_UsesAppLevelMqttPublisherPanel()
    {
        var root = FindRepositoryRoot();
        var markup = File.ReadAllText(Path.Combine(
            root,
            "src",
            "FluxMq.UI",
            "Components",
            "Workspace",
            "LiveInspectorPanel.razor"));
        var css = File.ReadAllText(Path.Combine(
            root,
            "src",
            "FluxMq.UI",
            "Components",
            "Workspace",
            "LiveInspectorPanel.razor.css"));

        markup.ShouldContain("Icons.Material.Filled.Send");
        markup.ShouldContain("MQTT Publisher");
        markup.ShouldContain("class=\"publisher-icon\" aria-hidden=\"true\"");
        System.Text.RegularExpressions.Regex.Matches(
                markup,
                @"<MudIcon\b(?:(?!/>).)*?/>",
                System.Text.RegularExpressions.RegexOptions.Singleline)
            .Cast<System.Text.RegularExpressions.Match>()
            .Select(static match => match.Value)
            .Where(static icon => !icon.Contains("aria-hidden=\"true\"", StringComparison.Ordinal))
            .ToArray()
            .ShouldBeEmpty();
        markup.ShouldContain("MQTT client");
        markup.ShouldContain("MQTT connection");
        markup.ShouldContain("ActiveAppLabel");
        markup.ShouldContain("ClientCountLabel");
        markup.ShouldContain("ConnectionMarkerClass");
        markup.ShouldContain("ConnectionMarkerLabel");
        markup.ShouldContain("EnsureLiveConnectionsForActiveProject");
        markup.ShouldContain("Live.AddConnectionIfAbsent(profile, subscription, name)");
        markup.ShouldContain("Live.ConnectAsync(connection.Id)");
        markup.ShouldContain("Live.PublishAsync(");
        markup.ShouldContain("RecordManualMqttPublish");
        markup.ShouldContain("diagnostics-panel");
        markup.ShouldContain("publisher-diagnostic muted");
        markup.ShouldContain("LatestDiagnosticClass");
        markup.ShouldContain("publish-form-grid");
        markup.ShouldContain("publish-field broker");
        markup.ShouldContain("publish-field topic");
        markup.ShouldContain("publish-field payload");
        markup.ShouldContain("publish-qos-select");
        markup.ShouldContain("aria-label=\"@PublishQosLabel\"");
        markup.ShouldContain("private string PublishQosLabel => $\"Quality of service, {_publishQos}\"");
        markup.ShouldContain("publish-retain-toggle");
        markup.ShouldContain("aria-label=\"@PublishRetainLabel\"");
        markup.ShouldContain("private string PublishRetainLabel => _retain");
        markup.ShouldContain("\"Publish retained message, enabled\"");
        markup.ShouldContain("\"Publish retained message, disabled\"");
        markup.ShouldNotContain("aria-label=\"Quality of service\"");
        markup.ShouldNotContain("aria-label=\"Publish retained message\"");
        markup.ShouldContain("aria-pressed=\"@(_retain ? \"true\" : \"false\")\"");
        markup.ShouldContain("publish-submit");
        markup.ShouldContain("publish-empty\" role=\"status\" aria-live=\"polite\"");
        markup.ShouldContain("No MQTT clients");
        markup.ShouldNotContain("Selected client");
        markup.ShouldNotContain("ConnectSelectedAsync");
        markup.ShouldNotContain("publish-static-state");
        markup.ShouldNotContain("publisher-empty-state");
        markup.ShouldNotContain("client-summary");
        markup.ShouldNotContain("Add an app MQTT connection to publish messages.");
        markup.ShouldNotContain("InspectorTab");
        markup.ShouldNotContain("<SessionPanel />");
        markup.ShouldNotContain("<TopicTreeView");
        markup.ShouldNotContain("<PayloadInspectorPanel");
        markup.ShouldNotContain("Live.StoredSessions");
        markup.ShouldNotContain("LiveTopicRows");
        markup.ShouldNotContain("LastPayloadMessage");
        markup.ShouldNotContain("DisplayedMessages");
        markup.ShouldNotContain("MessageSource");
        markup.ShouldNotContain("Show message list");
        markup.ShouldNotContain("Live topics");
        markup.ShouldNotContain("<span>Recordings</span>");
        markup.ShouldNotContain("<span>Inspect</span>");
        markup.ShouldNotContain("select-pill");
        markup.ShouldNotContain("pill-button");
        markup.ShouldNotContain("section-badge");
        markup.ShouldNotContain("ConnectionBadgeClass");
        markup.ShouldNotContain("connection-badge");
        markup.ShouldNotContain("MQTT state");
        markup.ShouldNotContain("ConnectionStateClass");
        markup.ShouldNotContain("ConnectionStateLabel");
        markup.ShouldNotContain("MQTT status");
        markup.ShouldNotContain("status-line");

        css.ShouldContain(".publisher-header");
        css.ShouldContain(".publisher-title-lockup");
        css.ShouldContain(".publisher-icon");
        css.ShouldContain(".connection-marker.connected");
        css.ShouldContain(".connection-marker.pending");
        css.ShouldContain(".connection-marker.faulted");
        css.ShouldNotContain(".connection-state");
        css.ShouldContain(".publisher-panel");
        css.ShouldContain(".publish-form-grid");
        css.ShouldContain("grid-template-columns: minmax(0, 1fr);");
        css.ShouldNotContain("grid-template-columns: minmax(0, 1fr) minmax(0, 1fr);");
        css.ShouldContain(".publish-field.payload,");
        css.ShouldContain("grid-column: 1 / -1;");
        css.ShouldContain("text-transform: none;");
        css.ShouldContain("height: 30px;");
        css.ShouldContain(".publish-empty");
        css.ShouldContain("min-height: 88px;");
        css.ShouldContain("max-height: 150px;");
        css.ShouldContain(".publish-qos-select");
        css.ShouldContain(".publish-retain-toggle.active");
        css.ShouldContain(".diagnostics-panel");
        css.ShouldContain(".publisher-diagnostic.info");
        css.ShouldContain(".publisher-diagnostic.warning");
        css.ShouldContain(".publisher-diagnostic.error");
        css.ShouldNotContain(".status-line");
        css.ShouldNotContain(".client-panel");
        css.ShouldNotContain(".client-summary");
        css.ShouldNotContain(".client-state-dot");
        css.ShouldNotContain(".client-action");
        css.ShouldNotContain(".client-error");
        css.ShouldNotContain(".publish-static-state");
        css.ShouldNotContain(".publisher-empty-state");
        css.ShouldNotContain(".inspector-tabs");
        css.ShouldNotContain(".inspector-tab");
        css.ShouldNotContain(".tab-live-dot");
        css.ShouldNotContain(".recordings-panel");
        css.ShouldNotContain(".topic-message-table");
        css.ShouldNotContain(".last-payload");
        css.ShouldNotContain(".recording-label");
        css.ShouldNotContain(".empty-topic-row");
        css.ShouldNotContain(".select-pill");
        css.ShouldNotContain(".pill-button");
        css.ShouldNotContain(".section-badge");
        css.ShouldNotContain(".connection-badge");
        css.ShouldNotContain("border-radius: 999px;");
    }

    [Fact]
    public void MainLayout_RemovesSessionOnlyLeftRail()
    {
        var root = FindRepositoryRoot();
        var markup = File.ReadAllText(Path.Combine(
            root,
            "src",
            "FluxMq.UI",
            "Components",
            "Layout",
            "MainLayout.razor"));
        var css = File.ReadAllText(Path.Combine(
            root,
            "src",
            "FluxMq.UI",
            "Components",
            "Layout",
            "MainLayout.razor.css"));
        var appCss = File.ReadAllText(Path.Combine(
            root,
            "src",
            "FluxMq.UI",
            "wwwroot",
            "app.css"));

        markup.ShouldContain("PrimaryContrastText = \"#FFFFFF\"");
        markup.ShouldContain("PrimaryContrastText = \"#062A26\"");
        markup.ShouldContain("BackgroundGray = \"#F7F9FB\"");
        markup.ShouldContain("BackgroundGray = \"#161B24\"");
        markup.ShouldContain("TextDisabled = \"#6B7280\"");
        markup.ShouldContain("PrimaryDarken = \"#115E59\"");
        markup.ShouldContain("PrimaryLighten = \"#5EEAD4\"");
        markup.ShouldContain("<MudDialogProvider BackdropClick=\"false\" CloseButton=\"true\" CloseOnEscapeKey=\"true\" />");
        markup.ShouldContain("<LiveInspectorPanel />");
        markup.ShouldContain("@if (HasActiveProject)");
        markup.ShouldContain("private bool HasActiveProject => Projects.ActiveProject is not null;");
        markup.ShouldContain("var connectionsAvailable = await Live.EnsureConnectionsAsync(project.GetConnectionResources());");
        markup.ShouldContain("no-active-project");
        markup.ShouldContain("PublisherPanelToggleLabel");
        markup.ShouldContain("Text=\"@NewProjectActionLabel\"");
        markup.ShouldContain("aria-label=\"@NewProjectActionLabel\"");
        markup.ShouldContain("Text=\"@OpenProjectActionLabel\"");
        markup.ShouldContain("aria-label=\"@OpenProjectActionLabel\"");
        markup.ShouldContain("Text=\"@SaveProjectActionLabel\"");
        markup.ShouldContain("aria-label=\"@SaveProjectActionLabel\"");
        markup.ShouldContain("Text=\"@SaveProjectAsActionLabel\"");
        markup.ShouldContain("aria-label=\"@SaveProjectAsActionLabel\"");
        markup.ShouldContain("Text=\"@ValidateProjectActionLabel\"");
        markup.ShouldContain("aria-label=\"@ValidateProjectActionLabel\"");
        markup.ShouldContain("Text=\"@RunProjectActionLabel\"");
        markup.ShouldContain("aria-label=\"@RunProjectActionLabel\"");
        markup.ShouldContain("Text=\"@StopProjectActionLabel\"");
        markup.ShouldContain("aria-label=\"@StopProjectActionLabel\"");
        markup.ShouldContain("private string ActiveProjectActionTarget");
        markup.ShouldContain("private string NewProjectActionLabel => \"Create new project\";");
        markup.ShouldContain("private string OpenProjectActionLabel => \"Open project file\";");
        markup.ShouldContain("private string SaveProjectActionLabel");
        markup.ShouldContain("private string RunProjectActionLabel");
        markup.ShouldContain("private string StopProjectActionLabel");
        markup.ShouldContain("aria-label=\"@_themeLabel\"");
        markup.ShouldContain("Text=\"@PublisherPanelToggleLabel\"");
        markup.ShouldContain("aria-label=\"@PublisherPanelToggleLabel\"");
        markup.ShouldContain("private string PublisherPanelToggleLabel => _rightOpen");
        markup.ShouldContain("$\"Hide MQTT publisher for {ActiveProjectActionTarget}\"");
        markup.ShouldContain("$\"Show MQTT publisher for {ActiveProjectActionTarget}\"");
        markup.ShouldContain("Class=\"flux-command-spin-icon\"");
        markup.ShouldContain("aria-hidden=\"true\"");
        markup.ShouldContain("<MudIcon Icon=\"@DragPreviewIcon(activeDrag.TargetKind)\" Size=\"Size.Small\" aria-hidden=\"true\" />");
        markup.ShouldContain("flux-bottom-bar");
        markup.ShouldContain("AppRuntimeMarkerClass");
        markup.ShouldContain("AppRuntimeSummaryLabel");
        markup.ShouldContain("AppRuntimeTooltip");
        markup.ShouldContain("role=\"status\"");
        markup.ShouldContain("aria-live=\"polite\"");
        markup.ShouldContain("aria-label=\"@AppRuntimeSummaryLabel\"");
        System.Text.RegularExpressions.Regex.IsMatch(
            markup,
            @"<div class=""@AppRuntimeMarkerClass""(?=[^>]*role=""status"")(?=[^>]*aria-live=""polite"")(?=[^>]*aria-label=""@AppRuntimeSummaryLabel"")[^>]*>",
            System.Text.RegularExpressions.RegexOptions.Singleline).ShouldBeTrue();
        markup.ShouldContain("<span class=\"flux-app-runtime-dot\" aria-hidden=\"true\"></span>");
        markup.ShouldContain("LiveConnectionMarkerClass");
        markup.ShouldContain("LiveConnectionSummaryLabel");
        markup.ShouldContain("aria-label=\"@LiveConnectionSummaryLabel\"");
        System.Text.RegularExpressions.Regex.IsMatch(
            markup,
            @"<div class=""@LiveConnectionMarkerClass""(?=[^>]*aria-label=""@LiveConnectionSummaryLabel"")(?![^>]*role=""status"")[^>]*>",
            System.Text.RegularExpressions.RegexOptions.Singleline).ShouldBeTrue();
        markup.ShouldContain("<span class=\"flux-live-connection-dot\" aria-hidden=\"true\"></span>");
        markup.ShouldContain("LiveConnectionDotClass");
        markup.ShouldContain("<span class=\"@LiveConnectionDotClass\" aria-hidden=\"true\"></span>");
        System.Text.RegularExpressions.Regex.IsMatch(
            markup,
            @"<div class=""flux-bottom-group""(?=[^>]*role=""status"")(?=[^>]*aria-live=""polite"")(?=[^>]*aria-label=""@LiveConnectionSummaryLabel"")[^>]*>",
            System.Text.RegularExpressions.RegexOptions.Singleline).ShouldBeTrue();
        markup.ShouldContain("private string LiveConnectionSummaryLabel => $\"MQTT {Live.State}\";");
        markup.ShouldNotContain("flux-statusbar");
        markup.ShouldNotContain("FlowStateClass");
        markup.ShouldNotContain("ActiveProjectStateLabel");
        markup.ShouldNotContain("ActiveProjectStateTooltip");
        markup.ShouldNotContain("RunStateClass");
        markup.ShouldNotContain("flux-flowstate");
        markup.ShouldNotContain("flux-flow-dot");
        markup.ShouldNotContain("flux-runstate");
        markup.ShouldNotContain("flux-run-dot");
        markup.ShouldNotContain("LiveStateDotClass");
        markup.ShouldNotContain("StatusDotClass");
        markup.ShouldNotContain("brokersReady");
        markup.ShouldNotContain("Text=\"Open file\"");
        markup.ShouldNotContain("Text=\"Save\"");
        markup.ShouldNotContain("Text=\"Save as\"");
        markup.ShouldNotContain("Text=\"Validate app\"");
        markup.ShouldNotContain("Text=\"Run app\"");
        markup.ShouldNotContain("Text=\"Stop app\"");
        markup.ShouldNotContain("aria-label=\"Open file\"");
        markup.ShouldNotContain("aria-label=\"Run app\"");
        markup.ShouldNotContain("aria-label=\"Stop app\"");
        markup.ShouldNotContain("aria-label=\"@(_rightOpen ? \"Hide MQTT publisher\" : \"Show MQTT publisher\")\"");
        markup.ShouldNotContain("Text=\"@(_rightOpen ? \"Hide MQTT publisher\" : \"Show MQTT publisher\")\"");
        markup.ShouldNotContain("Workspace navigation");
        markup.ShouldNotContain("No active project");
        markup.ShouldNotContain("Class=\"flux-command-spin-icon\" />");
        markup.ShouldNotContain("<MudIcon Icon=\"@DragPreviewIcon(activeDrag.TargetKind)\" Size=\"Size.Small\" />");
        markup.ShouldNotContain("<span class=\"flux-app-runtime-dot\"></span>");
        markup.ShouldNotContain("<span class=\"flux-live-connection-dot\"></span>");
        markup.ShouldNotContain("<span class=\"@LiveConnectionDotClass\"></span>");
        markup.ShouldNotContain("flux-rail");
        markup.ShouldNotContain("flux-left-panel");
        markup.ShouldNotContain("left-collapsed");
        markup.ShouldNotContain("_leftOpen");
        markup.ShouldNotContain("<SessionPanel />");

        css.ShouldContain("--flux-right-width: 360px;");
        css.ShouldContain("--flux-bottom-height: 28px;");
        css.ShouldContain("\"top top\"");
        css.ShouldContain("\"main right\"");
        css.ShouldContain("\"bottom bottom\"");
        css.ShouldContain("\"bottom\";");
        css.ShouldContain(".flux-breadcrumb");
        css.ShouldContain(".flux-bottom-bar");
        css.ShouldContain(".flux-live-dot");
        css.ShouldContain(".flux-app-runtime-marker");
        css.ShouldContain(".flux-app-runtime-marker.valid");
        css.ShouldContain(".flux-app-runtime-dot");
        css.ShouldContain(".flux-live-connection-marker");
        css.ShouldContain(".flux-live-connection-marker.live");
        css.ShouldContain(".flux-live-connection-dot");
        css.ShouldContain("font-size: 12.5px;");
        css.ShouldContain("font-weight: 650;");
        css.ShouldContain(".flux-shell.right-collapsed");
        css.ShouldContain(".flux-shell.no-active-project");
        css.ShouldContain("grid-template-areas: \"main\";");
        css.ShouldContain("grid-template-rows: minmax(0, 1fr);");
        css.ShouldContain("\"main\"");
        css.ShouldContain("grid-template-columns: minmax(0, 1fr) var(--flux-right-width);");
        css.ShouldNotContain("--flux-status-height");
        css.ShouldNotContain("\"status status\"");
        css.ShouldNotContain("\"status\";");
        css.ShouldNotContain(".flux-statusbar");
        css.ShouldNotContain(".flux-status-dot");
        css.ShouldNotContain(".flux-flowstate");
        css.ShouldNotContain(".flux-flow-dot");
        css.ShouldNotContain(".flux-runstate");
        css.ShouldNotContain(".flux-run-dot");
        css.ShouldNotContain("--flux-rail-width");
        css.ShouldNotContain("--flux-left-width");
        css.ShouldNotContain(".flux-rail");
        css.ShouldNotContain(".flux-left-panel");
        css.ShouldNotContain("left-collapsed");
        css.ShouldNotContain(".flux-panel-header");
        css.ShouldNotContain(".flux-panel-body");

        appCss.ShouldContain("--flux-bg: var(--mud-palette-background);");
        appCss.ShouldContain("--flux-surface: var(--mud-palette-surface);");
        appCss.ShouldContain("--flux-surface-2: var(--mud-palette-background-gray);");
        appCss.ShouldContain("--flux-border: var(--mud-palette-lines-default);");
        appCss.ShouldContain("--flux-border-strong: var(--mud-palette-lines-inputs);");
        appCss.ShouldContain("--flux-accent: var(--mud-palette-primary);");
        appCss.ShouldContain("--flux-text-muted: var(--mud-palette-text-disabled);");
        appCss.ShouldContain("--flux-accent-contrast: var(--mud-palette-primary-text);");
        appCss.ShouldContain("--flux-accent-soft: color-mix(in srgb, var(--mud-palette-primary)");
        appCss.ShouldContain(".flux-shell .mud-input > input.mud-input-root,");
        appCss.ShouldContain(".flux-shell .mud-input > div.mud-input-slot,");
        appCss.ShouldContain("font-size: 13px;");
        appCss.ShouldContain("line-height: 1.25;");
        appCss.ShouldContain(".flux-shell .mud-input > input.mud-input-root-outlined.mud-input-root-margin-dense,");
        appCss.ShouldContain("padding-bottom: 13.5px;");
        appCss.ShouldContain("padding-top: 7.5px;");
        appCss.ShouldContain(".flux-shell .mud-input-control > .mud-input-control-input-container > .mud-input-label-outlined.mud-input-label-margin-dense");
        appCss.ShouldContain("transform: translate(14px, 9px) scale(1);");
        appCss.ShouldNotContain("--flux-bg: #0a0d12;");
        appCss.ShouldNotContain("--flux-bg: #f4f6f8;");
        appCss.ShouldNotContain("--flux-surface: #11151c;");
        appCss.ShouldNotContain("--flux-surface: #ffffff;");
        appCss.ShouldNotContain("--flux-border: #1d232e;");
        appCss.ShouldNotContain("--flux-border: #dde3ea;");
        appCss.ShouldNotContain("--flux-accent: #2dd4bf;");
        appCss.ShouldNotContain("--flux-accent: #0f766e;");
        appCss.ShouldNotContain("--flux-accent-contrast: #062a26;");
        appCss.ShouldNotContain("--flux-accent-contrast: #ffffff;");
    }

    [Fact]
    public void TopicExplorerPanel_UsesFlatCompactWorkspaceChrome()
    {
        var root = FindRepositoryRoot();
        var markup = File.ReadAllText(Path.Combine(
            root,
            "src",
            "FluxMq.UI",
            "Components",
            "Workspace",
            "TopicExplorerPanel.razor"));
        var css = File.ReadAllText(Path.Combine(
            root,
            "src",
            "FluxMq.UI",
            "Components",
            "Workspace",
            "TopicExplorerPanel.razor.css"));
        var resolver = File.ReadAllText(Path.Combine(
            root,
            "src",
            "FluxMq.UI",
            "Services",
            "TopicExplorerMonitorResolver.cs"));

        markup.ShouldContain("aria-label=\"@TopicExplorerTreeLabel\"");
        markup.ShouldContain("private string TopicExplorerTreeLabel => $\"{TopicSourceLabel} topic tree, {BrokerCountLabel(BrokerGroups.Count)}, {TopicCountLabel(TopicCount)}\"");
        markup.ShouldContain("private static string BrokerCountLabel(int count)");
        markup.ShouldContain("private static string TopicCountLabel(int count)");
        markup.ShouldNotContain("aria-label=\"Topic tree\"");
        markup.ShouldContain("aria-label=\"@TopicExplorerDetailLabel\"");
        markup.ShouldContain("private string TopicExplorerDetailLabel => $\"{SelectedTopicLabel} latest message and history\"");
        markup.ShouldNotContain("aria-label=\"Topic latest message and history\"");
        markup.ShouldNotContain("aria-label=\"Topic last state and history\"");
        markup.ShouldContain("aria-label=\"@LatestTopicMessageLabel\"");
        markup.ShouldContain("private string LatestTopicMessageLabel => $\"{SelectedTopicLabel} latest topic message\"");
        markup.ShouldNotContain("aria-label=\"Latest topic message\"");
        markup.ShouldContain("aria-label=\"@PublishPanelLabel\"");
        markup.ShouldContain("private string PublishPanelLabel => $\"Publish MQTT message for {SelectedTopicLabel}\"");
        markup.ShouldNotContain("aria-label=\"Publish MQTT message\"");
        markup.ShouldContain("aria-label=\"@TopicHistoryPanelLabel\"");
        markup.ShouldContain("private string TopicHistoryPanelLabel => $\"{SelectedTopicLabel} message history, {HistorySummaryLabel}\"");
        markup.ShouldNotContain("aria-label=\"Topic message history\"");
        markup.ShouldContain("<h2>Topics</h2>");
        markup.ShouldContain("@implements IDisposable");
        markup.ShouldContain("@using System.Globalization");
        markup.ShouldContain("@using System.Text.Json.Serialization");
        markup.ShouldContain("@using MQTTnet.Protocol");
        markup.ShouldContain("@inject ProjectManagerService Projects");
        markup.ShouldContain("@inject ISnackbar Snackbar");
        markup.ShouldContain("EnsureTopicMonitorAsync");
        markup.ShouldContain("BuildTopicMonitorConnections");
        markup.ShouldContain("Live.EnsureConnectionsAsync(connections)");
        markup.ShouldContain("TopicExplorerMonitorResolver.Resolve");
        resolver.ShouldContain("LiveMqttWorkspaceService.TopicExplorerMonitorSubscription");
        resolver.ShouldContain("LiveMqttWorkspaceService.CreateTopicMonitorResourceName");
        markup.ShouldContain("Sub # + $SYS/#");
        markup.ShouldContain("ExplorerBrokerSnapshots");
        markup.ShouldContain("App broker monitor");
        markup.ShouldContain("topic-broker-group");
        markup.ShouldContain("topic-broker-row");
        markup.ShouldContain("topic-broker-main");
        markup.Split('\n')
            .Where(static line => line.Contains("<MudIcon ", StringComparison.Ordinal) &&
                !line.Contains("aria-hidden=\"true\"", StringComparison.Ordinal))
            .ToArray()
            .ShouldBeEmpty();
        System.Text.RegularExpressions.Regex.Matches(
                markup,
                @"<MudIconButton\b(?:(?!/>).)*?/>",
                System.Text.RegularExpressions.RegexOptions.Singleline)
            .Cast<System.Text.RegularExpressions.Match>()
            .Select(static match => match.Value)
            .Where(static iconButton => !iconButton.Contains("aria-label=", StringComparison.Ordinal) &&
                !iconButton.Contains("AriaLabel=", StringComparison.Ordinal))
            .ToArray()
            .ShouldBeEmpty();
        markup.ShouldContain("class=\"topic-explorer-title-icon\" aria-hidden=\"true\"");
        markup.ShouldContain("class=\"topic-broker-icon\" aria-hidden=\"true\"");
        markup.ShouldContain("topic-broker-connection");
        markup.ShouldContain("topic-broker-edit");
        markup.ShouldContain("Icons.Material.Filled.Settings");
        markup.ShouldContain("OpenBrokerMonitorEditorAsync");
        markup.ShouldContain("aria-label=\"@SelectBrokerLabel(group)\"");
        markup.ShouldContain("private string SelectBrokerLabel(TopicBrokerGroup group)");
        markup.ShouldContain("$\"Select broker {group.Name}, {BrokerConnectionLabel(group)}, {TopicCountLabel(group.TopicCount)}\"");
        markup.ShouldContain("<MudTooltip Text=\"@BrokerMonitorSettingsLabel(group)\">");
        markup.ShouldContain("aria-label=\"@BrokerMonitorSettingsLabel(group)\"");
        markup.ShouldContain("private string BrokerMonitorSettingsLabel(TopicBrokerGroup group)");
        markup.ShouldContain("$\"Open broker monitor settings for {group.Name}\"");
        markup.ShouldNotContain("aria-label=\"@($\"Select broker {group.Name}\")\"");
        markup.ShouldNotContain("aria-label=\"@($\"Open broker monitor settings {group.Name}\")\"");
        markup.ShouldNotContain("<MudTooltip Text=\"Broker monitor settings\">");
        markup.ShouldContain("PreserveCandidateExplorerNames");
        markup.ShouldContain("topic-broker-tree");
        markup.ShouldContain("topic-session-note");
        markup.ShouldContain("Class=\"topic-session-live-button\"");
        markup.ShouldContain("aria-label=\"@SwitchToLiveTrafficLabel\"");
        markup.ShouldContain("private string SwitchToLiveTrafficLabel => Live.SelectedStoredSession is { } session");
        markup.ShouldContain("$\"Switch {session.Name} to live traffic\"");
        markup.ShouldNotContain("aria-label=\"Switch to live traffic\"");
        markup.ShouldContain("aria-label=\"@ClearTopicSelectionLabel\"");
        markup.ShouldContain("<MudTooltip Text=\"@ClearTopicSelectionLabel\">");
        markup.ShouldContain("private string ClearTopicSelectionLabel => $\"Clear topic selection for {SelectedTopicLabel}\"");
        markup.ShouldNotContain("aria-label=\"Clear topic selection\"");
        markup.ShouldNotContain("<MudTooltip Text=\"Clear topic selection\">");
        markup.ShouldContain("OnClick=\"@Live.ClearStoredSessionSelection\"");
        markup.ShouldContain("Icon=\"@Icons.Material.Filled.Sensors\"");
        markup.ShouldNotContain("<button type=\"button\" @onclick=\"@Live.ClearStoredSessionSelection\">Switch to live</button>");
        markup.ShouldContain("BrokerGroups.Count brokers");
        markup.ShouldContain("VisibleBrokerGroups");
        markup.ShouldContain("BrokerLabel(LastMessage)");
        markup.ShouldContain("topic-stats-panel");
        markup.ShouldContain("aria-label=\"@TopicStatsPanelLabel\"");
        markup.ShouldContain("private string TopicStatsPanelLabel => $\"{SelectedTopicLabel} topic scope statistics, {MessageCountLabel(HistoryMessages.Count)}\"");
        markup.ShouldNotContain("aria-label=\"Topic scope statistics\"");
        markup.ShouldContain("var topicStats = TopicStats;");
        markup.ShouldContain("TopicStats => BuildTopicScopeStats(HistoryMessages)");
        markup.ShouldContain("TopicStatsScopeLabel");
        markup.ShouldContain("BuildTopicScopeStats");
        markup.ShouldContain("TopicScopeStats");
        markup.ShouldContain("TopicScopeStats.Empty");
        markup.ShouldContain("No messages in this scope");
        markup.ShouldContain("FormatByteCount(topicStats.TotalPayloadBytes)");
        markup.ShouldContain("FormatAveragePayloadBytes(topicStats.AveragePayloadBytes)");
        markup.ShouldContain("FormatTopicStatsLatest(topicStats)");
        markup.ShouldContain("messages.Sum(static message => (long)message.Payload.Length)");
        markup.ShouldContain("messages.Count(static message => message.Retain)");
        markup.ShouldContain("MqttQualityOfServiceLevel.AtMostOnce");
        markup.ShouldContain("MqttQualityOfServiceLevel.AtLeastOnce");
        markup.ShouldContain("MqttQualityOfServiceLevel.ExactlyOnce");
        markup.ShouldNotContain("TopicStatsResource");
        markup.ShouldNotContain("PersistTopicStats");
        markup.ShouldNotContain("SaveTopicStats");
        markup.ShouldContain("topic-publish-panel");
        markup.ShouldContain("topic-publish-grid");
        markup.ShouldContain("PublishConnections.Count > 0");
        markup.ShouldContain("PublishClientCountLabel");
        markup.ShouldContain("PublishConnectionLabel");
        markup.ShouldContain("PublishPayloadMeta");
        markup.ShouldContain("PublishButtonLabel");
        markup.ShouldContain("CanPublish");
        markup.ShouldContain("topic-publish-assist");
        markup.ShouldContain("aria-label=\"@PublishAssistLabel\"");
        markup.ShouldContain("private string PublishAssistLabel => $\"Publish assist for {SelectedTopicLabel}\"");
        markup.ShouldNotContain("aria-label=\"Publish assist\"");
        markup.ShouldContain("Use message");
        markup.ShouldContain("Use latest");
        markup.ShouldContain("Use selected");
        markup.ShouldContain("CanUseLatestPublishSource");
        markup.ShouldContain("CanUseSelectedPublishSource");
        markup.ShouldContain("ApplyMessagePublishSource");
        markup.ShouldContain("CanUseMessageAsPublishSource");
        markup.ShouldContain("PublishSourceHint");
        markup.ShouldContain("Only text payloads can be loaded into the publisher");
        markup.ShouldContain("_publishPayload = inspection.RawText");
        markup.ShouldContain("_publishQos = message.QualityOfService");
        markup.ShouldContain("_publishRetain = message.Retain");
        markup.ShouldContain("FindPublishConnectionForBroker(BrokerLabel(message))");
        markup.ShouldContain("topic-publish-recent");
        markup.ShouldContain("aria-label=\"@RecentPublishesLabel\"");
        markup.ShouldContain("private string RecentPublishesLabel => $\"Recent publishes, {RecentPublishCountLabel}\"");
        markup.ShouldContain("private string RecentPublishCountLabel => _recentPublishes.Count switch");
        markup.ShouldNotContain("aria-label=\"Recent publishes\"");
        markup.ShouldContain("MaxRecentPublishes");
        markup.ShouldContain("_recentPublishes");
        markup.ShouldContain("TopicRecentPublish");
        markup.ShouldContain("RecordRecentPublish(connectedConnection, topic, payload)");
        markup.ShouldContain("RecordRecentPublish");
        markup.ShouldContain("LoadRecentPublish");
        markup.ShouldContain("ClearRecentPublishes");
        markup.ShouldContain("RecentPublishMeta");
        markup.ShouldContain("RecentPublishTitle");
        markup.ShouldContain("No recent publishes");
        markup.ShouldContain("<MudTooltip Text=\"@ClearRecentPublishesLabel\">");
        markup.ShouldContain("aria-label=\"@ClearRecentPublishesLabel\"");
        markup.ShouldContain("private string ClearRecentPublishesLabel => $\"Clear {RecentPublishCountLabel}\"");
        markup.ShouldNotContain("aria-label=\"Clear recent publishes\"");
        markup.ShouldNotContain("<MudTooltip Text=\"Clear recent publishes\">");
        markup.ShouldContain("PayloadInspector.Inspect(payloadBytes)");
        markup.ShouldContain("connection.ResourceName");
        markup.ShouldContain("PublishConnectionOptionLabel(connection)");
        markup.ShouldContain("_recentPublishes.RemoveAll");
        markup.ShouldContain("_recentPublishes.Insert(0, recent)");
        markup.ShouldNotContain("PublishTemplate");
        markup.ShouldNotContain("SavedPublish");
        markup.ShouldNotContain("UpsertPublish");
        markup.ShouldContain("BuildPublishConnections");
        markup.ShouldContain("EnsurePublishConnectionsForActiveProject");
        markup.ShouldContain("Live.AddConnectionIfAbsent(profile, subscription, name)");
        markup.ShouldContain("!LiveMqttWorkspaceService.IsTopicMonitorConnection(connection)");
        markup.ShouldContain("FindPublishConnectionForBroker(_selectedBrokerName)");
        markup.ShouldContain("SyncPublishSelectionFromTopicSelection(prefillTopic: true)");
        markup.ShouldContain("SyncPublishSelectionFromTopicSelection(prefillTopic: false)");
        markup.ShouldContain("SameBrokerEndpoint(connection.Profile, monitor.Profile)");
        markup.ShouldContain("Live.ConnectAsync(connection.Id)");
        markup.ShouldContain("Live.PublishAsync(");
        markup.ShouldContain("RecordManualMqttPublish");
        markup.ShouldContain("aria-label=\"@PublishBrokerLabel\"");
        markup.ShouldContain("private string PublishBrokerLabel => $\"Publish broker for {SelectedTopicLabel}, {PublishClientCountLabel}\"");
        markup.ShouldNotContain("aria-label=\"Publish broker\"");
        markup.ShouldContain("aria-label=\"@PublishQosLabel\"");
        markup.ShouldContain("private string PublishQosLabel => $\"Publish quality of service for {SelectedTopicLabel}\"");
        markup.ShouldNotContain("aria-label=\"Publish quality of service\"");
        markup.ShouldContain("MqttQualityOfServiceLevel.AtMostOnce");
        markup.ShouldContain("topic-publish-retain");
        markup.ShouldContain("aria-label=\"@PublishRetainLabel\"");
        markup.ShouldContain("private string PublishRetainLabel => _publishRetain");
        markup.ShouldNotContain("aria-label=\"Publish retained message\"");
        markup.ShouldContain("aria-pressed=\"@(_publishRetain ? \"true\" : \"false\")\"");
        markup.ShouldContain("topic-publish-submit");
        markup.ShouldContain("No app brokers");
        markup.ShouldContain("<MudTh>Broker</MudTh>");
        markup.ShouldContain("<MudTh>Retain</MudTh>");
        markup.ShouldContain("<MudTd DataLabel=\"Broker\">@BrokerLabel(context)</MudTd>");
        markup.ShouldContain("<MudTd DataLabel=\"Retain\">@(context.Retain ? \"Yes\" : \"No\")</MudTd>");
        markup.ShouldContain("Compact=\"true\"");
        markup.ShouldContain("Class=\"topic-message-grid\"");
        markup.ShouldContain("Items=\"@VisibleHistoryMessages\"");
        markup.ShouldContain("SelectedItem=\"@SelectedHistoryMessage\"");
        markup.ShouldContain("SelectedItemChanged=\"@OnHistoryMessageSelected\"");
        markup.ShouldContain("RowClassFunc=\"@HistoryRowClass\"");
        markup.ShouldContain("SelectOnRowClick=\"true\"");
        markup.ShouldContain("FixedHeader=\"true\"");
        markup.ShouldContain("Height=\"100%\"");
        markup.ShouldContain("ItemSize=\"30\"");
        markup.ShouldContain("OverscanCount=\"8\"");
        markup.ShouldContain("Virtualize=\"true\"");
        markup.ShouldContain("<ColGroup>");
        markup.ShouldContain("class=\"topic-col-time\"");
        markup.ShouldContain("class=\"topic-col-topic\"");
        markup.ShouldContain("class=\"topic-col-retain\"");
        markup.ShouldContain("role=\"search\" aria-label=\"@TopicHistoryFiltersLabel\"");
        markup.ShouldContain("private string TopicHistoryFiltersLabel => $\"{SelectedTopicLabel} history filters\"");
        markup.ShouldNotContain("role=\"search\" aria-label=\"History filters\"");
        markup.ShouldContain("Placeholder=\"Filter history\"");
        markup.ShouldContain("topic-history-filter");
        markup.ShouldContain("topic-history-select");
        markup.ShouldContain("aria-label=\"@HistoryQosFilterLabel\"");
        markup.ShouldContain("private string HistoryQosFilterLabel => $\"Filter {SelectedTopicLabel} history by QoS, {HistoryQosFilterText(HistoryQosFilter)}\"");
        markup.ShouldContain("private static string HistoryQosFilterText(string filter)");
        markup.ShouldNotContain("aria-label=\"Filter history quality of service\"");
        markup.ShouldContain("aria-label=\"@HistoryRetainFilterLabel\"");
        markup.ShouldContain("private string HistoryRetainFilterLabel => $\"Filter {SelectedTopicLabel} history by retain state, {HistoryRetainFilterText(HistoryRetainFilter)}\"");
        markup.ShouldContain("private static string HistoryRetainFilterText(string filter)");
        markup.ShouldNotContain("aria-label=\"Filter history retain state\"");
        markup.ShouldContain("<option value=\"@HistoryFilterAll\">All</option>");
        markup.ShouldContain("<option value=\"@HistoryQos0\">QoS 0</option>");
        markup.ShouldContain("<option value=\"@HistoryQos1\">QoS 1</option>");
        markup.ShouldContain("<option value=\"@HistoryQos2\">QoS 2</option>");
        markup.ShouldContain("<option value=\"@HistoryRetained\">Retained</option>");
        markup.ShouldContain("<option value=\"@HistoryNotRetained\">Not retained</option>");
        markup.ShouldContain("ResetHistoryFilters");
        markup.ShouldContain("aria-label=\"@ResetHistoryFiltersLabel\"");
        markup.ShouldContain("<MudTooltip Text=\"@ResetHistoryFiltersLabel\">");
        markup.ShouldContain("private string ResetHistoryFiltersLabel => $\"Reset history filters for {SelectedTopicLabel}\"");
        markup.ShouldNotContain("aria-label=\"Reset history filters\"");
        markup.ShouldNotContain("<MudTooltip Text=\"Reset history filters\">");
        markup.ShouldContain("ExportVisibleHistoryAsync");
        markup.ShouldContain("ExportHistoryLabel");
        markup.ShouldContain("CanExportHistory");
        markup.ShouldContain("aria-label=\"@ExportVisibleHistoryLabel\"");
        markup.ShouldContain("private string ExportVisibleHistoryLabel => $\"{ExportHistoryLabel} for {SelectedTopicLabel}\"");
        markup.ShouldNotContain("aria-label=\"Export visible history as JSON\"");
        markup.ShouldContain("BuildHistoryExportJson");
        markup.ShouldContain("SuggestedHistoryExportPath");
        markup.ShouldContain("WriteHistoryExportAsync");
        markup.ShouldContain("ShowAsync<SaveAsDialog>");
        markup.ShouldContain("[nameof(SaveAsDialog.SubmitText)] = \"Export\"");
        markup.ShouldContain("JsonSerializer.Serialize(rows, HistoryExportJsonOptions)");
        markup.ShouldContain("JsonIgnoreCondition.WhenWritingNull");
        markup.ShouldContain("PayloadInspector.Inspect(message.Payload)");
        markup.ShouldContain("Convert.ToBase64String(message.Payload)");
        markup.ShouldContain("payloadBase64");
        markup.ShouldContain("payloadText = inspection.IsText ? inspection.RawText : null");
        markup.ShouldContain("payloadHex = inspection.HexDump");
        markup.ShouldContain("File.WriteAllTextAsync(fullPath, content)");
        markup.ShouldContain("VisibleHistoryMessages");
        markup.ShouldContain("MatchesHistoryTextFilter");
        markup.ShouldContain("MatchesHistoryQosFilter");
        markup.ShouldContain("MatchesHistoryRetainFilter");
        markup.ShouldContain("HistorySummaryLabel => HasHistoryFilters");
        markup.ShouldContain("MessageCountLabel(HistoryMessages.Count)");
        markup.ShouldContain("private MqttEnvelope? SelectedHistoryMessage");
        markup.ShouldContain("var messages = VisibleHistoryMessages;");
        markup.ShouldContain("aria-label=\"@SelectedMessageDetailsLabel\"");
        markup.ShouldContain("private string SelectedMessageDetailsLabel => SelectedHistoryMessage is { } message");
        markup.ShouldContain("$\"Selected MQTT message details for {BrokerLabel(message)} / {message.Topic}\"");
        markup.ShouldNotContain("aria-label=\"Selected MQTT message details\"");
        markup.ShouldContain("Message details");
        markup.ShouldContain("SelectedHistoryPayloadPreview");
        markup.ShouldContain("SelectedHistoryReceivedLabel");
        markup.ShouldContain("role=\"tablist\" aria-label=\"@LatestPayloadViewsLabel\"");
        markup.ShouldContain("private string LatestPayloadViewsLabel => $\"{SelectedTopicLabel} latest payload views\"");
        markup.ShouldNotContain("role=\"tablist\" aria-label=\"Latest payload views\"");
        markup.ShouldContain("role=\"tablist\" aria-label=\"@SelectedPayloadViewsLabel\"");
        markup.ShouldContain("private string SelectedPayloadViewsLabel => SelectedHistoryMessage is { } message");
        markup.ShouldContain("$\"Selected payload views for {BrokerLabel(message)} / {message.Topic}\"");
        markup.ShouldNotContain("role=\"tablist\" aria-label=\"Selected payload views\"");
        System.Text.RegularExpressions.Regex.Matches(markup, "aria-keyshortcuts=\"Enter Space ArrowLeft ArrowRight Home End\"").Count.ShouldBe(2);
        markup.ShouldContain("id=\"@LatestPayloadViewTabId(view)\"");
        markup.ShouldContain("aria-controls=\"@LatestPayloadPanelId\"");
        markup.ShouldContain("id=\"@LatestPayloadPanelId\"");
        markup.ShouldContain("aria-labelledby=\"@LatestPayloadViewTabId(_lastPayloadView)\"");
        markup.ShouldContain("id=\"@SelectedPayloadViewTabId(view)\"");
        markup.ShouldContain("aria-controls=\"@SelectedPayloadPanelId\"");
        markup.ShouldContain("id=\"@SelectedPayloadPanelId\"");
        markup.ShouldContain("aria-labelledby=\"@SelectedPayloadViewTabId(_selectedHistoryPayloadView)\"");
        markup.ShouldContain("@onkeydown=\"@((KeyboardEventArgs args) => OnLatestPayloadViewTabKeyDown(args, view))\"");
        markup.ShouldContain("@onkeydown=\"@((KeyboardEventArgs args) => OnSelectedPayloadViewTabKeyDown(args, view))\"");
        markup.ShouldContain("private static string LatestPayloadViewTabId");
        markup.ShouldContain("private static string SelectedPayloadViewTabId");
        markup.ShouldContain("PayloadViewOptions");
        markup.ShouldContain("SelectedPayloadViewOptions");
        markup.ShouldContain("@foreach (var view in PayloadViewOptions)");
        markup.ShouldContain("@foreach (var view in SelectedPayloadViewOptions)");
        markup.ShouldContain("PayloadFormattedView");
        markup.ShouldContain("PayloadRawView");
        markup.ShouldContain("PayloadHexView");
        markup.ShouldContain("PayloadMetaView");
        markup.ShouldContain("PayloadDiffView");
        markup.ShouldContain("PayloadDiffContextLineCount");
        markup.ShouldContain("PayloadDiffMaxChangedLines");
        markup.ShouldContain("PayloadViewButtonClass");
        markup.ShouldContain("PayloadViewLabel(view)");
        markup.ShouldContain("PayloadViewIcon(view)");
        markup.ShouldContain("OnLatestPayloadViewTabKeyDown");
        markup.ShouldContain("OnSelectedPayloadViewTabKeyDown");
        markup.ShouldContain("ResolvePayloadView");
        markup.ShouldContain("DisplayPayloadView(LastInspection, _lastPayloadView)");
        markup.ShouldContain("DisplaySelectedPayloadView");
        markup.ShouldContain("DisplaySelectedPayloadDiff");
        markup.ShouldContain("BuildPayloadDiffText");
        markup.ShouldContain("BuildUnifiedPayloadDiffText");
        markup.ShouldContain("BuildBinaryPayloadDiffText");
        markup.ShouldContain("FirstDifferingByteIndex");
        markup.ShouldContain("SplitPayloadLines");
        markup.ShouldContain("Selected message is the latest message.");
        markup.ShouldContain("Payload unchanged.");
        markup.ShouldContain("First differing byte:");
        markup.ShouldContain("CopyLatestPayloadViewAsync");
        markup.ShouldContain("CopySelectedHistoryPayloadViewAsync");
        markup.ShouldContain("Clipboard.Default.SetTextAsync");
        markup.ShouldContain("Clipboard.Default.SetTextAsync(SelectedHistoryPayloadPreview)");
        markup.ShouldContain("Payload diff copied");
        markup.ShouldContain("Snackbar.Add");
        markup.ShouldContain("Icons.Material.Filled.ContentCopy");
        markup.ShouldContain("Icons.Material.Filled.CompareArrows");
        markup.ShouldContain("<MudTooltip Text=\"@CopyLatestPayloadViewLabel\">");
        markup.ShouldContain("aria-label=\"@CopyLatestPayloadViewLabel\"");
        markup.ShouldContain("private string CopyLatestPayloadViewLabel => $\"Copy latest {PayloadViewLabel(_lastPayloadView)} payload view for {SelectedTopicLabel}\"");
        markup.ShouldNotContain("aria-label=\"Copy latest payload view\"");
        markup.ShouldNotContain("<MudTooltip Text=\"Copy latest payload view\">");
        markup.ShouldContain("<MudTooltip Text=\"@CopySelectedPayloadViewLabel\">");
        markup.ShouldContain("aria-label=\"@CopySelectedPayloadViewLabel\"");
        markup.ShouldContain("private string CopySelectedPayloadViewLabel => $\"Copy selected {PayloadViewLabel(_selectedHistoryPayloadView)} payload view for {SelectedTopicLabel}\"");
        markup.ShouldNotContain("aria-label=\"Copy selected payload view\"");
        markup.ShouldNotContain("<MudTooltip Text=\"Copy selected payload view\">");
        markup.ShouldContain("Select a history row to inspect MQTT metadata and payload.");
        markup.ShouldContain("LastMessage is null");
        markup.ShouldContain("topic-latest-message");
        markup.ShouldContain("LatestMessageSubtitle");
        markup.ShouldNotContain("topic-last-state");
        markup.ShouldNotContain("LastStateSubtitle");
        markup.ShouldContain("topic-last-payload");
        markup.ShouldContain("topic-last-meta");
        markup.ShouldContain("aria-label=\"@LatestMessageMetadataLabel\"");
        markup.ShouldContain("private string LatestMessageMetadataLabel => LastMessage is { } message");
        markup.ShouldContain("$\"Latest message metadata for {BrokerLabel(message)} / {message.Topic}\"");
        markup.ShouldNotContain("aria-label=\"Latest message metadata\"");
        markup.ShouldContain("topic-no-traffic");
        markup.ShouldContain("topic-monitor-list");
        markup.ShouldContain("aria-label=\"@BrokerMonitorsLabel\"");
        markup.ShouldContain("private string BrokerMonitorsLabel => $\"{BrokerCountLabel(NoTrafficBrokerGroups.Count)} shown for {SelectedTopicLabel}\"");
        markup.ShouldNotContain("aria-label=\"Broker monitors\"");
        markup.ShouldNotContain("aria-label=\"Broker monitor status\"");
        markup.ShouldContain("NoTrafficBrokerGroups");
        markup.ShouldContain("MonitorRowClass");
        markup.ShouldContain("topic-monitor-connection");
        markup.ShouldContain("BrokerConnectionClass");
        markup.ShouldContain("BrokerConnectionLabel");
        markup.ShouldNotContain("BrokerStateClass");
        markup.ShouldNotContain("BrokerStateLabel");
        markup.ShouldNotContain("topic-broker-state");
        markup.ShouldNotContain("topic-monitor-state");
        markup.ShouldContain("One broker monitor is subscribed to #.");
        markup.ShouldContain("No history for the current selection.");
        markup.ShouldContain("topic-history-panel");
        markup.ShouldContain("role=\"status\" aria-live=\"polite\"");
        markup.ShouldContain("PayloadInspector.Inspect(LastMessage.Payload)");
        markup.ShouldContain("LastPayloadPreview");
        markup.ShouldContain("PayloadInspector.Inspect(SelectedHistoryMessage.Payload)");
        markup.ShouldContain("DisplayFormattedPayload");
        markup.ShouldContain("DisplayRawPayload");
        markup.ShouldContain("DisplayHexPayload");
        markup.ShouldContain("DisplayPayloadMeta");
        markup.ShouldContain("HistorySummaryLabel");
        markup.ShouldContain("Latest message for selected broker topic and children");
        markup.ShouldContain("Live.SelectMessage(latest)");
        markup.ShouldContain("_selectedHistoryMessage = latest;");
        markup.ShouldContain("Live.SelectMessage(message);");
        markup.ShouldNotContain("<PayloadInspectorPanel");
        markup.ShouldNotContain("SelectedMessage is null");
        markup.ShouldNotContain("topic-payload-empty");
        markup.ShouldNotContain("No payload selected");
        markup.ShouldNotContain("SelectedMessage.Topic");
        markup.ShouldNotContain("Topic=\"@SelectedMessage?.Topic\"");
        markup.ShouldNotContain("The Topics tab connects to app brokers");
        markup.ShouldNotContain("The Topics tab starts separate broker monitor clients");
        markup.ShouldNotContain("Standalone broker monitor");
        markup.ShouldNotContain("RowsPerPage=");
        markup.ShouldNotContain("<MudTablePager");

        css.ShouldContain("grid-template-columns: minmax(420px, 480px) minmax(0, 1fr);");
        css.ShouldContain("padding: 8px 12px;");
        css.ShouldContain("height: 26px;");
        css.ShouldContain(".topic-search ::deep .mud-input-root");
        css.ShouldContain(".topic-broker-group");
        css.ShouldContain(".topic-broker-row");
        css.ShouldContain("grid-template-columns: minmax(0, 1fr) auto auto auto;");
        css.ShouldContain(".topic-broker-row.selected");
        css.ShouldContain(".topic-broker-main");
        css.ShouldContain("grid-template-columns: 24px minmax(0, 1fr);");
        css.ShouldContain(".topic-broker-edit");
        css.ShouldContain(".topic-broker-edit ::deep .mud-icon-root");
        css.ShouldContain("font-size: 16px;");
        css.ShouldContain(".topic-broker-row.live .topic-broker-connection");
        css.ShouldContain(".topic-broker-tree");
        css.ShouldContain(".topic-broker-empty");
        css.ShouldContain(".topic-session-note ::deep .topic-session-live-button");
        css.ShouldContain(".topic-session-note ::deep .topic-session-live-button .mud-icon-root");
        css.ShouldNotContain(".topic-session-note button");
        css.ShouldContain(".topic-latest-message");
        css.ShouldNotContain(".topic-last-state");
        css.ShouldContain("flex: 0 0 clamp(218px, 38%, 360px);");
        css.ShouldContain(".topic-last-body");
        css.ShouldContain("grid-template-columns: minmax(0, 1fr) minmax(320px, 360px);");
        css.ShouldContain(".topic-payload-header");
        css.ShouldContain(".topic-payload-heading");
        css.ShouldContain(".topic-payload-controls");
        css.ShouldContain(".topic-payload-view-button");
        css.ShouldContain(".topic-payload-view-button.active");
        css.ShouldContain(".topic-payload-copy");
        css.ShouldContain("flex-wrap: wrap;");
        css.ShouldContain(".topic-last-payload pre");
        css.ShouldContain("white-space: pre-wrap;");
        css.ShouldContain(".topic-last-meta");
        css.ShouldContain("align-self: stretch;");
        css.ShouldContain("flex-direction: column;");
        css.ShouldContain("grid-template-columns: minmax(72px, 0.24fr) minmax(0, 1fr);");
        css.ShouldContain(".topic-last-meta div:last-child");
        css.ShouldContain(".topic-stats-panel");
        css.ShouldContain(".topic-stats-header");
        css.ShouldContain(".topic-stats-grid");
        css.ShouldContain("grid-template-columns: repeat(auto-fit, minmax(82px, 1fr));");
        css.ShouldContain(".topic-stats-item,");
        css.ShouldContain(".topic-stats-item.primary");
        css.ShouldContain(".topic-stats-item.latest");
        css.ShouldContain(".topic-stats-empty");
        css.ShouldContain(".topic-publish-panel");
        css.ShouldContain(".topic-publish-header");
        css.ShouldContain(".topic-publish-grid");
        css.ShouldContain("grid-template-columns: minmax(170px, 0.34fr) minmax(200px, 0.42fr) minmax(260px, 1fr) auto;");
        css.ShouldContain(".topic-publish-field");
        css.ShouldContain(".topic-publish-input,");
        css.ShouldContain(".topic-publish-textarea,");
        css.ShouldContain(".topic-publish-static");
        css.ShouldContain("min-height: 54px;");
        css.ShouldContain("max-height: 78px;");
        css.ShouldContain(".topic-publish-actions");
        css.ShouldContain(".topic-publish-qos");
        css.ShouldContain(".topic-publish-retain.active");
        css.ShouldContain(".topic-publish-submit");
        css.ShouldContain(".topic-publish-submit:disabled");
        css.ShouldContain(".topic-publish-assist");
        css.ShouldContain("grid-template-columns: minmax(0, 0.7fr) minmax(0, 1.3fr);");
        css.ShouldContain(".topic-publish-assist-actions,");
        css.ShouldContain(".topic-publish-assist-button");
        css.ShouldContain(".topic-publish-assist-button:disabled");
        css.ShouldContain(".topic-publish-recent");
        css.ShouldContain(".topic-publish-recent-list");
        css.ShouldContain(".topic-publish-recent-row");
        css.ShouldContain(".topic-publish-recent-empty");
        css.ShouldContain(".topic-publish-recent-clear");
        css.ShouldContain(".topic-no-traffic");
        css.ShouldContain(".topic-no-traffic-copy");
        css.ShouldContain(".topic-monitor-list");
        css.ShouldContain(".topic-monitor-row");
        css.ShouldContain(".topic-monitor-dot");
        css.ShouldContain("border-radius: 50%;");
        css.ShouldContain("grid-template-columns: 8px minmax(92px, 0.3fr) minmax(140px, 1fr) auto auto;");
        css.ShouldContain(".topic-monitor-row.live .topic-monitor-connection");
        css.ShouldNotContain(".topic-broker-state");
        css.ShouldNotContain(".topic-monitor-state");
        css.ShouldNotContain("border-radius: 999px;");
        css.ShouldContain(".topic-history-panel");
        css.ShouldContain(".topic-history-header");
        css.ShouldContain(".topic-history-toolbar");
        css.ShouldContain(".topic-history-filter");
        css.ShouldContain(".topic-history-filter ::deep .mud-input-root");
        css.ShouldContain(".topic-history-select");
        css.ShouldContain(".topic-history-select select");
        css.ShouldContain(".topic-history-action");
        css.ShouldContain(".topic-history-action.export");
        css.ShouldContain(".topic-history-body");
        css.ShouldContain("grid-template-columns: minmax(0, 1fr) minmax(320px, 360px);");
        css.ShouldContain(".topic-history-empty");
        css.ShouldContain(".topic-message-table ::deep .mud-table {");
        css.ShouldContain(".topic-message-table ::deep .mud-table-container");
        css.ShouldContain(".topic-message-table ::deep table");
        css.ShouldContain("display: table;");
        css.ShouldContain(".topic-message-table ::deep col.topic-col-time");
        css.ShouldContain(".topic-message-table ::deep col.topic-col-broker");
        css.ShouldContain(".topic-message-table ::deep col.topic-col-qos");
        css.ShouldContain(".topic-message-table ::deep col.topic-col-retain");
        css.ShouldContain(".topic-message-table ::deep col.topic-col-bytes");
        css.ShouldContain(".topic-message-table ::deep th:nth-child(1),");
        css.ShouldContain(".topic-message-table ::deep td:nth-child(1)");
        css.ShouldContain(".topic-message-table ::deep th:nth-child(4),");
        css.ShouldContain(".topic-message-table ::deep td:nth-child(5)");
        css.ShouldContain(".topic-message-table ::deep th:nth-child(6),");
        css.ShouldContain(".topic-message-table ::deep td:nth-child(6)");
        css.ShouldContain("min-width: 100%;");
        css.ShouldContain("table-layout: fixed;");
        css.ShouldContain("width: 100%;");
        css.ShouldContain(".topic-message-table ::deep th,");
        css.ShouldContain(".topic-message-table ::deep .topic-history-row.selected td");
        css.ShouldContain(".topic-history-detail");
        css.ShouldContain(".topic-history-detail-header");
        css.ShouldContain(".topic-history-detail-meta");
        css.ShouldContain(".topic-history-detail-meta div:last-child");
        css.ShouldNotContain(".topic-history-detail-payload-label");
        css.ShouldContain(".topic-history-detail-payload pre");
        css.ShouldContain(".topic-history-detail-empty");
        css.ShouldContain("display: flex;");
        css.ShouldContain("height: 100%;");
        css.ShouldContain("flex: 1 1 auto;");
        css.ShouldContain("min-height: 32px;");
        css.ShouldContain("grid-template-rows: minmax(180px, 1fr) minmax(220px, 0.6fr);");
        css.ShouldContain("grid-template-columns: minmax(0, min(320px, 100%));");
        css.ShouldContain("justify-items: center;");
        css.ShouldContain("text-align: center;");
        markup.ShouldContain("topic-empty-panel\" role=\"status\" aria-live=\"polite\"");
        markup.ShouldContain("topic-publish-recent-empty\" role=\"status\" aria-live=\"polite\"");
        markup.ShouldNotContain("topic-empty-state");
        css.ShouldContain(".topic-empty-panel ::deep .mud-icon-root");
        css.ShouldContain("overflow-wrap: anywhere;");
        css.ShouldContain("flex-basis: clamp(252px, 40vh, 360px);");
        css.ShouldContain(".topic-publish-actions {");
        css.ShouldContain("flex-wrap: wrap;");
        css.ShouldNotContain(".topic-payload-frame");
        css.ShouldNotContain(".topic-empty-state");
        css.ShouldNotContain(".topic-payload-empty");
        css.ShouldNotContain(".topic-last-empty");
        css.ShouldNotContain(".topic-last-meta div:last-child:nth-child(odd)");
        css.ShouldNotContain(".topic-history-detail-meta div:last-child:nth-child(odd)");
        css.ShouldNotContain(".mud-table-pagination");
        css.ShouldNotContain("nth-last-child(-n + 2)");
        css.ShouldNotContain(".topic-message-table ::deep .mud-table-root");
        css.ShouldNotContain("margin-top: auto;");
        css.ShouldNotContain("flex: 0 0 clamp(132px, 30%, 252px);");
        css.ShouldNotContain("flex-basis: clamp(128px, 28%, 224px);");
        css.ShouldNotContain("grid-template-columns: minmax(0, 1fr) minmax(188px, 0.28fr);");
    }

    [Fact]
    public void TopicExplorerSetupDialog_UsesMudBlazorFormControls()
    {
        var root = FindRepositoryRoot();
        var markup = File.ReadAllText(Path.Combine(
            root,
            "src",
            "FluxMq.UI",
            "Components",
            "Workspace",
            "Dialogs",
            "TopicExplorerSetupDialog.razor"));
        var css = File.ReadAllText(Path.Combine(
            root,
            "src",
            "FluxMq.UI",
            "Components",
            "Workspace",
            "Dialogs",
            "TopicExplorerSetupDialog.razor.css"));

        markup.ShouldContain("MudTextField");
        markup.ShouldContain("MudNumericField");
        markup.ShouldContain("MudSelect");
        markup.ShouldContain("MudCheckBox");
        markup.ShouldContain("topic-explorer-setup-actions");
        markup.ShouldContain("Label=\"Broker\"");
        markup.ShouldContain("[Parameter] public string Title { get; set; } = \"Topic monitors\";");
        markup.ShouldContain("[Parameter] public string SaveLabel { get; set; } = \"Save monitors\";");
        markup.ShouldContain("[Parameter] public bool PreserveCandidateExplorerNames { get; set; }");
        markup.ShouldContain("@SaveLabel");
        markup.ShouldContain("PreferredExplorerName(candidate)");
        markup.ShouldContain("<MudIcon Icon=\"@Icons.Material.Filled.AccountTree\" Size=\"Size.Small\" aria-hidden=\"true\" />");
        markup.ShouldContain("CA certificate path");
        markup.ShouldContain("Client certificate path");
        markup.ShouldContain("Client certificate password");
        markup.ShouldContain("FilePicker.Default.PickAsync");
        markup.ShouldContain("Icons.Material.Filled.FolderOpen");
        markup.ShouldContain("Adornment=\"Adornment.End\"");
        markup.ShouldContain("OnAdornmentClick");
        markup.ShouldContain("AdornmentAriaLabel=\"Select CA certificate\"");
        markup.ShouldContain("AdornmentAriaLabel=\"Select client certificate\"");
        markup.ShouldContain("Class=\"d-flex align-center\" Style=\"min-height: 44px;\"");
        markup.ShouldContain("Variant=\"Variant.Outlined\"");
        markup.ShouldContain("Margin=\"Margin.Dense\"");
        markup.ShouldContain("sm=\"6\"");
        markup.ShouldNotContain("sm=\"8\"");
        markup.ShouldNotContain("sm=\"4\"");
        markup.ShouldNotContain("Label=\"Explorer key\"");
        markup.ShouldNotContain("Label=\"Display name\"");
        markup.ShouldNotContain("Label=\"Host\"");
        markup.ShouldNotContain("Label=\"Port\"");
        markup.ShouldNotContain("Label=\"Subscriptions\"");
        markup.ShouldNotContain("Typo=\"Typo.subtitle2\">@draft.DisplayName");
        markup.ShouldNotContain("Typo=\"Typo.caption\" Color=\"Color.Secondary\">@draft.Endpoint");
        markup.ShouldNotContain("<MudChip");
        markup.ShouldNotContain("<MudIcon Icon=\"@Icons.Material.Filled.AccountTree\" Size=\"Size.Small\" />");
        markup.ShouldNotContain("<MudIconButton Icon=\"@Icons.Material.Filled.FolderOpen\"");
        markup.ShouldNotContain("<input");
        markup.ShouldNotContain("mud-input-root");

        css.ShouldContain(".topic-explorer-setup-actions");
        css.ShouldContain("justify-content: flex-end;");
        css.ShouldContain("width: 100%;");
        css.ShouldContain("@media (max-width: 480px)");

        var usernameIndex = markup.IndexOf("Label=\"Username\"", StringComparison.Ordinal);
        var passwordIndex = markup.IndexOf("Label=\"Password\"", StringComparison.Ordinal);
        var useTlsIndex = markup.IndexOf("Label=\"Use TLS\"", StringComparison.Ordinal);
        usernameIndex.ShouldBeLessThan(passwordIndex);
        passwordIndex.ShouldBeLessThan(useTlsIndex);
    }

    [Fact]
    public void TopicExplorerPanel_StopsWhenSetupIsCanceled()
    {
        var root = FindRepositoryRoot();
        var markup = File.ReadAllText(Path.Combine(
            root,
            "src",
            "FluxMq.UI",
            "Components",
            "Workspace",
            "TopicExplorerPanel.razor"));

        markup.ShouldContain("if (!await EnsureTopicExplorerSetupAsync())");
        markup.ShouldContain("private async Task<bool> EnsureTopicExplorerSetupAsync()");
        markup.ShouldContain("ReferenceEquals(active, _setupPromptedProject)");
    }

    [Fact]
    public void TopicTreeNode_UsesCompactBranchLineChrome()
    {
        var root = FindRepositoryRoot();
        var markup = File.ReadAllText(Path.Combine(
            root,
            "src",
            "FluxMq.UI",
            "Components",
            "TopicTree",
            "TopicTreeNode.razor"));
        var css = File.ReadAllText(Path.Combine(
            root,
            "src",
            "FluxMq.UI",
            "Components",
            "TopicTree",
            "TopicTreeNode.razor.css"));
        var viewMarkup = File.ReadAllText(Path.Combine(
            root,
            "src",
            "FluxMq.UI",
            "Components",
            "TopicTree",
            "TopicTreeView.razor"));
        var viewCss = File.ReadAllText(Path.Combine(
            root,
            "src",
            "FluxMq.UI",
            "Components",
            "TopicTree",
            "TopicTreeView.razor.css"));

        markup.ShouldContain("topic-node-branch");
        markup.ShouldContain("class=\"topic-node-branch\" aria-hidden=\"true\"");
        markup.ShouldContain("style=\"@($\"--topic-depth:{Node.Depth};\")\"");
        markup.ShouldContain("TopicSelected.InvokeAsync(Node.FullPath)");
        markup.ShouldContain("Node.Children.Values.OrderBy(n => n.Name)");
        markup.ShouldContain("role=\"treeitem\"");
        markup.ShouldContain("tabindex=\"0\"");
        markup.ShouldContain("aria-label=\"@Node.FullPath\"");
        markup.ShouldContain("aria-level=\"@TopicNodeLevel()\"");
        markup.ShouldContain("aria-selected=\"@TopicNodeSelected()\"");
        markup.ShouldContain("aria-expanded=\"@TopicNodeExpanded()\"");
        markup.ShouldContain("aria-keyshortcuts=\"Enter Space\"");
        markup.ShouldContain("@onkeydown=\"SelectFromKeyboardAsync\"");
        markup.ShouldContain("id=\"@TopicNodeChildrenId()\"");
        markup.ShouldContain("class=\"topic-node-children\"");
        markup.ShouldContain("role=\"group\"");
        markup.ShouldContain("aria-label=\"@TopicNodeChildrenLabel()\"");
        markup.ShouldContain("<button type=\"button\"");
        markup.ShouldContain("class=\"topic-node-chevron\"");
        markup.ShouldContain("aria-label=\"@TopicNodeChevronLabel()\"");
        markup.ShouldContain("aria-expanded=\"@(_expanded ? \"true\" : \"false\")\"");
        markup.ShouldContain("aria-controls=\"@TopicNodeChildrenId()\"");
        markup.ShouldContain("@onclick=\"Toggle\"");
        markup.ShouldContain("@onclick:stopPropagation");
        markup.ShouldContain("topic-node-chevron-static");
        markup.ShouldContain("aria-hidden=\"true\"");
        markup.ShouldContain("private string TopicNodeChevronLabel()");
        markup.ShouldContain("private string TopicNodeChildrenId()");
        markup.ShouldContain("private string TopicNodeChildrenLabel()");
        markup.ShouldContain("private static string ToElementIdPart(string value)");
        markup.ShouldContain("private int TopicNodeLevel()");
        markup.ShouldContain("private string TopicNodeSelected()");
        markup.ShouldContain("private string? TopicNodeExpanded()");
        markup.ShouldContain("private Task SelectFromKeyboardAsync(KeyboardEventArgs args)");
        markup.ShouldNotContain("IgnoreChevronClick");
        markup.ShouldNotContain("@onclick=\"IgnoreChevronClick\"");
        markup.ShouldNotContain("<span class=\"topic-node-chevron\" @onclick=\"Toggle\"");
        System.Text.RegularExpressions.Regex.Matches(
                markup,
                @"<MudIcon\b(?:(?!/>).)*?/>",
                System.Text.RegularExpressions.RegexOptions.Singleline)
            .Cast<System.Text.RegularExpressions.Match>()
            .Select(static match => match.Value)
            .Where(static icon => !icon.Contains("aria-hidden=\"true\"", StringComparison.Ordinal) &&
                !icon.Contains("aria-label=", StringComparison.Ordinal) &&
                !icon.Contains("AriaLabel=", StringComparison.Ordinal))
            .ToArray()
            .ShouldBeEmpty();

        css.ShouldContain(".topic-node-branch");
        css.ShouldContain("border-radius: 1px;");
        css.ShouldContain("width: 1px;");
        css.ShouldContain("left: calc(7px + (var(--topic-depth) * 14px));");
        css.ShouldContain("appearance: none;");
        css.ShouldContain("background: transparent;");
        css.ShouldContain("border: 0;");
        css.ShouldContain(".topic-node-row:focus-visible");
        css.ShouldContain(".topic-node-chevron:focus-visible");
        css.ShouldContain(".topic-node-chevron-static");
        css.ShouldNotContain("border-radius: 999px;");

        viewMarkup.ShouldContain("class=\"topic-tree-nodes\" role=\"tree\" aria-label=\"@TopicTreeLabel\"");
        viewMarkup.ShouldContain("private string TopicTreeLabel");
        viewMarkup.ShouldContain("Topic tree with {topicText} and {messageText}");
        viewMarkup.ShouldContain("Filtered topic tree for {Filter.Trim()}, {topicText} and {messageText}");
        viewMarkup.ShouldContain("private static int CountTopics(IEnumerable<TopicNode> nodes)");
        viewMarkup.ShouldNotContain("aria-label=\"Topics\"");
        viewMarkup.ShouldContain("role=\"treeitem\"");
        viewMarkup.ShouldContain("aria-label=\"@node.FullPath\"");
        viewMarkup.ShouldContain("aria-level=\"@TopicNodeLevel(node)\"");
        viewMarkup.ShouldContain("aria-selected=\"@TopicNodeSelected(node)\"");
        viewMarkup.ShouldContain("aria-keyshortcuts=\"Enter Space\"");
        viewMarkup.ShouldContain("class=\"topic-node-flat-icon\" aria-hidden=\"true\"");
        viewMarkup.ShouldContain("<MudIcon Icon=\"@Icons.Material.Filled.SearchOff\" Size=\"Size.Small\" aria-hidden=\"true\" />");
        viewMarkup.ShouldContain("<MudIcon Icon=\"@Icons.Material.Filled.Topic\" Size=\"Size.Small\" aria-hidden=\"true\" />");
        System.Text.RegularExpressions.Regex.Matches(
                viewMarkup,
                @"<MudIcon\b(?:(?!/>).)*?/>",
                System.Text.RegularExpressions.RegexOptions.Singleline)
            .Cast<System.Text.RegularExpressions.Match>()
            .Select(static match => match.Value)
            .Where(static icon => !icon.Contains("aria-hidden=\"true\"", StringComparison.Ordinal) &&
                !icon.Contains("aria-label=", StringComparison.Ordinal) &&
                !icon.Contains("AriaLabel=", StringComparison.Ordinal))
            .ToArray()
            .ShouldBeEmpty();
        viewMarkup.ShouldContain("@onkeydown=\"@((KeyboardEventArgs args) => SelectTopicFromKeyboardAsync(args, node.FullPath))\"");
        viewMarkup.ShouldContain("private static int TopicNodeLevel(TopicNode node)");
        viewMarkup.ShouldContain("private string TopicNodeSelected(TopicNode node)");
        viewMarkup.ShouldContain("private Task SelectTopicFromKeyboardAsync(KeyboardEventArgs args, string? topic)");
        viewCss.ShouldContain(".topic-node-flat:focus-visible");
    }

    [Fact]
    public void StartupSplash_UsesCompactLoadingChrome()
    {
        var root = FindRepositoryRoot();
        var markup = File.ReadAllText(Path.Combine(
            root,
            "src",
            "FluxMq.UI",
            "Components",
            "StartupSplash.razor"));
        var css = File.ReadAllText(Path.Combine(
            root,
            "src",
            "FluxMq.UI",
            "Components",
            "StartupSplash.razor.css"));

        markup.ShouldContain("startup-splash");
        markup.ShouldContain("startup-splash-content");
        markup.ShouldContain("startup-splash-rail");
        markup.ShouldContain("brand/fluxmq-loader.svg");
        markup.ShouldContain("brand/fluxmq-wordmark.svg");
        markup.ShouldContain("MQTT flow studio");

        css.ShouldContain(".startup-splash-rail");
        css.ShouldContain("height: 3px;");
        css.ShouldContain("border-radius: 3px;");
        css.ShouldContain("animation: startup-splash-progress 1.8s ease-in-out infinite;");
        css.ShouldContain("@media (prefers-reduced-motion: reduce)");
        css.ShouldNotContain("border-radius: 999px;");
    }

    [Fact]
    public void TestStudio_UsesFlatCompactWorkspaceChrome()
    {
        var root = FindRepositoryRoot();
        var markup = File.ReadAllText(Path.Combine(
            root,
            "src",
            "FluxMq.UI",
            "Components",
            "Workspace",
            "TestStudio.razor"));
        var css = File.ReadAllText(Path.Combine(
            root,
            "src",
            "FluxMq.UI",
            "Components",
            "Workspace",
            "TestStudio.razor.css"));

        markup.ShouldContain("aria-label=\"@TestStudioLabel\"");
        markup.ShouldContain("private string TestStudioLabel");
        markup.ShouldContain("{scenario.Name} test studio workspace");
        markup.ShouldContain("Test studio workspace with no active scenario");
        markup.ShouldNotContain("aria-label=\"Test studio workspace\"");
        markup.ShouldContain("aria-label=\"@TestStudioToolbarLabel\"");
        markup.ShouldContain("Test studio toolbar for {scenario.Name}");
        markup.ShouldContain("Test studio toolbar with no active scenario");
        markup.ShouldContain("test-studio-title-icon");
        markup.ShouldContain("Icon=\"@Icons.Material.Filled.Science\" Size=\"Size.Small\" aria-hidden=\"true\"");
        markup.ShouldContain("aria-label=\"@TestStudioMetaLabel\"");
        markup.ShouldContain("Test studio counts for {scenario.Name}");
        markup.ShouldContain("Test studio counts with no active scenario");
        markup.ShouldContain("@TestCountLabel");
        markup.ShouldContain("@ActiveScenarioLabel");
        markup.ShouldContain("@RunCountLabel");
        markup.ShouldContain("test-studio-mode-switch");
        markup.ShouldContain("role=\"tablist\"");
        markup.ShouldContain("aria-label=\"@TestStudioModeLabel\"");
        markup.ShouldContain("Test studio mode for {scenario.Name}");
        markup.ShouldContain("Test studio mode with no active scenario");
        markup.ShouldContain("id=\"@DesignerTabId\"");
        markup.ShouldContain("aria-controls=\"@DesignerPanelId\"");
        System.Text.RegularExpressions.Regex.Matches(markup, "aria-keyshortcuts=\"Enter Space ArrowLeft ArrowRight Home End\"").Count.ShouldBe(2);
        markup.ShouldContain("@onkeydown=\"@OnDesignerTabKeyDown\"");
        markup.ShouldContain("ModeButtonClass(TestStudioMode.Designer)");
        markup.ShouldContain("id=\"@RunnerTabId\"");
        markup.ShouldContain("aria-controls=\"@RunnerPanelId\"");
        markup.ShouldContain("@onkeydown=\"@OnRunnerTabKeyDown\"");
        markup.ShouldContain("ModeButtonClass(TestStudioMode.Runner)");
        markup.ShouldContain("SelectModeFromKeyboard");
        markup.ShouldContain("PreviousMode");
        markup.ShouldContain("NextMode");
        markup.ShouldContain("role=\"tabpanel\"");
        markup.ShouldContain("aria-labelledby=\"@DesignerTabId\"");
        markup.ShouldContain("aria-labelledby=\"@RunnerTabId\"");
        markup.ShouldContain("private const string DesignerPanelId");
        markup.ShouldContain("private const string RunnerPanelId");
        markup.ShouldContain("Icon=\"@Icons.Material.Filled.EditNote\" Size=\"Size.Small\" aria-hidden=\"true\"");
        markup.ShouldContain("Icon=\"@Icons.Material.Filled.PlayCircle\" Size=\"Size.Small\" aria-hidden=\"true\"");
        markup.ShouldNotContain("Icon=\"@Icons.Material.Filled.Science\" Size=\"Size.Small\" />");
        markup.ShouldNotContain("Icon=\"@Icons.Material.Filled.EditNote\" Size=\"Size.Small\" />");
        markup.ShouldNotContain("Icon=\"@Icons.Material.Filled.PlayCircle\" Size=\"Size.Small\" />");
        markup.ShouldContain("TestScenarioDesigner Project=\"@Project\"");
        markup.ShouldContain("TestRunnerConsole Project=\"@Project\"");
        markup.ShouldNotContain("MudToggleGroup");
        markup.ShouldNotContain("MudToggleItem");
        markup.ShouldNotContain("aria-label=\"Test studio summary\"");
        markup.ShouldNotContain("aria-label=\"Test studio toolbar\"");
        markup.ShouldNotContain("aria-label=\"Test studio mode\"");

        css.ShouldContain(".test-studio-title-icon");
        css.ShouldContain(".test-studio-meta span");
        css.ShouldContain(".test-studio-mode-switch");
        css.ShouldContain(".test-studio-mode-button");
        css.ShouldContain("grid-template-columns: minmax(0, 1fr) auto;");
        css.ShouldContain("flex-wrap: nowrap;");
        css.ShouldContain("overflow: hidden;");
        css.ShouldContain("min-height: 42px;");
        css.ShouldContain("height: 24px;");
        css.ShouldContain("flex: 0 0 252px;");
        css.ShouldContain(".test-studio-mode-button.active");
        css.ShouldContain("@media (max-width: 760px)");
        css.ShouldContain("width: 100%;");
        css.ShouldNotContain("flex-basis: 100%;");
        css.ShouldNotContain(".mud-toggle-group");
    }

    [Fact]
    public void TestRunnerConsole_UsesFlatCompactRunnerChrome()
    {
        var root = FindRepositoryRoot();
        var markup = File.ReadAllText(Path.Combine(
            root,
            "src",
            "FluxMq.UI",
            "Components",
            "Workspace",
            "TestRunnerConsole.razor"));
        var css = File.ReadAllText(Path.Combine(
            root,
            "src",
            "FluxMq.UI",
            "Components",
            "Workspace",
            "TestRunnerConsole.razor.css"));

        markup.ShouldContain("aria-label=\"@TestRunnerConsoleLabel\"");
        markup.ShouldContain("private string TestRunnerConsoleLabel");
        markup.ShouldContain("{scenario.Name} test runner console");
        markup.ShouldContain("Test runner console with no active scenario");
        markup.ShouldNotContain("aria-label=\"Test runner console\"");
        markup.ShouldContain("test-runner-title-icon");
        markup.ShouldContain("test-runner-meta-strip");
        markup.Split('\n')
            .Where(static line => line.Contains("<MudIcon ", StringComparison.Ordinal) &&
                !line.Contains("aria-hidden=\"true\"", StringComparison.Ordinal))
            .ToArray()
            .ShouldBeEmpty();
        markup.ShouldContain("@NoTestEmptyTitle");
        markup.ShouldContain("@NoTestSelectionHint");
        markup.ShouldContain("test-runner-empty\" role=\"status\" aria-live=\"polite\" aria-label=\"@TestRunnerSetupStateLabel\"");
        markup.ShouldContain("private string TestRunnerSetupStateLabel");
        markup.ShouldContain("Test runner setup state with no tests");
        markup.ShouldContain("Test runner setup state with {FormatCount(Project.TestNames.Count, \"test\")}");
        markup.ShouldNotContain("aria-label=\"Test runner scenario setup state\"");
        markup.ShouldContain("test-runner-empty-cues");
        markup.ShouldContain("aria-label=\"@TestRunnerSetupLabel\"");
        markup.ShouldContain("private string TestRunnerSetupLabel");
        markup.ShouldContain("Test runner setup with no tests");
        markup.ShouldContain("Test runner setup with {FormatCount(Project.TestNames.Count, \"test\")}");
        markup.ShouldNotContain("aria-label=\"Test runner setup\"");
        markup.ShouldContain("aria-label=\"@ScenarioRunnerMetaLabel\"");
        markup.ShouldContain("Scenario runner facts for {scenario.Name}");
        markup.ShouldContain("Scenario runner facts with no active scenario");
        markup.ShouldContain("@ScenarioStepLabel");
        markup.ShouldContain("@ScenarioPhaseLabel");
        markup.ShouldContain("@RunHistorySummaryLabel");
        markup.ShouldContain("aria-label=\"@RunHistoryPanelLabel\"");
        markup.ShouldContain("private string RunHistoryPanelLabel");
        markup.ShouldContain("Recent runs for {scenario.Name}");
        markup.ShouldContain("Recent runs with no active scenario");
        markup.ShouldContain("aria-label=\"@ShowLatestRunLabel\"");
        markup.ShouldContain("private string ShowLatestRunLabel");
        markup.ShouldContain("Show latest run for {scenario.Name}");
        markup.ShouldContain("Show latest scenario run");
        markup.ShouldNotContain("aria-label=\"Recent scenario runs\"");
        markup.ShouldNotContain("aria-label=\"Show latest scenario run\"");
        markup.ShouldContain("RunMarkerClass(result.Status)");
        markup.ShouldContain("aria-label=\"@($\"Run result {result.Status}\")\"");
        markup.ShouldContain("ActiveRunMarkerClass");
        markup.ShouldContain("ActiveRunMarkerIcon");
        markup.ShouldContain("ActiveRunMarkerText");
        markup.ShouldContain("ActiveRunMarkerColor");
        markup.ShouldContain("test-run-history-panel");
        markup.ShouldContain("No run history");
        markup.ShouldContain("test-run-history-row");
        markup.ShouldContain("RunHistoryItemClass(historyRun)");
        markup.ShouldContain("RunHistoryAriaLabel(historyRun)");
        markup.ShouldContain("RunHistoryMarkerClass(historyRun)");
        markup.ShouldContain("RunHistoryIssueLabel(historyRun)");
        markup.ShouldContain("test-runner-report-actions");
        markup.ShouldContain("aria-label=\"@ReportActionsLabel\"");
        markup.ShouldContain("private string ReportActionsLabel");
        markup.ShouldContain("Scenario report actions for {ActiveRunScopeText} run");
        markup.ShouldContain("Class=\"test-runner-icon-action\"");
        markup.ShouldContain("aria-label=\"@ViewReportTooltip\"");
        markup.ShouldContain("aria-label=\"@CopyReportTooltip\"");
        markup.ShouldContain("aria-label=\"@SaveReportTooltip\"");
        markup.ShouldContain("Class=\"test-runner-run-action\"");
        markup.ShouldContain("test-runner-workspace");
        markup.ShouldContain("test-runner-preflight-strip");
        markup.ShouldContain("aria-label=\"@ScenarioPreflightLabel\"");
        markup.ShouldContain("private string ScenarioPreflightLabel");
        markup.ShouldContain("Preflight checks for {scenario.Name}");
        markup.ShouldContain("Preflight checks with no active scenario");
        markup.ShouldNotContain("aria-label=\"Scenario preflight\"");
        markup.ShouldContain("PreflightItemClass");
        markup.ShouldContain("test-runner-result-strip");
        markup.ShouldContain("@FirstRunStripClass");
        markup.ShouldContain("@FirstRunAriaLabel");
        markup.ShouldContain("@FirstRunIcon");
        markup.ShouldContain("@FirstRunSummaryLabel");
        markup.ShouldContain("@FirstRunTitle");
        markup.ShouldContain("Run scenario to begin");
        markup.ShouldContain("@FirstRunDescription");
        markup.ShouldContain("@FirstRunEventModeLabel");
        markup.ShouldContain("test-runner-first-run-cues");
        markup.ShouldContain("aria-label=\"@FirstRunFactsLabel\"");
        markup.ShouldContain("private string FirstRunFactsLabel");
        markup.ShouldContain("First run facts for {scenario.Name}");
        markup.ShouldContain("First run facts with no active scenario");
        markup.ShouldNotContain("aria-label=\"First run facts\"");
        markup.ShouldContain("RunSummaryClass(latest)");
        markup.ShouldContain("RunSummaryAriaLabel(latest)");
        markup.ShouldContain("test-runner-result-scope");
        markup.ShouldContain("RunResultScopeLabel");
        markup.ShouldContain("FormatRunIdText");
        markup.ShouldContain("test-runner-main");
        markup.ShouldContain("test-runner-section timeline");
        markup.ShouldContain("aria-label=\"@ScenarioTimelineLabel\"");
        markup.ShouldContain("private string ScenarioTimelineLabel");
        markup.ShouldContain("Timeline for {scenario.Name}");
        markup.ShouldContain("Timeline with no active scenario");
        markup.ShouldNotContain("aria-label=\"Scenario timeline\"");
        markup.ShouldContain("test-runner-section activity");
        markup.ShouldContain("aria-label=\"@ScenarioActivityLabel\"");
        markup.ShouldContain("private string ScenarioActivityLabel");
        markup.ShouldContain("Activity for {scenario.Name}: {ActivitySummaryLabel}");
        markup.ShouldContain("Activity with no active scenario");
        markup.ShouldNotContain("aria-label=\"Scenario activity\"");
        markup.ShouldContain("test-runner-activity-grid");
        markup.ShouldContain("test-runner-stream-block");
        markup.ShouldContain("TimelineStepLabel(step, stepResult)");
        markup.ShouldContain("TimelineStepMeta(stepResult)");
        markup.ShouldContain("test-runner-step-marker");
        markup.ShouldContain("test-runner-step-copy");
        markup.ShouldContain("StepStatusIcon(stepResult)");
        markup.ShouldContain("stepResult?.Status.ToString() ?? \"Idle\"");
        markup.ShouldContain("? \"Idle\"");
        markup.ShouldContain("RuntimeEventRowClass(flowEvent)");
        markup.ShouldContain("RuntimeEventLabel(flowEvent)");
        markup.ShouldContain("RuntimeEventIcon(flowEvent)");
        markup.ShouldContain("RuntimeEventTitle(flowEvent)");
        markup.ShouldContain("RuntimeEventKind(flowEvent)");
        markup.ShouldContain("RunnerLogRowClass(log)");
        markup.ShouldContain("RunnerLogLabel(log)");
        markup.ShouldContain("RunnerLogIcon(log)");
        markup.ShouldContain("RunnerLogTitle(log)");
        markup.ShouldContain("RunnerLogSeverityLabel(log)");
        markup.ShouldContain("test-runner-stream-time");
        markup.ShouldContain("test-runner-stream-icon");
        markup.ShouldContain("test-runner-stream-main");
        markup.ShouldContain("test-runner-stream-tag");
        markup.ShouldContain("test-runner-stream-empty");
        markup.ShouldContain("@RuntimeEventCountLabel");
        markup.ShouldContain("@ActivitySummaryLabel");
        markup.ShouldContain("@RunnerLogCountLabel");
        markup.ShouldContain("var connectionsAvailable = await Live.EnsureConnectionsAsync(Project.GetConnectionResources());");
        markup.ShouldNotContain("MudChip");
        markup.ShouldNotContain("ActiveRunChip");
        markup.ShouldNotContain("FormatRunIdChip");
        markup.ShouldNotContain("RunStatusPillClass(result.Status)");
        markup.ShouldNotContain("RunStateClass(result.Status)");
        markup.ShouldNotContain("RunStatusClass(result.Status)");
        markup.ShouldNotContain("aria-label=\"@($\"Run state {result.Status}\")\"");
        markup.ShouldNotContain("aria-label=\"@($\"Run status {result.Status}\")\"");
        markup.ShouldNotContain("RunHistoryStatusClass");
        markup.ShouldNotContain("RunHistoryStateClass");
        markup.ShouldNotContain("ActiveRunStateClass");
        markup.ShouldNotContain("ActiveRunStateIcon");
        markup.ShouldNotContain("ActiveRunStateText");
        markup.ShouldNotContain("ActiveRunStateColor");
        markup.ShouldNotContain("DiagnosisStateLabel");
        markup.ShouldNotContain("PreflightStateLabel");
        markup.ShouldNotContain("FirstRunStateLabel");
        markup.ShouldNotContain("test-runner-step-state");
        markup.ShouldNotContain("ActiveRunPillClass");
        markup.ShouldNotContain("test-runner-status-pill");
        markup.ShouldNotContain("test-runner-status-strip");
        markup.ShouldNotContain("test-runner-status-item");
        markup.ShouldNotContain("Runner ready");
        markup.ShouldNotContain("var ready = await Live.EnsureConnectionsAsync");
        markup.ShouldNotContain("if (!ready)");
        markup.ShouldNotContain("First run readiness");
        markup.ShouldNotContain("Ready for first run");
        markup.ShouldNotContain("?? \"Ready\"");
        markup.ShouldNotContain("? \"Ready\"");
        markup.ShouldNotContain("test-runner-result-strip empty ready");

        css.ShouldContain(".test-runner-title-icon");
        css.ShouldContain(".test-runner-empty-cues");
        css.ShouldContain(".test-runner-empty-cues span");
        css.ShouldContain(".test-runner-meta-strip span,");
        css.ShouldContain(".test-runner-run-marker");
        css.ShouldContain(".test-run-history-panel");
        css.ShouldContain(".test-run-history-empty strong");
        css.ShouldContain(".test-run-history-empty small");
        css.ShouldContain(".test-run-history-row");
        css.ShouldContain(".test-run-history-marker");
        css.ShouldContain("::deep .test-run-history-item.selected .test-run-history-row");
        css.ShouldContain(".test-runner-report-actions");
        css.ShouldContain(".test-runner-report-actions ::deep .mud-icon-button");
        css.ShouldContain("grid-template-columns: minmax(0, 1fr) auto;");
        css.ShouldContain("min-height: 42px;");
        css.ShouldContain("flex-wrap: nowrap;");
        css.ShouldContain(".test-runner-workspace");
        css.ShouldContain("grid-template-rows: auto auto minmax(0, 1fr);");
        css.ShouldContain(".test-runner-preflight-strip");
        css.ShouldContain("grid-template-columns: repeat(3, minmax(0, 1fr));");
        css.ShouldContain(".test-runner-result-strip");
        css.ShouldContain(".test-runner-result-strip.empty.runnable");
        css.ShouldContain(".test-runner-result-strip.empty.warning");
        css.ShouldContain(".test-runner-result-strip.history");
        css.ShouldContain(".test-runner-result-scope");
        css.ShouldContain(".test-runner-result-scope.first-run");
        css.ShouldContain(".test-runner-first-run-cues");
        css.ShouldContain(".test-runner-first-run-cues span");
        css.ShouldContain(".test-runner-main");
        css.ShouldContain("grid-template-columns: minmax(320px, 0.9fr) minmax(0, 1.35fr);");
        css.ShouldContain(".test-runner-step-marker");
        css.ShouldContain(".test-runner-step-copy");
        css.ShouldContain(".test-runner-step-copy small");
        css.ShouldContain(".test-runner-timeline-step.passed strong");
        css.ShouldContain(".test-runner-activity-grid");
        css.ShouldContain(".test-runner-stream-row::before");
        css.ShouldContain(".test-runner-stream-row.event.info::before");
        css.ShouldContain(".test-runner-stream-row.event.success::before");
        css.ShouldContain(".test-runner-stream-time");
        css.ShouldContain(".test-runner-stream-icon");
        css.ShouldContain(".test-runner-stream-main");
        css.ShouldContain(".test-runner-stream-tag");
        css.ShouldContain(".test-runner-stream-empty");
        css.ShouldContain("grid-template-columns: 52px 22px minmax(0, 1fr) auto;");
        css.ShouldContain("width: 2px;");
        css.ShouldContain("height: 26px;");
        css.ShouldContain("height: 22px;");
        css.ShouldContain(".test-runner-section-title small");
        css.ShouldContain("@media (max-width: 920px)");
        css.ShouldContain("grid-template-columns: minmax(0, 1fr);");
        css.ShouldNotContain(".test-runner-panel-title");
        css.ShouldNotContain(".test-runner-status-pill");
        css.ShouldNotContain(".test-runner-status-state");
        css.ShouldNotContain(".test-runner-status-strip");
        css.ShouldNotContain(".test-runner-status-item");
        css.ShouldNotContain(".test-run-history-status");
        css.ShouldNotContain(".test-runner-run-state");
        css.ShouldNotContain(".test-run-history-state");
        css.ShouldNotContain(".test-runner-step-state");
        css.ShouldNotContain(".test-runner-result-strip.empty.ready");
        css.ShouldNotContain("border-radius: 999px;");
        markup.ShouldNotContain("aria-label=\"Test runner setup state\"");
        markup.ShouldNotContain("aria-label=\"Scenario runner summary\"");
        markup.ShouldNotContain("aria-label=\"Scenario report actions\"");
    }

    [Fact]
    public void TestScenarioDesigner_UsesFlatCompactScenarioChrome()
    {
        var root = FindRepositoryRoot();
        var markup = File.ReadAllText(Path.Combine(
            root,
            "src",
            "FluxMq.UI",
            "Components",
            "Workspace",
            "TestScenarioDesigner.razor"));
        var css = File.ReadAllText(Path.Combine(
            root,
            "src",
            "FluxMq.UI",
            "Components",
            "Workspace",
            "TestScenarioDesigner.razor.css"));

        markup.ShouldContain("aria-label=\"@TestScenarioDesignerLabel\"");
        markup.ShouldContain("private string TestScenarioDesignerLabel");
        markup.ShouldContain("{scenario.Name} test scenario designer");
        markup.ShouldContain("Test scenario designer with no active scenario");
        markup.ShouldNotContain("aria-label=\"Test scenario designer\"");
        markup.ShouldContain("test-scenario-heading-icon");
        markup.ShouldContain("test-scenario-title-copy");
        markup.ShouldContain("test-scenario-meta-strip");
        markup.Split('\n')
            .Where(static line => line.Contains("<MudIcon ", StringComparison.Ordinal) &&
                !line.Contains("aria-hidden=\"true\"", StringComparison.Ordinal))
            .ToArray()
            .ShouldBeEmpty();
        markup.ShouldContain("@NoTestEmptyTitle");
        markup.ShouldContain("@NoTestSelectionHint");
        markup.ShouldContain("test-scenario-empty\" role=\"status\" aria-live=\"polite\" aria-label=\"@TestScenarioSetupStateLabel\"");
        markup.ShouldContain("private string TestScenarioSetupStateLabel");
        markup.ShouldContain("Test scenario designer setup with no tests");
        markup.ShouldContain("Test scenario designer setup with {FormatTestCount(Project.TestNames.Count)}");
        markup.ShouldContain("test-scenario-empty-cues");
        markup.ShouldContain("aria-label=\"@ScenarioDesignerSetupLabel\"");
        markup.ShouldContain("private string ScenarioDesignerSetupLabel");
        markup.ShouldContain("Scenario designer setup with no tests");
        markup.ShouldContain("Scenario designer setup with {FormatTestCount(Project.TestNames.Count)}");
        markup.ShouldContain("@NoStepsEmptyTitle");
        markup.ShouldContain("@NoStepsEmptyText");
        markup.ShouldContain("test-scenario-empty\" role=\"status\" aria-live=\"polite\" aria-label=\"@EmptyScenarioLabel\"");
        markup.ShouldContain("private string EmptyScenarioLabel");
        markup.ShouldContain("{scenario.Name} has no steps");
        markup.ShouldContain("Empty scenario with no active scenario");
        markup.ShouldContain("aria-label=\"@ScenarioStarterCuesLabel\"");
        markup.ShouldContain("private string ScenarioStarterCuesLabel");
        markup.ShouldContain("Starter cues for {scenario.Name}");
        markup.ShouldContain("Scenario starter cues with no active scenario");
        markup.ShouldNotContain("aria-label=\"Test scenario designer setup state\"");
        markup.ShouldNotContain("aria-label=\"Scenario designer setup\"");
        markup.ShouldNotContain("aria-label=\"Empty scenario\"");
        markup.ShouldNotContain("aria-label=\"Scenario starter cues\"");
        markup.ShouldContain("@ScenarioStepTypeCountText");
        markup.ShouldContain("@PhaseCountText");
        markup.ShouldContain("@RunModeText");
        markup.ShouldContain("@RecentRunCountText");
        markup.ShouldContain("aria-label=\"@ScenarioDesignerMetaLabel\"");
        markup.ShouldContain("Scenario designer facts for {scenario.Name}");
        markup.ShouldContain("Scenario designer facts with no active scenario");
        markup.ShouldContain("test-scenario-workspace");
        markup.ShouldContain("test-scenario-builder-strip");
        markup.ShouldContain("aria-label=\"@ScenarioBuilderFactsLabel\"");
        markup.ShouldContain("Scenario builder facts for {scenario.Name}");
        markup.ShouldContain("Scenario builder facts with no active scenario");
        markup.ShouldContain("BuilderMetricClass");
        markup.ShouldContain("@ActivePhaseCountText");
        markup.ShouldContain("@RunnerSummaryText");
        markup.ShouldContain("? \"Running\" : \"Idle\"");
        markup.ShouldContain("RunContextClass(result)");
        markup.ShouldContain("RunContextAriaLabel(result)");
        markup.ShouldContain("ActiveRunMarkerIcon");
        markup.ShouldContain("ActiveRunMarkerText");
        markup.ShouldContain("ActiveRunMarkerColor");
        markup.ShouldContain("test-run-context-marker");
        markup.ShouldContain("test-run-context-meta");
        markup.ShouldContain("test-run-context-reset");
        markup.ShouldContain("ReportActionsClass");
        markup.ShouldContain("ReportActionsLabel");
        markup.ShouldContain("test-run-history-panel");
        markup.ShouldContain("aria-label=\"@RunHistoryPanelLabel\"");
        markup.ShouldContain("private string RunHistoryPanelLabel");
        markup.ShouldContain("Recent runs for {scenario.Name}");
        markup.ShouldContain("Recent runs with no active scenario");
        markup.ShouldContain("aria-label=\"@ShowLatestRunLabel\"");
        markup.ShouldContain("title=\"@ShowLatestRunLabel\"");
        markup.ShouldContain("private string ShowLatestRunLabel");
        markup.ShouldContain("Show latest run for {scenario.Name}");
        markup.ShouldContain("Show latest scenario run");
        markup.ShouldNotContain("aria-label=\"Recent scenario runs\"");
        markup.ShouldNotContain("aria-label=\"Show latest run\"");
        markup.ShouldNotContain("title=\"Show latest run\"");
        markup.ShouldNotContain("aria-label=\"Show latest scenario run\"");
        markup.ShouldContain("No run history");
        markup.ShouldContain("test-run-history-row");
        markup.ShouldContain("RunHistoryItemClass(historyRun)");
        markup.ShouldContain("RunHistoryAriaLabel(historyRun)");
        markup.ShouldContain("RunHistoryMarkerClass(historyRun)");
        markup.ShouldContain("RunHistoryIssueLabel(historyRun)");
        markup.ShouldContain("PhaseLanesClass");
        markup.ShouldContain("PhaseLaneClass(phase)");
        markup.ShouldContain("test-scenario-report-actions");
        markup.ShouldContain("test-scenario-build-actions");
        markup.ShouldContain("aria-label=\"@ScenarioBuildActionsLabel\"");
        markup.ShouldContain("Scenario build actions for {scenario.Name}");
        markup.ShouldContain("Scenario build actions with no active scenario");
        markup.ShouldContain("Class=\"test-scenario-icon-action\"");
        markup.ShouldContain("aria-label=\"@ViewReportTooltip\"");
        markup.ShouldContain("aria-label=\"@CopyReportTooltip\"");
        markup.ShouldContain("aria-label=\"@SaveReportTooltip\"");
        markup.ShouldContain("Class=\"test-scenario-run-action\"");
        markup.ShouldContain("Class=\"test-scenario-add-menu\"");
        markup.ShouldContain("Scenario.Phases");
        markup.ShouldContain("test-phase-lanes");
        markup.ShouldContain("test-phase-icon");
        markup.ShouldContain("test-phase-copy");
        markup.ShouldContain("test-phase-count");
        markup.ShouldContain("test-step-title-block");
        markup.ShouldContain("test-step-meta");
        markup.ShouldContain("StepCardClass(step, stepResult)");
        markup.ReplaceLineEndings("\n")
            .ShouldContain("class=\"@StepCardClass(step, stepResult)\"\n                                             role=\"group\"");
        markup.ShouldContain("StepCardLabel(step, stepResult)");
        markup.ShouldContain("MoveStepEarlierLabel(step)");
        markup.ShouldContain("MoveStepLaterLabel(step)");
        markup.ShouldContain("EditStepLabel(step)");
        markup.ShouldContain("DeleteStepLabel(step)");
        markup.ShouldContain("Move {step.Name} earlier");
        markup.ShouldContain("Move {step.Name} later");
        markup.ShouldContain("Edit {step.Name}");
        markup.ShouldContain("Delete {step.Name}");
        markup.ShouldNotContain("aria-label=\"Move step earlier\"");
        markup.ShouldNotContain("aria-label=\"Move step later\"");
        markup.ShouldNotContain("aria-label=\"Edit step\"");
        markup.ShouldNotContain("aria-label=\"Delete step\"");
        markup.ShouldNotContain("title=\"Move earlier\"");
        markup.ShouldNotContain("title=\"Move later\"");
        markup.ShouldNotContain("title=\"Edit step\"");
        markup.ShouldNotContain("title=\"Delete step\"");
        markup.ShouldContain("tabindex=\"0\"");
        markup.ShouldContain("StepStatusIcon(stepResult)");
        markup.ShouldContain("StepResultMarkerClass(stepResult.Status)");
        markup.ShouldContain("test-step-result-strip");
        markup.ShouldContain("StepResultMetaLabel(stepResult)");
        markup.ShouldContain("StepResultScopeLabel");
        markup.ShouldContain("FormatRunIdText");
        markup.ShouldContain("StepResultEventLabel(stepResult)");
        markup.ShouldContain("test-step-result-marker idle");
        markup.ShouldContain("var connectionsAvailable = await Live.EnsureConnectionsAsync(Project.GetConnectionResources());");
        markup.ShouldNotContain("MudChip");
        markup.ShouldNotContain("ActiveRunChip");
        markup.ShouldNotContain("FormatRunIdChip");
        markup.ShouldNotContain("RunHistoryStatusClass");
        markup.ShouldNotContain("RunHistoryStateClass");
        markup.ShouldNotContain("ActiveRunStateIcon");
        markup.ShouldNotContain("ActiveRunStateText");
        markup.ShouldNotContain("ActiveRunStateColor");
        markup.ShouldNotContain("@RunnerStateText");
        markup.ShouldNotContain("StepStatusClass(stepResult.Status)");
        markup.ShouldNotContain("test-step-badges");
        markup.ShouldNotContain("test-run-context-status");
        markup.ShouldNotContain("test-run-context-state");
        markup.ShouldNotContain("test-step-status idle");
        markup.ShouldNotContain("test-step-state idle");
        markup.ShouldNotContain("Designer ready");
        markup.ShouldNotContain("var ready = await Live.EnsureConnectionsAsync");
        markup.ShouldNotContain("if (!ready)");
        markup.ShouldNotContain("? \"Running\" : \"Ready\"");
        markup.ShouldNotContain("aria-label=\"Test scenario setup state\"");
        markup.ShouldNotContain("aria-label=\"Scenario designer summary\"");
        markup.ShouldNotContain("aria-label=\"Scenario build actions\"");
        markup.ShouldNotContain("aria-label=\"Scenario build summary\"");

        css.ShouldContain(".test-scenario-heading-icon");
        css.ShouldContain(".test-scenario-meta-strip span");
        css.ShouldContain(".test-run-context");
        css.ShouldContain(".test-run-context.latest");
        css.ShouldContain(".test-run-context.history");
        css.ShouldContain(".test-run-context-marker");
        css.ShouldContain(".test-run-context-reset");
        css.ShouldContain(".test-run-history-panel");
        css.ShouldContain(".test-run-history-empty strong");
        css.ShouldContain(".test-run-history-empty small");
        css.ShouldContain(".test-run-history-row");
        css.ShouldContain(".test-run-history-marker");
        css.ShouldContain("::deep .test-run-history-item.selected .test-run-history-row");
        css.ShouldContain(".test-scenario-empty-cues");
        css.ShouldContain(".test-scenario-empty-cues span");
        css.ShouldContain(".test-scenario-empty-icon.timeline");
        css.ShouldContain(".test-scenario-workspace");
        css.ShouldContain("grid-template-rows: auto minmax(0, 1fr);");
        css.ShouldContain(".test-scenario-builder-strip");
        css.ShouldContain("grid-template-columns: repeat(3, minmax(0, 1fr));");
        css.ShouldContain(".test-builder-metric");
        css.ShouldContain(".test-scenario-report-actions");
        css.ShouldContain(".test-scenario-report-actions.latest");
        css.ShouldContain(".test-scenario-report-actions.history");
        css.ShouldContain(".test-scenario-report-actions.disabled");
        css.ShouldContain(".test-scenario-build-actions");
        css.ShouldContain(".test-scenario-report-actions ::deep .mud-icon-button");
        css.ShouldContain("grid-template-columns: minmax(0, 1fr) auto;");
        css.ShouldContain("min-height: 42px;");
        css.ShouldContain("flex-wrap: nowrap;");
        css.ShouldContain("grid-template-columns: repeat(auto-fit, minmax(206px, 1fr));");
        css.ShouldContain(".test-phase-lanes.drop-active");
        css.ShouldContain(".test-phase-lane.drop-target");
        css.ShouldContain(".test-phase-lane.empty.drop-target .test-phase-empty");
        css.ShouldContain(".test-phase-icon");
        css.ShouldContain(".test-step-card::before");
        css.ShouldContain(".test-step-card:hover,");
        css.ShouldContain(".test-step-card:focus-within,");
        css.ShouldContain(".test-step-card.selected");
        css.ShouldContain(".test-step-card.history");
        css.ShouldContain(".test-step-meta");
        css.ShouldContain(".test-step-result-marker");
        css.ShouldNotContain(".test-step-badges");
        css.ShouldNotContain(".test-run-status");
        css.ShouldNotContain(".test-run-history-status");
        css.ShouldNotContain(".test-run-context-status");
        css.ShouldNotContain(".test-run-context-state");
        css.ShouldNotContain(".test-run-history-state");
        css.ShouldNotContain(".test-step-status");
        css.ShouldNotContain(".test-step-state");
        css.ShouldNotContain("border-radius: 999px;");
        css.ShouldContain(".test-step-card.configured .test-step-index");
        css.ShouldContain(".test-step-card.issue");
        css.ShouldContain(".test-step-card.issue .test-step-index");
        css.ShouldContain(".test-step-result-strip");
        css.ShouldContain(".test-step-result-run.history");
        css.ShouldContain("min-height: 48px;");
        css.ShouldContain("grid-template-columns: minmax(0, 1fr) auto auto;");
        css.ShouldContain("height: 26px;");
        css.ShouldContain("height: 22px;");
        css.ShouldContain("@media (max-width: 760px)");
        css.ShouldContain("grid-template-columns: minmax(0, 1fr);");
    }

    [Fact]
    public void ScenarioStepEditorDialog_UsesFlatCompactEditorChrome()
    {
        var root = FindRepositoryRoot();
        var markup = File.ReadAllText(Path.Combine(
            root,
            "src",
            "FluxMq.UI",
            "Components",
            "Workspace",
            "Dialogs",
            "ScenarioStepEditorDialog.razor"));
        var css = File.ReadAllText(Path.Combine(
            root,
            "src",
            "FluxMq.UI",
            "Components",
            "Workspace",
            "Dialogs",
            "ScenarioStepEditorDialog.razor.css"));

        markup.ShouldContain("scenario-step-editor-title");
        markup.ShouldContain("scenario-step-editor-title-copy");
        markup.ShouldContain("Icon=\"@StepDescriptor.Icon\" Size=\"Size.Small\" aria-hidden=\"true\"");
        markup.ShouldContain("Class=\"scenario-step-editor\"");
        markup.ShouldContain("aria-label=\"@DialogTitle\"");
        markup.ShouldContain("Class=\"scenario-step-editor-toggle\"");
        markup.ShouldContain("scenario-step-editor-checks");
        markup.ShouldContain("IsDescriptorFieldEditor");
        markup.ShouldContain("scenario-step-editor-field-grid");
        markup.ShouldContain("StepDescriptor.Fields");
        markup.ShouldContain("CreateGenericConfiguration");
        markup.ShouldContain("InitializeGenericFields");
        markup.ShouldContain("GenericFieldValue(currentField)");
        markup.ShouldContain("FieldInputType(currentField)");
        markup.ShouldContain("scenario-step-editor-actions");
        markup.ShouldContain("scenario-step-editor-action-buttons");
        markup.ShouldContain("aria-label=\"@CancelStepEditLabel\"");
        markup.ShouldContain("aria-label=\"@ApplyStepEditLabel\"");
        markup.ShouldContain("private string CancelStepEditLabel => $\"Cancel editing {StepEditTargetLabel}\"");
        markup.ShouldContain("private string ApplyStepEditLabel => $\"Apply edits to {StepEditTargetLabel}\"");
        markup.ShouldContain("private string StepEditTargetLabel => string.IsNullOrWhiteSpace(Step.Name)");
        markup.ShouldNotContain("aria-label=\"Cancel step edit\"");
        markup.ShouldNotContain("aria-label=\"Apply step edit\"");
        markup.ShouldContain("class=\"scenario-step-editor-validation invalid\"");
        markup.ShouldContain("ValidationSummaryText");
        markup.ShouldContain("Icon=\"@Icons.Material.Filled.ErrorOutline\" Size=\"Size.Small\" aria-hidden=\"true\"");
        markup.ShouldNotContain("Icon=\"@StepDescriptor.Icon\" Size=\"Size.Small\" />");
        markup.ShouldNotContain("Icon=\"@Icons.Material.Filled.ErrorOutline\" Size=\"Size.Small\" />");
        markup.ShouldNotContain("ValidationStateText");
        markup.ShouldNotContain("ValidationStateClass");
        markup.ShouldNotContain("Ready to apply");
        markup.ShouldNotContain("scenario-step-editor-state");
        markup.ShouldNotContain("scenario-step-editor-state ready");
        markup.ShouldNotContain("scenario-step-editor-validation ready");
        markup.ShouldContain("Disabled=\"@HasValidationIssues\"");
        markup.ShouldContain("BuildValidationMessages");
        markup.ShouldContain("AddRequiredMessage(messages, _connectionField.Label, _connection)");
        markup.ShouldContain("IsIntegerField(field)");
        markup.ShouldContain("IsNumberField(field)");
        markup.ShouldContain("ScenarioStepCatalog.WindowMsKey");

        css.ShouldContain(".scenario-step-editor");
        css.ShouldContain(".scenario-step-editor-title-copy");
        css.ShouldContain("font-size: 14px;");
        css.ShouldContain("background: color-mix(in srgb, var(--flux-canvas) 24%, var(--flux-surface));");
        css.ShouldContain("border: 1px solid var(--flux-border-soft);");
        css.ShouldContain(".scenario-step-editor ::deep(.mud-input-root)");
        css.ShouldContain("min-height: 34px;");
        css.ShouldContain(".scenario-step-editor-toggle");
        css.ShouldContain("grid-template-columns: minmax(82px, 0.34fr) minmax(0, 1fr);");
        css.ShouldContain(".scenario-step-editor-checks");
        css.ShouldContain(".scenario-step-editor-field-grid");
        css.ShouldContain("grid-template-columns: repeat(2, minmax(0, 1fr));");
        css.ShouldContain(".scenario-step-editor-actions");
        css.ShouldContain("justify-content: space-between;");
        css.ShouldContain(".scenario-step-editor-validation");
        css.ShouldNotContain(".scenario-step-editor-state");
        css.ShouldNotContain(".scenario-step-editor-state.ready");
        css.ShouldNotContain(".scenario-step-editor-validation.ready");
        css.ShouldNotContain("display: none;");
        css.ShouldContain(".scenario-step-editor-validation.invalid");
        css.ShouldContain(".scenario-step-editor-action-buttons");
        css.ShouldContain("margin-left: auto;");
        css.ShouldContain("min-height: 28px;");
        css.ShouldContain("@media (max-width: 560px)");
        css.ShouldContain("flex-direction: column;");
        css.ShouldContain("grid-template-columns: minmax(0, 1fr);");
    }

    [Fact]
    public void ScenarioRunReportDialog_UsesFlatCompactReviewChrome()
    {
        var root = FindRepositoryRoot();
        var markup = File.ReadAllText(Path.Combine(
            root,
            "src",
            "FluxMq.UI",
            "Components",
            "Workspace",
            "Dialogs",
            "ScenarioRunReportDialog.razor"));
        var css = File.ReadAllText(Path.Combine(
            root,
            "src",
            "FluxMq.UI",
            "Components",
            "Workspace",
            "Dialogs",
            "ScenarioRunReportDialog.razor.css"));

        markup.ShouldContain("scenario-report-title");
        markup.ShouldContain("scenario-report-toolbar");
        markup.ShouldContain("scenario-report-meta-strip");
        markup.ShouldContain("aria-label=\"@ReportMetaSummaryLabel\"");
        markup.ShouldContain("aria-label=\"@ReportToolbarLabel\"");
        markup.ShouldContain("aria-label=\"@ReportExportActionsLabel\"");
        markup.ShouldContain("private string ReportMetaSummaryLabel");
        markup.ShouldContain("private string ReportToolbarLabel");
        markup.ShouldContain("private string ReportExportActionsLabel");
        markup.ShouldContain("Icon=\"@Icons.Material.Filled.Article\" Size=\"Size.Small\" aria-hidden=\"true\"");
        markup.ShouldContain("Icon=\"@RunScopeIcon\" Size=\"Size.Small\" aria-hidden=\"true\"");
        markup.ShouldContain("Icon=\"@Icons.Material.Filled.FactCheck\" Size=\"Size.Small\" aria-hidden=\"true\"");
        markup.ShouldContain("Icon=\"@Icons.Material.Filled.FormatListNumbered\" Size=\"Size.Small\" aria-hidden=\"true\"");
        markup.ShouldContain("scenario-report-summary-grid");
        markup.ShouldContain("IssueMetricClass");
        markup.ShouldContain("Icon=\"@Icons.Material.Filled.Tag\" Size=\"Size.Small\" aria-hidden=\"true\"");
        markup.ShouldContain("Icon=\"@Icons.Material.Filled.Schedule\" Size=\"Size.Small\" aria-hidden=\"true\"");
        markup.ShouldContain("Icon=\"@Icons.Material.Filled.Timer\" Size=\"Size.Small\" aria-hidden=\"true\"");
        markup.ShouldContain("Icon=\"@IssueIcon\" Size=\"Size.Small\" aria-hidden=\"true\"");
        markup.ShouldContain("scenario-report-viewer");
        markup.ShouldContain("aria-label=\"@RunDetailsLabel\"");
        markup.ShouldContain("private string RunDetailsLabel");
        markup.ShouldContain("HasSummaryReport");
        markup.ShouldContain("HasJsonReport");
        markup.ShouldContain("scenario-report-empty");
        markup.ShouldContain("Icon=\"@Icons.Material.Filled.Article\" Size=\"Size.Medium\" aria-hidden=\"true\"");
        markup.ShouldContain("Icon=\"@Icons.Material.Filled.DataObject\" Size=\"Size.Medium\" aria-hidden=\"true\"");
        markup.ShouldContain("<pre aria-label=\"Text scenario report\">@TextReport</pre>");
        markup.ShouldContain("<pre aria-label=\"Scenario report JSON\">@JsonReport</pre>");
        markup.ShouldContain("scenario-report-action-group");
        markup.ShouldContain("Disabled=\"@(!HasSummaryReport)\"");
        markup.ShouldContain("Disabled=\"@(!HasJsonReport)\"");
        markup.ShouldContain("aria-label=\"@CopySummaryAriaLabel\"");
        markup.ShouldContain("aria-label=\"@SaveSummaryAriaLabel\"");
        markup.ShouldContain("aria-label=\"@CopyJsonAriaLabel\"");
        markup.ShouldContain("aria-label=\"@SaveJsonAriaLabel\"");
        markup.ShouldContain("private string CopySummaryAriaLabel");
        markup.ShouldContain("private string SaveSummaryAriaLabel");
        markup.ShouldContain("private string CopyJsonAriaLabel");
        markup.ShouldContain("private string SaveJsonAriaLabel");
        markup.ShouldContain("scenario-report-empty\" role=\"status\"");
        markup.ShouldContain("scenario-report-close");
        markup.ShouldContain("aria-label=\"@CloseReportLabel\"");
        markup.ShouldContain("private string CloseReportLabel => $\"Close {ReportTitleTargetLabel}\"");
        markup.ShouldContain("private string ReportTitleTargetLabel => string.IsNullOrWhiteSpace(Title)");
        markup.ShouldNotContain("aria-label=\"Close scenario report\"");
        markup.ShouldNotContain("aria-label=\"Scenario report summary\"");
        markup.ShouldNotContain("aria-label=\"Scenario report actions\"");
        markup.ShouldNotContain("aria-label=\"Scenario report export actions\"");
        markup.ShouldNotContain("aria-label=\"Copy summary report\"");
        markup.ShouldNotContain("aria-label=\"Save summary report\"");
        markup.ShouldNotContain("aria-label=\"Copy JSON report\"");
        markup.ShouldNotContain("aria-label=\"Save JSON report\"");
        markup.ShouldNotContain("aria-label=\"Run details\"");
        markup.ShouldNotContain("scenario-report-export-state");
        markup.ShouldNotContain("ExportState");
        markup.ShouldNotContain("AvailableReportFormatCount");
        markup.ShouldNotContain("formats ready");
        markup.ShouldNotContain("No export content");
        markup.ShouldNotContain("Icon=\"@Icons.Material.Filled.Article\" Size=\"Size.Small\" />");
        markup.ShouldNotContain("Icon=\"@RunScopeIcon\" Size=\"Size.Small\" />");
        markup.ShouldNotContain("Icon=\"@Icons.Material.Filled.FactCheck\" Size=\"Size.Small\" />");
        markup.ShouldNotContain("Icon=\"@Icons.Material.Filled.FormatListNumbered\" Size=\"Size.Small\" />");
        markup.ShouldNotContain("Icon=\"@Icons.Material.Filled.Tag\" Size=\"Size.Small\" />");
        markup.ShouldNotContain("Icon=\"@Icons.Material.Filled.Schedule\" Size=\"Size.Small\" />");
        markup.ShouldNotContain("Icon=\"@Icons.Material.Filled.Timer\" Size=\"Size.Small\" />");
        markup.ShouldNotContain("Icon=\"@IssueIcon\" Size=\"Size.Small\" />");
        markup.ShouldNotContain("Icon=\"@Icons.Material.Filled.Article\" Size=\"Size.Medium\" />");
        markup.ShouldNotContain("Icon=\"@Icons.Material.Filled.DataObject\" Size=\"Size.Medium\" />");
        markup.ShouldContain("scenario-report-empty\" role=\"status\" aria-live=\"polite\"");
        markup.ShouldNotContain("MudChip");
        markup.ShouldNotContain("MudTextField");

        css.ShouldContain(".scenario-report-title");
        css.ShouldContain(".scenario-report-toolbar");
        css.ShouldContain(".scenario-report-meta-strip");
        css.ShouldNotContain(".scenario-report-export-state");
        css.ShouldContain(".scenario-report-summary-grid");
        css.ShouldContain("grid-template-columns: repeat(4, minmax(0, 1fr));");
        css.ShouldContain("min-height: min(62vh, 540px);");
        css.ShouldContain(".scenario-report-tabs");
        css.ShouldContain("min-height: 30px;");
        css.ShouldContain(".scenario-report-viewer pre");
        css.ShouldContain("min-height: 300px;");
        css.ShouldContain(".scenario-report-empty");
        css.ShouldContain("font-family: Consolas, \"Courier New\", monospace;");
        css.ShouldContain(".scenario-report-actions");
        css.ShouldContain(".scenario-report-action-group.compact");
        css.ShouldContain("border: 1px solid var(--flux-border-soft);");
        css.ShouldContain(".scenario-report-action-group ::deep(.scenario-report-action)");
        css.ShouldContain("width: 24px;");
        css.ShouldContain("::deep(.scenario-report-close)");
        css.ShouldContain("@media (max-width: 760px)");
        css.ShouldContain("grid-template-columns: repeat(2, minmax(0, 1fr));");
    }

    [Fact]
    public void NewAppDialog_UsesFlatCompactSetupChrome()
    {
        var root = FindRepositoryRoot();
        var markup = File.ReadAllText(Path.Combine(
            root,
            "src",
            "FluxMq.UI",
            "Components",
            "Workspace",
            "Dialogs",
            "NewAppDialog.razor"));
        var css = File.ReadAllText(Path.Combine(
            root,
            "src",
            "FluxMq.UI",
            "Components",
            "Workspace",
            "Dialogs",
            "NewAppDialog.razor.css"));

        markup.ShouldContain("new-app-dialog-title");
        markup.ShouldContain("new-app-dialog-section");
        markup.ShouldContain("new-app-dialog-grid connection");
        markup.ShouldContain("new-app-dialog-security-row");
        markup.ShouldContain("new-app-dialog-actions");
        markup.ShouldContain("aria-label=\"@CancelNewAppLabel\"");
        markup.ShouldContain("aria-label=\"@CreateNewAppLabel\"");
        markup.ShouldContain("private string CancelNewAppLabel => $\"Cancel {NewAppTargetLabel} setup\"");
        markup.ShouldContain("private string CreateNewAppLabel => $\"Create {NewAppTargetLabel} with {FirstPipelineTargetLabel}\"");
        markup.ShouldContain("private string NewAppTargetLabel => string.IsNullOrWhiteSpace(_appName)");
        markup.ShouldContain("private string FirstPipelineTargetLabel => string.IsNullOrWhiteSpace(_pipelineName)");
        markup.ShouldNotContain("aria-label=\"Cancel new app\"");
        markup.ShouldNotContain("aria-label=\"Create app\"");
        markup.ShouldContain("<MudIcon Icon=\"@Icons.Material.Filled.Apps\" Size=\"Size.Small\" aria-hidden=\"true\" />");
        markup.ShouldContain("<MudIcon Icon=\"@Icons.Material.Filled.AccountTree\" Size=\"Size.Small\" aria-hidden=\"true\" />");
        markup.ShouldContain("<MudIcon Icon=\"@Icons.Material.Filled.Cable\" Size=\"Size.Small\" aria-hidden=\"true\" />");
        markup.ShouldContain("<MudIcon Icon=\"@Icons.Material.Filled.Lock\" Size=\"Size.Small\" aria-hidden=\"true\" />");
        markup.ShouldContain("_port is >= 1 and <= 65535");
        markup.ShouldNotContain("new-app-dialog-status");
        markup.ShouldNotContain("FormStatusClass");
        markup.ShouldNotContain("FormStatusText");
        markup.ShouldNotContain(">Ready<");
        markup.ShouldNotContain("<MudIcon Icon=\"@Icons.Material.Filled.Apps\" Size=\"Size.Small\" />");
        markup.ShouldNotContain("<MudIcon Icon=\"@Icons.Material.Filled.AccountTree\" Size=\"Size.Small\" />");
        markup.ShouldNotContain("<MudIcon Icon=\"@Icons.Material.Filled.Cable\" Size=\"Size.Small\" />");
        markup.ShouldNotContain("<MudIcon Icon=\"@Icons.Material.Filled.Lock\" Size=\"Size.Small\" />");
        markup.ShouldNotContain("MudDivider");
        markup.ShouldNotContain("HelperText=");

        css.ShouldContain(".new-app-dialog-title");
        css.ShouldContain(".new-app-dialog-section");
        css.ShouldContain("border: 1px solid var(--flux-border-soft);");
        css.ShouldContain(".new-app-dialog-grid.connection");
        css.ShouldContain("grid-template-columns: minmax(0, 1.1fr) minmax(0, 1.6fr) 92px;");
        css.ShouldContain(".new-app-dialog-security-row");
        css.ShouldContain("::deep(.new-app-dialog-create)");
        css.ShouldContain("@media (max-width: 760px)");
        css.ShouldContain("grid-template-columns: minmax(0, 1fr);");
        css.ShouldContain("@media (max-width: 480px)");
        css.ShouldNotContain(".new-app-dialog-status");
        css.ShouldNotContain(".new-app-dialog-status.ready");
    }

    [Fact]
    public void AddConnectionDialog_UsesFlatCompactSetupChrome()
    {
        var root = FindRepositoryRoot();
        var markup = File.ReadAllText(Path.Combine(
            root,
            "src",
            "FluxMq.UI",
            "Components",
            "Workspace",
            "Dialogs",
            "AddConnectionDialog.razor"));
        var css = File.ReadAllText(Path.Combine(
            root,
            "src",
            "FluxMq.UI",
            "Components",
            "Workspace",
            "Dialogs",
            "AddConnectionDialog.razor.css"));

        markup.ShouldContain("add-connection-dialog-title");
        markup.ShouldContain("add-connection-dialog-section");
        markup.ShouldContain("add-connection-dialog-grid broker");
        markup.ShouldContain("add-connection-dialog-checkbox-cell");
        markup.ShouldContain("add-connection-dialog-actions");
        markup.ShouldContain("aria-label=\"@AddConnectionLabel\"");
        markup.ShouldContain("aria-label=\"@CancelConnectionLabel\"");
        markup.ShouldContain("private string AddConnectionLabel => $\"Add connection for {ConnectionTargetLabel}\";");
        markup.ShouldContain("private string CancelConnectionLabel => $\"Cancel new connection for {ConnectionTargetLabel}\";");
        markup.ShouldContain("private string ConnectionTargetLabel");
        markup.ShouldContain("return $\"{name} at {host}:{_port.ToString(System.Globalization.CultureInfo.InvariantCulture)}\";");
        markup.ShouldNotContain("aria-label=\"Add connection\"");
        markup.ShouldNotContain("aria-label=\"Cancel new connection\"");
        markup.ShouldContain("<MudIcon Icon=\"@Icons.Material.Filled.Cable\" Size=\"Size.Small\" aria-hidden=\"true\" />");
        markup.ShouldContain("<MudIcon Icon=\"@Icons.Material.Filled.Dns\" Size=\"Size.Small\" aria-hidden=\"true\" />");
        markup.ShouldContain("<MudIcon Icon=\"@Icons.Material.Filled.Lock\" Size=\"Size.Small\" aria-hidden=\"true\" />");
        markup.ShouldContain("_port is >= 1 and <= 65535");
        markup.ShouldContain("_keepAliveSeconds > 0");
        markup.ShouldContain("Label=\"Broker name\"");
        markup.ShouldContain("Label=\"Client ID\"");
        markup.ShouldContain("Label=\"Keep alive seconds\"");
        markup.ShouldContain("Label=\"Clean start\"");
        markup.ShouldContain("Label=\"Use TLS\"");
        markup.ShouldContain("Label=\"Allow untrusted certificate\"");
        markup.ShouldContain("CA certificate path");
        markup.ShouldContain("Client certificate path");
        markup.ShouldContain("Client certificate password");
        markup.ShouldContain("FilePicker.Default.PickAsync");
        markup.ShouldContain("Icons.Material.Filled.FolderOpen");
        markup.ShouldContain("Adornment=\"Adornment.End\"");
        markup.ShouldContain("OnAdornmentClick");
        markup.ShouldContain("AdornmentAriaLabel=\"Select CA certificate\"");
        markup.ShouldContain("AdornmentAriaLabel=\"Select client certificate\"");
        markup.ShouldContain("AllowUntrustedCertificates = _useTls && _allowUntrustedCertificates");
        markup.ShouldContain("KeepAlive = TimeSpan.FromSeconds(_keepAliveSeconds)");
        markup.ShouldContain("CleanStart = _cleanStart");
        markup.ShouldContain("LiveMqttWorkspaceService.DefaultBrokerMonitorSubscription");
        markup.ShouldNotContain("@inject IDialogService");
        markup.ShouldNotContain("MudDivider");
        markup.ShouldNotContain("add-connection-dialog-security-row");
        markup.ShouldNotContain("add-connection-dialog-status");
        markup.ShouldNotContain("FormStatusClass");
        markup.ShouldNotContain("FormStatusText");
        markup.ShouldNotContain(">Ready<");
        markup.ShouldNotContain("<MudIcon Icon=\"@Icons.Material.Filled.Cable\" Size=\"Size.Small\" />");
        markup.ShouldNotContain("<MudIcon Icon=\"@Icons.Material.Filled.Dns\" Size=\"Size.Small\" />");
        markup.ShouldNotContain("<MudIcon Icon=\"@Icons.Material.Filled.Lock\" Size=\"Size.Small\" />");
        markup.ShouldNotContain("Label=\"TLS\"");
        markup.ShouldNotContain("<input");

        css.ShouldContain(".add-connection-dialog-title");
        css.ShouldContain(".add-connection-dialog-section");
        css.ShouldContain("border: 1px solid var(--flux-border-soft);");
        css.ShouldContain(".add-connection-dialog-grid.broker");
        css.ShouldContain("grid-template-columns: repeat(2, minmax(0, 1fr));");
        css.ShouldContain(".add-connection-dialog-checkbox-cell");
        css.ShouldContain("min-height: 42px;");
        css.ShouldContain("::deep(.add-connection-dialog-add)");
        css.ShouldContain("@media (max-width: 760px)");
        css.ShouldContain("grid-template-columns: minmax(0, 1fr);");
        css.ShouldContain("@media (max-width: 480px)");
        css.ShouldNotContain(".add-connection-dialog-security-row");
        css.ShouldNotContain(".add-connection-dialog-status");
        css.ShouldNotContain(".add-connection-dialog-status.ready");
        css.ShouldNotContain("grid-template-columns: minmax(0, 1.05fr) minmax(0, 1.55fr) 92px;");
    }

    [Fact]
    public void MetricDesigner_PrimaryCreateActionUsesAccentContrastText()
    {
        var root = FindRepositoryRoot();
        var markup = File.ReadAllText(Path.Combine(
            root,
            "src",
            "FluxMq.UI",
            "Components",
            "Workspace",
            "MetricDesigner.razor"));
        var css = File.ReadAllText(Path.Combine(
            root,
            "src",
            "FluxMq.UI",
            "Components",
            "Workspace",
            "MetricDesigner.razor.css"));
        var appCss = File.ReadAllText(Path.Combine(
            root,
            "src",
            "FluxMq.UI",
            "wwwroot",
            "app.css"));

        markup.ShouldContain("<MudSelect T=\"string\"");
        markup.ShouldContain("class=\"metrics-list-toolbar\"");
        markup.ShouldContain("PopoverClass=\"metrics-type-select-popover\"");
        markup.ShouldContain("ListClass=\"metrics-type-select-list\"");
        markup.ShouldContain("Value=\"@_typeFilter\"");
        markup.ShouldContain("ValueChanged=\"@SetTypeFilter\"");
        markup.ShouldContain("Underline=\"false\"");
        markup.ShouldContain("Modal=\"false\"");
        markup.ShouldContain("RelativeWidth=\"DropdownWidth.Adaptive\"");
        markup.ShouldContain("MaxHeight=\"320\"");
        markup.ShouldContain("Class=\"metrics-type-filter-icon\" Icon=\"@Icons.Material.Filled.FilterAlt\"");
        markup.ShouldContain("<MudSelectItem T=\"string\" Value=\"@AllTypes\">All types</MudSelectItem>");
        markup.ShouldContain("<strong>@EditorEmptyTitle</strong>");
        markup.ShouldContain("Rows.Count == 0 ? \"No metrics yet\" : \"No metric selected\"");
        markup.ShouldNotContain("Ready to edit");
        markup.ShouldNotContain("Adornment=\"Adornment.Start\"");
        markup.ShouldNotContain("AdornmentIcon=\"@Icons.Material.Filled.FilterAlt\"");
        markup.ShouldNotContain("class=\"metrics-toolbar\"");
        markup.ShouldNotContain("<select value=\"@_typeFilter\"");
        markup.ShouldNotContain("OnTypeFilterChanged");
        markup.IndexOf("class=\"metrics-list-pane\"", StringComparison.Ordinal)
            .ShouldBeLessThan(markup.IndexOf("class=\"metrics-list-toolbar\"", StringComparison.Ordinal));
        markup.IndexOf("class=\"metrics-list-toolbar\"", StringComparison.Ordinal)
            .ShouldBeLessThan(markup.IndexOf("class=\"metrics-list-head\"", StringComparison.Ordinal));
        css.ShouldContain(".metrics-new-button");
        css.ShouldContain("background: var(--flux-accent);");
        css.ShouldContain("color: var(--flux-accent-contrast);");
        css.ShouldContain(".metrics-new-button ::deep .mud-icon-root");
        var buttonBlock = css[
            css.IndexOf(".metrics-new-button {", StringComparison.Ordinal)..
            css.IndexOf(".metrics-new-button:hover", StringComparison.Ordinal)];
        buttonBlock.ShouldNotContain("color: var(--mud-palette-primary-text);");
        css.ShouldContain(".metrics-type-field ::deep .metrics-type-select");
        css.ShouldContain(".metrics-type-field ::deep .metrics-type-select .mud-input");
        css.ShouldContain(".metrics-list-toolbar");
        css.ShouldContain("background: color-mix(in srgb, var(--flux-surface) 76%, transparent);");
        css.ShouldContain("grid-template-columns: minmax(0, 1fr);");
        css.ShouldContain(".metrics-type-field ::deep .metrics-type-filter-icon");
        css.ShouldContain("position: absolute;");
        css.ShouldContain("padding: 0 8px 0 34px;");
        css.ShouldNotContain(".metrics-toolbar");
        css.ShouldNotContain(".metrics-filter-field select option");
        appCss.ShouldContain(".metrics-type-select-popover");
        appCss.ShouldContain(".metrics-type-select-list");
        appCss.ShouldContain("box-sizing: border-box;");
        appCss.ShouldContain("width: min(300px, calc(100vw - 24px)) !important;");
        appCss.ShouldContain("overflow-x: hidden !important;");
        appCss.ShouldContain(".metrics-type-select-list .mud-list-item");
        appCss.ShouldContain(".metrics-type-select-list .mud-list-item-text");
        appCss.ShouldContain("text-overflow: ellipsis;");
        appCss.ShouldContain("white-space: nowrap;");
        appCss.ShouldContain(".metrics-type-select-list .mud-selected-item");
    }

    [Fact]
    public void MetricDesigner_UsesNeutralMetricMarkerHooks()
    {
        var root = FindRepositoryRoot();
        var markup = File.ReadAllText(Path.Combine(
            root,
            "src",
            "FluxMq.UI",
            "Components",
            "Workspace",
            "MetricDesigner.razor"));
        var css = File.ReadAllText(Path.Combine(
            root,
            "src",
            "FluxMq.UI",
            "Components",
            "Workspace",
            "MetricDesigner.razor.css"));

        markup.ShouldContain("metrics-type-summary");
        markup.ShouldContain("metrics-latest-marker live");
        markup.ShouldContain("metrics-latest-marker muted");
        markup.ShouldContain("@if (ShowUnsavedIndicator)");
        markup.ShouldContain("metrics-unsaved-indicator dirty");
        markup.ShouldContain("metrics-unsaved-dot");
        markup.ShouldContain("metrics-side-indicator live");
        markup.ShouldContain("metrics-side-indicator danger");
        markup.ShouldContain("metrics-preview-marker");
        markup.ShouldContain("aria-label=\"@MetricListPaneLabel\"");
        markup.ShouldContain("private string MetricListPaneLabel");
        markup.ShouldContain("$\"Metric resources, {MetricCountText}, {MetricFilterStateLabel}\"");
        markup.ShouldContain("aria-label=\"@MetricSearchLabel\"");
        markup.ShouldContain("private string MetricSearchLabel");
        markup.ShouldContain("$\"Search metrics, current query {_search}\"");
        markup.ShouldContain("aria-label=\"@MetricTypeFilterLabel\"");
        markup.ShouldContain("private string MetricTypeFilterLabel");
        markup.ShouldContain("Metric type filter, {selected.DisplayName}");
        markup.ShouldContain("aria-label=\"@MetricEditorPaneLabel\"");
        markup.ShouldContain("private string MetricEditorPaneLabel");
        markup.ShouldContain("$\"Metric editor for {SelectedMetricActionTarget}\"");
        markup.ShouldContain("title=\"@UnsavedMetricChangesLabel\"");
        markup.ShouldContain("private string UnsavedMetricChangesLabel => $\"Unsaved changes for {SelectedMetricActionTarget}\"");
        markup.ShouldContain("aria-label=\"@DisplayNameFieldLabel\"");
        markup.ShouldContain("private string DisplayNameFieldLabel => $\"Display name for {SelectedMetricActionTarget}\"");
        markup.ShouldContain("aria-label=\"@ChangeMetricTypeLabel\"");
        markup.ShouldContain("private string ChangeMetricTypeLabel => $\"Change metric type for {SelectedMetricActionTarget}\"");
        markup.ShouldContain("aria-label=\"@DescriptionFieldLabel\"");
        markup.ShouldContain("private string DescriptionFieldLabel => $\"Description for {SelectedMetricActionTarget}\"");
        markup.ShouldContain("aria-label=\"@MetricDetailsLabel\"");
        markup.ShouldContain("private string MetricDetailsLabel");
        markup.ShouldContain("Metric details for {(_draft.DisplayName.Length == 0 ? _draft.Id : _draft.DisplayName)}");
        markup.ShouldContain("title=\"@NoReadingTitle\"");
        markup.ShouldContain("private string NoReadingTitle => $\"No live reading for {SelectedMetricActionTarget}\"");
        markup.ShouldContain("aria-label=\"@MetricReferenceListLabel\"");
        markup.ShouldContain("private string MetricReferenceListLabel");
        markup.ShouldContain("$\"Dashboard bindings for {SelectedMetricActionTarget}, {ReferenceSummaries.Count.ToString(CultureInfo.InvariantCulture)} references\"");
        markup.ShouldContain("title=\"@ShowMetricFieldLabel(item)\"");
        markup.ShouldContain("private static string ShowMetricFieldLabel(MetricValidationItem item)");
        markup.ShouldContain("aria-label=\"@ParameterFieldLabel(parameter)\"");
        markup.ShouldContain("private string ParameterFieldLabel(MetricParamSpec parameter)");
        markup.ShouldContain("$\"{parameter.DisplayName} for {SelectedMetricActionTarget}\"");
        markup.ShouldContain("title=\"@ClearMetricSearchLabel\"");
        markup.ShouldContain("aria-label=\"@ClearMetricSearchLabel\"");
        markup.ShouldContain("private string ClearMetricSearchLabel");
        markup.ShouldContain("$\"Clear metric search for {_search}\"");
        markup.ShouldNotContain("title=\"Clear search\"");
        markup.ShouldNotContain("aria-label=\"Clear search\"");
        markup.ShouldContain("Text=\"@ResetMetricFiltersLabel\"");
        markup.ShouldContain("aria-label=\"@ResetMetricFiltersLabel\"");
        markup.ShouldContain("private string ResetMetricFiltersLabel => $\"Reset metric filters, {MetricFilterStateLabel}\"");
        markup.ShouldContain("private string MetricFilterStateLabel");
        markup.ShouldContain("private string TypeFilterLabel(string typeId)");
        markup.ShouldNotContain("Text=\"Reset filters\"");
        markup.ShouldNotContain("aria-label=\"Reset filters\"");
        markup.ShouldContain("Text=\"@CancelMetricChangesLabel\"");
        markup.ShouldContain("aria-label=\"@CancelMetricChangesLabel\"");
        markup.ShouldContain("private string CancelMetricChangesLabel => $\"Cancel changes for {SelectedMetricActionTarget}\"");
        markup.ShouldNotContain("Text=\"Cancel changes\"");
        markup.ShouldNotContain("aria-label=\"Cancel changes\"");
        markup.ShouldContain("title=\"@RenameMetricLabel\"");
        markup.ShouldContain("aria-label=\"@RenameMetricLabel\"");
        markup.ShouldContain("private string RenameMetricLabel => $\"Rename {SelectedMetricActionTarget}\"");
        markup.ShouldNotContain("title=\"Rename metric\"");
        markup.ShouldNotContain("aria-label=\"Rename metric\"");
        markup.ShouldContain("Text=\"@DuplicateMetricLabel\"");
        markup.ShouldContain("aria-label=\"@DuplicateMetricLabel\"");
        markup.ShouldContain("private string DuplicateMetricLabel => $\"Duplicate {SelectedMetricActionTarget}\"");
        markup.ShouldNotContain("Text=\"Duplicate metric\"");
        markup.ShouldNotContain("aria-label=\"Duplicate metric\"");
        markup.ShouldContain("Text=\"@DeleteMetricLabel\"");
        markup.ShouldContain("aria-label=\"@DeleteMetricLabel\"");
        markup.ShouldContain("private string DeleteMetricLabel => $\"Delete {SelectedMetricActionTarget}\"");
        markup.ShouldNotContain("Text=\"Delete metric\"");
        markup.ShouldNotContain("aria-label=\"Delete metric\"");
        markup.ShouldContain("metrics-list-empty create-empty\" role=\"status\" aria-live=\"polite\"");
        markup.ShouldContain("metrics-list-empty filter-empty\" role=\"status\" aria-live=\"polite\"");
        markup.ShouldContain("metrics-editor-empty\" role=\"status\" aria-live=\"polite\"");
        markup.ShouldContain("metrics-preview-empty\" role=\"status\" aria-live=\"polite\"");
        markup.ShouldContain("metrics-reference-empty\" role=\"status\" aria-live=\"polite\"");
        markup.ShouldContain("aria-label=\"@MetricRowLabel(current)\"");
        markup.ShouldContain("class=\"metrics-param-help\" role=\"img\" tabindex=\"0\" aria-label=\"@assistiveText\"");
        markup.ShouldContain("private static string MetricRowLabel(MetricDesignerRow row)");
        markup.ShouldContain("Select metric {row.DisplayName} ({row.Id})");
        markup.Split('\n')
            .Where(static line => line.Contains("<MudIcon ", StringComparison.Ordinal) &&
                !line.Contains("aria-hidden=\"true\"", StringComparison.Ordinal))
            .ToArray()
            .ShouldBeEmpty();
        markup.ShouldContain("Class=\"metrics-heading-icon\" aria-hidden=\"true\"");
        markup.ShouldContain("class=\"metrics-empty-icon\" aria-hidden=\"true\"");
        markup.ShouldContain("class=\"metrics-empty-icon muted\" aria-hidden=\"true\"");
        markup.ShouldContain("Class=\"metrics-reference-icon\" aria-hidden=\"true\"");
        markup.ShouldContain("role=\"radiogroup\"");
        markup.ShouldContain("aria-orientation=\"horizontal\"");
        markup.ShouldContain("role=\"radio\"");
        markup.ShouldContain("aria-checked=\"@AriaState(IsToggleSelected(toggleValue, TrueValue))\"");
        markup.ShouldNotContain("metrics-latest-state");
        markup.ShouldNotContain("metrics-preview-state");
        markup.ShouldNotContain("ShowEditorState");
        markup.ShouldNotContain("metrics-editor-state");
        markup.ShouldNotContain("metrics-state-dot");
        markup.ShouldNotContain("metrics-list-empty create-state");
        markup.ShouldNotContain("metrics-list-empty filter-state");
        markup.ShouldNotContain("metrics-type-pill");
        markup.ShouldNotContain("metrics-latest-pill");
        markup.ShouldNotContain("metrics-side-badge");
        markup.ShouldNotContain("metrics-preview-status");
        markup.ShouldNotContain("aria-label=\"Metric resources\"");
        markup.ShouldNotContain("aria-label=\"Search metrics\"");
        markup.ShouldNotContain("aria-label=\"Metric type\"");
        markup.ShouldNotContain("aria-label=\"Metric editor\"");
        markup.ShouldNotContain("title=\"This metric has unsaved changes\"");
        markup.ShouldNotContain("aria-label=\"Display name\"");
        markup.ShouldNotContain("aria-label=\"Change metric type\"");
        markup.ShouldNotContain("aria-label=\"Description\"");
        markup.ShouldNotContain("title=\"Stopped or not emitted.\"");
        markup.ShouldNotContain("aria-label=\"Dashboard bindings for selected metric\"");
        markup.ShouldNotContain("title=\"Show field\"");
        markup.ShouldNotContain("aria-label=\"@parameter.DisplayName\"");
        markup.ShouldNotContain("aria-label=\"Metric details\"");
        markup.ShouldNotContain("Class=\"metrics-heading-icon\" />");
        markup.ShouldNotContain("class=\"metrics-empty-icon\">");
        markup.ShouldNotContain("class=\"metrics-empty-icon muted\">");
        markup.ShouldNotContain("Class=\"metrics-reference-icon\" />");

        css.ShouldContain(".metrics-type-summary");
        css.ShouldContain(".metrics-type-summary ::deep .mud-icon-root");
        css.ShouldContain(".metrics-latest-marker");
        css.ShouldContain(".metrics-latest-marker.live");
        css.ShouldContain(".metrics-latest-marker.muted");
        css.ShouldContain(".metrics-unsaved-indicator");
        css.ShouldContain(".metrics-unsaved-indicator.dirty");
        css.ShouldContain(".metrics-unsaved-dot");
        css.ShouldContain("border-radius: 50%;");
        css.ShouldContain(".metrics-side-indicator");
        css.ShouldContain(".metrics-side-indicator.live");
        css.ShouldContain(".metrics-side-indicator.danger");
        css.ShouldContain(".metrics-preview-marker");
        css.ShouldContain(".metrics-list-empty.filter-empty");
        css.ShouldNotContain(".metrics-latest-state");
        css.ShouldNotContain(".metrics-preview-state");
        css.ShouldNotContain(".metrics-editor-state");
        css.ShouldNotContain(".metrics-state-dot");
        css.ShouldNotContain(".metrics-list-empty.create-state");
        css.ShouldNotContain(".metrics-list-empty.filter-state");
        css.ShouldNotContain(".metrics-type-pill");
        css.ShouldNotContain(".metrics-latest-pill");
        css.ShouldNotContain(".metrics-side-badge");
        css.ShouldNotContain(".metrics-preview-status");
        css.ShouldNotContain("border-radius: 999px;");
    }

    [Fact]
    public void MetricCreateDialog_UsesFlatCompactCreationChrome()
    {
        var root = FindRepositoryRoot();
        var markup = File.ReadAllText(Path.Combine(
            root,
            "src",
            "FluxMq.UI",
            "Components",
            "Workspace",
            "Dialogs",
            "MetricCreateDialog.razor"));
        var css = File.ReadAllText(Path.Combine(
            root,
            "src",
            "FluxMq.UI",
            "Components",
            "Workspace",
            "Dialogs",
            "MetricCreateDialog.razor.css"));

        markup.ShouldContain("class=\"metric-create-title-icon\" aria-hidden=\"true\"");
        markup.ShouldContain("metric-create-title-copy");
        markup.ShouldContain("role=\"status\"");
        markup.ShouldContain("aria-live=\"polite\"");
        markup.ShouldContain("role=\"form\" aria-label=\"Create metric\"");
        markup.ShouldContain("role=\"search\"");
        markup.ShouldContain("title=\"@ClearMetricTypeSearchLabel\"");
        markup.ShouldContain("aria-label=\"@ClearMetricTypeSearchLabel\"");
        markup.ShouldContain("private string ClearMetricTypeSearchLabel => string.IsNullOrWhiteSpace(_search)");
        markup.ShouldContain("$\"Clear metric type search for {_search}\"");
        markup.ShouldNotContain("title=\"Clear search\"");
        markup.ShouldNotContain("aria-label=\"Clear search\"");
        markup.ShouldContain("aria-activedescendant=\"@SelectedMetricTypeOptionId\"");
        markup.ShouldContain("id=\"@MetricTypeOptionId(current)\"");
        markup.ShouldContain("aria-label=\"@MetricTypeOptionLabel(current)\"");
        markup.ShouldContain("MetricTypeOptionLabel(MetricDescriptor descriptor)");
        markup.ShouldContain("private string? SelectedMetricTypeOptionId");
        markup.ShouldContain("MetricTypeOptionId(MetricDescriptor descriptor)");
        markup.ShouldContain("SanitizeIdToken(string value)");
        System.Text.RegularExpressions.Regex.Matches(
                markup,
                @"<MudIcon\b(?:(?!/>).)*?/>",
                System.Text.RegularExpressions.RegexOptions.Singleline)
            .Cast<System.Text.RegularExpressions.Match>()
            .Select(static match => match.Value)
            .Where(static icon => !icon.Contains("aria-hidden=\"true\"", StringComparison.Ordinal))
            .ToArray()
            .ShouldBeEmpty();
        markup.ShouldContain("aria-label=\"@CancelMetricCreateLabel\"");
        markup.ShouldContain("aria-label=\"@CreateMetricLabel\"");
        markup.ShouldContain("private string CancelMetricCreateLabel => $\"Cancel metric creation for {MetricCreateTargetLabel}\"");
        markup.ShouldContain("private string CreateMetricLabel => $\"Create metric {MetricCreateTargetLabel}\"");
        markup.ShouldContain("private string MetricCreateTargetLabel => string.IsNullOrWhiteSpace(_name)");
        markup.ShouldNotContain("aria-label=\"Cancel metric creation\"");
        markup.ShouldNotContain("                       aria-label=\"Create metric\"");
        markup.ShouldContain("Color=\"Color.Primary\"");
        markup.ShouldContain("Variant=\"Variant.Filled\"");
        markup.ShouldContain("aria-invalid=\"@(!CanCreate)\"");
        markup.ShouldContain("metric-create-count");
        markup.ShouldContain("metric-create-empty\" role=\"status\" aria-live=\"polite\"");
        markup.ShouldContain("metric-create-empty-defaults\" role=\"status\" aria-live=\"polite\"");
        markup.ShouldNotContain("Class=\"metric-create-submit\"");
        markup.ShouldNotContain("metric-create-status");
        markup.ShouldNotContain("CreateStatusClass");
        markup.ShouldNotContain("CreateStatusText");
        markup.ShouldNotContain(">Ready<");
        markup.ShouldNotContain("MudStack");
        markup.ShouldNotContain("MudGrid");
        markup.ShouldNotContain("MudDivider");
        markup.ShouldNotContain("HelperText=");

        css.ShouldContain(".metric-create-title-icon");
        css.ShouldContain(".metric-create-title-copy");
        css.ShouldContain("grid-template-columns: 26px minmax(0, 1fr);");
        css.ShouldContain("border: 1px solid var(--flux-border-soft);");
        css.ShouldContain("border-radius: 6px;");
        css.ShouldContain("height: min(304px, calc(100vh - 220px));");
        css.ShouldContain("min-height: 28px;");
        css.ShouldContain(".metric-create-empty-defaults");
        css.ShouldContain("@media (max-width: 760px)");
        css.ShouldContain("@media (max-width: 520px)");
        css.ShouldNotContain(".metric-create-status");
        css.ShouldNotContain(".metric-create-status.ready");
        css.ShouldNotContain("metric-create-submit");
        css.ShouldNotContain("border-radius: 999px;");
        css.ShouldNotContain("box-shadow: 0 ");
    }

    [Fact]
    public void MetricActionDialogs_UseFlatCompactModalChrome()
    {
        var root = FindRepositoryRoot();
        var dialogPath = Path.Combine(
            root,
            "src",
            "FluxMq.UI",
            "Components",
            "Workspace",
            "Dialogs");
        var dialogs = new (string FileName, string Prefix)[]
        {
            ("MetricConfirmDialog", "metric-confirm"),
            ("MetricRenameDialog", "metric-rename"),
            ("MetricDuplicateDialog", "metric-duplicate"),
            ("MetricDeleteDialog", "metric-delete"),
            ("MetricTypeChangeDialog", "metric-type-change")
        };

        foreach (var (fileName, prefix) in dialogs)
        {
            var markup = File.ReadAllText(Path.Combine(dialogPath, $"{fileName}.razor"));
            var css = File.ReadAllText(Path.Combine(dialogPath, $"{fileName}.razor.css"));

            markup.ShouldContain($"{prefix}-title");
            markup.ShouldContain($"{prefix}-title-icon");
            markup.ShouldContain($"{prefix}-title-copy");
            markup.ShouldContain("aria-label=");
            System.Text.RegularExpressions.Regex.Matches(
                    markup,
                    @"<MudIcon\b(?:(?!/>).)*?/>",
                    System.Text.RegularExpressions.RegexOptions.Singleline)
                .Cast<System.Text.RegularExpressions.Match>()
                .Select(static match => match.Value)
                .Where(static icon => !icon.Contains("aria-hidden=\"true\"", StringComparison.Ordinal))
                .ToArray()
                .ShouldBeEmpty();
            if (prefix == "metric-confirm")
            {
                markup.ShouldContain("class=\"metric-confirm-title-icon @ToneClass\" aria-hidden=\"true\"");
            }
            else
            {
                markup.ShouldContain($"class=\"{prefix}-title-icon\" aria-hidden=\"true\"");
            }
            markup.ShouldNotContain("MudStack");
            markup.ShouldNotContain("MudGrid");
            markup.ShouldNotContain("MudDivider");
            markup.ShouldNotContain("HelperText=");

            css.ShouldContain($".{prefix}-title");
            css.ShouldContain($".{prefix}-title-icon");
            css.ShouldContain($".{prefix}-title-copy");
            if (prefix is "metric-confirm" or "metric-delete")
            {
                markup.ShouldContain($"{prefix}-tone");
                markup.ShouldNotContain($"{prefix}-status");
                markup.ShouldContain("role=\"status\"");
                markup.ShouldContain("aria-live=\"polite\"");
                css.ShouldContain($".{prefix}-tone");
                css.ShouldNotContain($".{prefix}-status");
                css.ShouldContain("grid-template-columns: 26px minmax(0, 1fr) auto;");
            }
            else
            {
                markup.ShouldNotContain($"{prefix}-status");
                markup.ShouldNotContain(">Ready<");
                css.ShouldNotContain($".{prefix}-status");
                css.ShouldContain("grid-template-columns: 26px minmax(0, 1fr);");
            }
            css.ShouldContain("border: 1px solid var(--flux-border-soft);");
            css.ShouldContain("border-radius: 6px;");
            css.ShouldContain("min-height: 28px;");
            css.ShouldContain("@media (max-width:");
            css.ShouldNotContain("border-radius: 999px;");
            css.ShouldNotContain("box-shadow: 0 ");
        }

        File.ReadAllText(Path.Combine(dialogPath, "MetricConfirmDialog.razor"))
            .ShouldContain("metric-confirm-tone");
        File.ReadAllText(Path.Combine(dialogPath, "MetricConfirmDialog.razor"))
            .ShouldNotContain("metric-confirm-status");
        File.ReadAllText(Path.Combine(dialogPath, "MetricConfirmDialog.razor"))
            .ShouldContain("role=\"status\"");
        File.ReadAllText(Path.Combine(dialogPath, "MetricConfirmDialog.razor"))
            .ShouldContain("role=\"alert\"");
        File.ReadAllText(Path.Combine(dialogPath, "MetricConfirmDialog.razor"))
            .ShouldContain("aria-label=\"@CancelConfirmationLabel\"");
        File.ReadAllText(Path.Combine(dialogPath, "MetricConfirmDialog.razor"))
            .ShouldContain("aria-label=\"@ConfirmActionLabel\"");
        File.ReadAllText(Path.Combine(dialogPath, "MetricConfirmDialog.razor"))
            .ShouldContain("private string CancelConfirmationLabel => $\"Cancel {ConfirmationTargetLabel}\"");
        File.ReadAllText(Path.Combine(dialogPath, "MetricConfirmDialog.razor"))
            .ShouldContain("private string ConfirmActionLabel => $\"{ConfirmText} for {ConfirmationTargetLabel}\"");
        File.ReadAllText(Path.Combine(dialogPath, "MetricConfirmDialog.razor"))
            .ShouldNotContain("aria-label=\"Cancel confirmation\"");
        File.ReadAllText(Path.Combine(dialogPath, "MetricDeleteDialog.razor"))
            .ShouldContain("metric-delete-tone");
        File.ReadAllText(Path.Combine(dialogPath, "MetricDeleteDialog.razor"))
            .ShouldContain("metric-delete-empty\" role=\"status\" aria-live=\"polite\"");
        File.ReadAllText(Path.Combine(dialogPath, "MetricDeleteDialog.razor"))
            .ShouldNotContain("metric-delete-status");
        File.ReadAllText(Path.Combine(dialogPath, "MetricDeleteDialog.razor"))
            .ShouldNotContain("DeleteStatusText");
        File.ReadAllText(Path.Combine(dialogPath, "MetricDeleteDialog.razor"))
            .ShouldContain("role=\"status\"");
        File.ReadAllText(Path.Combine(dialogPath, "MetricDeleteDialog.razor"))
            .ShouldContain("role=\"alert\"");
        File.ReadAllText(Path.Combine(dialogPath, "MetricDeleteDialog.razor"))
            .ShouldContain("aria-label=\"@CancelMetricDeleteLabel\"");
        File.ReadAllText(Path.Combine(dialogPath, "MetricDeleteDialog.razor"))
            .ShouldContain("aria-label=\"@DeleteMetricLabel\"");
        File.ReadAllText(Path.Combine(dialogPath, "MetricDeleteDialog.razor"))
            .ShouldContain("private string CancelMetricDeleteLabel => $\"Cancel delete for {MetricDeleteTargetLabel}\"");
        File.ReadAllText(Path.Combine(dialogPath, "MetricDeleteDialog.razor"))
            .ShouldContain("private string DeleteMetricLabel => $\"Delete {MetricDeleteTargetLabel}\"");
        File.ReadAllText(Path.Combine(dialogPath, "MetricDeleteDialog.razor"))
            .ShouldNotContain("aria-label=\"Cancel metric delete\"");
        File.ReadAllText(Path.Combine(dialogPath, "MetricDeleteDialog.razor"))
            .ShouldNotContain("aria-label=\"Delete metric\"");
        File.ReadAllText(Path.Combine(dialogPath, "MetricRenameDialog.razor"))
            .ShouldNotContain("metric-rename-status");
        File.ReadAllText(Path.Combine(dialogPath, "MetricRenameDialog.razor"))
            .ShouldNotContain("RenameStatusText");
        File.ReadAllText(Path.Combine(dialogPath, "MetricRenameDialog.razor"))
            .ShouldContain("aria-label=\"@CancelMetricRenameLabel\"");
        File.ReadAllText(Path.Combine(dialogPath, "MetricRenameDialog.razor"))
            .ShouldContain("aria-label=\"@RenameMetricLabel\"");
        File.ReadAllText(Path.Combine(dialogPath, "MetricRenameDialog.razor"))
            .ShouldContain("private string CancelMetricRenameLabel => $\"Cancel rename for {CurrentMetricTargetLabel}\"");
        File.ReadAllText(Path.Combine(dialogPath, "MetricRenameDialog.razor"))
            .ShouldContain("private string RenameMetricLabel => $\"Rename {CurrentMetricTargetLabel} to {MetricRenameTargetLabel}\"");
        File.ReadAllText(Path.Combine(dialogPath, "MetricRenameDialog.razor"))
            .ShouldNotContain("aria-label=\"Cancel metric rename\"");
        File.ReadAllText(Path.Combine(dialogPath, "MetricRenameDialog.razor"))
            .ShouldNotContain("                       aria-label=\"Rename metric\"");
        File.ReadAllText(Path.Combine(dialogPath, "MetricDuplicateDialog.razor"))
            .ShouldNotContain("metric-duplicate-status");
        File.ReadAllText(Path.Combine(dialogPath, "MetricDuplicateDialog.razor"))
            .ShouldNotContain("DuplicateStatusText");
        File.ReadAllText(Path.Combine(dialogPath, "MetricDuplicateDialog.razor"))
            .ShouldContain("aria-label=\"@CancelMetricDuplicateLabel\"");
        File.ReadAllText(Path.Combine(dialogPath, "MetricDuplicateDialog.razor"))
            .ShouldContain("aria-label=\"@DuplicateMetricLabel\"");
        File.ReadAllText(Path.Combine(dialogPath, "MetricDuplicateDialog.razor"))
            .ShouldContain("private string CancelMetricDuplicateLabel => $\"Cancel duplicate for {SourceMetricTargetLabel}\"");
        File.ReadAllText(Path.Combine(dialogPath, "MetricDuplicateDialog.razor"))
            .ShouldContain("private string DuplicateMetricLabel => $\"Duplicate {SourceMetricTargetLabel} as {DuplicateMetricTargetLabel}\"");
        File.ReadAllText(Path.Combine(dialogPath, "MetricDuplicateDialog.razor"))
            .ShouldNotContain("aria-label=\"Cancel metric duplicate\"");
        File.ReadAllText(Path.Combine(dialogPath, "MetricDuplicateDialog.razor"))
            .ShouldNotContain("                       aria-label=\"Duplicate metric\"");
        File.ReadAllText(Path.Combine(dialogPath, "MetricTypeChangeDialog.razor"))
            .ShouldNotContain("metric-type-change-status");
        File.ReadAllText(Path.Combine(dialogPath, "MetricTypeChangeDialog.razor"))
            .ShouldContain("metric-type-change-empty\" role=\"status\" aria-live=\"polite\"");
        File.ReadAllText(Path.Combine(dialogPath, "MetricTypeChangeDialog.razor"))
            .ShouldContain("metric-type-change-empty-defaults\" role=\"status\" aria-live=\"polite\"");
        File.ReadAllText(Path.Combine(dialogPath, "MetricTypeChangeDialog.razor"))
            .ShouldContain("aria-activedescendant=\"@SelectedMetricTypeOptionId\"");
        File.ReadAllText(Path.Combine(dialogPath, "MetricTypeChangeDialog.razor"))
            .ShouldContain("id=\"@MetricTypeOptionId(current)\"");
        File.ReadAllText(Path.Combine(dialogPath, "MetricTypeChangeDialog.razor"))
            .ShouldContain("aria-label=\"@MetricTypeOptionLabel(current)\"");
        File.ReadAllText(Path.Combine(dialogPath, "MetricTypeChangeDialog.razor"))
            .ShouldContain("aria-label=\"@CancelMetricTypeChangeLabel\"");
        File.ReadAllText(Path.Combine(dialogPath, "MetricTypeChangeDialog.razor"))
            .ShouldContain("aria-label=\"@SubmitTypeChangeLabel\"");
        File.ReadAllText(Path.Combine(dialogPath, "MetricTypeChangeDialog.razor"))
            .ShouldContain("private string CancelMetricTypeChangeLabel => $\"Cancel type change from {CurrentTypeTargetLabel}\"");
        File.ReadAllText(Path.Combine(dialogPath, "MetricTypeChangeDialog.razor"))
            .ShouldContain("private string SubmitTypeChangeLabel => CanChange && SelectedDescriptor is { } descriptor");
        File.ReadAllText(Path.Combine(dialogPath, "MetricTypeChangeDialog.razor"))
            .ShouldNotContain("aria-label=\"Cancel metric type change\"");
        File.ReadAllText(Path.Combine(dialogPath, "MetricTypeChangeDialog.razor"))
            .ShouldContain("title=\"@ClearMetricTypeSearchLabel\"");
        File.ReadAllText(Path.Combine(dialogPath, "MetricTypeChangeDialog.razor"))
            .ShouldContain("aria-label=\"@ClearMetricTypeSearchLabel\"");
        File.ReadAllText(Path.Combine(dialogPath, "MetricTypeChangeDialog.razor"))
            .ShouldContain("private string ClearMetricTypeSearchLabel => string.IsNullOrWhiteSpace(_search)");
        File.ReadAllText(Path.Combine(dialogPath, "MetricTypeChangeDialog.razor"))
            .ShouldContain("$\"Clear metric type search for {_search}\"");
        File.ReadAllText(Path.Combine(dialogPath, "MetricTypeChangeDialog.razor"))
            .ShouldNotContain("title=\"Clear search\"");
        File.ReadAllText(Path.Combine(dialogPath, "MetricTypeChangeDialog.razor"))
            .ShouldNotContain("aria-label=\"Clear search\"");
        File.ReadAllText(Path.Combine(dialogPath, "MetricTypeChangeDialog.razor"))
            .ShouldContain("MetricTypeOptionLabel(MetricDescriptor descriptor)");
        File.ReadAllText(Path.Combine(dialogPath, "MetricTypeChangeDialog.razor"))
            .ShouldContain("private string? SelectedMetricTypeOptionId");
        File.ReadAllText(Path.Combine(dialogPath, "MetricTypeChangeDialog.razor"))
            .ShouldContain("MetricTypeOptionId(MetricDescriptor descriptor)");
        File.ReadAllText(Path.Combine(dialogPath, "MetricTypeChangeDialog.razor"))
            .ShouldContain("SanitizeIdToken(string value)");
        File.ReadAllText(Path.Combine(dialogPath, "MetricTypeChangeDialog.razor"))
            .ShouldNotContain("TypeChangeStatusText");
        File.ReadAllText(Path.Combine(dialogPath, "MetricRenameDialog.razor.css"))
            .ShouldNotContain(".metric-rename-status");
        File.ReadAllText(Path.Combine(dialogPath, "MetricRenameDialog.razor.css"))
            .ShouldNotContain(".metric-rename-status.ready");
        File.ReadAllText(Path.Combine(dialogPath, "MetricDuplicateDialog.razor.css"))
            .ShouldNotContain(".metric-duplicate-status");
        File.ReadAllText(Path.Combine(dialogPath, "MetricDuplicateDialog.razor.css"))
            .ShouldNotContain(".metric-duplicate-status.ready");
        File.ReadAllText(Path.Combine(dialogPath, "MetricTypeChangeDialog.razor.css"))
            .ShouldNotContain(".metric-type-change-status");
        File.ReadAllText(Path.Combine(dialogPath, "MetricTypeChangeDialog.razor.css"))
            .ShouldNotContain(".metric-type-change-status.ready");
        File.ReadAllText(Path.Combine(dialogPath, "MetricTypeChangeDialog.razor.css"))
            .ShouldContain("height: min(304px, calc(100vh - 220px));");
    }

    [Fact]
    public void NewPipelineDialog_UsesFlatCompactCreateChrome()
    {
        var root = FindRepositoryRoot();
        var markup = File.ReadAllText(Path.Combine(
            root,
            "src",
            "FluxMq.UI",
            "Components",
            "Workspace",
            "Dialogs",
            "NewPipelineDialog.razor"));
        var css = File.ReadAllText(Path.Combine(
            root,
            "src",
            "FluxMq.UI",
            "Components",
            "Workspace",
            "Dialogs",
            "NewPipelineDialog.razor.css"));

        markup.ShouldContain("new-pipeline-dialog-title");
        markup.ShouldContain("new-pipeline-dialog-section");
        markup.ShouldContain("new-pipeline-dialog-actions");
        markup.ShouldContain("Disabled=\"@(!IsValid)\"");
        markup.ShouldContain("CancelAriaLabel");
        markup.ShouldContain("SubmitAriaLabel");
        markup.ShouldContain("DialogResult.Ok(_name.Trim())");
        markup.ShouldContain("<MudIcon Icon=\"@Icons.Material.Filled.AddCircle\" Size=\"Size.Small\" aria-hidden=\"true\" />");
        markup.ShouldNotContain("new-pipeline-dialog-status");
        markup.ShouldNotContain("FormStatusClass");
        markup.ShouldNotContain("FormStatusText");
        markup.ShouldNotContain(">Ready<");
        markup.ShouldNotContain("<MudIcon Icon=\"@Icons.Material.Filled.AddCircle\" Size=\"Size.Small\" />");
        markup.ShouldNotContain("MudGrid");
        markup.ShouldNotContain("MudStack");

        css.ShouldContain(".new-pipeline-dialog-title");
        css.ShouldContain(".new-pipeline-dialog-section");
        css.ShouldContain("border: 1px solid var(--flux-border-soft);");
        css.ShouldContain(".new-pipeline-dialog-field");
        css.ShouldContain("::deep(.new-pipeline-dialog-submit)");
        css.ShouldContain("@media (max-width: 480px)");
        css.ShouldContain("grid-template-columns: repeat(2, minmax(0, 1fr));");
        css.ShouldNotContain(".new-pipeline-dialog-status");
        css.ShouldNotContain(".new-pipeline-dialog-status.ready");
    }

    [Fact]
    public void SaveAsDialog_UsesFlatCompactSaveChrome()
    {
        var root = FindRepositoryRoot();
        var markup = File.ReadAllText(Path.Combine(
            root,
            "src",
            "FluxMq.UI",
            "Components",
            "Workspace",
            "Dialogs",
            "SaveAsDialog.razor"));
        var css = File.ReadAllText(Path.Combine(
            root,
            "src",
            "FluxMq.UI",
            "Components",
            "Workspace",
            "Dialogs",
            "SaveAsDialog.razor.css"));

        markup.ShouldContain("save-as-dialog-title");
        markup.ShouldContain("save-as-dialog-section");
        markup.ShouldContain("save-as-dialog-helper");
        markup.ShouldContain("save-as-dialog-actions");
        markup.ShouldContain("Disabled=\"@(!IsValid)\"");
        markup.ShouldContain("OnKeyDown");
        markup.ShouldContain("DialogResult.Ok(_path.Trim())");
        markup.ShouldContain("<MudIcon Icon=\"@Icons.Material.Filled.SaveAs\" Size=\"Size.Small\" aria-hidden=\"true\" />");
        markup.ShouldNotContain("save-as-dialog-status");
        markup.ShouldNotContain("FormStatusClass");
        markup.ShouldNotContain("FormStatusText");
        markup.ShouldNotContain(">Ready<");
        markup.ShouldNotContain("<MudIcon Icon=\"@Icons.Material.Filled.SaveAs\" Size=\"Size.Small\" />");
        markup.ShouldNotContain("HelperText=");
        markup.ShouldNotContain("MudStack");

        css.ShouldContain(".save-as-dialog-title");
        css.ShouldContain(".save-as-dialog-section");
        css.ShouldContain("border: 1px solid var(--flux-border-soft);");
        css.ShouldContain(".save-as-dialog-helper");
        css.ShouldContain("overflow-wrap: anywhere;");
        css.ShouldContain("::deep(.save-as-dialog-submit)");
        css.ShouldContain("@media (max-width: 480px)");
        css.ShouldContain("grid-template-columns: repeat(2, minmax(0, 1fr));");
        css.ShouldNotContain(".save-as-dialog-status");
        css.ShouldNotContain(".save-as-dialog-status.ready");
    }

    [Fact]
    public void StartRecordingDialog_UsesFlatCompactRecordingChrome()
    {
        var root = FindRepositoryRoot();
        var markup = File.ReadAllText(Path.Combine(
            root,
            "src",
            "FluxMq.UI",
            "Components",
            "Workspace",
            "Dialogs",
            "StartRecordingDialog.razor"));
        var css = File.ReadAllText(Path.Combine(
            root,
            "src",
            "FluxMq.UI",
            "Components",
            "Workspace",
            "Dialogs",
            "StartRecordingDialog.razor.css"));

        markup.ShouldContain("start-recording-title");
        markup.ShouldContain("start-recording-title-icon");
        markup.ShouldContain("start-recording-title-copy");
        markup.ShouldContain("role=\"form\" aria-label=\"Start recording\"");
        markup.ShouldContain("start-recording-section");
        markup.ShouldContain("start-recording-fields");
        markup.ShouldContain("start-recording-summary");
        markup.ShouldContain("aria-label=\"Recording project\"");
        markup.ShouldContain("aria-label=\"Recording session name\"");
        markup.ShouldContain("aria-label=\"@StartRecordingLabel\"");
        markup.ShouldContain("aria-label=\"@CancelRecordingLabel\"");
        markup.ShouldContain("private string StartRecordingLabel => $\"Start recording {RecordingTargetLabel}\";");
        markup.ShouldContain("private string CancelRecordingLabel => $\"Cancel recording {RecordingTargetLabel}\";");
        markup.ShouldContain("private string RecordingTargetLabel => $\"{RecordingSessionName} in {RecordingProjectName}\";");
        markup.ShouldContain("private string RecordingProjectName => string.IsNullOrWhiteSpace(_project) ? \"Default\" : _project.Trim();");
        markup.ShouldContain("private string RecordingSessionName => string.IsNullOrWhiteSpace(_session) ? DefaultSessionName : _session.Trim();");
        markup.ShouldNotContain("                       aria-label=\"Start recording\"");
        markup.ShouldNotContain("aria-label=\"Cancel recording\"");
        markup.ShouldContain("DefaultSessionName");
        markup.ShouldContain("ProjectSummaryText");
        markup.ShouldContain("OnKeyDown");
        markup.ShouldContain("StartRecordingResult(project, session)");
        markup.ShouldContain("<MudIcon Icon=\"@Icons.Material.Filled.FiberManualRecord\" Size=\"Size.Small\" aria-hidden=\"true\" />");
        markup.ShouldContain("<MudIcon Icon=\"@Icons.Material.Filled.FolderOpen\" Size=\"Size.Small\" aria-hidden=\"true\" />");
        markup.ShouldNotContain("start-recording-status");
        markup.ShouldNotContain(">Ready<");
        markup.ShouldNotContain("<MudIcon Icon=\"@Icons.Material.Filled.FiberManualRecord\" Size=\"Size.Small\" />");
        markup.ShouldNotContain("<MudIcon Icon=\"@Icons.Material.Filled.FolderOpen\" Size=\"Size.Small\" />");
        markup.ShouldNotContain("MudStack");
        markup.ShouldNotContain("<MudText Typo=\"Typo.h6\">Start Recording</MudText>");

        css.ShouldContain(".start-recording-title");
        css.ShouldContain(".start-recording-title-icon");
        css.ShouldContain(".start-recording-title-copy");
        css.ShouldContain(".start-recording-section");
        css.ShouldContain("border: 1px solid var(--flux-border-soft);");
        css.ShouldContain("border-radius: 6px;");
        css.ShouldContain(".start-recording-field ::deep(.mud-input-root)");
        css.ShouldContain(".start-recording-summary");
        css.ShouldContain(".start-recording-actions");
        css.ShouldContain("min-height: 28px;");
        css.ShouldContain("@media (max-width: 520px)");
        css.ShouldNotContain(".start-recording-status");
        css.ShouldNotContain("border-radius: 999px;");
        css.ShouldNotContain("box-shadow: 0 ");
    }

    [Fact]
    public void PayloadInspectorPanel_UsesFlatCompactInspectorChrome()
    {
        var root = FindRepositoryRoot();
        var markup = File.ReadAllText(Path.Combine(
            root,
            "src",
            "FluxMq.UI",
            "Components",
            "Payloads",
            "PayloadInspectorPanel.razor"));
        var css = File.ReadAllText(Path.Combine(
            root,
            "src",
            "FluxMq.UI",
            "Components",
            "Payloads",
            "PayloadInspectorPanel.razor.css"));

        markup.ShouldContain("<section class=\"payload-inspector\" aria-label=\"@Title\">");
        markup.ShouldContain("payload-inspector-header");
        markup.ShouldContain("class=\"payload-format-marker @FormatMarkerClass\" aria-hidden=\"true\"");
        markup.ShouldContain("<MudIcon Icon=\"@FormatIcon\" Size=\"Size.Small\" aria-hidden=\"true\" />");
        markup.ShouldNotContain("<MudIcon Icon=\"@FormatIcon\" Size=\"Size.Small\" />");
        markup.ShouldContain("payload-meta-strip");
        markup.ShouldContain("payload-view-switch");
        markup.ShouldContain("<MudIcon Icon=\"@Icons.Material.Filled.DataObject\" Size=\"Size.Small\" aria-hidden=\"true\" />");
        markup.ShouldContain("<MudIcon Icon=\"@Icons.Material.Filled.Subject\" Size=\"Size.Small\" aria-hidden=\"true\" />");
        markup.ShouldContain("<MudIcon Icon=\"@Icons.Material.Filled.Tag\" Size=\"Size.Small\" aria-hidden=\"true\" />");
        markup.ShouldContain("<MudIcon Icon=\"@Icons.Material.Filled.Info\" Size=\"Size.Small\" aria-hidden=\"true\" />");
        markup.ShouldNotContain("<MudIcon Icon=\"@Icons.Material.Filled.DataObject\" Size=\"Size.Small\" />");
        markup.ShouldNotContain("<MudIcon Icon=\"@Icons.Material.Filled.Subject\" Size=\"Size.Small\" />");
        markup.ShouldNotContain("<MudIcon Icon=\"@Icons.Material.Filled.Tag\" Size=\"Size.Small\" />");
        markup.ShouldNotContain("<MudIcon Icon=\"@Icons.Material.Filled.Info\" Size=\"Size.Small\" />");
        markup.ShouldContain("id=\"@PayloadViewTabId(FormattedView)\"");
        markup.ShouldContain("role=\"tab\"");
        System.Text.RegularExpressions.Regex.Matches(markup, "aria-keyshortcuts=\"Enter Space ArrowLeft ArrowRight Home End\"").Count.ShouldBe(4);
        markup.ShouldContain("aria-selected=\"@IsActiveView(FormattedView)\"");
        markup.ShouldContain("aria-controls=\"@PayloadViewPanelId\"");
        markup.ShouldContain("@onkeydown=\"@OnFormattedViewTabKeyDown\"");
        markup.ShouldContain("@onkeydown=\"@OnRawViewTabKeyDown\"");
        markup.ShouldContain("@onkeydown=\"@OnHexViewTabKeyDown\"");
        markup.ShouldContain("@onkeydown=\"@OnMetaViewTabKeyDown\"");
        markup.ShouldContain("PayloadViewOrder");
        markup.ShouldContain("SelectPayloadViewFromKeyboard");
        markup.ShouldContain("ResolvePayloadView");
        markup.ShouldContain("role=\"tabpanel\"");
        markup.ShouldContain("aria-labelledby=\"@PayloadViewTabId(_activeView)\"");
        markup.ShouldContain("tabindex=\"0\"");
        markup.ShouldContain("payload-inspector-meta-list");
        markup.ShouldContain("private const string FormattedView = \"formatted\";");
        markup.ShouldContain("private readonly string _payloadViewIdPrefix");
        markup.ShouldContain("private string PayloadViewPanelId");
        markup.ShouldContain("private string PayloadViewTabId(string view)");
        markup.ShouldContain("private string FormatMarkerClass");
        markup.ShouldContain("private string FormatIcon");
        markup.ShouldNotContain("payload-format-state");
        markup.ShouldNotContain("private string FormatClass");
        markup.ShouldNotContain("payload-format-badge");
        markup.ShouldNotContain("MudToggleGroup");
        markup.ShouldNotContain("MudChip");
        markup.ShouldNotContain("<MudPaper");

        css.ShouldContain("border-radius: 5px;");
        css.ShouldContain(".payload-format-marker");
        css.ShouldContain(".payload-format-marker.scalar");
        css.ShouldContain(".payload-format-marker.structured");
        css.ShouldContain(".payload-format-marker.binary");
        css.ShouldContain(".payload-inspector-header");
        css.ShouldContain("min-height: 38px;");
        css.ShouldContain(".payload-view-switch");
        css.ShouldContain("grid-template-columns: repeat(4, minmax(0, 1fr));");
        css.ShouldContain(".payload-view-button.active");
        css.ShouldContain("white-space: pre-wrap;");
        css.ShouldContain("overflow-wrap: anywhere;");
        css.ShouldContain(".payload-inspector-meta-list div");
        css.ShouldContain("grid-template-columns: 76px minmax(0, 1fr);");
        css.ShouldContain("@media (max-width: 520px)");
        css.ShouldNotContain(".payload-format-state");
        css.ShouldNotContain(".payload-format-badge");
    }

    [Fact]
    public void EmptyView_HidesDecorativeInboxIcon()
    {
        var root = FindRepositoryRoot();
        var markup = File.ReadAllText(Path.Combine(
            root,
            "src",
            "FluxMq.UI",
            "Components",
            "EmptyView.razor"));

        markup.ShouldContain("<div class=\"empty-view\" role=\"status\" aria-live=\"polite\">");
        markup.ShouldContain("<MudIcon Icon=\"@Icons.Material.Outlined.Inbox\" Color=\"Color.Secondary\" Size=\"Size.Large\" aria-hidden=\"true\" />");
        markup.ShouldContain("@(Message ?? \"No data\")");
        markup.ShouldNotContain("<MudIcon Icon=\"@Icons.Material.Outlined.Inbox\" Color=\"Color.Secondary\" Size=\"Size.Large\" />");
    }

    [Fact]
    public void WorkspaceLogPanel_UsesFlatCompactWorkspaceChrome()
    {
        var root = FindRepositoryRoot();
        var markup = File.ReadAllText(Path.Combine(
            root,
            "src",
            "FluxMq.UI",
            "Components",
            "Workspace",
            "WorkspaceLogPanel.razor"));
        var css = File.ReadAllText(Path.Combine(
            root,
            "src",
            "FluxMq.UI",
            "Components",
            "Workspace",
            "WorkspaceLogPanel.razor.css"));

        markup.ShouldContain("Logs.Count > 0");
        markup.ShouldContain("role=\"search\" aria-label=\"@WorkspaceLogFiltersLabel\"");
        markup.ShouldContain("private string WorkspaceLogFiltersLabel => string.IsNullOrWhiteSpace(ForcedScope)");
        markup.ShouldContain("$\"{ScopeLabel(ForcedScope)} workspace log filters\"");
        markup.ShouldContain("role=\"log\" aria-label=\"@WorkspaceLogListLabel\"");
        markup.ShouldContain("private string WorkspaceLogListLabel => HasActiveFilters");
        markup.ShouldContain("FormatLogCount(FilteredLogs.Count)");
        markup.ShouldContain("private static string FormatLogCount(int count)");
        markup.ShouldContain("workspace-log-stats");
        markup.ShouldContain("aria-label=\"@WorkspaceLogActionsLabel\"");
        markup.ShouldContain("private string WorkspaceLogActionsLabel => Logs.Count == 0");
        markup.ShouldContain("$\"Workspace log commands, {FormatLogCount(Logs.Count)} total\"");
        markup.ShouldContain("aria-label=\"@WorkspaceLogStatsLabel\"");
        markup.ShouldContain("private string WorkspaceLogStatsLabel => ProblemCount > 0");
        markup.ShouldContain("$\"Workspace log totals, {FormatLogCount(Logs.Count)} total and {ProblemCount} {ProblemCountLabel}\"");
        markup.ShouldNotContain("aria-label=\"Workspace log commands\"");
        markup.ShouldNotContain("aria-label=\"Workspace log total and problem count\"");
        markup.ShouldNotContain("aria-label=\"Log commands\"");
        markup.ShouldNotContain("aria-label=\"Log summary\"");
        markup.ShouldNotContain("role=\"search\" aria-label=\"Log filters\"");
        markup.ShouldNotContain("role=\"log\" aria-label=\"Workspace logs\"");
        System.Text.RegularExpressions.Regex.Matches(
                markup,
                @"<MudIcon\b(?:(?!/>).)*?/>",
                System.Text.RegularExpressions.RegexOptions.Singleline)
            .Cast<System.Text.RegularExpressions.Match>()
            .Select(static match => match.Value)
            .Where(static icon => !icon.Contains("aria-hidden=\"true\"", StringComparison.Ordinal))
            .ToArray()
            .ShouldBeEmpty();
        markup.ShouldContain("WorkspaceLogFilter.Problems");
        markup.ShouldContain("workspace-log-segment");
        markup.ShouldContain("aria-label=\"@ScopeFilterGroupLabel\"");
        markup.ShouldContain("aria-label=\"@SeverityFilterGroupLabel\"");
        markup.ShouldContain("private string ScopeFilterGroupLabel");
        markup.ShouldContain("$\"Workspace log scope filter, {ScopeOptions.Count.ToString(CultureInfo.InvariantCulture)} options\"");
        markup.ShouldContain("private string SeverityFilterGroupLabel");
        markup.ShouldContain("$\"Workspace log level filter, {SeverityOptions.Length.ToString(CultureInfo.InvariantCulture)} options\"");
        markup.ShouldNotContain("aria-label=\"Workspace log scope filter\"");
        markup.ShouldNotContain("aria-label=\"Workspace log level filter\"");
        markup.ShouldContain("aria-label=\"@FixedScopeLabel\"");
        markup.ShouldContain("private string FixedScopeLabel");
        markup.ShouldContain("$\"{ScopeLabel(ForcedScope)} fixed workspace log scope\"");
        markup.ShouldContain("aria-label=\"@LogSearchLabel\"");
        markup.ShouldContain("private string LogSearchLabel");
        markup.ShouldContain("$\"Search {ScopeLabel(EffectiveScope).ToLowerInvariant()} workspace logs\"");
        markup.ShouldNotContain("aria-label=\"Fixed scope\"");
        markup.ShouldNotContain("aria-label=\"Search logs\"");
        markup.ShouldNotContain("aria-label=\"Scope filter\"");
        markup.ShouldNotContain("aria-label=\"Level filter\"");
        markup.ShouldContain("aria-label=\"@ScopeFilterButtonLabel(scope)\"");
        markup.ShouldContain("aria-label=\"@SeverityFilterButtonLabel(severity)\"");
        markup.ShouldContain("private static string ScopeFilterButtonLabel(string scope)");
        markup.ShouldContain("private static string SeverityFilterButtonLabel(string severity)");
        markup.ShouldContain("[Parameter] public WorkspaceLogQuery? InitialQuery { get; set; }");
        markup.ShouldContain("InitialQuery.Equals(_appliedInitialQuery)");
        markup.ShouldContain("_severity = string.IsNullOrWhiteSpace(InitialQuery.Severity)");
        markup.ShouldContain("_search = InitialQuery.Search");
        markup.ShouldContain("Text=\"@CopyVisibleLogsLabel\"");
        markup.ShouldContain("Text=\"@ExportVisibleLogsLabel\"");
        markup.ShouldContain("Text=\"@ResetLogFiltersLabel\"");
        markup.ShouldContain("Text=\"@ClearLogsLabel\"");
        markup.ShouldContain("aria-label=\"@CopyVisibleLogsLabel\"");
        markup.ShouldContain("aria-label=\"@ExportVisibleLogsLabel\"");
        markup.ShouldContain("aria-label=\"@ResetLogFiltersLabel\"");
        markup.ShouldContain("aria-label=\"@ClearLogsLabel\"");
        markup.ShouldContain("private string VisibleLogActionTargetLabel => HasActiveFilters");
        markup.ShouldContain("private string CopyVisibleLogsLabel => $\"Copy {VisibleLogActionTargetLabel}\"");
        markup.ShouldContain("private string ExportVisibleLogsLabel => $\"Export {VisibleLogActionTargetLabel}\"");
        markup.ShouldContain("private string ResetLogFiltersLabel => string.IsNullOrWhiteSpace(ForcedScope)");
        markup.ShouldContain("private string ClearLogsLabel => $\"Clear {FormatLogCount(Logs.Count)}\"");
        markup.ShouldNotContain("Text=\"Copy visible logs\"");
        markup.ShouldNotContain("Text=\"Export visible logs\"");
        markup.ShouldNotContain("Text=\"Reset filters\"");
        markup.ShouldNotContain("Text=\"Clear logs\"");
        markup.ShouldNotContain("aria-label=\"Copy visible logs\"");
        markup.ShouldNotContain("aria-label=\"Export visible logs\"");
        markup.ShouldNotContain("aria-label=\"Reset log filters\"");
        markup.ShouldNotContain("aria-label=\"Clear logs\"");
        markup.ShouldContain("Disabled=\"@(FilteredLogs.Count == 0)\"");
        markup.ShouldContain("CopyVisibleLogsAsync");
        markup.ShouldContain("ExportVisibleLogsAsync");
        markup.ShouldContain("Clipboard.Default.SetTextAsync(BuildVisibleLogsText(logs))");
        markup.ShouldContain("ShowAsync<SaveAsDialog>");
        markup.ShouldContain("[nameof(SaveAsDialog.SubmitText)] = \"Export\"");
        markup.ShouldContain("BuildVisibleLogsJson(logs)");
        markup.ShouldContain("SuggestedLogExportPath");
        markup.ShouldContain("workspace-logs-{stamp}.json");
        markup.ShouldContain("WriteLogExportAsync");
        markup.ShouldContain("File.WriteAllTextAsync(fullPath, content)");
        markup.ShouldContain("aria-label=\"@WorkspaceLogRowLabel(entry)\"");
        markup.ShouldContain("private static string WorkspaceLogRowLabel(WorkspaceLogEntry entry)");
        markup.ShouldContain("$\"Workspace log row, {SeverityLabel(entry)} at {FormatLogTime(entry)} on {FormatLogDate(entry)}, {EntryScopeLabel(entry)}, {SourceCode(entry)}\"");
        markup.ShouldContain("workspace-log-row-icon");
        markup.ShouldContain("title=\"@WorkspaceLogSeverityTitle(entry)\"");
        markup.ShouldContain("private static string WorkspaceLogSeverityTitle(WorkspaceLogEntry entry)");
        markup.ShouldContain("$\"{SeverityLabel(entry)} log severity\"");
        markup.ShouldNotContain("aria-label=\"@($\"{SeverityLabel(entry)} log at {FormatLogTime(entry)} from {SourceCode(entry)}\")\"");
        markup.ShouldNotContain("title=\"@SeverityLabel(entry)\"");
        markup.ShouldContain("DetailLabels(entry)");
        markup.ShouldContain("SeverityIcon(entry)");
        markup.ShouldContain("workspace-log-empty\" role=\"status\" aria-live=\"polite\"");

        css.ShouldContain("padding: 7px 10px;");
        css.ShouldContain("height: 26px;");
        css.ShouldContain("font-size: 14px;");
        css.ShouldContain("grid-template-columns: minmax(210px, 0.82fr) minmax(260px, 1fr) minmax(220px, 320px);");
        css.ShouldContain("padding: 5px 8px;");
        css.ShouldContain("--workspace-log-control-height: 40px;");
        css.ShouldContain("--workspace-log-segment-button-height: 30px;");
        css.ShouldContain(".workspace-log-filter-block");
        css.ShouldContain("grid-template-columns: auto minmax(0, 1fr);");
        css.ShouldContain("min-height: var(--workspace-log-control-height);");
        css.ShouldContain("flex-wrap: nowrap;");
        css.ShouldContain("height: var(--workspace-log-segment-button-height);");
        css.ShouldContain("overflow-x: auto;");
        css.ShouldContain(".workspace-log-filter-button.active");
        css.ShouldContain(".workspace-log-search ::deep .mud-input-root");
        css.ShouldContain(".workspace-log-search ::deep .mud-input-control-input-container");
        css.ShouldContain(".workspace-log-search ::deep .mud-input-adornment");
        css.ShouldContain("height: var(--workspace-log-control-height);");
        css.ShouldContain("min-height: var(--workspace-log-control-height);");
        css.ShouldNotContain("min-height: 30px;");
        css.ShouldContain("grid-template-columns: 24px 70px minmax(130px, 0.34fr) minmax(0, 1fr);");
        css.ShouldContain(".workspace-log-row::before");
        css.ShouldContain("width: 3px;");
        css.ShouldContain(".workspace-log-row-meta span");
        css.ShouldContain("grid-template-columns: minmax(0, min(360px, 100%));");
        css.ShouldContain("justify-items: center;");
        css.ShouldContain("text-align: center;");
        css.ShouldContain("min-height: 0;");
        css.ShouldContain(".workspace-log-empty ::deep .mud-icon-root");
        css.ShouldContain("overflow-wrap: anywhere;");
        css.ShouldContain("grid-template-columns: 24px 60px minmax(0, 1fr);");
        css.ShouldContain("grid-template-columns: 42px minmax(0, 1fr);");
        markup.ShouldNotContain("Action needed");
        markup.ShouldNotContain("Show problems");
        markup.ShouldNotContain("ProblemStatus");
        markup.ShouldNotContain("LogExportService");
        markup.ShouldNotContain("PersistLog");
        markup.ShouldNotContain("SaveLogFilter");
        css.ShouldNotContain(".workspace-log-status");
        css.ShouldNotContain("grid-template-columns: 72px 86px minmax(0, 1fr);");
    }

    [Fact]
    public void AppJsonPanel_UsesFlatCompactCodeViewerChrome()
    {
        var root = FindRepositoryRoot();
        var markup = File.ReadAllText(Path.Combine(
            root,
            "src",
            "FluxMq.UI",
            "Components",
            "Workspace",
            "AppJsonPanel.razor"));
        var css = File.ReadAllText(Path.Combine(
            root,
            "src",
            "FluxMq.UI",
            "Components",
            "Workspace",
            "AppJsonPanel.razor.css"));

        markup.ShouldContain("aria-label=\"@AppJsonToolbarLabel\"");
        markup.ShouldContain("private string AppJsonToolbarLabel => Project.HasUnsavedChanges");
        markup.ShouldContain("$\"Application JSON toolbar for {Project.Name}, unsaved changes\"");
        markup.ShouldContain("$\"Application JSON toolbar for {Project.Name}\"");
        markup.ShouldNotContain("aria-label=\"Application JSON toolbar\"");
        markup.ShouldContain("class=\"app-json-title-icon\" aria-hidden=\"true\"");
        System.Text.RegularExpressions.Regex.Matches(
                markup,
                @"<MudIcon\b(?:(?!/>).)*?/>",
                System.Text.RegularExpressions.RegexOptions.Singleline)
            .Cast<System.Text.RegularExpressions.Match>()
            .Select(static match => match.Value)
            .Where(static icon => !icon.Contains("aria-hidden=\"true\"", StringComparison.Ordinal))
            .ToArray()
            .ShouldBeEmpty();
        markup.ShouldContain("<strong>App JSON</strong>");
        markup.ShouldContain("@FileLabel");
        markup.ShouldContain("aria-label=\"@AppJsonMetaLabel\"");
        markup.ShouldContain("private string AppJsonMetaLabel => $\"Application JSON summary for {Project.Name}: {JsonLineCountLabel}, {JsonSizeLabel}\"");
        markup.ShouldContain("private string JsonLineCountLabel => JsonLineCount == 1 ? \"1 line\" : $\"{JsonLineCount} lines\"");
        markup.ShouldNotContain("aria-label=\"Application JSON line and size summary\"");
        markup.ShouldContain("@JsonLineCount lines");
        markup.ShouldContain("@JsonSizeLabel");
        markup.ShouldContain("app-json-unsaved-indicator");
        markup.ShouldContain("role=\"status\" aria-live=\"polite\">Unsaved</span>");
        markup.ShouldNotContain("app-json-state");
        markup.ShouldContain("Text=\"@CopyAppJsonLabel\"");
        markup.ShouldContain("aria-label=\"@CopyAppJsonLabel\"");
        markup.ShouldContain("private string CopyAppJsonLabel => $\"Copy application JSON for {Project.Name}\"");
        markup.ShouldNotContain("Text=\"Copy JSON\"");
        markup.ShouldNotContain("aria-label=\"Copy JSON\"");
        markup.ShouldContain("Disabled=\"@string.IsNullOrWhiteSpace(_fullJson)\"");
        markup.ShouldContain("app-json-editor-shell");
        markup.ShouldContain("app-json-empty");
        markup.ShouldContain("No JSON available");
        markup.ShouldContain("aria-label=\"@AppJsonEditorLabel\"");
        markup.ShouldContain("private string AppJsonEditorLabel => $\"Application definition JSON for {Project.Name}\"");
        markup.ShouldNotContain("aria-label=\"Application definition JSON\"");
        markup.ShouldContain("<StandaloneCodeEditor @ref=\"_editor\"");
        markup.ShouldContain("CssClass=\"app-json-monaco-editor\"");
        markup.ShouldContain("ConstructionOptions=\"@EditorConstructionOptions\"");
        markup.ShouldContain("private StandaloneCodeEditor? _editor;");
        markup.ShouldContain("private StandaloneEditorConstructionOptions EditorConstructionOptions");
        markup.ShouldContain("Language = \"json\"");
        markup.ShouldContain("ReadOnly = true");
        markup.ShouldContain("ScrollBeyondLastLine = false");
        markup.ShouldContain("fluxmqMonaco.ensureConfigured");
        markup.ShouldContain("private async Task ConfigureMonacoAsync()");
        markup.ShouldContain("private async Task SyncEditorAsync()");
        markup.ShouldContain("private static bool IsNonCriticalEditorException(Exception exception)");
        markup.ShouldContain("App JSON Monaco configuration failed");
        markup.ShouldContain("App JSON Monaco sync failed");
        markup.ShouldContain("JSException or JSDisconnectedException");
        markup.ShouldContain("private string FileLabel");
        markup.ShouldContain("private int JsonLineCount");
        markup.ShouldContain("private string JsonSizeLabel");
        markup.ShouldNotContain("<pre class=\"app-json-body\"");
        markup.ShouldNotContain("MudChip");
        markup.ShouldNotContain("MudStack Row=\"true\"");
        markup.ShouldNotContain("Visual view");
        markup.ShouldNotContain("app-json-view-button");
        markup.ShouldNotContain("aria-label=\"JSON summary\"");
        markup.ShouldNotContain("ReturnToVisualViewAsync");

        css.ShouldContain("grid-template-columns: minmax(0, 1fr) auto auto auto;");
        css.ShouldContain("min-height: 38px;");
        css.ShouldContain("padding: 5px 8px;");
        css.ShouldContain("height: 26px;");
        css.ShouldContain(".app-json-meta span,");
        css.ShouldContain(".app-json-unsaved-indicator");
        css.ShouldContain(".app-json-unsaved-indicator::before");
        css.ShouldContain("border-radius: 50%;");
        css.ShouldNotContain("border-radius: 999px;");
        css.ShouldNotContain(".app-json-state");
        css.ShouldContain(".app-json-toolbar ::deep .mud-icon-button");
        css.ShouldContain(".app-json-editor-shell ::deep .app-json-monaco-editor");
        css.ShouldContain(".app-json-editor-shell ::deep .app-json-monaco-editor .monaco-editor,");
        css.ShouldContain("overflow: hidden;");
        css.ShouldNotContain(".app-json-body");
        css.ShouldNotContain("app-json-view-button");
        css.ShouldContain(".app-json-editor-shell");
        css.ShouldContain("margin: 6px;");
        css.ShouldContain(".app-json-empty");
        css.ShouldContain("grid-template-columns: minmax(0, min(360px, 100%));");
        css.ShouldContain("justify-items: center;");
        css.ShouldContain("text-align: center;");
        css.ShouldContain("overflow-wrap: anywhere;");
        css.ShouldContain("@media (max-width: 760px)");
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
    public void DashboardDesigner_UsesContainerResponsiveLiveGridWithoutRepackingEditGrid()
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
        css.ShouldContain("@container dashboard-layout (max-width: 540px)");
        css.ShouldContain("--dashboard-grid-column-min: 96px;");
        css.ShouldContain("--dashboard-grid-row-min: 128px;");
        css.ShouldContain("--dashboard-grid-column-min: 0px;");
        css.ShouldContain("--dashboard-grid-row-min: 112px;");
        var normalizedCss = css.ReplaceLineEndings("\n");
        normalizedCss.ShouldContain(".dashboard-grid-stage {\n    display: grid;\n    flex: 1 1 auto;\n    grid-template-columns: 42px minmax(max-content, 1fr);");
        normalizedCss.ShouldContain(".dashboard-grid {\n    background: color-mix(in srgb, var(--flux-surface-2) 20%, transparent);\n    border: 1px solid color-mix(in srgb, var(--mud-palette-text-secondary) 10%, var(--flux-border));\n    border-radius: 5px;\n    box-sizing: border-box;");
        normalizedCss.ShouldContain("min-width: max-content;");
        normalizedCss.ShouldContain(".dashboard-live-grid {\n        grid-column: 1;\n        grid-row: 1;\n        grid-template-columns: repeat(2, minmax(var(--dashboard-grid-column-min, 156px), 1fr)) !important;");
        normalizedCss.ShouldContain(".dashboard-live-cell {\n        grid-column: span var(--dashboard-cell-tablet-span, 1) !important;");
        normalizedCss.ShouldContain(".dashboard-live-cell {\n        grid-column: span var(--dashboard-cell-mobile-span, 1) !important;");
        normalizedCss.ShouldNotContain(".dashboard-grid,\n    .dashboard-live-grid");
        normalizedCss.ShouldNotContain(".dashboard-cell,\n    .dashboard-live-cell");
        css.ShouldContain("grid-auto-rows: minmax(var(--dashboard-grid-row-min, 136px), 1fr);");
    }

    [Fact]
    public void DashboardDesigner_UsesFlatCompactDashboardToolbar()
    {
        var root = FindRepositoryRoot();
        var razor = File.ReadAllText(Path.Combine(
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

        razor.ShouldContain("role=\"region\" aria-label=\"@DashboardDesignerLabel\"");
        razor.ShouldContain("private string DashboardDesignerLabel => string.IsNullOrWhiteSpace(Project.ActiveDashboardName)");
        razor.ShouldContain("$\"{DashboardTitle} dashboard designer\"");
        razor.ShouldNotContain("role=\"region\" aria-label=\"Dashboard designer\"");
        razor.ShouldContain("class=\"dashboard-toolbar\" role=\"toolbar\" aria-label=\"@DashboardToolbarLabel\"");
        razor.ShouldContain("private string DashboardToolbarLabel => $\"{DashboardTitle} dashboard toolbar\";");
        razor.ShouldNotContain("aria-label=\"Dashboard toolbar\"");
        razor.ShouldContain("dashboard-meta-strip");
        razor.ShouldContain("aria-label=\"@DashboardMetaSummaryLabel\"");
        razor.ShouldContain("@DashboardSummaryLabel");
        razor.ShouldContain("aria-label=\"@DashboardToolbarActionsLabel\"");
        razor.ShouldContain("aria-label=\"@DashboardEditCommandsLabel\"");
        razor.ShouldContain("aria-label=\"@GridLayoutCommandsLabel\"");
        razor.ShouldContain("aria-label=\"@SelectionCommandsLabel\"");
        razor.ShouldContain("private string DashboardMetaSummaryLabel");
        razor.ShouldContain("private string DashboardToolbarActionsLabel");
        razor.ShouldContain("private string DashboardEditCommandsLabel");
        razor.ShouldContain("private string GridLayoutCommandsLabel");
        razor.ShouldContain("private string SelectionCommandsLabel");
        razor.ShouldNotContain("@DashboardStatusLabel");
        razor.ShouldNotContain("aria-label=\"Dashboard summary\"");
        razor.ShouldNotContain("aria-label=\"Dashboard mode and commands\"");
        razor.ShouldNotContain("aria-label=\"Dashboard edit commands\"");
        razor.ShouldNotContain("aria-label=\"Grid layout commands\"");
        razor.ShouldNotContain("aria-label=\"Selection commands\"");
        razor.Split('\n')
            .Where(static line => line.Contains("<MudIcon ", StringComparison.Ordinal) &&
                !line.Contains("aria-hidden=\"true\"", StringComparison.Ordinal))
            .ToArray()
            .ShouldBeEmpty();
        razor.ShouldContain("@GridSizeLabel");
        razor.ShouldContain("@CellCountLabel");
        razor.ShouldContain("@WidgetCountLabel");
        razor.ShouldContain("dashboard-mode-shell");
        razor.ShouldContain("DashboardModeIndicatorClass");
        razor.ShouldNotContain("DashboardModeStateClass");
        razor.ShouldNotContain("dashboard-mode-state");
        razor.ShouldContain("dashboard-tool-group dashboard-tool-group-grid");
        razor.ShouldContain("dashboard-tool-group dashboard-tool-group-selection");
        razor.ShouldContain("aria-label=\"@DashboardLayoutEditorLabel\"");
        razor.ShouldContain("private string DashboardLayoutEditorLabel => $\"{DashboardTitle} dashboard layout editor\";");
        razor.ShouldNotContain("aria-label=\"Dashboard layout editor\"");
        razor.ShouldContain("role=\"grid\" aria-label=\"@DashboardLayoutGridLabel\"");
        razor.ShouldContain("private string DashboardLayoutGridLabel => $\"{DashboardTitle} editable dashboard layout grid\";");
        razor.ShouldNotContain("aria-label=\"Dashboard layout grid\"");
        razor.ShouldContain("aria-multiselectable=\"true\"");
        razor.ShouldContain("aria-label=\"@LiveDashboardGridLabel\"");
        razor.ShouldContain("private string LiveDashboardGridLabel => $\"{DashboardTitle} live dashboard grid\";");
        razor.ShouldNotContain("aria-label=\"Live dashboard grid\"");
        razor.ShouldContain("aria-label=\"@CellAriaLabel(currentCell)\"");
        razor.ShouldContain("title=\"@CellAriaLabel(currentCell)\"");
        razor.ShouldContain("tabindex=\"0\"");
        razor.ShouldContain("SelectCellFromKeyboard");
        razor.ShouldContain("private static bool IsActivationKey");
        razor.ShouldContain("Text=\"@GridPickerButtonLabel\"");
        razor.ShouldContain("aria-label=\"@GridPickerButtonLabel\"");
        razor.ShouldNotContain("Text=\"Set grid layout\"");
        razor.ShouldContain("Text=\"@MergeSelectedCellsLabel\"");
        razor.ShouldContain("aria-label=\"@MergeSelectedCellsLabel\"");
        razor.ShouldContain("private string MergeSelectedCellsLabel => SelectedCells.Count switch");
        razor.ShouldContain("$\"Merge {SelectedCells.Count.ToString(CultureInfo.InvariantCulture)} selected cells in {DashboardTitle}\"");
        razor.ShouldNotContain("Text=\"Merge selected cells\"");
        razor.ShouldNotContain("aria-label=\"Merge selected cells\"");
        razor.ShouldContain("Text=\"@SplitPickerButtonLabel\"");
        razor.ShouldContain("aria-label=\"@SplitPickerButtonLabel\"");
        razor.ShouldNotContain("Text=\"Split selected cell\"");
        razor.ShouldContain("aria-label=\"@CloseGridPickerLabel\"");
        razor.ShouldContain("private string CloseGridPickerLabel => $\"Close {DashboardTitle} grid layout picker\";");
        razor.ShouldNotContain("aria-label=\"Close grid layout picker\"");
        razor.ShouldContain("@onclick=\"@CloseGridPicker\"");
        razor.ShouldContain("aria-label=\"@CloseSplitPickerLabel\"");
        razor.ShouldContain("private string CloseSplitPickerLabel => SelectedCells.Count == 1");
        razor.ShouldContain("$\"Close split picker for {CellTitle(SelectedCells[0])}\"");
        razor.ShouldContain("$\"Close {DashboardTitle} split picker\"");
        razor.ShouldNotContain("aria-label=\"Close split picker\"");
        razor.ShouldContain("@onclick=\"@CloseSplitPicker\"");
        razor.ShouldNotContain("class=\"dashboard-picker-backdrop\" @onclick");
        razor.ShouldContain("title=\"@GridPickerCellAriaLabel(r, c)\"");
        razor.ShouldContain("aria-label=\"@GridPickerCellAriaLabel(r, c)\"");
        razor.ShouldNotContain("title=\"@($\"Set grid to {c} columns x {r} rows\")\"");
        razor.ShouldContain("title=\"@SplitPickerCellAriaLabel(r, c)\"");
        razor.ShouldContain("aria-label=\"@SplitPickerCellAriaLabel(r, c)\"");
        razor.ShouldNotContain("title=\"@($\"Split selected cell to {c} columns x {r} rows\")\"");
        razor.ShouldContain("GridPickerCellAriaLabel");
        razor.ShouldContain("SplitPickerCellAriaLabel");
        razor.ShouldContain("disabled=\"@IsSplitPickerCellDisabled(r, c)\"");
        razor.ShouldContain("private string CellAriaLabel");
        razor.ShouldContain("<MudIcon Icon=\"@Icons.Material.Filled.SelectAll\" Size=\"Size.Small\" aria-hidden=\"true\" />");
        razor.ShouldNotContain("<MudIcon Icon=\"@Icons.Material.Filled.SelectAll\" Size=\"Size.Small\" />");

        css.ShouldContain("grid-template-columns: minmax(0, 1fr) auto;");
        css.ShouldContain(".dashboard-meta-strip span");
        css.ShouldContain(".dashboard-mode-shell");
        css.ShouldContain(".dashboard-mode-indicator.edit");
        css.ShouldContain(".dashboard-mode-indicator.live");
        css.ShouldNotContain(".dashboard-mode-state");
        css.ShouldContain(".dashboard-tool-group");
        css.ShouldContain("min-height: 32px;");
        css.ShouldContain("@media (max-width: 980px)");
        css.ShouldContain("grid-template-columns: minmax(0, 1fr);");
    }

    [Fact]
    public void DashboardDesigner_UsesFlatLivePreviewChrome()
    {
        var root = FindRepositoryRoot();
        var razor = File.ReadAllText(Path.Combine(
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

        razor.ShouldContain("dashboard-live-head");
        razor.ShouldContain("class=\"dashboard-live-frame\" role=\"group\" aria-label=\"@LivePreviewFrameLabel\"");
        razor.ShouldContain("private string LivePreviewFrameLabel => $\"{DashboardTitle} live dashboard preview\";");
        razor.ShouldNotContain("aria-label=\"Live dashboard preview\"");
        razor.ShouldContain("dashboard-live-summary");
        razor.ShouldContain("aria-label=\"@LivePreviewSummaryLabel\"");
        razor.ShouldContain("private string LivePreviewSummaryLabel");
        razor.ShouldContain("@LivePreviewSubtitle");
        razor.ShouldContain("@LivePreviewContentLabel");
        razor.ShouldContain("LivePreviewContentClass");
        razor.ShouldNotContain("aria-label=\"Live preview summary\"");
        razor.ShouldNotContain("@LivePreviewStateLabel");
        razor.ShouldNotContain("LivePreviewStateClass");
        razor.ShouldContain("SwitchToEditMode");
        razor.ShouldContain("dashboard-live-viewport");
        razor.ShouldContain("dashboard-live-empty-note");
        razor.ShouldContain("No widgets in live preview");
        razor.ShouldContain("Read-only runtime view without layout controls.");
        razor.ShouldContain("<MudIcon Icon=\"@Icons.Material.Filled.Widgets\" Size=\"Size.Small\" aria-hidden=\"true\" />");
        razor.ShouldContain("class=\"dashboard-live-widget\"");

        css.ShouldContain(".dashboard-live-head");
        css.ShouldContain("grid-template-columns: minmax(0, 1fr) auto auto;");
        razor.ShouldContain("HasDashboardWidgets ? \"active\" : \"empty\"");
        css.ShouldContain(".dashboard-live-summary .active");
        css.ShouldContain(".dashboard-live-summary .empty");
        css.ShouldNotContain(".dashboard-live-summary .ready");
        css.ShouldContain("::deep .dashboard-live-edit-button");
        css.ShouldContain(".dashboard-live-viewport");
        css.ShouldContain(".dashboard-live-empty-note");
        css.ShouldContain("grid-template-columns: minmax(0, min(320px, 100%));");
        css.ShouldContain("transform: translate(-50%, -50%);");
        css.ShouldContain("text-align: center;");
        css.ShouldContain("max-width: calc(100% - 16px);");
    }

    [Fact]
    public void DashboardQueryPreviewFrame_UsesNeutralPreviewSourceChrome()
    {
        var root = FindRepositoryRoot();
        var markup = File.ReadAllText(Path.Combine(
            root,
            "src",
            "FluxMq.UI",
            "Components",
            "Workspace",
            "DashboardQueryPreviewFrame.razor"));
        var css = File.ReadAllText(Path.Combine(
            root,
            "src",
            "FluxMq.UI",
            "Components",
            "Workspace",
            "DashboardQueryPreviewFrame.razor.css"));

        markup.ShouldContain("Widget preview");
        markup.ShouldContain("Current style and draft query");
        markup.ShouldContain("Refresh sample");
        markup.ShouldContain("title=\"@RefreshSampleLabel\"");
        markup.ShouldContain("aria-label=\"@RefreshSampleLabel\"");
        markup.ShouldContain("private string RefreshSampleLabel => IsLive");
        System.Text.RegularExpressions.Regex.Matches(
                markup,
                @"<MudIcon\b(?:(?!/>).)*?/>",
                System.Text.RegularExpressions.RegexOptions.Singleline)
            .Cast<System.Text.RegularExpressions.Match>()
            .Select(static match => match.Value)
            .Where(static icon => !icon.Contains("aria-hidden=\"true\"", StringComparison.Ordinal))
            .ToArray()
            .ShouldBeEmpty();
        markup.ShouldContain("PreviewSourceClass");
        markup.ShouldContain("dashboard-query-preview-frame-source live");
        markup.ShouldContain("dashboard-query-preview-frame-source sample");
        markup.ShouldNotContain("DataStateClass");
        markup.ShouldNotContain("dashboard-query-preview-frame-state");
        markup.ShouldNotContain("BadgeClass");
        markup.ShouldNotContain("dashboard-query-preview-frame-badge");
        markup.ShouldNotContain("title=\"Refresh sample data\"");
        markup.ShouldNotContain("aria-label=\"Refresh sample data\"");

        css.ShouldContain(".dashboard-query-preview-frame-source");
        css.ShouldContain(".dashboard-query-preview-frame-source.live");
        css.ShouldContain(".dashboard-query-preview-frame-source.sample");
        css.ShouldNotContain(".dashboard-query-preview-frame-state");
        css.ShouldNotContain(".dashboard-query-preview-frame-badge");
    }

    [Fact]
    public void DashboardDesigner_EditGridUsesFlatEditingStateAffordances()
    {
        var root = FindRepositoryRoot();
        var razor = File.ReadAllText(Path.Combine(
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

        razor.ShouldContain("class=\"@DashboardGridFrameClass\"");
        razor.ShouldContain("dashboard-drop-hint");
        razor.ShouldContain("DashboardDragHintText");
        razor.ShouldNotContain("dashboard-drop-status");
        razor.ShouldNotContain("DashboardDragStatusText");
        razor.ShouldContain("drop-target");
        razor.ShouldContain("move-target");
        razor.ShouldContain("dashboard-cell-drop-mark");
        razor.ShouldContain("aria-keyshortcuts=\"Enter Space\"");
        razor.ShouldNotContain("drop-ready");
        razor.ShouldContain("role=\"status\" aria-live=\"polite\"");
        razor.ShouldContain("<MudIcon Icon=\"@DashboardDragHintIcon\" Size=\"Size.Small\" aria-hidden=\"true\" />");
        razor.ShouldNotContain("<MudIcon Icon=\"@DashboardDragHintIcon\" Size=\"Size.Small\" />");
        razor.ShouldContain("dashboard-grid-empty-icon");
        razor.ShouldContain("@EmptyGridHint");
        css.ShouldContain("grid-template-columns: 42px minmax(max-content, 1fr);");
        css.ShouldContain("grid-template-rows: 32px minmax(0, 1fr);");
        css.ShouldContain("overscroll-behavior: contain;");
        css.ShouldContain("border-right: 1px solid var(--flux-border-soft);");
        css.ShouldContain(".dashboard-grid-frame.adding-widget .dashboard-grid");
        css.ShouldContain(".dashboard-drop-hint");
        css.ShouldNotContain(".dashboard-drop-status");
        css.ShouldContain(".dashboard-grid-empty-icon");
        css.ShouldContain("left: 8px;");
        css.ShouldContain("max-width: min(340px, calc(100% - 16px));");
        css.ShouldContain(".dashboard-track-handle:focus-visible");
        css.ShouldContain(".dashboard-cell:focus-visible");
        css.ShouldContain(".dashboard-cell.drop-target");
        css.ShouldContain(".dashboard-grid-frame.adding-widget .dashboard-cell.drop-target");
        css.ShouldContain(".dashboard-grid-frame.adding-widget .dashboard-cell.drop-target .dashboard-cell-drop-mark");
        css.ShouldContain(".dashboard-cell.move-target");
        css.ShouldContain(".dashboard-cell.selected::after");
        css.ShouldContain(".dashboard-cell.dropping::after");
        css.ShouldContain("opacity: 0.5;");
        css.ShouldContain("border: 1px dashed color-mix(in srgb, var(--flux-accent) 78%, transparent);");
        css.ShouldContain(".dashboard-cell.moving-source::before");
        css.ShouldContain(".dashboard-cell-drop-mark");
        css.ShouldContain(".dashboard-cell:hover .dashboard-cell-placeholder");
        css.ShouldNotContain(".dashboard-cell.drop-ready");
    }

    [Fact]
    public void DashboardDesigner_UsesAccessibleGridTrackControls()
    {
        var root = FindRepositoryRoot();
        var razor = File.ReadAllText(Path.Combine(
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

        razor.ShouldContain("class=\"@TrackHandleClass(ColumnAxis)\"");
        razor.ShouldContain("aria-label=\"@TrackHandleLabel(ColumnAxis");
        razor.ShouldContain("class=\"@TrackHandleClass(RowAxis)\"");
        razor.ShouldContain("aria-label=\"@TrackHandleLabel(RowAxis");
        razor.ShouldContain("TrackShortLabel(ColumnAxis");
        razor.ShouldContain("TrackShortLabel(RowAxis");
        razor.ShouldContain("TrackHandleIcon(ColumnAxis)");
        razor.ShouldContain("TrackHandleLabel(string axis, int index, string size, double padding)");
        razor.ShouldContain("dashboard-track-icon");
        razor.ShouldContain("dashboard-track-main");
        razor.ShouldContain("dashboard-track-name");

        css.ShouldContain("grid-template-columns: 14px minmax(0, 1fr) auto;");
        css.ShouldContain(".dashboard-track-icon");
        css.ShouldContain(".dashboard-track-main");
        css.ShouldContain(".dashboard-track-name");
        css.ShouldContain(".dashboard-track-padding");
        css.ShouldContain(".dashboard-row-handles .dashboard-track-padding");
        css.ShouldContain("grid-template-columns: 14px minmax(0, 1fr);");
        css.ShouldContain("grid-template-columns: minmax(0, 1fr);");
        css.ShouldContain(".dashboard-track-icon ::deep .mud-icon-root");
        css.ShouldContain(".dashboard-column-handles .dashboard-track-main");
    }

    [Fact]
    public void DashboardTrackEditorDialog_UsesCompactFlatSizingWorkflow()
    {
        var root = FindRepositoryRoot();
        var razor = File.ReadAllText(Path.Combine(
            root,
            "src",
            "FluxMq.UI",
            "Components",
            "Workspace",
            "Dialogs",
            "DashboardTrackEditorDialog.razor"));
        var css = File.ReadAllText(Path.Combine(
            root,
            "src",
            "FluxMq.UI",
            "Components",
            "Workspace",
            "Dialogs",
            "DashboardTrackEditorDialog.razor.css"));

        razor.ShouldContain("dashboard-track-editor-title");
        razor.ShouldContain("dashboard-track-editor-preview");
        razor.ShouldContain("dashboard-track-editor-toggle");
        razor.ShouldContain("<MudIcon Icon=\"@AxisIcon\" Size=\"MudBlazor.Size.Small\" aria-hidden=\"true\" />");
        razor.ShouldContain("<MudIcon Icon=\"@Icons.Material.Filled.ArrowForward\" Size=\"MudBlazor.Size.Small\" Color=\"Color.Secondary\" aria-hidden=\"true\" />");
        razor.ShouldContain("<MudIcon Icon=\"@Icons.Material.Filled.Settings\" Size=\"MudBlazor.Size.Small\" aria-hidden=\"true\" />");
        razor.ShouldContain("<MudIcon Icon=\"@Icons.Material.Filled.Info\" Size=\"MudBlazor.Size.Small\" aria-hidden=\"true\" />");
        razor.ShouldContain("<MudToggleGroup T=\"string\"");
        razor.ShouldContain("SelectionMode=\"SelectionMode.SingleSelection\"");
        razor.ShouldContain("@TrackCode");
        razor.ShouldContain("@CurrentSummary");
        razor.ShouldContain("aria-label=\"@TrackSummaryLabel\"");
        razor.ShouldContain("private string TrackSummaryLabel => $\"{Title} sizing facts\"");
        razor.ShouldContain("aria-label=\"@ResetTrackEditLabel\"");
        razor.ShouldContain("aria-label=\"@CancelTrackEditLabel\"");
        razor.ShouldContain("aria-label=\"@ApplyTrackEditLabel\"");
        razor.ShouldContain("private string ResetTrackEditLabel => $\"Reset sizing edits for {Title}\"");
        razor.ShouldContain("private string CancelTrackEditLabel => $\"Cancel editing {Title} sizing\"");
        razor.ShouldContain("private string ApplyTrackEditLabel => $\"Apply sizing changes to {Title}\"");
        razor.ShouldContain("@ResultSize");
        razor.ShouldContain("@ModeDescription");
        razor.ShouldContain("StartIcon=\"@Icons.Material.Filled.RestartAlt\"");
        razor.ShouldContain("Disabled=\"@(!CanSubmit)\"");
        razor.ShouldNotContain("<MudIcon Icon=\"@AxisIcon\" Size=\"MudBlazor.Size.Small\" />");
        razor.ShouldNotContain("<MudIcon Icon=\"@Icons.Material.Filled.ArrowForward\" Size=\"MudBlazor.Size.Small\" Color=\"Color.Secondary\" />");
        razor.ShouldNotContain("<MudIcon Icon=\"@Icons.Material.Filled.Settings\" Size=\"MudBlazor.Size.Small\" />");
        razor.ShouldNotContain("<MudIcon Icon=\"@Icons.Material.Filled.Info\" Size=\"MudBlazor.Size.Small\" />");
        razor.ShouldNotContain("aria-label=\"Track summary\"");
        razor.ShouldNotContain("aria-label=\"Reset\"");
        razor.ShouldNotContain("aria-label=\"Cancel\"");
        razor.ShouldNotContain("aria-label=\"Apply\"");

        css.ShouldContain(".dashboard-track-editor-preview");
        css.ShouldContain("grid-template-columns: minmax(0, 1fr) 18px minmax(0, 1fr);");
        css.ShouldContain(".dashboard-track-editor-toggle ::deep .mud-button-group-root");
        css.ShouldContain("grid-template-columns: repeat(3, minmax(0, 1fr));");
        css.ShouldContain(".dashboard-track-editor-fields");
        css.ShouldContain(".dashboard-track-editor-note");
        css.ShouldContain(".dashboard-track-editor-actions");
        css.ShouldContain("@media (max-width: 520px)");
    }

    [Fact]
    public void DashboardDesigner_UsesFirstWidgetEmptyGridOnboarding()
    {
        var root = FindRepositoryRoot();
        var razor = File.ReadAllText(Path.Combine(
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

        razor.ShouldContain("@EmptyGridTitle");
        razor.ShouldContain("Drag from the catalog, or select a cell and click a widget.");
        razor.ShouldContain("Release on a highlighted cell to place it.");
        razor.ShouldNotContain("dashboard-grid-empty-steps");
        razor.ShouldNotContain("Catalog widget");

        css.ShouldNotContain(".dashboard-grid-empty-steps");
        css.ShouldContain("grid-template-columns: 24px minmax(0, 1fr);");
        css.ShouldContain(".dashboard-grid-frame.empty-grid:not(.adding-widget) .dashboard-cell-drop-mark");
        css.ShouldContain(".dashboard-grid-frame.empty-grid:not(.adding-widget) .dashboard-cell:hover .dashboard-cell-drop-mark");
        css.ShouldContain("opacity: 0.32;");
        css.ShouldContain("opacity: 0.78;");
    }

    [Fact]
    public void DashboardCatalogHandoff_UsesDirectWidgetEditAndPlacementCues()
    {
        var root = FindRepositoryRoot();
        var catalog = File.ReadAllText(Path.Combine(
            root,
            "src",
            "FluxMq.UI",
            "Components",
            "Workspace",
            "ComponentCatalogPanel.razor"));
        var catalogCss = File.ReadAllText(Path.Combine(
            root,
            "src",
            "FluxMq.UI",
            "Components",
            "Workspace",
            "ComponentCatalogPanel.razor.css"));
        var designer = File.ReadAllText(Path.Combine(
            root,
            "src",
            "FluxMq.UI",
            "Components",
            "Workspace",
            "DashboardDesigner.razor"));
        var designerCss = File.ReadAllText(Path.Combine(
            root,
            "src",
            "FluxMq.UI",
            "Components",
            "Workspace",
            "DashboardDesigner.razor.css"));

        catalog.ShouldContain("title=\"@CatalogItemTitle(item)\"");
        catalog.ShouldContain("private string CatalogItemTitle(CatalogEntry item)");
        catalog.ShouldContain("WorkspaceArtifactKind.Dashboard => $\"Click to place {item.DisplayName} in the selected cell, or drag to choose a cell\"");
        catalog.ShouldContain("Use the edit action on the placed widget to configure it.");
        catalog.ShouldNotContain("title=\"@CatalogItemTitle\"");
        catalog.ShouldNotContain("WorkspaceArtifactKind.Dashboard when CanUseItem => \"Click to place in the selected cell, or drag to choose a cell\"");
        catalogCss.ShouldContain(".component-catalog.dashboard .catalog-add-button");
        catalogCss.ShouldContain(".component-catalog.dashboard .catalog-drag-grip");

        designer.ShouldContain("var currentWidgetLabel = WidgetLabel(currentCell.Widget);");
        designer.ShouldContain("title=\"@WidgetMoveLabel(currentWidgetLabel)\"");
        designer.ShouldContain("private static string WidgetMoveLabel(string widgetLabel)");
        designer.ShouldNotContain("title=\"@($\"Drag {currentWidgetLabel} to move; use toolbar to edit\")\"");
        designer.ShouldContain("DropTargetHint(MoveTargetCellName, $\"Move {WidgetLabel(_movingWidgetName)}\")");
        designer.ShouldContain("DropTargetHint(_hoveredDropCellName, $\"Place {DashboardComponentDragLabel}\")");
        designer.ShouldContain("private string DashboardComponentDragLabel");
        designer.ShouldContain("class=\"dashboard-cell-widget-action edit\"");
        designer.ShouldContain("title=\"@WidgetEditLabel(currentWidgetLabel)\"");
        designer.ShouldContain("aria-label=\"@WidgetEditLabel(currentWidgetLabel)\"");
        designer.ShouldContain("private static string WidgetEditLabel(string widgetLabel)");
        designer.ShouldNotContain("title=\"@($\"Edit {currentWidgetLabel} settings\")\"");
        designer.ShouldNotContain("aria-label=\"@($\"Edit {currentWidgetLabel} settings\")\"");
        designer.ShouldContain("title=\"@WidgetSimulateLabel(currentWidgetLabel)\"");
        designer.ShouldContain("aria-label=\"@WidgetSimulateLabel(currentWidgetLabel)\"");
        designer.ShouldContain("private static string WidgetSimulateLabel(string widgetLabel)");
        designer.ShouldNotContain("title=\"@($\"Simulate {currentWidgetLabel} data\")\"");
        designer.ShouldNotContain("aria-label=\"@($\"Simulate {currentWidgetLabel} data\")\"");
        designer.ShouldContain("title=\"@WidgetDeleteLabel(currentWidgetLabel)\"");
        designer.ShouldContain("aria-label=\"@WidgetDeleteLabel(currentWidgetLabel)\"");
        designer.ShouldContain("private static string WidgetDeleteLabel(string widgetLabel)");
        designer.ShouldNotContain("title=\"@($\"Delete {currentWidgetLabel}\")\"");
        designer.ShouldNotContain("aria-label=\"@($\"Delete {currentWidgetLabel}\")\"");
        designer.ShouldNotContain("Drag to move widget; use toolbar to edit");
        designer.ShouldNotContain("DropTargetHint(MoveTargetCellName, \"Move widget\")");
        designer.ShouldNotContain("DropTargetHint(_hoveredDropCellName, \"Place widget\")");
        designer.ShouldNotContain("title=\"Edit widget settings\"");
        designer.ShouldNotContain("title=\"Simulate widget data\"");
        designer.ShouldNotContain("aria-label=\"Simulate widget data\"");
        designer.ShouldNotContain("title=\"Delete widget\"");
        designer.ShouldNotContain("aria-label=\"Delete widget\"");
        designer.ShouldContain("Icons.Material.Filled.Settings");
        designer.ShouldContain("OpenWidgetEditorAsync(currentCell.Widget)");
        designer.ShouldContain("SelectSingleCell(targetCellName)");
        designerCss.ShouldContain(".dashboard-cell-widget-action.edit");
    }

    [Fact]
    public void DashboardDesigner_UsesFlatEmptyAndStackedDashboardStates()
    {
        var root = FindRepositoryRoot();
        var razor = File.ReadAllText(Path.Combine(
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
        var normalizedCss = css.Replace("\r\n", "\n", StringComparison.Ordinal);
        var inspectorCss = File.ReadAllText(Path.Combine(
            root,
            "src",
            "FluxMq.UI",
            "Components",
            "Workspace",
            "DashboardInspector.razor.css"));
        var inspectorRazor = File.ReadAllText(Path.Combine(
            root,
            "src",
            "FluxMq.UI",
            "Components",
            "Workspace",
            "DashboardInspector.razor"));

        razor.ShouldContain("dashboard-empty-icon-tile");
        razor.ShouldContain("dashboard-empty-actions");
        razor.ShouldContain("@EmptyDashboardTitle");
        razor.ShouldContain("@EmptyDashboardActionLabel");
        razor.ShouldContain("\"empty-grid\"");
        razor.ShouldContain("@EmptyGridHint");
        css.ShouldContain(".dashboard-grid-frame.empty-grid");
        css.ShouldContain(".dashboard-grid-empty-note");
        css.ShouldContain("position: absolute;");
        css.ShouldContain("grid-template-columns: minmax(0, min(420px, 100%));");
        css.ShouldContain("justify-items: center;");
        css.ShouldContain("text-align: center;");
        css.ShouldContain(".dashboard-grid-picker > .dashboard-command-label");
        css.ShouldContain("min-height: clamp(320px, 52vh, 480px);");
        css.ShouldContain("overflow-x: auto;");
        css.ShouldContain(".dashboard-empty-actions");
        css.ShouldContain("max-width: min(100%, 560px);");
        css.ShouldContain("--dashboard-grid-row-min: 86px;");
        css.ShouldContain("@media (max-width: 420px)");
        normalizedCss.ShouldContain(".dashboard-empty-panel {\n        grid-template-columns: minmax(0, 1fr);\n        justify-items: center;\n        width: 100%;\n    }");
        normalizedCss.ShouldNotContain(".dashboard-empty-panel {\n        align-items: flex-start;\n        flex-direction: column;");
        inspectorCss.ShouldContain("max-height: clamp(188px, 34vh, 280px);");
        inspectorCss.ShouldContain("min-height: 164px;");
        inspectorRazor.ShouldNotContain("Use the grid to place widgets");
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

        css.ShouldContain("font-size: clamp(24px, min(8cqw, 30cqh), 46px);");
        css.ShouldContain("font-size: clamp(28px, min(10cqw, 34cqh), 52px);");
        css.ShouldContain("height: clamp(30px, min(42cqh, 38cqw), 74px);");
        css.ShouldContain("@container (max-width: 240px)");
        css.ShouldContain("@container (max-height: 150px)");
        css.ShouldContain(".dashboard-digital-readout-display");

        var designerCss = File.ReadAllText(Path.Combine(
            root,
            "src",
            "FluxMq.UI",
            "Components",
            "Workspace",
            "DashboardDesigner.razor.css"));
        designerCss.ShouldContain("font-size: clamp(24px, min(16cqw, 30cqh), 38px);");
        designerCss.ShouldContain("font-size: clamp(26px, min(18cqw, 32cqh), 42px);");
        designerCss.ShouldNotContain("font-size: 38px;");
        designerCss.ShouldNotContain("font-size: 42px;");
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
        var propertyGridRowCss = File.ReadAllText(Path.Combine(
            root,
            "src",
            "FluxMq.UI",
            "Components",
            "Workspace",
            "PropertyGridRow.razor.css"));
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
        layoutRows.ShouldContain("property-grid-action-strip");
        layoutRows.ShouldContain("property-grid-icon-action");
        layoutRows.ShouldContain("title=\"@DuplicateWidgetActionLabel\"");
        layoutRows.ShouldContain("aria-label=\"@DuplicateWidgetActionLabel\"");
        layoutRows.ShouldContain("title=\"@DeleteWidgetActionLabel\"");
        layoutRows.ShouldContain("aria-label=\"@DeleteWidgetActionLabel\"");
        layoutRows.ShouldContain("private string WidgetActionLabel");
        layoutRows.ShouldContain("private string DuplicateWidgetActionLabel => $\"Duplicate {WidgetActionLabel}\";");
        layoutRows.ShouldContain("private string DeleteWidgetActionLabel => $\"Delete {WidgetActionLabel}\";");
        layoutRows.ShouldNotContain("@($\"Duplicate {WidgetActionLabel}\")");
        layoutRows.ShouldNotContain("@($\"Delete {WidgetActionLabel}\")");
        layoutRows.ShouldNotContain("title=\"Duplicate widget\"");
        layoutRows.ShouldNotContain("aria-label=\"Duplicate widget\"");
        layoutRows.ShouldNotContain("title=\"Delete widget\"");
        layoutRows.ShouldNotContain("aria-label=\"Delete widget\"");
        layoutRows.ShouldContain("Icons.Material.Filled.ContentCopy");
        layoutRows.ShouldContain("Icons.Material.Filled.DeleteOutline");
        layoutRows.ShouldNotContain("layout-action-strip");
        layoutRows.ShouldNotContain("property-grid-button-group");
        layoutRows.ShouldNotContain("property-grid-action-button");
        styleRows.ShouldContain("property-grid-action-strip");
        styleRows.ShouldContain("property-grid-icon-action");
        styleRows.ShouldContain("PropertyGridIconSegment Value=\"@draft.GetValue(currentField.Key)\"");
        styleRows.ShouldContain("Options=\"@segmentOptions\"");
        styleRows.ShouldContain("ShowLabels=\"true\"");
        styleRows.ShouldContain("StyleSegmentOptions");
        styleRows.ShouldContain("StyleSelectIcon");
        styleRows.ShouldContain("MaxSegmentedSelectOptions");
        styleRows.ShouldContain("DashboardCellStyleDraft.WidgetFitKey");
        styleRows.ShouldContain("Icons.Material.Filled.FitScreen");
        styleRows.ShouldContain("Icons.Material.Filled.CloseFullscreen");
        styleRows.ShouldContain("Icons.Material.Filled.Check");
        styleRows.ShouldContain("Icons.Material.Filled.Close");
        styleRows.ShouldContain("title=\"@ResetCellStyleLabel\"");
        styleRows.ShouldContain("aria-label=\"@ResetCellStyleLabel\"");
        styleRows.ShouldContain("private const string ResetCellStyleLabel = \"Reset selected cell style to defaults\";");
        styleRows.ShouldNotContain("title=\"Reset cell style\"");
        styleRows.ShouldNotContain("aria-label=\"Reset cell style\"");
        styleRows.ShouldContain("Icons.Material.Filled.RestartAlt");
        styleRows.ShouldNotContain("property-grid-action-button");
        propertyGridRowCss.ShouldContain(".property-grid-action-strip");
        propertyGridRowCss.ShouldContain(".property-grid-icon-action");
        propertyGridRowCss.ShouldContain(".property-grid-icon-action.danger");
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
        // Only app-metric tiles show a data group now; the multi-metric binding editor was retired.
        inspector.ShouldContain("DashboardInspectorAppMetricRows");
        inspector.ShouldNotContain("DashboardInspectorMetricBindingRows");
        inspector.ShouldContain("DashboardInspectorMetricBindingState.Initialize");
        inspector.ShouldContain("DashboardInspectorMetricBindingState.Current");
        inspector.ShouldNotContain("<PropertyGridRow Name=\"Metric query\">");
        inspector.ShouldNotContain("private RenderFragment RenderMetricParameterField");
        inspector.ShouldNotContain("CurrentBindingMetrics(");
        appRows.ShouldContain("title=\"@OpenMetricLabel\"");
        appRows.ShouldContain("aria-label=\"@OpenMetricLabel\"");
        appRows.ShouldContain("private string OpenMetricLabel => string.IsNullOrWhiteSpace(MetricId)");
        appRows.ShouldContain("$\"Open metric {MetricActionTargetLabel}\"");
        appRows.ShouldContain("private string MetricActionTargetLabel");
        appRows.ShouldNotContain("title=\"Open metric\"");
        appRows.ShouldNotContain("aria-label=\"Open metric\"");
        appRows.ShouldContain("property-grid-action-strip");
        appRows.ShouldContain("property-grid-icon-action");
        appRows.ShouldContain("Icons.Material.Filled.OpenInNew");
        appRows.ShouldContain("ParameterChanged");
        appRows.ShouldContain("PropertyGridIconSegment");
        appRows.ShouldContain("BooleanOptions");
        appRows.ShouldContain("ParameterSelectSegmentOptions");
        appRows.ShouldContain("ShowLabels=\"true\"");
        appRows.ShouldContain("MaxSegmentedSelectOptions");
        appRows.ShouldContain("Icons.Material.Filled.Tune");
        appRows.ShouldContain("Icons.Material.Filled.RadioButtonChecked");
        appRows.ShouldNotContain("property-grid-button-group");
        appRows.ShouldNotContain("property-grid-action-button");
        appRows.ShouldNotContain("ChoiceClass");
    }

    [Fact]
    public void DashboardInspector_EditsInlineWindowOnWidgetDraft()
    {
        var root = FindRepositoryRoot();
        var inspector = File.ReadAllText(Path.Combine(
            root,
            "src",
            "FluxMq.UI",
            "Components",
            "Workspace",
            "DashboardInspector.razor"));
        var windowRows = File.ReadAllText(Path.Combine(
            root,
            "src",
            "FluxMq.UI",
            "Components",
            "Workspace",
            "DashboardInspectorMetricWindowRows.razor"));

        // The dashboard-local metric-query authoring path is retired: charts/topic/payload widgets
        // edit their window inline on the widget draft instead of a separate option-rows component.
        inspector.ShouldContain("DashboardInspectorMetricWindowRows");
        inspector.ShouldNotContain("DashboardInspectorMetricQueryOptionRows");
        inspector.ShouldNotContain("AggregationChanged");
        inspector.ShouldNotContain("WindowSelectOptions");
        inspector.ShouldNotContain("PropertyGridSelect Value=\"@draft.Window\"");
        inspector.ShouldContain("SetMetricWindowAsync");
        windowRows.ShouldContain("PropertyGridRow Name=\"@Labels.WindowRow\"");
        windowRows.ShouldContain("PropertyGridIconSegment Value=\"@Draft.Window\"");
        windowRows.ShouldContain("Options=\"@WindowSegmentOptions\"");
        windowRows.ShouldContain("ShowLabels=\"true\"");
        windowRows.ShouldContain("Icons.Material.Filled.RadioButtonChecked");
        windowRows.ShouldContain("new(\"30s\"");
        windowRows.ShouldContain("new(\"900s\"");
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
        filterRows.ShouldContain("PropertyGridIconSegment Value=\"@Draft.Status\"");
        filterRows.ShouldContain("Options=\"@StatusSegmentOptions\"");
        filterRows.ShouldContain("Options=\"@QosFilterOptions\"");
        filterRows.ShouldContain("Options=\"@RetainFilterOptions\"");
        filterRows.ShouldContain("ShowLabels=\"true\"");
        filterRows.ShouldContain("StatusOptionIcon");
        filterRows.ShouldContain("Icons.Material.Filled.Tune");
        filterRows.ShouldContain("Icons.Material.Filled.RadioButtonChecked");
        filterRows.ShouldContain("FilterChanged");
        filterRows.ShouldContain("QosFilterOptions");
        filterRows.ShouldContain("RetainFilterOptions");
        filterRows.ShouldContain("DashboardEventFilterCatalog.AttributeFilterKey(\"qos\")");
        filterRows.ShouldContain("DashboardEventFilterCatalog.AttributeFilterKey(\"retain\")");
        filterRows.ShouldNotContain("property-grid-button-group");
        filterRows.ShouldNotContain("ChoiceClass");
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
        visualRows.ShouldContain("PropertyGridIconSegment Value=\"@Draft.MetricVisualizationId\"");
        visualRows.ShouldContain("Options=\"@VisualizationSegmentOptions\"");
        visualRows.ShouldContain("ShowLabels=\"true\"");
        visualRows.ShouldContain("MetricVisualizationIcon");
        visualRows.ShouldContain("BooleanOptions");
        visualRows.ShouldContain("SelectSegmentOptions");
        visualRows.ShouldContain("AlignmentSegmentOptions");
        visualRows.ShouldContain("DashboardMetricDigitalVisualizationOptions.StyleKey");
        visualRows.ShouldContain("DashboardMetricValueVisualizationOptions.FitModeKey");
        visualRows.ShouldContain("<PropertyGridColorPicker");
        visualRows.ShouldContain("<PropertyGridIconSegment");
        visualRows.ShouldContain("HorizontalAlignmentOptions");
        visualRows.ShouldContain("ValuePlacementOptions");
        visualRows.ShouldContain("PropertyChanged");
        visualRows.ShouldNotContain("PropertyGridSelect Value=\"@Draft.MetricVisualizationId\"");
        visualRows.ShouldNotContain("property-grid-button-group");
        visualRows.ShouldNotContain("ChoiceClass");
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
        var visualRowsCss = File.ReadAllText(Path.Combine(
            root,
            "src",
            "FluxMq.UI",
            "Components",
            "Workspace",
            "DashboardInspectorVisualMetricRows.razor.css"));

        inspector.ShouldContain("DashboardInspectorVisualMetricRows");
        inspector.ShouldNotContain("SetMetricCardColumnsFromEventAsync");
        inspector.ShouldNotContain("private static string VisualMetricLabel");
        inspector.ShouldNotContain("PropertyGridRow Name=\"@InspectorLabels.PrimaryCardRow\"");
        visualRows.ShouldContain("PropertyGridRow Name=\"@Labels.PrimaryCardRow\"");
        visualRows.ShouldContain("PropertyGridIconSegment Value=\"@Draft.PrimaryMetric\"");
        visualRows.ShouldContain("Options=\"@PrimaryMetricSegmentOptions\"");
        visualRows.ShouldContain("MaxPrimaryMetricSegmentOptions");
        visualRows.ShouldContain("MetricSegmentOptions");
        visualRows.ShouldContain("Icons.Material.Filled.RadioButtonChecked");
        visualRows.ShouldContain("PropertyGridRow Name=\"@Labels.AddCardRow\"");
        visualRows.ShouldContain("PropertyGridRow Name=\"@Labels.ColumnsRow\"");
        visualRows.ShouldContain("visual-metric-row");
        visualRows.ShouldContain("visual-metric-position");
        visualRows.ShouldContain("visual-metric-actions");
        visualRows.ShouldContain("visual-metric-add-row");
        visualRows.ShouldContain("visual-metric-add-button");
        visualRows.ShouldContain("DashboardInspectorMetricMove");
        visualRows.ShouldContain("VisualMetricLabel");
        visualRows.ShouldContain("var currentMetricLabel = VisualMetricLabel(currentMetric);");
        visualRows.ShouldContain("aria-label=\"@MetricCardCommandsLabel(currentMetricLabel)\"");
        visualRows.ShouldContain("title=\"@MoveMetricUpLabel(currentMetricLabel)\"");
        visualRows.ShouldContain("aria-label=\"@MoveMetricUpLabel(currentMetricLabel)\"");
        visualRows.ShouldContain("title=\"@MoveMetricDownLabel(currentMetricLabel)\"");
        visualRows.ShouldContain("aria-label=\"@MoveMetricDownLabel(currentMetricLabel)\"");
        visualRows.ShouldContain("title=\"@RemoveMetricCardLabel(currentMetricLabel)\"");
        visualRows.ShouldContain("aria-label=\"@RemoveMetricCardLabel(currentMetricLabel)\"");
        visualRows.ShouldContain("private static string MetricCardCommandsLabel(string metricLabel)");
        visualRows.ShouldContain("private static string MoveMetricUpLabel(string metricLabel)");
        visualRows.ShouldContain("private static string MoveMetricDownLabel(string metricLabel)");
        visualRows.ShouldContain("private static string RemoveMetricCardLabel(string metricLabel)");
        visualRows.ShouldNotContain("@($\"Move {VisualMetricLabel(currentMetric)} up\")");
        visualRows.ShouldNotContain("@($\"Move {VisualMetricLabel(currentMetric)} down\")");
        visualRows.ShouldNotContain("@($\"Remove {VisualMetricLabel(currentMetric)}\")");
        visualRows.ShouldContain("CardColumnsChanged");
        visualRowsCss.ShouldContain(".visual-metric-row");
        visualRowsCss.ShouldContain("grid-template-columns: 20px minmax(0, 1fr) auto;");
        visualRowsCss.ShouldContain(".visual-metric-actions");
        visualRowsCss.ShouldContain("grid-template-columns: repeat(3, 20px);");
        visualRowsCss.ShouldContain(".visual-metric-add-row");
        visualRowsCss.ShouldContain("grid-template-columns: minmax(0, 1fr) 24px;");
        visualRowsCss.ShouldContain("width: 22px;");
        visualRowsCss.ShouldContain("@container property-grid (max-width: 280px)");
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
        displayRows.ShouldContain("PropertyGridIconSegment Value=\"@Draft.GaugeStyle\"");
        displayRows.ShouldContain("Options=\"@GaugeStyleOptions\"");
        displayRows.ShouldContain("PropertyGridRow Name=\"Min\"");
        displayRows.ShouldContain("PropertyGridColorPicker");
        displayRows.ShouldContain("GaugePropertyChanged");
        displayRows.ShouldContain("PropertyGridRow Name=\"@Labels.ChartRow\"");
        displayRows.ShouldContain("PropertyGridIconSegment Value=\"@Draft.ChartType\"");
        displayRows.ShouldContain("Options=\"@ChartTypeOptions\"");
        displayRows.ShouldContain("PropertyGridRow Name=\"Line width\"");
        displayRows.ShouldContain("PropertyGridRow Name=\"@Labels.TopicSystemRow\"");
        displayRows.ShouldContain("Options=\"@SystemTopicOptions\"");
        displayRows.ShouldContain("Options=\"@TableDensityOptions\"");
        displayRows.ShouldContain("Options=\"@BarOrientationOptions\"");
        displayRows.ShouldContain("Options=\"@BooleanOptions\"");
        displayRows.ShouldContain("Icons.Material.Filled.DensitySmall");
        displayRows.ShouldContain("Icons.Material.Filled.VisibilityOff");
        displayRows.ShouldNotContain("property-grid-button-group");
        displayRows.ShouldNotContain("private static string ChoiceClass");
        displayRows.ShouldNotContain("BooleanChoiceClass");
        displayRows.ShouldContain("GaugeStyleChanged");
        displayRows.ShouldContain("ChartTypeChanged");
        displayRows.ShouldContain("TopicActivityVisualPropertyChanged");
        displayRows.ShouldContain("TopicTreeVisualPropertyChanged");
        displayRows.ShouldContain("LineChartVisualPropertyChanged");
        displayRows.ShouldContain("AreaChartVisualPropertyChanged");
        displayRows.ShouldContain("BarChartVisualPropertyChanged");
        displayRows.ShouldContain("DonutChartVisualPropertyChanged");
        inspector.ShouldContain("TopicActivityVisualPropertyChanged=\"@SetTopicActivityVisualPropertyAsync\"");
        inspector.ShouldContain("TopicTreeVisualPropertyChanged=\"@SetTopicTreeVisualPropertyAsync\"");
        inspector.ShouldContain("LineChartVisualPropertyChanged=\"@SetLineChartVisualPropertyAsync\"");
        inspector.ShouldContain("AreaChartVisualPropertyChanged=\"@SetAreaChartVisualPropertyAsync\"");
        inspector.ShouldContain("BarChartVisualPropertyChanged=\"@SetBarChartVisualPropertyAsync\"");
        inspector.ShouldContain("DonutChartVisualPropertyChanged=\"@SetDonutChartVisualPropertyAsync\"");
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
        // App-metric tiles load binding state; inline event widgets clear it and edit config directly.
        inspector.ShouldContain("if (!IsAppMetricConsumerType(_widgetDraft.Profile.Type))");
        inspector.ShouldContain("ClearMetricDraftState();");
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
        markup.ShouldContain("title=\"@CellPlaceholderTitle(cell)\"");
        markup.ShouldContain("private static string CellPlaceholderTitle(DashboardCellSnapshot cell)");
        markup.ShouldNotContain("title=\"@($\"{CellTitle(cell)} {CellSpan(cell)}\")\"");
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
        var eventGauge = File.ReadAllText(Path.Combine(widgetsPath, "DashboardMetricGaugeVisualizationView.razor"));
        var lineChartModule = File.ReadAllText(Path.Combine(widgetsPath, "DashboardLineChartModuleView.razor"));
        var lineChart = File.ReadAllText(Path.Combine(widgetsPath, "DashboardLineChartWidget.razor"));
        var areaChartModule = File.ReadAllText(Path.Combine(widgetsPath, "DashboardAreaChartModuleView.razor"));
        var areaChart = File.ReadAllText(Path.Combine(widgetsPath, "DashboardAreaChartWidget.razor"));
        var barChartModule = File.ReadAllText(Path.Combine(widgetsPath, "DashboardBarChartModuleView.razor"));
        var barChart = File.ReadAllText(Path.Combine(widgetsPath, "DashboardBarChartWidget.razor"));
        var donutChartModule = File.ReadAllText(Path.Combine(widgetsPath, "DashboardDonutChartModuleView.razor"));
        var donutChart = File.ReadAllText(Path.Combine(widgetsPath, "DashboardDonutChartWidget.razor"));

        kpi.ShouldContain("DashboardMetricVisualizationHost");
        counter.ShouldContain("DashboardMetricValueVisualizationView");
        eventRate.ShouldContain("DashboardMetricValueVisualizationView");
        rateTile.ShouldContain("DashboardMetricValueVisualizationView");
        statusValue.ShouldContain("DashboardMetricValueVisualizationView");
        statusValue.ShouldNotContain("DashboardMetricTile");
        eventGaugeModule.ShouldContain("DashboardMetricVisualizationHost");
        eventGaugeModule.ShouldNotContain("DashboardEventGaugeWidget");
        eventGaugeModule.ShouldNotContain("Context.Snapshot");
        eventGauge.ShouldContain("Context.MetricValue");
        eventGauge.ShouldContain("Metric.FormattedValue");
        eventGauge.ShouldNotContain("PrimaryMetricCard");
        eventGauge.ShouldNotContain("DashboardEventSnapshot Snapshot");
        eventRate.ShouldNotContain("DashboardEventRateWidget");
        eventRate.ShouldNotContain("Context.Snapshot");
        lineChartModule.ShouldContain("DashboardLineChartWidget");
        lineChartModule.ShouldNotContain("DashboardEventChartWidget");
        lineChart.ShouldContain("DashboardLineChartVisualOptions");
        lineChart.ShouldContain("Context.Snapshot");
        lineChart.ShouldNotContain("DashboardChartWidgetOptions.NormalizeType");
        areaChartModule.ShouldContain("DashboardAreaChartWidget");
        areaChartModule.ShouldNotContain("DashboardEventChartWidget");
        areaChart.ShouldContain("DashboardAreaChartVisualOptions");
        areaChart.ShouldContain("Context.Snapshot");
        areaChart.ShouldNotContain("DashboardChartWidgetOptions.NormalizeType");
        barChartModule.ShouldContain("DashboardBarChartWidget");
        barChartModule.ShouldNotContain("DashboardEventChartWidget");
        barChart.ShouldContain("DashboardBarChartVisualOptions");
        barChart.ShouldContain("Context.Snapshot");
        barChart.ShouldNotContain("DashboardChartWidgetOptions.NormalizeType");
        donutChartModule.ShouldContain("DashboardDonutChartWidget");
        donutChartModule.ShouldNotContain("IFluxChartAdapter");
        donutChartModule.ShouldNotContain("DashboardSeriesBars");
        donutChart.ShouldContain("DashboardDonutChartVisualOptions");
        donutChart.ShouldContain("Context.Snapshot");
        donutChart.ShouldNotContain("IFluxChartAdapter");
        donutChart.ShouldNotContain("DashboardSeriesBars");
        widgetView.ShouldContain("DashboardWidgetCatalog.EventCounterType");
        widgetView.ShouldContain("DashboardWidgetCatalog.EventRateType");
        widgetView.ShouldContain("DashboardWidgetCatalog.RateTileType");
        widgetView.ShouldContain("DashboardWidgetCatalog.StatusValueType");
        widgetView.ShouldContain("DashboardWidgetCatalog.LineChartType");
        widgetView.ShouldContain("DashboardWidgetCatalog.AreaChartType");
        widgetView.ShouldContain("DashboardWidgetCatalog.BarChartType");
        widgetView.ShouldContain("DashboardWidgetCatalog.DonutChartType");
    }

    [Fact]
    public void DashboardWidgets_HideDecorativeHeaderIcons()
    {
        var root = FindRepositoryRoot();
        var widgetsPath = Path.Combine(root, "src", "FluxMq.UI", "Components", "Workspace", "DashboardWidgets");
        var widgetFiles = Directory
            .GetFiles(widgetsPath, "*.razor")
            .Append(Path.Combine(root, "src", "FluxMq.UI", "Components", "Workspace", "DashboardWidgetView.razor"))
            .ToArray();
        var headerIconFiles = widgetFiles
            .Where(static file => File.ReadAllText(file).Contains("dashboard-widget-icon", StringComparison.Ordinal))
            .ToArray();

        widgetFiles.Length.ShouldBeGreaterThanOrEqualTo(10);
        headerIconFiles.Length.ShouldBeGreaterThanOrEqualTo(10);
        foreach (var file in widgetFiles)
        {
            var markup = File.ReadAllText(file);
            System.Text.RegularExpressions.Regex.Matches(
                    markup,
                    @"<MudIcon\b(?:(?!/>).)*?/>",
                    System.Text.RegularExpressions.RegexOptions.Singleline)
                .Cast<System.Text.RegularExpressions.Match>()
                .Select(static match => match.Value)
                .Where(static icon => !icon.Contains("aria-hidden=\"true\"", StringComparison.Ordinal))
                .ToArray()
                .ShouldBeEmpty();
            if (headerIconFiles.Contains(file, StringComparer.Ordinal))
            {
                markup.ShouldContain("class=\"dashboard-widget-icon\" aria-hidden=\"true\"");
            }

            markup.ShouldNotContain("<div class=\"dashboard-widget-icon\">");
        }

        File.ReadAllText(Path.Combine(widgetsPath, "DashboardLatestEventWidget.razor"))
            .ShouldContain("class=\"dashboard-widget-payload\" aria-label=\"Latest event payload\"");
    }

    [Fact]
    public void DashboardWidgets_ExposeEmptyStatesAsStatusMessages()
    {
        var root = FindRepositoryRoot();
        var widgetsPath = Path.Combine(root, "src", "FluxMq.UI", "Components", "Workspace", "DashboardWidgets");
        var widgetFiles = Directory.GetFiles(widgetsPath, "*.razor");
        var emptyWidgets = widgetFiles
            .Select(static file => (File: file, Markup: File.ReadAllText(file)))
            .Where(static widget => widget.Markup.Contains("dashboard-widget-empty", StringComparison.Ordinal))
            .ToArray();

        emptyWidgets.Length.ShouldBeGreaterThanOrEqualTo(8);
        foreach (var (_, markup) in emptyWidgets)
        {
            System.Text.RegularExpressions.Regex.Matches(markup, "dashboard-widget-empty").Count
                .ShouldBe(System.Text.RegularExpressions.Regex.Matches(markup, "class=\"dashboard-widget-empty\" role=\"status\" aria-live=\"polite\"").Count);
        }

        File.ReadAllText(Path.Combine(widgetsPath, "DashboardTopicBars.razor"))
            .ShouldContain("dashboard-topic-empty\" role=\"status\" aria-live=\"polite\"");
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

    [Fact]
    public void ComponentCatalogPanel_ShowsDashboardWidgetRequirementsInDenseRows()
    {
        var root = FindRepositoryRoot();
        var markup = File.ReadAllText(Path.Combine(
            root,
            "src",
            "FluxMq.UI",
            "Components",
            "Workspace",
            "ComponentCatalogPanel.razor"));
        var css = File.ReadAllText(Path.Combine(
            root,
            "src",
            "FluxMq.UI",
            "Components",
            "Workspace",
            "ComponentCatalogPanel.razor.css"));
        var layout = File.ReadAllText(Path.Combine(
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

        markup.ShouldContain("ShouldShowRequirements(item)");
        markup.ShouldContain("catalog-item-meta");
        markup.ShouldContain("aria-label=\"@CatalogRequirementMetaLabel(item)\"");
        markup.ShouldContain("CatalogRequirementMetaLabel(CatalogEntry item)");
        markup.ShouldContain("catalog-item-meta-value");
        markup.ShouldContain("RequirementLabel(requirement)");
        markup.ShouldContain("CatalogItemClass(item)");
        markup.ShouldContain("aria-grabbed=\"@CatalogItemGrabbed(item)\"");
        markup.ShouldContain("CatalogItemAriaLabel(item)");
        markup.ShouldContain("catalog-item-affordance");
        markup.ShouldContain("catalog-drag-grip");
        markup.ShouldContain("DragIndicator");
        markup.ShouldContain("IsDraggingItem(CatalogEntry item)");
        css.ShouldContain("grid-template-areas:");
        css.ShouldContain("\"description meta\"");
        css.ShouldContain(".component-catalog.dashboard .catalog-item-meta-value:nth-child(n+2)");
        css.ShouldContain("grid-template-columns: 22px minmax(0, 1fr) 42px;");
        css.ShouldContain("position: sticky;");
        css.ShouldContain("min-height: 38px;");
        css.ShouldContain(".catalog-item.dragging");
        css.ShouldContain(".catalog-item-affordance");
        css.ShouldContain(".catalog-drag-grip");
        css.ShouldContain("opacity: 0.46;");
        css.ShouldContain("box-shadow: inset 2px 0 0 var(--flux-accent);");
        layout.ShouldContain("DragPreviewIcon(activeDrag.TargetKind)");
        layout.ShouldContain("WorkspaceArtifactKind.Dashboard => Icons.Material.Filled.Widgets");
        layout.ShouldContain("DragPreviewTargetClass");
        appCss.ShouldContain(".flux-drag-preview.dashboard");
        appCss.ShouldContain("min-height: 30px;");
        appCss.ShouldContain("max-width: 220px;");
        appCss.ShouldContain(".flux-drag-preview.over-designer .mud-icon-root");
        markup.ShouldNotContain("catalog-item-badges");
        markup.ShouldNotContain("catalog-item-badge");
        markup.ShouldNotContain("aria-label=\"Widget data requirements\"");
        css.ShouldNotContain(".catalog-item-badges");
        css.ShouldNotContain(".catalog-item-badge");
    }

    [Fact]
    public void DashboardWidgetEditorDialog_UsesFlatCompactEditorChrome()
    {
        var root = FindRepositoryRoot();
        var markup = File.ReadAllText(Path.Combine(
            root,
            "src",
            "FluxMq.UI",
            "Components",
            "Workspace",
            "Dialogs",
            "DashboardWidgetEditorDialog.razor"));
        var css = File.ReadAllText(Path.Combine(
            root,
            "src",
            "FluxMq.UI",
            "Components",
            "Workspace",
            "Dialogs",
            "DashboardWidgetEditorDialog.razor.css"));

        markup.ShouldContain("dashboard-widget-editor-title-icon");
        markup.ShouldContain("dashboard-widget-editor-title-copy");
        markup.ShouldContain("dashboard-widget-editor-meta-strip");
        markup.ShouldContain("aria-label=\"@WidgetSummaryLabel\"");
        markup.ShouldContain("private string WidgetSummaryLabel => $\"{Profile.Title} widget facts for {Widget.Name}\"");
        markup.ShouldContain("role=\"form\" aria-label=\"@EditorAriaLabel\"");
        markup.ShouldContain("@EditorModeLabel");
        markup.ShouldContain("@EditorDetailLabel");
        markup.ShouldContain("Rounded=\"false\"");
        markup.ShouldContain("dashboard-widget-editor-section-head");
        markup.ShouldContain("dashboard-widget-editor-action-spacer");
        markup.ShouldContain("dashboard-widget-editor-actions");
        markup.ShouldContain("aria-label=\"@ResetWidgetEditLabel\"");
        markup.ShouldContain("aria-label=\"@CancelWidgetEditLabel\"");
        markup.ShouldContain("aria-label=\"@ApplyWidgetEditLabel\"");
        markup.ShouldContain("private string ResetWidgetEditLabel => $\"Reset edits for {WidgetEditTargetLabel}\"");
        markup.ShouldContain("private string CancelWidgetEditLabel => $\"Cancel editing {WidgetEditTargetLabel}\"");
        markup.ShouldContain("private string ApplyWidgetEditLabel => $\"Apply edits for {WidgetEditTargetLabel}\"");
        markup.ShouldContain("private string WidgetEditTargetLabel => string.IsNullOrWhiteSpace(_draft.Title)");
        markup.ShouldContain("dashboard-widget-editor-empty\" role=\"status\" aria-live=\"polite\"");
        markup.ShouldContain("<MudIcon Icon=\"@Profile.Icon\" Size=\"Size.Small\" aria-hidden=\"true\" />");
        markup.ShouldContain("<MudIcon Icon=\"@Icons.Material.Filled.Speed\" Size=\"Size.Small\" aria-hidden=\"true\" />");
        markup.ShouldContain("<MudIcon Icon=\"@Icons.Material.Filled.ShowChart\" Size=\"Size.Small\" aria-hidden=\"true\" />");
        markup.ShouldContain("<MudIcon Icon=\"@Icons.Material.Filled.Dashboard\" Size=\"Size.Small\" aria-hidden=\"true\" />");
        markup.ShouldContain("<MudIcon Icon=\"@Icons.Material.Filled.FilterAltOff\" Size=\"Size.Small\" aria-hidden=\"true\" />");
        markup.ShouldContain("<MudIcon Icon=\"@Icons.Material.Filled.AccountTree\" Size=\"Size.Small\" aria-hidden=\"true\" />");
        markup.ShouldContain("StartIcon=\"@Icons.Material.Filled.RestartAlt\"");
        markup.ShouldContain("Disabled=\"@(!HasChanges)\"");
        markup.ShouldContain("ConfigurationEquals");
        markup.ShouldContain("FilterAltOff");
        markup.ShouldNotContain("dashboard-widget-editor-action-status");
        markup.ShouldNotContain("ActionStatusLabel");
        markup.ShouldNotContain("No changes");
        markup.ShouldNotContain("Unsaved changes");
        markup.ShouldNotContain("aria-label=\"Reset\"");
        markup.ShouldNotContain("aria-label=\"Cancel\"");
        markup.ShouldNotContain("aria-label=\"Apply\"");
        markup.ShouldNotContain("<MudIcon Icon=\"@Profile.Icon\" Size=\"Size.Small\" />");
        markup.ShouldNotContain("<MudIcon Icon=\"@Icons.Material.Filled.Speed\" Size=\"Size.Small\" />");
        markup.ShouldNotContain("<MudIcon Icon=\"@Icons.Material.Filled.ShowChart\" Size=\"Size.Small\" />");
        markup.ShouldNotContain("<MudIcon Icon=\"@Icons.Material.Filled.Dashboard\" Size=\"Size.Small\" />");
        markup.ShouldNotContain("<MudIcon Icon=\"@Icons.Material.Filled.FilterAltOff\" Size=\"Size.Small\" />");
        markup.ShouldNotContain("<MudIcon Icon=\"@Icons.Material.Filled.AccountTree\" Size=\"Size.Small\" />");
        markup.ShouldNotContain("<MudDivider />");
        markup.ShouldNotContain("aria-label=\"Widget summary\"");

        css.ShouldContain(".dashboard-widget-editor-title-icon");
        css.ShouldContain(".dashboard-widget-editor-title-copy");
        css.ShouldContain(".dashboard-widget-editor-meta-strip span");
        css.ShouldContain(".dashboard-widget-editor-shell");
        css.ShouldContain("max-height: min(68vh, 620px);");
        css.ShouldContain("overflow-y: auto;");
        css.ShouldContain("grid-template-columns: 30px minmax(0, 1fr);");
        css.ShouldContain("position: sticky;");
        css.ShouldContain(".dashboard-widget-editor-section-head");
        css.ShouldContain(".dashboard-widget-editor-action-spacer");
        css.ShouldContain(".dashboard-widget-editor-actions");
        css.ShouldContain("grid-template-columns: repeat(3, minmax(0, 1fr));");
        css.ShouldContain("@media (max-width: 700px)");
        css.ShouldNotContain(".dashboard-widget-editor-action-status");
    }

    [Fact]
    public void ComponentCatalogPanel_UsesFlatCompactCatalogChrome()
    {
        var root = FindRepositoryRoot();
        var markup = File.ReadAllText(Path.Combine(
            root,
            "src",
            "FluxMq.UI",
            "Components",
            "Workspace",
            "ComponentCatalogPanel.razor"));
        var css = File.ReadAllText(Path.Combine(
            root,
            "src",
            "FluxMq.UI",
            "Components",
            "Workspace",
            "ComponentCatalogPanel.razor.css"));

        markup.ShouldContain("aria-label=\"@CatalogPanelLabel\"");
        markup.ShouldContain("catalog-title-copy");
        markup.ShouldContain("catalog-title-label");
        System.Text.RegularExpressions.Regex.Matches(
                markup,
                @"<MudIcon\b(?:(?!/>).)*?/>",
                System.Text.RegularExpressions.RegexOptions.Singleline)
            .Cast<System.Text.RegularExpressions.Match>()
            .Select(static match => match.Value)
            .Where(static icon => !icon.Contains("aria-hidden=\"true\"", StringComparison.Ordinal))
            .ToArray()
            .ShouldBeEmpty();
        markup.ShouldContain("catalog-meta-strip");
        markup.ShouldContain("aria-label=\"@CatalogMetaLabel\"");
        markup.ShouldContain("private string CatalogMetaLabel");
        markup.ShouldContain("$\"{CatalogTitle} summary, {CatalogModeLabel}, {CatalogUseAvailabilityLabel.ToLowerInvariant()}, {CatalogFilterScopeLabel.ToLowerInvariant()} results\"");
        markup.ShouldNotContain("aria-label=\"Catalog mode, availability, and filter\"");
        markup.ShouldContain("@CatalogModeLabel");
        markup.ShouldContain("@CatalogUseAvailabilityLabel");
        markup.ShouldContain("@CatalogFilterScopeLabel");
        markup.ShouldContain("CatalogUseAvailabilityClass");
        markup.ShouldContain("aria-label=\"@SearchPlaceholder\"");
        markup.ShouldContain("aria-label=\"@CatalogListLabel\"");
        markup.ShouldContain("private string CatalogListLabel");
        markup.ShouldContain("role=\"status\" aria-live=\"polite\"");
        markup.ShouldContain("@EmptyIcon");
        markup.ShouldContain("@EmptyHintLabel");
        markup.ShouldContain("catalog-item-affordance");
        markup.ShouldContain("role=\"button\"");
        markup.ShouldContain("tabindex=\"@(!CanUseItem ? \"-1\" : \"0\")\"");
        markup.ShouldContain("CatalogItemAriaLabel(item)");
        markup.ShouldContain("aria-disabled=\"@CatalogItemDisabled\"");
        markup.ShouldContain("aria-grabbed=\"@CatalogItemGrabbed(item)\"");
        markup.ShouldContain("aria-keyshortcuts=\"Enter Space\"");
        markup.ShouldContain("args.Key is \"Enter\" or \" \" or \"Spacebar\"");
        markup.ShouldContain("private string CatalogItemDisabled");
        markup.ShouldContain("ShouldShowStepMetadata(item)");
        markup.ShouldContain("catalog-step-meta");
        markup.ShouldContain("aria-label=\"@CatalogStepMetaLabel(item)\"");
        markup.ShouldContain("CatalogStepMetaLabel(CatalogEntry item)");
        markup.ShouldContain("StepPhaseMetaClass(item)");
        markup.ShouldContain("StepKindLabel(item)");
        markup.ShouldContain("StepParameterLabel(item)");
        markup.ShouldContain("Available");
        markup.ShouldContain("descriptor.DefaultPhase");
        markup.ShouldContain("descriptor.Fields.Count");
        markup.ShouldNotContain("CatalogUseState");
        markup.ShouldNotContain("CatalogSearchStateLabel");
        markup.ShouldNotContain("Catalog state");
        markup.ShouldNotContain("catalog-step-badges");
        markup.ShouldNotContain("aria-label=\"Test step metadata\"");
        markup.ShouldNotContain("StepPhaseBadgeClass(item)");
        markup.ShouldNotContain("catalog-item-badge");
        markup.ShouldNotContain(">Ready<");
        markup.ShouldNotContain("catalog-use-state ready");
        markup.ShouldNotContain("catalog-use-state");

        css.ShouldContain(".catalog-title-copy");
        css.ShouldContain(".catalog-title-label");
        css.ShouldContain(".catalog-meta-strip span,");
        css.ShouldContain("background: var(--flux-canvas);");
        css.ShouldContain("border-bottom: 1px solid var(--flux-border-soft);");
        css.ShouldContain(".catalog-use-availability.available");
        css.ShouldContain(".catalog-use-availability.inactive");
        css.ShouldNotContain(".catalog-use-state.ready");
        css.ShouldNotContain(".catalog-use-state");
        css.ShouldContain(".catalog-empty ::deep .mud-icon-root");
        css.ShouldContain("grid-template-columns: minmax(0, min(280px, 100%));");
        css.ShouldContain("flex: 1 1 auto;");
        css.ShouldContain("justify-items: center;");
        css.ShouldContain("text-align: center;");
        css.ShouldContain("overflow-wrap: anywhere;");
        css.ShouldContain(".component-catalog.pipeline .catalog-empty");
        css.ShouldContain(".component-catalog.pipeline");
        css.ShouldContain(".component-catalog.pipeline .catalog-meta-strip");
        css.ShouldContain(".component-catalog.pipeline .catalog-item");
        css.ShouldContain("grid-template-columns: 20px minmax(0, 1fr) 38px;");
        css.ShouldContain("min-height: 31px;");
        css.ShouldContain("height: 20px;");
        css.ShouldContain(".catalog-icon-tile.actor");
        css.ShouldContain("color-mix(in srgb, var(--mud-palette-tertiary) 80%, var(--mud-palette-text-primary));");
        css.ShouldContain(".component-catalog.test .catalog-item");
        css.ShouldContain("grid-template-columns: 24px minmax(0, 1fr) 34px;");
        css.ShouldContain("min-height: 46px;");
        css.ShouldContain(".component-catalog.dashboard .catalog-item:not(.dragging):hover,");
        css.ShouldContain(".component-catalog.dashboard .catalog-item:not(.dragging):focus-visible");
        css.ShouldContain("inset 2px 0 0 color-mix(in srgb, var(--flux-accent) 54%, transparent),");
        css.ShouldContain(".component-catalog.test .catalog-item:not(.dragging):hover,");
        css.ShouldContain(".component-catalog.test .catalog-item:not(.dragging):focus-visible");
        css.ShouldContain("inset 2px 0 0 color-mix(in srgb, var(--mud-palette-warning) 54%, transparent),");
        css.ShouldContain(".catalog-step-meta");
        css.ShouldContain(".component-catalog.test .catalog-item-meta-value.setup");
        css.ShouldContain(".component-catalog.test .catalog-drag-grip");
        css.ShouldContain("display: none;");
        css.ShouldContain("@media (max-width: 760px)");
        css.ShouldNotContain(".catalog-step-badges");
        css.ShouldNotContain(".catalog-item-badge");
        css.ShouldNotContain("border-radius: 999px;");
    }

    [Fact]
    public void FlowDesigner_UsesFlatCompactCanvasChrome()
    {
        var root = FindRepositoryRoot();
        var markup = File.ReadAllText(Path.Combine(
            root,
            "src",
            "FluxMq.UI",
            "Components",
            "Workspace",
            "FlowDesigner.razor"));
        var css = File.ReadAllText(Path.Combine(
            root,
            "src",
            "FluxMq.UI",
            "Components",
            "Workspace",
            "FlowDesigner.razor.css"));

        markup.ShouldContain("aria-label=\"@FlowDesignerRegionLabel\"");
        markup.ShouldContain("private string FlowDesignerRegionLabel => Flow.ActiveWorkflowName is null");
        markup.ShouldContain("$\"{Flow.ActiveWorkflowName} flow designer canvas\"");
        markup.ShouldNotContain("aria-label=\"Flow designer canvas\"");
        markup.ShouldContain("aria-label=\"@PipelineDiagramCanvasLabel\"");
        markup.ShouldContain("private string PipelineDiagramCanvasLabel => Flow.ActiveWorkflowName is null");
        markup.ShouldContain("$\"{Flow.ActiveWorkflowName} pipeline diagram canvas\"");
        markup.ShouldNotContain("aria-label=\"Pipeline diagram canvas\"");
        markup.ShouldContain("flow-canvas-title-copy");
        markup.ShouldContain("flow-canvas-meta-strip");
        markup.ShouldContain("aria-label=\"@PipelineCanvasSummaryLabel\"");
        markup.ShouldContain("private string PipelineCanvasSummaryLabel => Flow.ActiveWorkflowName is null");
        markup.ShouldContain("$\"{Flow.ActiveWorkflowName} pipeline summary\"");
        markup.ShouldNotContain("aria-label=\"Pipeline canvas summary\"");
        markup.Split('\n')
            .Where(static line => line.Contains("<MudIcon ", StringComparison.Ordinal) &&
                !line.Contains("aria-hidden=\"true\"", StringComparison.Ordinal))
            .ToArray()
            .ShouldBeEmpty();
        markup.ShouldContain(": \"Pipeline loaded\";");
        markup.ShouldNotContain(": \"Ready\";");
        markup.ShouldContain("@WorkflowModeLabel");
        markup.ShouldContain("@WorkflowSelectionLabel");
        markup.ShouldContain("aria-label=\"@PipelineCanvasActionsLabel\"");
        markup.ShouldContain("private string PipelineCanvasActionsLabel => Flow.ActiveWorkflowName is null");
        markup.ShouldContain("$\"{Flow.ActiveWorkflowName} runtime and canvas commands\"");
        markup.ShouldNotContain("aria-label=\"Pipeline canvas runtime and commands\"");
        markup.ShouldContain("flow-runtime-marker @RuntimeMarkerClass");
        markup.ShouldContain("flow-runtime-marker-dot");
        markup.ShouldContain("@RuntimeMarkerLabel");
        markup.ShouldContain("flow-canvas-metrics");
        markup.ShouldContain("aria-label=\"@PipelineCanvasMetricsLabel\"");
        markup.ShouldContain("private string PipelineCanvasMetricsLabel => Flow.ActiveWorkflowName is null");
        markup.ShouldContain("$\"{Flow.ActiveWorkflowName} pipeline canvas metrics\"");
        markup.ShouldNotContain("aria-label=\"Pipeline canvas metrics\"");
        markup.ShouldContain("flow-canvas-stat");
        markup.ShouldContain("flow-canvas-command-group");
        markup.ShouldContain("aria-label=\"@PipelineCanvasCommandLabel\"");
        markup.ShouldContain("private string PipelineCanvasCommandLabel => Flow.ActiveWorkflowName is null");
        markup.ShouldContain("$\"{Flow.ActiveWorkflowName} canvas commands\"");
        markup.ShouldNotContain("aria-label=\"Pipeline canvas commands\"");
        markup.ShouldContain("aria-label=\"@ZoomToFitLabel\"");
        markup.ShouldContain("private string ZoomToFitLabel => Flow.ActiveWorkflowName is null");
        markup.ShouldContain("$\"Zoom {Flow.ActiveWorkflowName} pipeline canvas to fit\"");
        markup.ShouldNotContain("aria-label=\"Zoom to fit\"");
        markup.ShouldContain("role=\"status\" aria-live=\"polite\"");
        markup.ShouldContain("@EmptyCanvasHint");
        markup.ShouldContain("flow-canvas-empty-icon");
        markup.ShouldContain("AddCircle");
        markup.ShouldContain("DiagramCanvas");
        markup.ShouldContain("NavigatorWidget");
        markup.ShouldContain("ViewStrokeColor=\"#38BDF8\"");
        markup.ShouldContain("flow-link-condition-title");
        markup.ShouldContain("Label=\"Expression\"");
        markup.ShouldContain("Class=\"flow-link-condition-action apply\"");
        markup.ShouldContain("Text=\"@ApplyLinkConditionLabel\"");
        markup.ShouldContain("aria-label=\"@ApplyLinkConditionLabel\"");
        markup.ShouldContain("private string ApplyLinkConditionLabel => _selectedWorkflowLink is null");
        markup.ShouldContain("$\"Apply condition to {SelectedLinkLabel}\"");
        markup.ShouldContain("Text=\"@ClearLinkConditionLabel\"");
        markup.ShouldContain("aria-label=\"@ClearLinkConditionLabel\"");
        markup.ShouldContain("private string ClearLinkConditionLabel => _selectedWorkflowLink is null");
        markup.ShouldContain("$\"Clear condition from {SelectedLinkLabel}\"");
        markup.ShouldNotContain("Text=\"Apply condition\"");
        markup.ShouldNotContain("Text=\"Clear condition\"");
        markup.ShouldNotContain("aria-label=\"Apply link condition\"");
        markup.ShouldNotContain("aria-label=\"Clear link condition\"");
        markup.ShouldNotContain("ViewStrokeColor=\"#FBBF24\"");
        markup.ShouldNotContain("flow-link-condition-meta");
        markup.ShouldNotContain("Icons.Material.Filled.Link");
        markup.ShouldNotContain("aria-label=\"Pipeline canvas state and commands\"");
        markup.ShouldNotContain("aria-label=\"Pipeline canvas status and commands\"");
        markup.ShouldNotContain("flow-state");
        markup.ShouldNotContain("flow-state-dot");
        markup.ShouldNotContain("RuntimeStateLabel");
        markup.ShouldNotContain("RuntimeStateClass");
        markup.ShouldNotContain(">Ready<");
        markup.IndexOf("class=\"flow-canvas-header\"", StringComparison.Ordinal)
            .ShouldBeLessThan(markup.IndexOf("class=\"flow-canvas\" role=\"group\"", StringComparison.Ordinal));

        css.ShouldContain(".flow-canvas-header");
        css.ShouldContain("flex: 0 0 auto;");
        css.ShouldContain("margin: 10px 10px 0;");
        css.ShouldContain("position: relative;");
        css.ShouldContain("z-index: 2;");
        css.ShouldContain("flex: 1 1 0;");
        css.ShouldContain("height: auto;");
        css.ShouldContain("grid-template-columns: minmax(0, 1fr) auto;");
        css.ShouldContain(".flow-canvas-title-copy");
        css.ShouldContain(".flow-runtime-marker");
        css.ShouldContain(".flow-runtime-marker.valid");
        css.ShouldContain(".flow-runtime-marker-dot");
        css.ShouldContain(".flow-canvas-metrics");
        css.ShouldContain(".flow-canvas-stat");
        css.ShouldContain(".flow-canvas-stat:not(:last-child)::after");
        css.ShouldContain(".flow-canvas-command-group");
        css.ShouldContain("min-height: 46px;");
        css.ShouldContain("flex-wrap: wrap;");
        css.ShouldContain("min-height: 24px;");
        css.ShouldContain("height: 26px;");
        css.ShouldContain("min-width: 238px;");
        css.ShouldContain("min-height: 22px;");
        css.ShouldContain(".flow-canvas-empty-icon");
        css.ShouldContain("max-width: min(100%, 420px);");
        css.ShouldContain("justify-items: center;");
        css.ShouldContain("text-align: center;");
        css.ShouldContain(".flow-link-condition-title");
        css.ShouldContain("grid-template-columns: minmax(170px, 0.42fr) minmax(240px, 1fr) 30px 30px;");
        css.ShouldContain(".flow-link-condition-panel ::deep .flow-link-condition-action.mud-icon-button");
        css.ShouldContain(".flow-link-condition-panel ::deep .flow-link-condition-action.apply.mud-icon-button");
        css.ShouldContain("color-mix(in srgb, var(--flux-accent) 48%, transparent)");
        css.ShouldContain("color-mix(in srgb, var(--flux-accent) 70%, var(--mud-palette-info))");
        css.ShouldNotContain("#FBBF24");
        css.ShouldNotContain("#A78BFA");
        css.ShouldNotContain("#DDD6FE");
        css.ShouldNotContain(".flow-link-condition-meta");
        css.ShouldNotContain(".flow-state");
        css.ShouldNotContain(".flow-state-dot");
        css.ShouldNotContain("flex-wrap: nowrap;");
        css.ShouldContain("max-width: min(100%, 340px);");
        css.ShouldContain("grid-row: auto;");
        css.ShouldContain("@media (max-width: 720px)");
        css.ShouldContain("grid-template-columns: minmax(0, 1fr);");
        css.ShouldContain("margin: 8px 8px 0;");
        css.ShouldNotContain("top: 10px;");
        markup.ShouldNotContain("flow-canvas-chip");
        css.ShouldNotContain(".flow-canvas-chip");
        css.ShouldNotContain("[class$=\"-contract-label\"]");
        css.ShouldNotContain("[class$=\"-contracts\"]");
        css.ShouldNotContain("[class$=\"-contract\"]");
    }

    [Fact]
    public void MqttTriggerNodeWidget_UsesCompactSummaryAndStructuredEditor()
    {
        var root = FindRepositoryRoot();
        var markup = File.ReadAllText(Path.Combine(
            root,
            "src",
            "FluxMq.UI",
            "Components",
            "Workspace",
            "Nodes",
            "MqttTrigger",
            "MqttTriggerNodeWidget.razor"));
        var css = File.ReadAllText(Path.Combine(
            root,
            "src",
            "FluxMq.UI",
            "Components",
            "Workspace",
            "Nodes",
            "MqttTrigger",
            "MqttTriggerNodeWidget.razor.css"));

        markup.ShouldContain("EditDialogMaxWidth=\"@MaxWidth.Large\"");
        markup.ShouldContain("ShowHeaderIcon=\"false\"");
        markup.ShouldContain("ShowDisplayName=\"true\"");
        markup.ShouldContain("ShowCategoryToken=\"false\"");
        markup.ShouldNotContain("<HeaderBadge>");
        markup.ShouldNotContain("mqtt-trigger-component-badge");
        markup.ShouldNotContain("mqtt-trigger-component-icon");
        markup.ShouldNotContain("mqtt-trigger-icon-node");
        markup.ShouldContain("mqtt-trigger-summary");
        markup.ShouldContain("mqtt-trigger-meta");
        markup.ShouldContain("mqtt-trigger-meta-item broker");
        markup.ShouldContain("mqtt-trigger-subscription-list");
        markup.ShouldContain("mqtt-trigger-subscription-row");
        markup.ShouldContain("mqtt-trigger-token");
        markup.ShouldContain("Q@(subscription.QualityOfService)");
        markup.ShouldContain("TopicClass(subscription)");
        markup.ShouldContain("AdditionalSubscriptionCount");
        markup.ShouldContain("_boundedCapacity");
        markup.ShouldContain("Label=\"Output buffer\"");
        markup.ShouldContain("MqttTriggerNodeModel.NormalizeBoundedCapacity");
        markup.ShouldContain("EditorValidationError=\"@ValidateEditor\"");
        markup.ShouldContain("mqtt-trigger-editor");
        markup.ShouldContain("mqtt-trigger-layout");
        markup.ShouldContain("mqtt-trigger-sidecar");
        markup.ShouldNotContain("mqtt-trigger-editor-section");
        markup.ShouldContain("aria-label=\"Broker settings\"");
        markup.ShouldContain("aria-label=\"Subscriptions\"");
        markup.ShouldNotContain("mqtt-trigger-section-title");
        markup.ShouldNotContain("mqtt-trigger-subscription-head");
        markup.ShouldNotContain("Icons.Material.Filled.Dns");
        markup.ShouldNotContain("Icons.Material.Filled.Topic");
        markup.ShouldNotContain("mqtt-trigger-editor-grid");
        markup.ShouldNotContain("mqtt-trigger-broker-cell");
        markup.ShouldNotContain("mqtt-trigger-field-note");
        markup.ShouldNotContain("Add a broker connection in the left panel to enable the dropdown.");
        markup.ShouldNotContain("HelperText=\"Add a broker connection in the left panel to enable the dropdown.\"");
        markup.ShouldContain("<MudTable T=\"MqttTriggerSubscriptionEditorRow\"");
        markup.ShouldContain("Items=\"@SubscriptionEditorRows\"");
        markup.ShouldContain("Elevation=\"0\"");
        markup.ShouldContain("mqtt-trigger-subscription-table");
        markup.ShouldContain("<MudTh>Topic filter</MudTh>");
        markup.ShouldContain("<MudTh>QoS</MudTh>");
        markup.ShouldContain("<MudTh>Retained</MudTh>");
        markup.ShouldContain("<MudTh>Keep flag</MudTh>");
        markup.ShouldContain("Class=\"mqtt-trigger-add-cell\"");
        markup.ShouldContain("Icon=\"@Icons.Material.Filled.Add\"");
        markup.ShouldContain("Variant=\"Variant.Text\"");
        markup.ShouldContain("Color=\"Color.Primary\"");
        markup.ShouldContain("Text=\"@AddSubscriptionLabel\"");
        markup.ShouldContain("aria-label=\"@AddSubscriptionLabel\"");
        markup.ShouldContain("private string AddSubscriptionLabel => $\"Add subscription to {Node.NodeName}\";");
        markup.ShouldContain("Text=\"@RemoveSubscriptionLabel(index)\"");
        markup.ShouldContain("aria-label=\"@RemoveSubscriptionLabel(index)\"");
        markup.ShouldContain("private string RemoveSubscriptionLabel(int index)");
        markup.ShouldContain("$\"Remove {target} from {Node.NodeName}\"");
        markup.ShouldNotContain("Text=\"Add subscription\"");
        markup.ShouldNotContain("aria-label=\"Add subscription\"");
        markup.ShouldNotContain("Text=\"Remove subscription\"");
        markup.ShouldNotContain("aria-label=\"@($\"Remove subscription {index + 1}\")\"");
        markup.ShouldContain("<MudTd DataLabel=\"Topic filter\">");
        markup.ShouldContain("<MudTd DataLabel=\"QoS\">");
        markup.ShouldContain("<MudTd DataLabel=\"Retained\">");
        markup.ShouldContain("<MudTd DataLabel=\"Keep flag\">");
        markup.ShouldContain("<NoRecordsContent>");
        markup.ShouldContain("Add at least one subscription before saving.");
        markup.ShouldContain("MqttTriggerSubscriptionEditorRow");
        markup.ShouldContain("private IEnumerable<MqttTriggerSubscriptionEditorRow> SubscriptionEditorRows");
        markup.ShouldContain("ValueChanged=\"@(v => UpdateTopicFilterAsync(index, v))\"");
        markup.ShouldContain("private async Task UpdateTopicFilterAsync");
        markup.ShouldContain("private string? ValidateEditor()");
        markup.ShouldContain("Add at least one subscription.");
        markup.ShouldContain("if (subscriptions.Count == 0)");
        markup.ShouldNotContain("@if (_draftSubscriptions.Count > 1)");
        markup.ShouldNotContain("mqtt-trigger-remove-placeholder");
        markup.ShouldNotContain("_draftSubscriptions.Count <= 1");
        markup.ShouldNotContain("Disabled=\"@(_draftSubscriptions.Count <= 1)\"");
        markup.ShouldContain("Class=\"mqtt-trigger-qos-select\"");
        markup.ShouldContain("mqtt-trigger-table-check");
        markup.ShouldContain("Receive retained messages for subscription");
        markup.ShouldContain("Keep retain flag for subscription");
        markup.ShouldNotContain("AdornmentIcon=\"@Icons.Material.Filled.Tag\"");
        markup.ShouldNotContain("mqtt-trigger-hero");
        markup.ShouldNotContain("mqtt-trigger-status");
        markup.ShouldNotContain("mqtt-trigger-preview-head");
        markup.ShouldNotContain("mqtt-trigger-row-index");
        markup.ShouldNotContain("mqtt-trigger-editor-columns");
        markup.ShouldNotContain("mqtt-trigger-flag-group");
        markup.ShouldNotContain("<table class=\"mqtt-trigger-subscription-table\"");
        markup.ShouldNotContain("OnClick=\"@AddRow\">Add subscription</MudButton>");
        markup.ShouldNotContain("StartIcon=\"@Icons.Material.Filled.Add\"");
        markup.ShouldNotContain("Icon=\"@Icons.Material.Filled.Add\"\r\n                                                   Variant=\"Variant.Outlined\"");
        markup.ShouldNotContain("flow-node-filters");
        markup.ShouldNotContain("Variant=\"Variant.Filled\"");
        markup.ShouldNotContain("Class=\"trigger-qos-select\"");

        css.ShouldContain(".mqtt-trigger-summary");
        css.ShouldNotContain(".mqtt-trigger-component-badge");
        css.ShouldNotContain(".mqtt-trigger-component-icon");
        css.ShouldNotContain(".mqtt-trigger-icon-node");
        css.ShouldContain(".mqtt-trigger-layout");
        css.ShouldContain(".mqtt-trigger-sidecar");
        css.ShouldNotContain(".mqtt-trigger-editor-section");
        css.ShouldNotContain(".mqtt-trigger-broker-cell");
        css.ShouldNotContain(".mqtt-trigger-field-note");
        css.ShouldContain("padding: 0;");
        css.ShouldContain("gap: 7px;");
        css.ShouldNotContain("gap: 8px;");
        css.ShouldNotContain("background: var(--flux-surface);");
        css.ShouldNotContain("border: 1px solid var(--flux-border-soft);");
        css.ShouldNotContain(".mqtt-trigger-section-title");
        css.ShouldNotContain(".mqtt-trigger-subscription-head");
        css.ShouldContain("grid-template-columns: minmax(0, 1fr) minmax(0, 1fr);");
        css.ShouldContain(".mqtt-trigger-meta-item.broker");
        css.ShouldContain("grid-column: 1 / -1;");
        css.ShouldContain("-webkit-line-clamp: 2;");
        css.ShouldContain("overflow-wrap: anywhere;");
        css.ShouldContain("white-space: normal;");
        css.ShouldNotContain("grid-template-columns: minmax(0, 1.15fr) minmax(0, 0.7fr) minmax(0, 0.85fr);");
        css.ShouldContain("grid-template-columns: minmax(0, 1fr) minmax(1.65rem, max-content) minmax(2.15rem, max-content) minmax(2.35rem, max-content);");
        css.ShouldNotContain("grid-template-columns: minmax(0, 1fr) 26px 34px 38px;");
        css.ShouldContain("display: inline-flex;");
        css.ShouldContain("overflow-wrap: anywhere;");
        css.ShouldContain("white-space: normal;");
        css.ShouldNotContain("text-overflow: ellipsis;");
        css.ShouldNotContain("white-space: nowrap;");
        css.ShouldContain("grid-template-columns: minmax(0, 1fr) minmax(170px, 0.34fr);");
        css.ShouldNotContain("grid-template-columns: minmax(0, 1fr) minmax(10rem, 0.36fr);");
        css.ShouldNotContain("grid-template-columns: minmax(0, 1fr) 190px;");
        css.ShouldContain(".mqtt-trigger-editor ::deep(.mud-input-label)");
        css.ShouldContain("font-size: 0.72rem;");
        css.ShouldContain(".mqtt-trigger-editor ::deep(.mud-input > input.mud-input-root)");
        css.ShouldContain(".mqtt-trigger-subscription-table");
        css.ShouldContain(".mqtt-trigger-col-qos");
        css.ShouldContain("width: 18%;");
        css.ShouldContain("width: 14%;");
        css.ShouldNotContain("width: 128px;");
        css.ShouldNotContain("width: 104px;");
        css.ShouldContain(".mqtt-trigger-subscription-editor ::deep(.mqtt-trigger-subscription-table .mud-table-root)");
        css.ShouldContain(".mqtt-trigger-subscription-editor ::deep(.mqtt-trigger-subscription-table .mud-table-head .mud-table-cell)");
        css.ShouldContain(".mqtt-trigger-subscription-editor ::deep(.mqtt-trigger-subscription-table .mud-table-body .mud-table-cell)");
        css.ShouldContain("padding: 0 10px 7px 0;");
        css.ShouldContain("padding: 7px 10px 7px 0;");
        css.ShouldNotContain("padding: 0 10px 8px 0;");
        css.ShouldNotContain("padding: 8px 10px 8px 0;");
        css.ShouldContain("color: color-mix(in srgb, var(--mud-palette-text-primary) 80%, var(--mud-palette-text-secondary));");
        css.ShouldContain("color: color-mix(in srgb, var(--mud-palette-text-secondary) 88%, var(--mud-palette-text-primary));");
        css.ShouldContain(".mqtt-trigger-subscription-editor ::deep(.mqtt-trigger-add-cell)");
        css.ShouldContain(".mqtt-trigger-subscription-editor ::deep(.mqtt-trigger-add)");
        css.ShouldContain(".mqtt-trigger-table-check");
        css.ShouldContain(".mqtt-trigger-empty");
        css.ShouldContain("min-height: 36px;");
        css.ShouldNotContain("min-height: 40px;");
        css.ShouldContain("padding: 7px 0;");
        css.ShouldNotContain("padding: 8px 0;");
        css.ShouldContain("min-width: 640px;");
        css.ShouldNotContain(".mqtt-trigger-hero");
        css.ShouldNotContain(".mqtt-trigger-status");
        css.ShouldNotContain(".mqtt-trigger-preview-head");
        css.ShouldNotContain(".mqtt-trigger-row-index");
        css.ShouldNotContain(".mqtt-trigger-editor-columns");
        css.ShouldNotContain(".mqtt-trigger-editor-row");
        css.ShouldNotContain(".mqtt-trigger-flag-group");
        css.ShouldContain("@media (max-width: 720px)");
    }

    [Fact]
    public void ConnectionStateTriggerNodeWidget_UsesCompactSummaryAndFlatEditor()
    {
        var root = FindRepositoryRoot();
        var markup = File.ReadAllText(Path.Combine(
            root,
            "src",
            "FluxMq.UI",
            "Components",
            "Workspace",
            "Nodes",
            "ConnectionStateTrigger",
            "ConnectionStateTriggerNodeWidget.razor"));
        var css = File.ReadAllText(Path.Combine(
            root,
            "src",
            "FluxMq.UI",
            "Components",
            "Workspace",
            "Nodes",
            "ConnectionStateTrigger",
            "ConnectionStateTriggerNodeWidget.razor.css"));

        markup.ShouldContain("EditDialogMaxWidth=\"MaxWidth.Medium\"");
        markup.ShouldContain("ShowHeaderIcon=\"false\"");
        markup.ShouldContain("ShowDisplayName=\"true\"");
        markup.ShouldContain("ShowCategoryToken=\"false\"");
        markup.ShouldContain("@inject NodeEditDialogRefreshService DialogRefresh");
        markup.ShouldContain("EditorValidationError=\"@ValidateEditor\"");
        markup.ShouldContain("connection-state-trigger-summary");
        markup.ShouldContain("connection-state-trigger-meta");
        markup.ShouldContain("ConnectionCaption");
        markup.ShouldContain("<span>Output</span>");
        markup.ShouldContain("Client state");
        markup.ShouldContain("State changes");
        markup.ShouldNotContain("connection-state-trigger-contracts");
        markup.ShouldNotContain("aria-label=\"Connection state trigger output fields\"");
        markup.ShouldNotContain("connection-state-trigger-token");
        markup.ShouldNotContain("connection-state-trigger-contract-label");
        markup.ShouldNotContain("profileId");
        markup.ShouldContain("state");
        markup.ShouldNotContain("errors</span>");
        markup.ShouldContain("connection-state-trigger-editor");
        markup.ShouldContain("aria-label=\"Connection state trigger settings\"");
        markup.ShouldContain("Label=\"Broker connection\"");
        markup.ShouldContain("Value=\"@_connection\"");
        markup.ShouldContain("ValueChanged=\"@SetConnection\"");
        markup.ShouldContain("Label=\"Connection name\"");
        markup.ShouldContain("Class=\"connection-state-trigger-broker-field\"");
        markup.ShouldContain("Flow.SyncConnectionAndUpdateNode");
        markup.ShouldContain("DialogRefresh.RefreshAsync(Node.NodeName)");
        markup.ShouldContain("private string? ValidateEditor()");
        markup.ShouldContain("Select a broker connection before saving.");
        markup.ShouldNotContain("@bind-Value=\"_connection\"");
        markup.ShouldNotContain("MqttClientStateChanged");
        markup.ShouldNotContain("connection-state-trigger-editor-surface");
        markup.ShouldNotContain("aria-label=\"Broker state source\"");
        markup.ShouldNotContain("connection-state-trigger-editor-title");
        markup.ShouldNotContain("Emit an event whenever the selected broker connection changes state");
        markup.ShouldNotContain("connection-state-trigger-broker-panel");
        markup.ShouldNotContain("connection-state-trigger-editor-grid");
        markup.ShouldNotContain("connection-state-trigger-field-note");
        markup.ShouldNotContain("connection-state-trigger-event-table");
        markup.ShouldNotContain("aria-label=\"Connection state event payload\"");
        markup.ShouldNotContain("connection-state-trigger-event-head");
        markup.ShouldNotContain("<span>Role</span>");
        markup.ShouldNotContain("<span>Contract</span>");
        markup.ShouldNotContain("<span>Fields</span>");
        markup.ShouldNotContain("connection-state-trigger-event-line");
        markup.ShouldNotContain("connection-state-trigger-event-token-group");
        markup.ShouldNotContain("connection-state-trigger-section-heading");
        markup.ShouldNotContain("connection-state-trigger-panel-header");
        markup.ShouldNotContain("connection-state-trigger-panel-kicker");
        markup.ShouldNotContain("connection-state-trigger-panel-token");
        markup.ShouldNotContain("connection-state-trigger-editor-contract");
        markup.ShouldNotContain("connection-state-trigger-editor-token-group");
        markup.ShouldNotContain("profile</span>");
        markup.ShouldNotContain("HelperText=\"Add a broker connection in the left panel to enable the dropdown.\"");
        markup.ShouldNotContain("<Stat ");
        markup.ShouldNotContain("<MudChip");
        markup.ShouldNotContain("<MudGrid");
        markup.ShouldNotContain("d-flex flex-wrap gap-1");

        css.ShouldContain(".connection-state-trigger-summary");
        css.ShouldContain(".connection-state-trigger-meta");
        css.ShouldContain("grid-template-columns: minmax(0, 1fr) minmax(0, 0.78fr);");
        css.ShouldContain(".connection-state-trigger-meta-item.broker");
        css.ShouldContain("grid-column: 1 / -1;");
        css.ShouldContain("-webkit-line-clamp: 2;");
        css.ShouldContain("overflow-wrap: anywhere;");
        css.ShouldNotContain("grid-template-columns: minmax(0, 1.18fr) minmax(0, 0.72fr) minmax(0, 0.78fr);");
        css.ShouldNotContain("grid-template-columns: minmax(0, 1fr) 86px 92px;");
        css.ShouldNotContain(".connection-state-trigger-contracts");
        css.ShouldNotContain(".connection-state-trigger-contract");
        css.ShouldNotContain(".connection-state-trigger-contract-label");
        css.ShouldContain("white-space: normal;");
        css.ShouldNotContain(".connection-state-trigger-token");
        css.ShouldContain("overflow-wrap: anywhere;");
        css.ShouldContain(".connection-state-trigger-editor");
        css.ShouldContain("gap: 7px;");
        css.ShouldNotContain("gap: 8px;");
        css.ShouldContain("min-height: 34px;");
        css.ShouldNotContain("min-height: 36px;");
        css.ShouldContain(".connection-state-trigger-editor ::deep(.mud-input-label)");
        css.ShouldContain("font-size: 0.72rem;");
        css.ShouldNotContain(".connection-state-trigger-editor-surface");
        css.ShouldNotContain("background: color-mix(in srgb, var(--flux-surface-2) 4%, transparent);");
        css.ShouldNotContain("border: 1px solid color-mix(in srgb, var(--flux-border-soft) 42%, transparent);");
        css.ShouldNotContain(".connection-state-trigger-editor-title");
        css.ShouldNotContain(".connection-state-trigger-broker-panel");
        css.ShouldNotContain(".connection-state-trigger-panel-header");
        css.ShouldNotContain(".connection-state-trigger-panel-kicker");
        css.ShouldNotContain(".connection-state-trigger-panel-token");
        css.ShouldNotContain("border-bottom: 1px solid color-mix(in srgb, var(--flux-border-soft) 46%, transparent);");
        css.ShouldNotContain(".connection-state-trigger-editor-grid");
        css.ShouldNotContain(".connection-state-trigger-event-table");
        css.ShouldNotContain(".connection-state-trigger-event-head");
        css.ShouldNotContain(".connection-state-trigger-event-line");
        css.ShouldNotContain("grid-template-columns: 56px minmax(150px, 1fr) minmax(0, 1.4fr);");
        css.ShouldNotContain(".connection-state-trigger-event-token-group");
        css.ShouldNotContain(".connection-state-trigger-field-note");
        css.ShouldContain(".connection-state-trigger-editor ::deep(.mud-input-control)");
        css.ShouldContain("@media (max-width: 640px)");
        css.ShouldNotContain(".connection-state-trigger-section-heading");
        css.ShouldNotContain(".connection-state-trigger-editor-contract");
        css.ShouldNotContain(".connection-state-trigger-editor-token-group");
        css.ShouldNotContain(".flow-node-filters");
        css.ShouldNotContain("border-radius: 999px;");
    }

    [Fact]
    public void StateReducerNodeWidget_UsesCompactSummaryAndFlatEditor()
    {
        var root = FindRepositoryRoot();
        var markup = File.ReadAllText(Path.Combine(
            root,
            "src",
            "FluxMq.UI",
            "Components",
            "Workspace",
            "Nodes",
            "StateReducer",
            "StateReducerNodeWidget.razor"));
        var css = File.ReadAllText(Path.Combine(
            root,
            "src",
            "FluxMq.UI",
            "Components",
            "Workspace",
            "Nodes",
            "StateReducer",
            "StateReducerNodeWidget.razor.css"));

        markup.ShouldContain("CategoryColor=\"@Color.Info\"");
        markup.ShouldContain("EditDialogMaxWidth=\"MaxWidth.Large\"");
        markup.ShouldContain("@implements IDisposable");
        markup.ShouldContain("@inject AppThemeService ThemeService");
        markup.ShouldContain("@inject IJSRuntime JsRuntime");
        markup.ShouldContain("@inject NodeEditDialogRefreshService DialogRefresh");
        markup.ShouldContain("EditorValidationError=\"@ValidateEditor\"");
        markup.ShouldContain("ShowHeaderIcon=\"false\"");
        markup.ShouldContain("ShowDisplayName=\"true\"");
        markup.ShouldContain("ShowCategoryToken=\"false\"");
        markup.ShouldContain("state-reducer-summary");
        markup.ShouldContain("state-reducer-meta");
        markup.ShouldContain("EngineCaption");
        markup.ShouldContain("KeyCaption");
        markup.ShouldContain("MaxKeysCaption");
        markup.ShouldContain("BufferCaption");
        markup.ShouldContain("state-reducer-types");
        markup.ShouldContain("aria-label=\"State reducer data types\"");
        markup.ShouldContain("state-reducer-type-label");
        markup.ShouldContain("state-reducer-token");
        markup.ShouldContain("StateReducerInput");
        markup.ShouldContain("StateReducerResult");
        markup.ShouldNotContain("state-reducer-contracts");
        markup.ShouldNotContain("aria-label=\"State reducer contract fields\"");
        markup.ShouldNotContain("state-reducer-contract-label");
        markup.ShouldNotContain(">Contract<");
        markup.ShouldContain("state-reducer-expression-preview");
        markup.ShouldContain("state-reducer-editor");
        markup.ShouldContain("aria-label=\"State reducer settings\"");
        markup.ShouldContain("state-reducer-config-row");
        markup.ShouldContain("aria-label=\"State reducer configuration\"");
        markup.ShouldContain("state-reducer-rule-row");
        markup.ShouldContain("aria-label=\"State reducer rule\"");
        markup.ShouldContain("state-reducer-expression-workspace");
        markup.ShouldContain("aria-label=\"State reducer expression\"");
        markup.ShouldContain("state-reducer-editor-host");
        markup.ShouldContain("@ref=\"_reducerEditorHost\"");
        markup.ShouldContain("CssClass=\"state-reducer-monaco-editor\"");
        markup.ShouldContain("ConstructionOptions=\"@EditorConstructionOptions\"");
        markup.ShouldContain("OnDidChangeModelContent=\"@OnReducerContentChanged\"");
        markup.ShouldContain("state-reducer-rule-sidecar");
        markup.ShouldNotContain("state-reducer-editor-surface");
        markup.ShouldNotContain("state-reducer-rule-surface");
        markup.ShouldNotContain("state-reducer-rule-title");
        markup.ShouldNotContain("Transform the current input and existing state into the next state");
        markup.ShouldNotContain("state-reducer-rule-grid");
        markup.ShouldNotContain("state-reducer-rule-aside");
        markup.ShouldNotContain("state-reducer-rule-panel");
        markup.ShouldNotContain("state-reducer-panel-header");
        markup.ShouldNotContain("state-reducer-panel-kicker");
        markup.ShouldNotContain("state-reducer-panel-token");
        markup.ShouldNotContain("state-reducer-source-row");
        markup.ShouldContain("Label=\"Engine\"");
        markup.ShouldContain("Value=\"@_engine\"");
        markup.ShouldContain("ValueChanged=\"@SetEngine\"");
        markup.ShouldNotContain("@bind-Value=\"_engine\"");
        markup.ShouldContain("EngineOptionLabel(engine)");
        markup.ShouldContain("Label=\"Expression name\"");
        markup.ShouldContain("@bind-Value=\"_expressionName\"");
        markup.ShouldContain("Label=\"Input buffer\"");
        markup.ShouldContain("Value=\"@_boundedCapacity\"");
        markup.ShouldContain("ValueChanged=\"@SetBoundedCapacity\"");
        markup.ShouldNotContain("@bind-Value=\"_boundedCapacity\"");
        markup.ShouldContain("Label=\"Max keys\"");
        markup.ShouldContain("Value=\"@_maxKeys\"");
        markup.ShouldContain("ValueChanged=\"@SetMaxKeys\"");
        markup.ShouldNotContain("@bind-Value=\"_maxKeys\"");
        markup.ShouldNotContain("state-reducer-expression-row");
        markup.ShouldNotContain("state-reducer-key-cell");
        markup.ShouldNotContain("aria-label=\"State key expression\"");
        markup.ShouldNotContain("state-reducer-reducer-cell");
        markup.ShouldNotContain("state-reducer-config-grid");
        markup.ShouldNotContain("state-reducer-expression-grid");
        markup.ShouldNotContain("state-reducer-workbench");
        markup.ShouldNotContain("state-reducer-key-panel");
        markup.ShouldNotContain("state-reducer-reducer-panel");
        markup.ShouldNotContain("state-reducer-section-heading");
        markup.ShouldContain("Label=\"Key expression\"");
        markup.ShouldContain("@bind-Value=\"_keyExpression\"");
        markup.ShouldContain("Placeholder=\"topic or blank\"");
        markup.ShouldNotContain("Value=\"@_reducer\"");
        markup.ShouldNotContain("ValueChanged=\"@SetReducer\"");
        markup.ShouldNotContain("@bind-Value=\"_reducer\"");
        markup.ShouldContain("Lines=\"4\"");
        markup.ShouldNotContain("Lines=\"12\"");
        markup.ShouldNotContain("state-reducer-field-note");
        markup.ShouldNotContain("Blank keeps one shared state.");
        markup.ShouldContain("state-reducer-reference");
        markup.ShouldContain("state-reducer-reference-label");
        markup.ShouldContain("state-reducer-variable-list");
        markup.ShouldContain("ExpressionVariables");
        markup.ShouldContain("private StandaloneCodeEditor? _editor;");
        markup.ShouldContain("private ElementReference _reducerEditorHost;");
        markup.ShouldContain("private StandaloneEditorConstructionOptions EditorConstructionOptions");
        markup.ShouldContain("Language = string.Equals(_engine, \"jsonata\", StringComparison.OrdinalIgnoreCase) ? \"jsonata\" : \"csharp\"");
        markup.ShouldContain("private async Task SetEngine(string value)");
        markup.ShouldContain("private async Task SetMaxKeys(int value)");
        markup.ShouldContain("private async Task SetBoundedCapacity(int value)");
        markup.ShouldContain("private async Task OnReducerContentChanged()");
        markup.ShouldContain("private async Task LayoutEditorAfterRenderAsync()");
        markup.ShouldContain("fluxmqMonaco.measureElement");
        markup.ShouldContain("await _editor.Layout(new Dimension");
        markup.ShouldContain("private static bool IsNonCriticalEditorException(Exception exception)");
        markup.ShouldContain("private sealed class EditorHostSize");
        markup.ShouldContain("private string? ValidateEditor()");
        markup.ShouldContain("Enter a reducer expression before saving.");
        markup.ShouldContain("Input buffer must be between 1 and 100000.");
        markup.ShouldContain("Max keys must be between 0 and 1000000.");
        markup.ShouldNotContain("CategoryColor=\"@Color.Warning\"");
        markup.ShouldNotContain("<MudChip");
        markup.ShouldNotContain("<MudGrid");
        markup.ShouldNotContain("d-flex flex-wrap gap-1");
        markup.ShouldNotContain("node-expr-field");
        markup.ShouldNotContain("node-expr-preview");

        css.ShouldContain(".state-reducer-summary");
        css.ShouldContain(".state-reducer-meta");
        css.ShouldContain("grid-template-columns: minmax(0, 0.72fr) minmax(0, 1.36fr) minmax(0, 0.68fr) minmax(0, 0.58fr);");
        css.ShouldNotContain("grid-template-columns: 70px minmax(0, 1fr) 62px 54px;");
        css.ShouldContain(".state-reducer-types");
        css.ShouldContain(".state-reducer-type-row");
        css.ShouldContain(".state-reducer-type-label");
        css.ShouldNotContain(".state-reducer-contracts");
        css.ShouldNotContain(".state-reducer-contract");
        css.ShouldNotContain(".state-reducer-contract-label");
        css.ShouldContain("flex: 0 0 100%;");
        css.ShouldContain("white-space: normal;");
        css.ShouldContain(".state-reducer-token");
        css.ShouldContain("display: inline-flex;");
        css.ShouldContain("overflow-wrap: anywhere;");
        css.ShouldContain(".state-reducer-expression-preview");
        css.ShouldContain("display: -webkit-box;");
        css.ShouldContain("-webkit-line-clamp: 2;");
        css.ShouldContain("white-space: normal;");
        css.ShouldContain(".state-reducer-editor");
        css.ShouldNotContain("gap: 10px;");
        css.ShouldNotContain("gap: 8px;");
        css.ShouldContain("gap: 7px;");
        css.ShouldContain("padding-top: 7px;");
        css.ShouldNotContain("padding-top: 8px;");
        css.ShouldContain("min-height: 34px;");
        css.ShouldNotContain("min-height: 36px;");
        css.ShouldNotContain(".state-reducer-editor-surface");
        css.ShouldContain(".state-reducer-config-row");
        css.ShouldNotContain(".state-reducer-rule-surface");
        css.ShouldNotContain(".state-reducer-rule-title");
        css.ShouldContain(".state-reducer-rule-row");
        css.ShouldContain("grid-template-columns: minmax(0, 1.5fr) minmax(0, 0.5fr);");
        css.ShouldContain("height: clamp(420px, 58vh, 680px);");
        css.ShouldNotContain("grid-template-columns: minmax(0, 1.52fr) minmax(240px, 0.48fr);");
        css.ShouldContain(".state-reducer-expression-workspace");
        css.ShouldContain(".state-reducer-workspace-header");
        css.ShouldContain(".state-reducer-editor-host");
        css.ShouldContain(".state-reducer-expression-workspace ::deep(.state-reducer-monaco-editor)");
        css.ShouldContain("height: 100% !important;");
        css.ShouldContain(".state-reducer-rule-sidecar");
        css.ShouldNotContain(".state-reducer-rule-grid");
        css.ShouldNotContain(".state-reducer-rule-aside");
        css.ShouldNotContain("border-left: 1px solid color-mix(in srgb, var(--flux-border-soft) 36%, transparent);");
        css.ShouldNotContain(".state-reducer-rule-panel");
        css.ShouldNotContain("background: color-mix(in srgb, var(--flux-surface-2) 4%, transparent);");
        css.ShouldNotContain("border: 1px solid color-mix(in srgb, var(--flux-border-soft) 42%, transparent);");
        css.ShouldNotContain("padding: 12px;");
        css.ShouldNotContain("padding: 14px;");
        css.ShouldNotContain("padding-bottom: 12px;");
        css.ShouldNotContain(".state-reducer-panel-header");
        css.ShouldNotContain(".state-reducer-panel-kicker");
        css.ShouldNotContain(".state-reducer-panel-token");
        css.ShouldNotContain(".state-reducer-source-row");
        css.ShouldContain("grid-template-columns: minmax(0, 0.78fr) minmax(0, 1.22fr) minmax(0, 0.72fr) minmax(0, 0.78fr);");
        css.ShouldNotContain("grid-template-columns: minmax(150px, 0.78fr) minmax(190px, 1.22fr) 118px 138px;");
        css.ShouldNotContain(".state-reducer-expression-row");
        css.ShouldNotContain("grid-template-columns: minmax(220px, 0.58fr) minmax(0, 1.42fr);");
        css.ShouldNotContain(".state-reducer-key-cell");
        css.ShouldNotContain(".state-reducer-reducer-cell");
        css.ShouldNotContain(".state-reducer-config-grid");
        css.ShouldNotContain(".state-reducer-expression-grid");
        css.ShouldNotContain(".state-reducer-workbench");
        css.ShouldNotContain(".state-reducer-key-panel");
        css.ShouldNotContain(".state-reducer-reducer-panel");
        css.ShouldNotContain(".state-reducer-section-heading");
        css.ShouldNotContain(".state-reducer-field-note");
        css.ShouldContain(".state-reducer-reference");
        css.ShouldContain(".state-reducer-reference-label");
        css.ShouldContain(".state-reducer-variable-list");
        css.ShouldNotContain("background: color-mix(in srgb, var(--flux-surface-2) 7%, transparent);");
        css.ShouldNotContain("border: 1px solid color-mix(in srgb, var(--flux-border-soft) 24%, transparent);");
        css.ShouldNotContain("border-radius: 5px;");
        css.ShouldContain("flex-wrap: wrap;");
        css.ShouldContain("flex: 1 1 180px;");
        css.ShouldNotContain("grid-template-columns: 72px minmax(0, 1fr);");
        css.ShouldContain("padding: 7px 2px 0;");
        css.ShouldNotContain(".state-reducer-editor ::deep(.state-reducer-reducer-field textarea.mud-input-root)");
        css.ShouldNotContain("min-height: 268px;");
        css.ShouldContain(".state-reducer-editor ::deep(.state-reducer-key-field textarea.mud-input-root)");
        css.ShouldContain("min-height: 96px;");
        css.ShouldContain(".state-reducer-editor ::deep(.mud-input-control)");
        css.ShouldContain("@media (max-width: 760px)");
        css.ShouldNotContain(".flow-node-filters");
        css.ShouldNotContain("border-radius: 999px;");
    }

    [Fact]
    public void FlowAssertionNodeWidget_UsesCompactSummaryAndFlatEditor()
    {
        var root = FindRepositoryRoot();
        var markup = File.ReadAllText(Path.Combine(
            root,
            "src",
            "FluxMq.UI",
            "Components",
            "Workspace",
            "Nodes",
            "FlowAssertion",
            "FlowAssertionNodeWidget.razor"));
        var css = File.ReadAllText(Path.Combine(
            root,
            "src",
            "FluxMq.UI",
            "Components",
            "Workspace",
            "Nodes",
            "FlowAssertion",
            "FlowAssertionNodeWidget.razor.css"));

        markup.ShouldContain("EditDialogMaxWidth=\"MaxWidth.Large\"");
        markup.ShouldContain("ShowHeaderIcon=\"false\"");
        markup.ShouldContain("ShowDisplayName=\"true\"");
        markup.ShouldContain("ShowCategoryToken=\"false\"");
        markup.ShouldContain("@implements IDisposable");
        markup.ShouldContain("@inject AppThemeService ThemeService");
        markup.ShouldContain("@inject IJSRuntime JsRuntime");
        markup.ShouldContain("@inject NodeEditDialogRefreshService DialogRefresh");
        markup.ShouldContain("EditorValidationError=\"@ValidateEditor\"");
        markup.ShouldContain("flow-assertion-summary");
        markup.ShouldContain("flow-assertion-meta");
        markup.ShouldContain("AssertionCaption");
        markup.ShouldContain("InputTypeCaption");
        markup.ShouldContain("BufferCaption");
        markup.ShouldContain("flow-assertion-output-fields");
        markup.ShouldContain("flow-assertion-output-label");
        markup.ShouldContain("aria-label=\"Assertion output fields\"");
        markup.ShouldContain("flow-assertion-token");
        markup.ShouldContain("result");
        markup.ShouldContain("passed");
        markup.ShouldContain("failed");
        markup.ShouldNotContain("flow-assertion-contracts");
        markup.ShouldNotContain("flow-assertion-contract-label");
        markup.ShouldContain("flow-assertion-expression-preview");
        markup.ShouldContain("flow-assertion-editor");
        markup.ShouldContain("aria-label=\"Flow assertion settings\"");
        markup.ShouldContain("flow-assertion-config-row");
        markup.ShouldContain("aria-label=\"Flow assertion configuration\"");
        markup.ShouldContain("flow-assertion-rule-row");
        markup.ShouldContain("aria-label=\"Flow assertion rule\"");
        markup.ShouldContain("flow-assertion-expression-workspace");
        markup.ShouldContain("aria-label=\"Assertion expression\"");
        markup.ShouldContain("flow-assertion-workspace-header");
        markup.ShouldContain("flow-assertion-editor-host");
        markup.ShouldContain("StandaloneCodeEditor");
        markup.ShouldContain("CssClass=\"flow-assertion-code-editor\"");
        markup.ShouldContain("ConstructionOptions=\"@EditorConstructionOptions\"");
        markup.ShouldContain("OnDidChangeModelContent=\"@OnExpressionContentChanged\"");
        markup.ShouldNotContain("flow-assertion-rule-stack");
        markup.ShouldNotContain("flow-assertion-editor-surface");
        markup.ShouldNotContain("flow-assertion-rule-surface");
        markup.ShouldNotContain("flow-assertion-rule-composer");
        markup.ShouldNotContain("flow-assertion-rule-grid");
        markup.ShouldNotContain("flow-assertion-rule-aside");
        markup.ShouldNotContain("flow-assertion-rule-panel");
        markup.ShouldNotContain("flow-assertion-source-row");
        markup.ShouldContain("Label=\"Assertion name\"");
        markup.ShouldContain("@bind-Value=\"_assertionName\"");
        markup.ShouldContain("Label=\"Input type\"");
        markup.ShouldContain("@bind-Value=\"_inputType\"");
        markup.ShouldContain("Label=\"Input buffer\"");
        markup.ShouldContain("@bind-Value=\"_boundedCapacity\"");
        markup.ShouldNotContain("flow-assertion-expression-row");
        markup.ShouldNotContain("flow-assertion-expression-cell");
        markup.ShouldContain("aria-label=\"Assertion expression\"");
        markup.ShouldNotContain("flow-assertion-message-cell");
        markup.ShouldNotContain("aria-label=\"Assertion failure output\"");
        markup.ShouldContain("Condition");
        markup.ShouldContain("Pass condition");
        markup.ShouldNotContain("Label=\"Assertion\"");
        markup.ShouldNotContain("Label=\"Expression\"");
        markup.ShouldNotContain("Value=\"@_expression\"");
        markup.ShouldNotContain("ValueChanged=\"@SetExpression\"");
        markup.ShouldNotContain("Immediate=\"true\"");
        markup.ShouldNotContain("@bind-Value=\"_expression\"");
        markup.ShouldNotContain("Lines=\"12\"");
        markup.ShouldContain("Label=\"Failure message\"");
        markup.ShouldContain("@bind-Value=\"_failureMessage\"");
        markup.ShouldContain("Lines=\"4\"");
        markup.ShouldNotContain("Lines=\"3\"");
        markup.ShouldContain("Placeholder=\"Message on failure\"");
        markup.ShouldNotContain("flow-assertion-reference");
        markup.ShouldNotContain("flow-assertion-reference-label");
        markup.ShouldContain("flow-assertion-variable-strip");
        markup.ShouldContain("flow-assertion-variable-label");
        markup.ShouldContain("flow-assertion-variable-list");
        markup.ShouldNotContain("flow-assertion-config-grid");
        markup.ShouldNotContain("flow-assertion-expression-grid");
        markup.ShouldNotContain("flow-assertion-field-note");
        markup.ShouldNotContain("Used when the expression evaluates to false.");
        markup.ShouldContain("ExpressionVariables");
        markup.ShouldNotContain("<MudChip");
        markup.ShouldNotContain("<MudGrid");
        markup.ShouldNotContain("<MudExpansionPanel");
        markup.ShouldNotContain("<MudSimpleTable");
        markup.ShouldNotContain("d-flex flex-wrap gap-1");
        markup.ShouldNotContain("node-expr-field");
        markup.ShouldNotContain("node-expr-preview");
        markup.ShouldNotContain("flow-assertion-workbench");
        markup.ShouldNotContain("flow-assertion-expression-panel");
        markup.ShouldNotContain("flow-assertion-message-panel");
        markup.ShouldNotContain("flow-assertion-section-heading");
        markup.ShouldNotContain("flow-assertion-panel-header");
        markup.ShouldNotContain("flow-assertion-panel-kicker");
        markup.ShouldNotContain("flow-assertion-panel-token");
        markup.ShouldContain("private async Task OnExpressionContentChanged()");
        markup.ShouldContain("private StandaloneEditorConstructionOptions EditorConstructionOptions(StandaloneCodeEditor editor)");
        markup.ShouldContain("private async Task ConfigureCodeEditorAsync()");
        markup.ShouldContain("fluxmqMonaco.measureElement");
        markup.ShouldContain("await DialogRefresh.RefreshAsync(Node.NodeName);");
        markup.ShouldNotContain("private async Task SetExpression(string value)");
        markup.ShouldContain("private string? ValidateEditor()");
        markup.ShouldContain("Enter an assertion condition before saving.");

        css.ShouldContain(".flow-assertion-summary");
        css.ShouldContain(".flow-assertion-meta");
        css.ShouldContain("grid-template-columns: minmax(0, 1.18fr) minmax(0, 0.6fr) minmax(0, 0.42fr);");
        css.ShouldContain("-webkit-line-clamp: 2;");
        css.ShouldContain(".flow-assertion-output-fields");
        css.ShouldContain(".flow-assertion-output-row");
        css.ShouldContain(".flow-assertion-output-label");
        css.ShouldNotContain(".flow-assertion-contracts");
        css.ShouldNotContain(".flow-assertion-contract");
        css.ShouldNotContain(".flow-assertion-contract-label");
        css.ShouldContain("display: flex;");
        css.ShouldContain("flex-wrap: wrap;");
        css.ShouldContain("flex: 0 0 100%;");
        css.ShouldNotContain("grid-template-columns: repeat(3, minmax(0, auto));");
        css.ShouldNotContain("grid-template-columns: repeat(2, minmax(0, 1fr));");
        css.ShouldNotContain("grid-column: 1 / -1;");
        css.ShouldContain(".flow-assertion-token");
        css.ShouldContain("display: inline-flex;");
        css.ShouldContain("justify-content: center;");
        css.ShouldContain("color-mix(in srgb, var(--mud-palette-info) 70%, var(--mud-palette-text-primary));");
        css.ShouldContain("color: var(--flux-text-muted);");
        css.ShouldNotContain("color-mix(in srgb, var(--mud-palette-warning) 76%, var(--mud-palette-text-primary));");
        css.ShouldNotContain("color: var(--mud-palette-warning);");
        css.ShouldContain(".flow-assertion-token.pass");
        css.ShouldContain(".flow-assertion-token.fail");
        css.ShouldContain("overflow-wrap: anywhere;");
        css.ShouldContain(".flow-assertion-expression-preview");
        css.ShouldNotContain("text-overflow: ellipsis;");
        css.ShouldContain("white-space: normal;");
        css.ShouldContain(".flow-assertion-editor");
        css.ShouldNotContain("gap: 10px;");
        css.ShouldNotContain("gap: 9px;");
        css.ShouldNotContain("gap: 8px;");
        css.ShouldContain("gap: 7px;");
        css.ShouldContain("gap: 6px;");
        css.ShouldContain("padding-top: 7px;");
        css.ShouldNotContain("padding-top: 8px;");
        css.ShouldNotContain("padding-top: 9px;");
        css.ShouldContain("padding: 7px 2px 0;");
        css.ShouldNotContain("padding: 8px 2px 0;");
        css.ShouldContain("min-height: 34px;");
        css.ShouldNotContain("min-height: 36px;");
        css.ShouldContain(".flow-assertion-config-row");
        css.ShouldContain(".flow-assertion-rule-row");
        css.ShouldContain("grid-template-columns: minmax(0, 1.5fr) minmax(0, 0.5fr);");
        css.ShouldContain("height: clamp(420px, 58vh, 680px);");
        css.ShouldNotContain(".flow-assertion-rule-stack");
        css.ShouldNotContain("grid-template-columns: minmax(0, 1.52fr) minmax(240px, 0.48fr);");
        css.ShouldContain(".flow-assertion-rule-sidecar");
        css.ShouldNotContain(".flow-assertion-editor-surface");
        css.ShouldNotContain(".flow-assertion-rule-surface");
        css.ShouldNotContain(".flow-assertion-rule-title");
        css.ShouldNotContain(".flow-assertion-rule-composer");
        css.ShouldNotContain(".flow-assertion-rule-grid");
        css.ShouldNotContain(".flow-assertion-rule-aside");
        css.ShouldNotContain("border-left: 1px solid color-mix(in srgb, var(--flux-border-soft) 36%, transparent);");
        css.ShouldContain(".flow-assertion-expression-workspace");
        css.ShouldContain(".flow-assertion-workspace-header");
        css.ShouldContain(".flow-assertion-editor-host");
        css.ShouldContain(".flow-assertion-expression-workspace ::deep(.flow-assertion-code-editor)");
        css.ShouldNotContain(".flow-assertion-rule-panel");
        css.ShouldNotContain("background: color-mix(in srgb, var(--flux-surface-2) 4%, transparent);");
        css.ShouldNotContain("border: 1px solid color-mix(in srgb, var(--flux-border-soft) 42%, transparent);");
        css.ShouldNotContain("padding: 12px;");
        css.ShouldNotContain("padding: 14px;");
        css.ShouldNotContain("padding-bottom: 12px;");
        css.ShouldNotContain(".flow-assertion-source-row");
        css.ShouldContain("grid-template-columns: minmax(0, 1.24fr) minmax(0, 1fr) minmax(0, 0.54fr);");
        css.ShouldNotContain("grid-template-columns: minmax(220px, 1.24fr) minmax(180px, 1fr) 138px;");
        css.ShouldNotContain(".flow-assertion-expression-row");
        css.ShouldNotContain(".flow-assertion-expression-cell");
        css.ShouldNotContain(".flow-assertion-message-cell");
        css.ShouldNotContain("grid-template-columns: minmax(0, 1.35fr) minmax(240px, 0.65fr);");
        css.ShouldNotContain(".flow-assertion-config-grid");
        css.ShouldNotContain(".flow-assertion-expression-grid");
        css.ShouldNotContain(".flow-assertion-field-note");
        css.ShouldNotContain(".flow-assertion-reference");
        css.ShouldNotContain(".flow-assertion-reference-label");
        css.ShouldContain(".flow-assertion-variable-strip");
        css.ShouldContain(".flow-assertion-variable-label");
        css.ShouldContain(".flow-assertion-variable-list");
        css.ShouldContain("grid-template-columns: minmax(0, 1fr);");
        css.ShouldNotContain("grid-template-columns: 72px minmax(0, 1fr);");
        css.ShouldNotContain("background: color-mix(in srgb, var(--flux-surface-2) 7%, transparent);");
        css.ShouldNotContain("border: 1px solid color-mix(in srgb, var(--flux-border-soft) 24%, transparent);");
        css.ShouldNotContain("border-radius: 5px;");
        css.ShouldNotContain("padding: 7px;");
        css.ShouldNotContain(".flow-assertion-editor ::deep(.flow-assertion-expression-field textarea.mud-input-root)");
        css.ShouldNotContain("min-height: 292px;");
        css.ShouldContain(".flow-assertion-editor ::deep(.flow-assertion-message-field textarea.mud-input-root)");
        css.ShouldContain("min-height: 96px;");
        css.ShouldContain(".flow-assertion-editor ::deep(.mud-input-control)");
        css.ShouldNotContain(".flow-assertion-variables");
        css.ShouldNotContain(".flow-assertion-workbench");
        css.ShouldNotContain(".flow-assertion-expression-panel");
        css.ShouldNotContain(".flow-assertion-message-panel");
        css.ShouldNotContain(".flow-assertion-section-heading");
        css.ShouldNotContain(".flow-assertion-panel-header");
        css.ShouldNotContain(".flow-assertion-panel-kicker");
        css.ShouldNotContain(".flow-assertion-panel-token");
        css.ShouldContain("@media (max-width: 760px)");
        css.ShouldNotContain(".flow-node-filters");
        css.ShouldNotContain("border-radius: 999px;");
    }

    [Fact]
    public void MessageFilterNodeWidget_UsesCompactSummaryAndFlatEditor()
    {
        var root = FindRepositoryRoot();
        var markup = File.ReadAllText(Path.Combine(
            root,
            "src",
            "FluxMq.UI",
            "Components",
            "Workspace",
            "Nodes",
            "MessageFilter",
            "MessageFilterNodeWidget.razor"));
        var css = File.ReadAllText(Path.Combine(
            root,
            "src",
            "FluxMq.UI",
            "Components",
            "Workspace",
            "Nodes",
            "MessageFilter",
            "MessageFilterNodeWidget.razor.css"));

        markup.ShouldContain("EditDialogMaxWidth=\"MaxWidth.Large\"");
        markup.ShouldContain("ShowHeaderIcon=\"false\"");
        markup.ShouldContain("ShowDisplayName=\"true\"");
        markup.ShouldContain("ShowCategoryToken=\"false\"");
        markup.ShouldContain("@implements IDisposable");
        markup.ShouldContain("@inject AppThemeService ThemeService");
        markup.ShouldContain("@inject IJSRuntime JsRuntime");
        markup.ShouldContain("@inject NodeEditDialogRefreshService DialogRefresh");
        markup.ShouldContain("EditorValidationError=\"@ValidateEditor\"");
        markup.ShouldContain("message-filter-summary");
        markup.ShouldContain("message-filter-meta");
        markup.ShouldContain("<span>Topic scope</span>");
        markup.ShouldContain("<span>Condition</span>");
        markup.ShouldContain("PatternCountCaption");
        markup.ShouldContain("ConditionCaption");
        markup.ShouldContain("message-filter-patterns");
        markup.ShouldContain("aria-label=\"Topic filter patterns\"");
        markup.ShouldContain("SummaryPatterns");
        markup.ShouldContain("PatternOverflow");
        markup.ShouldContain("message-filter-expression-preview");
        markup.ShouldContain("message-filter-expression-label");
        markup.ShouldContain("message-filter-expression-text");
        markup.ShouldContain("message-filter-token");
        markup.ShouldContain("message-filter-editor");
        markup.ShouldContain("aria-label=\"Flow filter settings\"");
        markup.ShouldNotContain("message-filter-rules-surface");
        markup.ShouldContain("aria-label=\"Filter rules\"");
        markup.ShouldNotContain("message-filter-editor-status");
        markup.ShouldNotContain("DraftModeCaption");
        markup.ShouldNotContain("DraftPatternCountCaption");
        markup.ShouldNotContain("message-filter-rule-composer");
        markup.ShouldContain("message-filter-rule-row");
        markup.ShouldNotContain("message-filter-rule-layout");
        markup.ShouldNotContain("message-filter-rule-grid");
        markup.ShouldContain("message-filter-condition-workspace");
        markup.ShouldContain("message-filter-workspace-header");
        markup.ShouldContain("message-filter-editor-host");
        markup.ShouldContain("StandaloneCodeEditor");
        markup.ShouldContain("CssClass=\"message-filter-code-editor\"");
        markup.ShouldContain("ConstructionOptions=\"@EditorConstructionOptions\"");
        markup.ShouldContain("OnDidChangeModelContent=\"@OnExpressionContentChanged\"");
        markup.ShouldContain("message-filter-scope-editor");
        markup.ShouldContain("message-filter-sidecar");
        markup.ShouldNotContain("message-filter-condition-editor");
        markup.ShouldContain("message-filter-section-bar");
        markup.ShouldNotContain("message-filter-expression-row");
        markup.ShouldNotContain("message-filter-pattern-table");
        markup.ShouldContain("message-filter-pattern-list");
        markup.ShouldNotContain("message-filter-pattern-table-header");
        markup.ShouldNotContain("aria-hidden=\"true\"");
        markup.ShouldContain("aria-label=\"Topic pattern filters\"");
        markup.ShouldContain("aria-label=\"Condition expression\"");
        markup.ShouldContain("message-filter-pattern-row");
        markup.ShouldContain("aria-label=\"@($\"Topic pattern {index + 1}\")\"");
        markup.ShouldContain("ValueChanged=\"@(value => SetPattern(index, value))\"");
        markup.ShouldContain("Text=\"@AddPatternLabel\"");
        markup.ShouldContain("aria-label=\"@AddPatternLabel\"");
        markup.ShouldContain("private string AddPatternLabel => $\"Add topic pattern to {Node.NodeName}\"");
        markup.ShouldContain("AddPattern");
        markup.ShouldContain("Text=\"@RemovePatternLabel(index)\"");
        markup.ShouldContain("aria-label=\"@RemovePatternLabel(index)\"");
        markup.ShouldContain("private string RemovePatternLabel(int index)");
        markup.ShouldContain("$\"Remove {target} from {Node.NodeName}\"");
        markup.ShouldContain("RemovePattern(index)");
        markup.ShouldNotContain("Text=\"Add pattern\"");
        markup.ShouldNotContain("Text=\"Remove pattern\"");
        markup.ShouldNotContain("aria-label=\"Add topic pattern\"");
        markup.ShouldNotContain("aria-label=\"@($\"Remove topic pattern {index + 1}\")\"");
        markup.ShouldContain("RefreshDialogAsync");
        markup.ShouldContain("DialogRefresh.RefreshAsync(Node.NodeName)");
        markup.ShouldContain("private string? ValidateEditor()");
        markup.ShouldContain("Add at least one topic pattern before saving.");
        markup.ShouldNotContain("ValueChanged=\"@(v => _draftPatterns[index] = v ?? string.Empty)\"");
        markup.ShouldContain("role=\"status\"");
        markup.ShouldNotContain("message-filter-rules-panel");
        markup.ShouldNotContain("message-filter-pattern-header");
        markup.ShouldNotContain("message-filter-expression-area");
        markup.ShouldNotContain("aria-label=\"Expression filter\"");
        markup.ShouldContain("aria-label=\"Condition expression\"");
        markup.ShouldContain("aria-label=\"Condition expression variables\"");
        markup.ShouldNotContain("@bind-Value=\"_expression\"");
        markup.ShouldNotContain("Lines=\"7\"");
        markup.ShouldNotContain("message-filter-expression-field");
        markup.ShouldContain("message-filter-reference");
        markup.ShouldContain("message-filter-reference-label");
        markup.ShouldContain("message-filter-variable-list");
        markup.ShouldContain("ExpressionVariables");
        markup.ShouldNotContain("<MudChip");
        markup.ShouldNotContain("<MudTabs");
        markup.ShouldNotContain("<MudTabPanel");
        markup.ShouldNotContain("<MudExpansionPanel");
        markup.ShouldNotContain("<MudSimpleTable");
        markup.ShouldNotContain("<MudGrid");
        markup.ShouldNotContain("flow-node-filters");
        markup.ShouldNotContain("d-flex flex-wrap gap-1");
        markup.ShouldNotContain("message-filter-rule-workbench");
        markup.ShouldNotContain("message-filter-pattern-panel");
        markup.ShouldNotContain("message-filter-expression-panel");
        markup.ShouldNotContain("message-filter-section-heading");
        markup.ShouldNotContain("message-filter-row-index");
        markup.ShouldNotContain("message-filter-panel-title");
        markup.ShouldNotContain("message-filter-panel-header");
        markup.ShouldNotContain("message-filter-panel-kicker");
        markup.ShouldNotContain("message-filter-panel-token");
        markup.ShouldNotContain("Topic patterns are used unless");
        markup.ShouldNotContain("message-filter-rule-title");
        markup.ShouldNotContain("message-filter-pattern-column");
        markup.ShouldNotContain("message-filter-expression-column");
        markup.ShouldNotContain("message-filter-column-heading");
        markup.ShouldNotContain("DraftRuleCaption");
        markup.ShouldContain("PatternLabel");
        markup.ShouldContain("private async Task OnExpressionContentChanged()");
        markup.ShouldContain("private StandaloneEditorConstructionOptions EditorConstructionOptions(StandaloneCodeEditor editor)");
        markup.ShouldContain("private async Task ConfigureCodeEditorAsync()");
        markup.ShouldContain("fluxmqMonaco.measureElement");
        markup.ShouldContain("await DialogRefresh.RefreshAsync(Node.NodeName);");

        css.ShouldContain(".message-filter-summary");
        css.ShouldContain(".message-filter-meta");
        css.ShouldContain("grid-template-columns: minmax(0, 1.3fr) minmax(0, 0.7fr);");
        css.ShouldNotContain("grid-template-columns: minmax(0, 1fr) 88px;");
        css.ShouldContain("-webkit-line-clamp: 2;");
        css.ShouldContain("overflow-wrap: anywhere;");
        css.ShouldContain("white-space: normal;");
        css.ShouldContain(".message-filter-patterns");
        css.ShouldContain("display: flex;");
        css.ShouldContain("flex-wrap: wrap;");
        css.ShouldNotContain("grid-template-columns: repeat(3, minmax(0, auto));");
        css.ShouldContain(".message-filter-token");
        css.ShouldContain("display: inline-flex;");
        css.ShouldContain(".message-filter-token.system");
        css.ShouldContain("color-mix(in srgb, var(--mud-palette-info) 72%, var(--mud-palette-text-primary));");
        css.ShouldNotContain("color-mix(in srgb, var(--mud-palette-warning) 76%, var(--mud-palette-text-primary));");
        css.ShouldContain("overflow-wrap: anywhere;");
        css.ShouldContain(".message-filter-expression-preview");
        css.ShouldContain("display: grid;");
        css.ShouldContain(".message-filter-expression-label");
        css.ShouldContain(".message-filter-expression-text");
        css.ShouldContain("display: -webkit-box;");
        css.ShouldContain("-webkit-line-clamp: 2;");
        css.ShouldContain("white-space: normal;");
        css.ShouldNotContain("text-overflow: ellipsis;");
        css.ShouldContain(".message-filter-editor");
        css.ShouldNotContain(".message-filter-rules-surface");
        css.ShouldNotContain("background: color-mix(in srgb, var(--flux-surface-2) 4%, transparent);");
        css.ShouldNotContain("border: 1px solid color-mix(in srgb, var(--flux-border-soft) 42%, transparent);");
        css.ShouldNotContain("padding: 12px;");
        css.ShouldNotContain("padding: 14px;");
        css.ShouldNotContain(".message-filter-editor-status");
        css.ShouldNotContain(".message-filter-editor-status > div");
        css.ShouldNotContain(".message-filter-rule-composer");
        css.ShouldNotContain(".message-filter-editor-grid");
        css.ShouldContain(".message-filter-rule-row");
        css.ShouldContain("grid-template-columns: minmax(0, 1.5fr) minmax(0, 0.5fr);");
        css.ShouldContain("height: clamp(420px, 58vh, 680px);");
        css.ShouldContain("padding-top: 7px;");
        css.ShouldContain(".message-filter-condition-workspace");
        css.ShouldContain(".message-filter-workspace-header");
        css.ShouldContain(".message-filter-editor-host");
        css.ShouldContain(".message-filter-sidecar");
        css.ShouldContain(".message-filter-condition-workspace ::deep(.message-filter-code-editor)");
        css.ShouldNotContain(".message-filter-rule-layout");
        css.ShouldNotContain("grid-template-columns: minmax(0, 1.1fr) minmax(14rem, 0.9fr);");
        css.ShouldNotContain("grid-template-columns: minmax(0, 1fr) 310px;");
        css.ShouldNotContain(".message-filter-rule-grid");
        css.ShouldContain(".message-filter-scope-editor");
        css.ShouldNotContain(".message-filter-condition-editor");
        css.ShouldContain(".message-filter-section-bar");
        css.ShouldNotContain(".message-filter-expression-row");
        css.ShouldNotContain(".message-filter-pattern-table");
        css.ShouldNotContain(".message-filter-pattern-table-header");
        css.ShouldNotContain("grid-template-columns: repeat(2, minmax(0, 1fr));");
        css.ShouldNotContain("border-left: 1px solid color-mix(in srgb, var(--flux-border-soft) 36%, transparent);");
        css.ShouldContain(".message-filter-pattern-list");
        css.ShouldContain(".message-filter-section-bar ::deep(.message-filter-add.mud-icon-button)");
        css.ShouldContain("justify-self: end;");
        css.ShouldContain("border-radius: 4px;");
        css.ShouldContain(".message-filter-section-bar ::deep(.message-filter-add .mud-icon-root)");
        css.ShouldContain(".message-filter-pattern-row ::deep(.message-filter-remove .mud-icon-root)");
        css.ShouldNotContain(".message-filter-rules-panel");
        css.ShouldNotContain(".message-filter-pattern-header");
        css.ShouldNotContain(".message-filter-expression-area");
        css.ShouldContain(".message-filter-pattern-row");
        css.ShouldContain("min-height: 34px;");
        css.ShouldNotContain("min-height: 36px;");
        css.ShouldNotContain("min-height: 40px;");
        css.ShouldContain("grid-template-columns: minmax(0, 1fr) minmax(1.75rem, max-content);");
        css.ShouldNotContain("grid-template-columns: minmax(0, 1fr) 28px;");
        css.ShouldContain("align-items: center;");
        css.ShouldContain(".message-filter-reference");
        css.ShouldContain("border-top: 1px solid color-mix(in srgb, var(--flux-border-soft) 30%, transparent);");
        css.ShouldContain(".message-filter-reference-label");
        css.ShouldContain(".message-filter-variable-list");
        css.ShouldContain(".message-filter-editor ::deep(.mud-input-control)");
        css.ShouldContain("@media (max-width: 760px)");
        css.ShouldNotContain(".message-filter-table-head");
        css.ShouldNotContain(".message-filter-variables");
        css.ShouldNotContain(".message-filter-rule-workbench");
        css.ShouldNotContain(".message-filter-pattern-panel");
        css.ShouldNotContain(".message-filter-expression-panel");
        css.ShouldNotContain(".message-filter-section-heading");
        css.ShouldNotContain(".message-filter-row-index");
        css.ShouldNotContain(".message-filter-panel-title");
        css.ShouldNotContain(".message-filter-panel-header");
        css.ShouldNotContain(".message-filter-panel-kicker");
        css.ShouldNotContain(".message-filter-panel-token");
        css.ShouldNotContain(".message-filter-rule-title");
        css.ShouldNotContain(".message-filter-pattern-column");
        css.ShouldNotContain(".message-filter-expression-column");
        css.ShouldNotContain(".message-filter-column-heading");
        css.ShouldNotContain("grid-template-columns: minmax(0, 1.42fr) minmax(260px, 0.58fr);");
        css.ShouldNotContain(".flow-node-filters");
        css.ShouldNotContain("border-radius: 999px;");
    }

    [Fact]
    public void JsonSchemaValidatorNodeWidget_UsesCompactSummaryAndFlatEditor()
    {
        var root = FindRepositoryRoot();
        var markup = File.ReadAllText(Path.Combine(
            root,
            "src",
            "FluxMq.UI",
            "Components",
            "Workspace",
            "Nodes",
            "JsonSchemaValidator",
            "JsonSchemaValidatorNodeWidget.razor"));
        var css = File.ReadAllText(Path.Combine(
            root,
            "src",
            "FluxMq.UI",
            "Components",
            "Workspace",
            "Nodes",
            "JsonSchemaValidator",
            "JsonSchemaValidatorNodeWidget.razor.css"));

        markup.ShouldContain("EditDialogMaxWidth=\"MaxWidth.Large\"");
        markup.ShouldContain("EditDialogContentClass=\"json-schema-validator-dialog\"");
        markup.ShouldContain("ShowHeaderIcon=\"false\"");
        markup.ShouldContain("ShowDisplayName=\"true\"");
        markup.ShouldContain("ShowCategoryToken=\"false\"");
        markup.ShouldContain("@inject NodeEditDialogRefreshService DialogRefresh");
        markup.ShouldContain("EditorValidationError=\"@ValidateEditor\"");
        markup.ShouldContain("json-schema-validator-summary");
        markup.ShouldContain("json-schema-validator-meta");
        markup.ShouldContain("SchemaTargetCaption");
        markup.ShouldContain("SchemaIdCaption");
        markup.ShouldContain("json-schema-validator-fields");
        markup.ShouldContain("json-schema-validator-field-label");
        markup.ShouldContain("aria-label=\"Validator input fields\"");
        markup.ShouldContain("aria-label=\"Validator output fields\"");
        markup.ShouldContain("json-schema-validator-token");
        markup.ShouldContain("MqttEnvelope");
        markup.ShouldContain("result");
        markup.ShouldContain("valid");
        markup.ShouldContain("invalid");
        markup.ShouldNotContain("json-schema-validator-contracts");
        markup.ShouldNotContain("json-schema-validator-contract-label");
        markup.ShouldContain("json-schema-validator-editor");
        markup.ShouldContain("aria-label=\"JSON schema validator settings\"");
        markup.ShouldContain("json-schema-validator-config-row");
        markup.ShouldContain("aria-label=\"JSON schema configuration\"");
        markup.ShouldContain("json-schema-validator-schema-area");
        markup.ShouldNotContain("json-schema-validator-schema-workspace");
        markup.ShouldContain("Label=\"Schema source\"");
        markup.ShouldContain("ValueChanged=\"@SetSchemaSource\"");
        markup.ShouldContain("aria-label=\"Schema source\"");
        markup.ShouldContain("Label=\"Schema id\"");
        markup.ShouldContain("aria-label=\"Schema id\"");
        markup.ShouldContain("@bind-Value=\"_schemaId\"");
        markup.ShouldContain("Label=\"JSON Schema file\"");
        markup.ShouldContain("aria-label=\"JSON Schema file\"");
        markup.ShouldContain("Value=\"@_schemaPath\"");
        markup.ShouldContain("ValueChanged=\"@SetSchemaPath\"");
        markup.ShouldContain("aria-label=\"Select JSON Schema file\"");
        markup.ShouldContain("PickSchemaFileAsync");
        markup.ShouldContain("DialogRefresh.RefreshAsync(Node.NodeName)");
        markup.ShouldContain("private string? ValidateEditor()");
        markup.ShouldContain("Select a JSON Schema file before saving.");
        markup.ShouldNotContain("@bind-Value=\"_schemaPath\"");
        markup.ShouldContain("json-schema-validator-file-source");
        markup.ShouldContain("aria-label=\"JSON schema file source\"");
        markup.ShouldNotContain("Source</span>");
        markup.ShouldNotContain("Schema ID</span>");
        markup.ShouldNotContain("Schema file</span>");
        markup.ShouldNotContain("json-schema-validator-editor-surface");
        markup.ShouldNotContain("aria-label=\"JSON schema workspace\"");
        markup.ShouldNotContain("json-schema-validator-field-note");
        markup.ShouldNotContain("The validator loads this file when the flow runs.");
        markup.ShouldContain("json-schema-validator-inline-source");
        markup.ShouldContain("aria-label=\"Inline JSON schema\"");
        markup.ShouldContain("CssClass=\"schema-monaco-editor\"");
        markup.ShouldContain("ConstructionOptions=\"@EditorConstructionOptions\"");
        markup.ShouldContain("private StandaloneCodeEditor? _initializedEditor;");
        markup.ShouldContain("!ReferenceEquals(_initializedEditor, _editor)");
        markup.ShouldContain("ScrollBeyondLastLine = false");
        markup.ShouldContain("IsNonCriticalEditorException");
        markup.ShouldNotContain("json-schema-validator-panel-title");
        markup.ShouldNotContain("json-schema-validator-config-grid");
        markup.ShouldNotContain("json-schema-validator-file-panel");
        markup.ShouldNotContain("json-schema-validator-section-heading");
        markup.ShouldNotContain("json-schema-validator-schema-panel");
        markup.ShouldNotContain("json-schema-validator-source-row");
        markup.ShouldNotContain("json-schema-validator-panel-header");
        markup.ShouldNotContain("json-schema-validator-panel-kicker");
        markup.ShouldNotContain("json-schema-validator-panel-token");
        markup.ShouldNotContain("SchemaModeCaption");
        markup.ShouldNotContain("<strong>Schema file</strong>");
        markup.ShouldNotContain("<strong>Inline JSON schema</strong>");
        markup.ShouldNotContain("<MudChip");
        markup.ShouldNotContain("<MudGrid");
        markup.ShouldNotContain("d-flex flex-wrap gap-1");
        markup.ShouldNotContain("<style>");

        css.ShouldContain(".json-schema-validator-summary");
        css.ShouldContain(".json-schema-validator-meta");
        css.ShouldContain("grid-template-columns: minmax(0, 1.34fr) minmax(0, 0.66fr);");
        css.ShouldNotContain("grid-template-columns: minmax(0, 1fr) 90px;");
        css.ShouldContain("-webkit-line-clamp: 2;");
        css.ShouldContain(".json-schema-validator-fields");
        css.ShouldContain(".json-schema-validator-field-row");
        css.ShouldContain(".json-schema-validator-field-label");
        css.ShouldNotContain(".json-schema-validator-contracts");
        css.ShouldNotContain(".json-schema-validator-contract");
        css.ShouldNotContain(".json-schema-validator-contract-label");
        css.ShouldContain("flex: 0 0 100%;");
        css.ShouldContain("white-space: normal;");
        css.ShouldNotContain("grid-template-columns: repeat(3, minmax(0, auto));");
        css.ShouldContain("overflow-wrap: anywhere;");
        css.ShouldNotContain("text-overflow: ellipsis;");
        css.ShouldContain(".json-schema-validator-token");
        css.ShouldContain("display: inline-flex;");
        css.ShouldContain(".json-schema-validator-editor");
        css.ShouldNotContain("gap: 10px;");
        css.ShouldNotContain("gap: 8px;");
        css.ShouldContain("gap: 7px;");
        css.ShouldNotContain(".json-schema-validator-editor-surface");
        css.ShouldContain(".json-schema-validator-config-row");
        css.ShouldContain(".json-schema-validator-schema-area");
        css.ShouldContain("display: grid;");
        css.ShouldContain("flex: 1 1 auto;");
        css.ShouldContain("grid-template-rows: minmax(0, 1fr);");
        css.ShouldContain("height: 100%;");
        css.ShouldContain("min-height: 0;");
        css.ShouldContain("overflow: hidden;");
        css.ShouldNotContain(".json-schema-validator-schema-workspace");
        css.ShouldNotContain("background: color-mix(in srgb, var(--flux-surface-2) 4%, transparent);");
        css.ShouldNotContain("border: 1px solid color-mix(in srgb, var(--flux-border-soft) 42%, transparent);");
        css.ShouldNotContain("border-bottom: 1px solid color-mix(in srgb, var(--flux-border-soft) 34%, transparent);");
        css.ShouldNotContain("padding: 12px;");
        css.ShouldContain("grid-template-columns: minmax(0, 0.64fr) minmax(0, 1.36fr);");
        css.ShouldNotContain("grid-template-columns: minmax(160px, 210px) minmax(0, 1fr);");
        css.ShouldContain(".json-schema-validator-file-source");
        css.ShouldContain(".json-schema-validator-file-row");
        css.ShouldContain("grid-template-columns: minmax(0, 1fr) minmax(1.75rem, max-content);");
        css.ShouldContain(".json-schema-validator-file-row ::deep(.schema-file-picker-button.mud-icon-button)");
        css.ShouldContain("justify-self: end;");
        css.ShouldContain("border-radius: 4px;");
        css.ShouldContain(".json-schema-validator-file-row ::deep(.schema-file-picker-button .mud-icon-root)");
        css.ShouldContain("height: 1.75rem;");
        css.ShouldContain("min-height: 1.75rem;");
        css.ShouldContain("min-width: 1.75rem;");
        css.ShouldContain("width: 1.75rem;");
        css.ShouldContain("font-size: 0.98rem;");
        css.ShouldNotContain("grid-template-columns: minmax(0, 1fr) 28px;");
        css.ShouldNotContain("height: 30px;");
        css.ShouldNotContain("min-height: 30px;");
        css.ShouldNotContain("min-width: 30px;");
        css.ShouldNotContain("width: 30px;");
        css.ShouldNotContain("font-size: 1.04rem;");
        css.ShouldNotContain(".json-schema-validator-field-note");
        css.ShouldContain(".json-schema-validator-inline-source");
        css.ShouldContain(".json-schema-validator-inline-source ::deep(.schema-monaco-editor)");
        css.ShouldContain(".json-schema-validator-inline-source ::deep(.schema-monaco-editor > div)");
        css.ShouldContain(".json-schema-validator-inline-source ::deep(.schema-monaco-editor .monaco-editor)");
        css.ShouldContain("height: 100% !important;");
        css.ShouldNotContain("grid-template-columns: minmax(0, 1fr) 30px;");
        css.ShouldContain("min-height: 34px;");
        css.ShouldNotContain("height: clamp(520px, 68vh, 760px);");
        css.ShouldNotContain("min-height: 520px;");
        css.ShouldNotContain("height: clamp(360px, 58vh, 560px);");
        css.ShouldContain("@media (max-width: 640px)");
        css.ShouldNotContain(".json-schema-validator-schema-panel");
        css.ShouldNotContain(".json-schema-validator-source-row");
        css.ShouldNotContain(".json-schema-validator-panel-title");
        css.ShouldNotContain(".json-schema-validator-config-grid");
        css.ShouldNotContain(".json-schema-validator-file-panel");
        css.ShouldNotContain(".json-schema-validator-section-heading");
        css.ShouldNotContain(".json-schema-validator-panel-header");
        css.ShouldNotContain(".json-schema-validator-panel-kicker");
        css.ShouldNotContain(".json-schema-validator-panel-token");
        css.ShouldNotContain(".flow-node-filters");
        css.ShouldNotContain("border: 1px solid var(--flux-border-strong);");
    }

    [Fact]
    public void DynamicMapperNodeWidget_UsesCompactSummaryAndScopedWorkbench()
    {
        var root = FindRepositoryRoot();
        var markup = File.ReadAllText(Path.Combine(
            root,
            "src",
            "FluxMq.UI",
            "Components",
            "Workspace",
            "Nodes",
            "DynamicMapper",
            "DynamicMapperNodeWidget.razor"));
        var css = File.ReadAllText(Path.Combine(
            root,
            "src",
            "FluxMq.UI",
            "Components",
            "Workspace",
            "Nodes",
            "DynamicMapper",
            "DynamicMapperNodeWidget.razor.css"));

        markup.ShouldContain("EditDialogMaxWidth=\"MaxWidth.ExtraExtraLarge\"");
        markup.ShouldContain("EditDialogContentClass=\"dynamic-mapper-dialog\"");
        markup.ShouldContain("ShowHeaderIcon=\"false\"");
        System.Text.RegularExpressions.Regex.Matches(
                markup,
                @"<MudIcon\b(?:(?!/>).)*?/>",
                System.Text.RegularExpressions.RegexOptions.Singleline)
            .Cast<System.Text.RegularExpressions.Match>()
            .Select(static match => match.Value)
            .Where(static icon => !icon.Contains("aria-hidden=\"true\"", StringComparison.Ordinal))
            .ToArray()
            .ShouldBeEmpty();
        System.Text.RegularExpressions.Regex.Matches(
                markup,
                @"<MudIconButton\b(?:(?!/>).)*?/>",
                System.Text.RegularExpressions.RegexOptions.Singleline)
            .Cast<System.Text.RegularExpressions.Match>()
            .Select(static match => match.Value)
            .Where(static iconButton => !iconButton.Contains("aria-label=", StringComparison.Ordinal) &&
                !iconButton.Contains("AriaLabel=", StringComparison.Ordinal))
            .ToArray()
            .ShouldBeEmpty();
        markup.ShouldContain("ShowDisplayName=\"true\"");
        markup.ShouldContain("ShowCategoryToken=\"false\"");
        markup.ShouldContain("EditorValidationError=\"@ValidateEditor\"");
        markup.ShouldContain("dynamic-mapper-summary");
        markup.ShouldContain("dynamic-mapper-meta");
        markup.ShouldContain("dynamic-mapper-meta-item input");
        markup.ShouldContain("EngineCaption");
        markup.ShouldContain("OutputModeCaption");
        markup.ShouldContain("dynamic-mapper-fields");
        markup.ShouldContain("dynamic-mapper-field-label");
        markup.ShouldContain("aria-label=\"Mapper input variables\"");
        markup.ShouldContain("SummaryVariables");
        markup.ShouldContain("dynamic-mapper-token");
        markup.ShouldNotContain("aria-label=\"Mapper output fields\"");
        markup.ShouldNotContain("Output fields");
        markup.ShouldNotContain("SummaryOutputFields");
        markup.ShouldNotContain("SummaryOutputOverflow");
        markup.ShouldNotContain("dynamic-mapper-contracts");
        markup.ShouldNotContain("dynamic-mapper-contract-label");
        markup.ShouldContain("dynamic-mapper-editor");
        markup.ShouldContain("aria-label=\"Dynamic mapper settings\"");
        markup.ShouldContain("dynamic-mapper-control-row");
        markup.ShouldContain("aria-label=\"Mapper configuration\"");
        markup.ShouldContain("Label=\"Input schema\"");
        markup.ShouldContain("ValueChanged=\"@SetInputType\"");
        markup.ShouldContain("Label=\"Engine\"");
        markup.ShouldContain("ValueChanged=\"@SetEngine\"");
        markup.ShouldContain("Label=\"Result mode\"");
        markup.ShouldContain("Class=\"dynamic-mapper-result-mode-field\"");
        markup.ShouldNotContain("Label=\"Result contract\"");
        markup.ShouldNotContain("Class=\"dynamic-mapper-contract-field\"");
        markup.ShouldContain("ValueChanged=\"@SetOutputMode\"");
        markup.ShouldNotContain("OutputContractCaption");
        markup.ShouldNotContain("OutputContractLabel");
        markup.ShouldNotContain("SetOutputContract");
        markup.ShouldContain("Label=\"Typed schema\"");
        markup.ShouldContain("ValueChanged=\"@SetOutputType\"");
        markup.ShouldContain("Label=\"JSON Schema file\"");
        markup.ShouldContain("ValueChanged=\"@SetOutputSchemaPath\"");
        markup.ShouldContain("PickOutputSchemaFileAsync");
        markup.ShouldContain("aria-label=\"Select schema file\"");
        markup.ShouldContain("dynamic-mapper-workspace");
        markup.ShouldContain("dynamic-mapper-input-workspace");
        markup.ShouldContain("dynamic-mapper-expression-workspace");
        markup.ShouldContain("dynamic-mapper-result-workspace");
        markup.ShouldContain("dynamic-mapper-editor-host");
        markup.ShouldContain("@ref=\"_inputEditorHost\"");
        markup.ShouldContain("@ref=\"_expressionEditorHost\"");
        markup.ShouldContain("@ref=\"_resultEditorHost\"");
        markup.ShouldNotContain("WorkspaceClass");
        markup.ShouldContain("dynamic-mapper-workspace-header");
        markup.ShouldContain("dynamic-mapper-workspace-actions");
        markup.ShouldContain("dynamic-mapper-heading-token");
        markup.ShouldContain("Preview unavailable.");
        markup.ShouldNotContain("Preview is not ready.");
        markup.ShouldNotContain("OutputShapeLabel");
        markup.ShouldContain("ReloadWorkspaceSample");
        markup.ShouldContain("aria-label=\"Reload sample input\"");
        markup.ShouldContain("dynamic-mapper-input-error");
        markup.ShouldContain("role=\"alert\"");
        markup.ShouldContain("private string? ValidateEditor()");
        markup.ShouldContain("Enter a mapping expression before saving.");
        markup.ShouldContain("Id=\"@InputEditorId\"");
        markup.ShouldContain("Id=\"@EditorId\"");
        markup.ShouldContain("ConstructionOptions=\"@InputEditorConstructionOptions\"");
        markup.ShouldContain("ConstructionOptions=\"@EditorConstructionOptions\"");
        markup.ShouldContain("ConstructionOptions=\"@ResultEditorConstructionOptions\"");
        markup.ShouldContain("CssClass=\"dynamic-mapper-monaco-editor dynamic-mapper-input-editor\"");
        markup.ShouldContain("CssClass=\"dynamic-mapper-monaco-editor dynamic-mapper-expression-editor\"");
        markup.ShouldContain("CssClass=\"dynamic-mapper-monaco-editor dynamic-mapper-result-editor\"");
        markup.ShouldContain("private StandaloneCodeEditor? _initializedEditor;");
        markup.ShouldContain("private StandaloneCodeEditor? _initializedInputEditor;");
        markup.ShouldContain("private StandaloneCodeEditor? _initializedResultEditor;");
        markup.ShouldContain("private ElementReference _inputEditorHost;");
        markup.ShouldContain("private ElementReference _expressionEditorHost;");
        markup.ShouldContain("private ElementReference _resultEditorHost;");
        markup.ShouldContain("!ReferenceEquals(_initializedEditor, _editor)");
        markup.ShouldContain("!ReferenceEquals(_initializedInputEditor, _inputEditor)");
        markup.ShouldContain("!ReferenceEquals(_initializedResultEditor, _resultEditor)");
        markup.ShouldContain("private async Task SyncEditorAsync()");
        markup.ShouldContain("private async Task SyncResultEditorAsync()");
        markup.ShouldContain("private async Task LayoutEditorsAfterRenderAsync()");
        markup.ShouldContain("private async Task LayoutEditorAsync(StandaloneCodeEditor? editor, ElementReference host)");
        markup.ShouldContain("fluxmqMonaco.measureElement");
        markup.ShouldContain("await editor.Layout(new Dimension");
        markup.ShouldContain("private sealed class EditorHostSize");
        markup.ShouldContain("DynamicMapperWorkbenchPreview.Preview(");
        markup.ShouldContain("DynamicMapperWorkbenchPreview.PreviewAny(");
        markup.ShouldContain("ScrollBeyondLastLine = false");
        markup.ShouldContain("IsNonCriticalEditorException");
        markup.ShouldNotContain("dynamic-mapper-drawer-grid");
        markup.ShouldNotContain("dynamic-mapper-config-grid");
        markup.ShouldNotContain("dynamic-mapper-source-drawer");
        markup.ShouldNotContain("dynamic-mapper-result-drawer");
        markup.ShouldNotContain("dynamic-mapper-sample-drawer");
        markup.ShouldNotContain("dynamic-mapper-sample-strip");
        markup.ShouldNotContain("Source Fields");
        markup.ShouldNotContain("dynamic-mapper-variable-list");
        markup.ShouldNotContain("dynamic-mapper-shape-list");
        markup.ShouldNotContain("dynamic-mapper-sample-workspace");
        markup.ShouldNotContain("dynamic-mapper-sample-popover");
        markup.ShouldNotContain("ToggleSampleEditorAsync");
        markup.ShouldNotContain("<style>");
        markup.ShouldNotContain("<MudChip");
        markup.ShouldNotContain("<MudAlert");
        markup.ShouldNotContain("d-flex flex-wrap gap-1");
        markup.ShouldNotContain("Class=\"mapper-panel");
        markup.ShouldNotContain("Class=\"mapper-workbench");
        markup.ShouldNotContain("CssClass=\"mapper-monaco-editor");

        css.ShouldContain(".dynamic-mapper-summary");
        css.ShouldContain(".dynamic-mapper-meta");
        css.ShouldContain("grid-template-columns: minmax(0, 1fr) minmax(0, 1fr) minmax(0, 0.58fr) minmax(0, 0.54fr);");
        css.ShouldNotContain("grid-template-columns: minmax(0, 1fr) minmax(0, 1fr) 76px 72px;");
        css.ShouldContain("-webkit-line-clamp: 2;");
        css.ShouldContain(".dynamic-mapper-fields");
        css.ShouldContain(".dynamic-mapper-field-row");
        css.ShouldContain(".dynamic-mapper-field-label");
        css.ShouldNotContain(".dynamic-mapper-contracts");
        css.ShouldNotContain(".dynamic-mapper-contract");
        css.ShouldNotContain(".dynamic-mapper-contract-label");
        css.ShouldNotContain(".dynamic-mapper-contract-field");
        css.ShouldContain(".dynamic-mapper-token");
        css.ShouldContain("flex-wrap: wrap;");
        css.ShouldNotContain("grid-template-columns: repeat(4, minmax(0, auto));");
        css.ShouldContain("flex: 0 0 100%;");
        css.ShouldNotContain("grid-column: 1 / -1;");
        css.ShouldContain("display: inline-flex;");
        css.ShouldContain("overflow-wrap: anywhere;");
        css.ShouldNotContain("text-overflow: ellipsis;");
        css.ShouldContain(".dynamic-mapper-editor");
        css.ShouldContain(".dynamic-mapper-control-row");
        css.ShouldContain("grid-template-columns: minmax(0, 0.66fr) minmax(0, 0.48fr) minmax(0, 0.56fr) minmax(0, 0.78fr);");
        css.ShouldNotContain("grid-template-columns: minmax(128px, 0.74fr) minmax(108px, 0.56fr) minmax(128px, 0.64fr) minmax(160px, 0.9fr);");
        css.ShouldContain("grid-template-columns: minmax(0, 1fr) minmax(1.5rem, max-content);");
        css.ShouldNotContain("grid-template-columns: minmax(0, 1fr) 28px;");
        css.ShouldContain(".dynamic-mapper-workspace");
        css.ShouldContain("grid-template-columns: minmax(240px, 0.72fr) minmax(420px, 1.48fr) minmax(260px, 0.8fr);");
        css.ShouldContain("grid-template-rows: minmax(0, 1fr);");
        css.ShouldNotContain(".dynamic-mapper-workspace.has-sample");
        css.ShouldNotContain("grid-template-rows: minmax(0, 1fr) minmax(128px, 0.2fr);");
        css.ShouldContain("height: 100%;");
        css.ShouldContain("min-height: 0;");
        css.ShouldNotContain("height: clamp(760px, 82vh, 980px);");
        css.ShouldContain("overflow: hidden;");
        css.ShouldContain(".dynamic-mapper-input-workspace");
        css.ShouldContain(".dynamic-mapper-expression-workspace");
        css.ShouldContain(".dynamic-mapper-expression-workspace ::deep(.dynamic-mapper-expression-editor.dynamic-mapper-monaco-editor)");
        css.ShouldContain(".dynamic-mapper-result-workspace");
        css.ShouldContain(".dynamic-mapper-result-workspace ::deep(.dynamic-mapper-result-editor.dynamic-mapper-monaco-editor)");
        css.ShouldContain(".dynamic-mapper-editor-host");
        css.ShouldContain("grid-template-rows: minmax(0, 1fr);");
        css.ShouldContain("height: 100%;");
        css.ShouldContain("min-height: 0;");
        css.ShouldContain(".dynamic-mapper-workspace-actions");
        markup.ShouldContain("PreviewResultMarkerClass");
        markup.ShouldContain("PreviewResultMarkerText");
        markup.ShouldContain("dynamic-mapper-result-marker ok");
        markup.ShouldNotContain("PreviewResultStateClass");
        markup.ShouldNotContain("PreviewResultStateText");
        markup.ShouldNotContain("dynamic-mapper-result-state");
        markup.ShouldNotContain("PreviewStatusClass");
        markup.ShouldNotContain("PreviewStatusText");
        css.ShouldContain(".dynamic-mapper-result-marker");
        css.ShouldContain(".dynamic-mapper-result-marker.ok");
        css.ShouldContain(".dynamic-mapper-result-marker.error");
        css.ShouldNotContain(".dynamic-mapper-result-state");
        css.ShouldNotContain(".dynamic-mapper-result-status");
        css.ShouldContain("min-height: 27px;");
        css.ShouldContain("min-height: 25px;");
        css.ShouldContain("font-size: 0.58rem;");
        css.ShouldContain("height: 1.5rem;");
        css.ShouldContain("min-height: 1.5rem;");
        css.ShouldContain("min-width: 1.5rem;");
        css.ShouldContain("width: 1.5rem;");
        css.ShouldNotContain("height: 24px;");
        css.ShouldNotContain("min-height: 24px;");
        css.ShouldNotContain("min-width: 24px;");
        css.ShouldNotContain("height: 26px;");
        css.ShouldNotContain("min-height: 26px;");
        css.ShouldNotContain("min-width: 26px;");
        css.ShouldContain(".dynamic-mapper-workspace ::deep(.dynamic-mapper-monaco-editor)");
        css.ShouldContain(".dynamic-mapper-workspace ::deep(.dynamic-mapper-monaco-editor > div)");
        css.ShouldContain("box-sizing: border-box;");
        css.ShouldContain("display: block;");
        css.ShouldContain(".dynamic-mapper-workspace ::deep(.dynamic-mapper-input-editor)");
        css.ShouldContain(".dynamic-mapper-input-error");
        css.ShouldNotContain(".dynamic-mapper-drawer-grid");
        css.ShouldNotContain(".dynamic-mapper-config-grid");
        css.ShouldNotContain(".dynamic-mapper-drawer");
        css.ShouldNotContain(".dynamic-mapper-sample-drawer");
        css.ShouldNotContain(".dynamic-mapper-result-grid");
        css.ShouldNotContain(".dynamic-mapper-variable");
        css.ShouldNotContain(".dynamic-mapper-sample-workspace");
        css.ShouldNotContain(".dynamic-mapper-sample-popover");
        css.ShouldNotContain(".dynamic-mapper-sample-strip");
        css.ShouldNotContain("grid-template-rows: minmax(560px, 1fr) 112px;");
        css.ShouldNotContain("grid-template-columns: minmax(0, 0.7fr) minmax(0, 1.4fr) minmax(0, 0.86fr);");
        css.ShouldContain("@media (max-width: 960px)");
        css.ShouldNotContain(".dynamic-mapper-workbench");
        css.ShouldNotContain(".dynamic-mapper-panel");
        css.ShouldNotContain(".mapper-workbench");
        css.ShouldNotContain(".mapper-panel");
        css.ShouldNotContain(".mapper-monaco-editor");
    }

    [Fact]
    public void MqttPublisherNodeWidget_UsesCompactSummaryAndFlatEditor()
    {
        var root = FindRepositoryRoot();
        var markup = File.ReadAllText(Path.Combine(
            root,
            "src",
            "FluxMq.UI",
            "Components",
            "Workspace",
            "Nodes",
            "Actors",
            "MqttPublisherNodeWidget.razor"));
        var css = File.ReadAllText(Path.Combine(
            root,
            "src",
            "FluxMq.UI",
            "Components",
            "Workspace",
            "Nodes",
            "Actors",
            "MqttPublisherNodeWidget.razor.css"));

        markup.ShouldContain("ShowHeaderIcon=\"false\"");
        markup.ShouldContain("ShowDisplayName=\"true\"");
        markup.ShouldContain("ShowCategoryToken=\"false\"");
        markup.ShouldContain("@inject NodeEditDialogRefreshService DialogRefresh");
        markup.ShouldContain("EditorValidationError=\"@ValidateEditor\"");
        markup.ShouldNotContain("EditDialogMaxWidth=");
        markup.ShouldContain("mqtt-publisher-summary");
        markup.ShouldContain("mqtt-publisher-meta");
        markup.ShouldContain("mqtt-publisher-meta-item broker");
        markup.ShouldContain("ActorNodeConfiguration.NormalizeBoundedCapacity(Model.BoundedCapacity)");
        markup.ShouldNotContain("mqtt-publisher-contract");
        markup.ShouldNotContain("aria-label=\"Publish request fields\"");
        markup.ShouldNotContain("mqtt-publisher-token");
        markup.ShouldNotContain("<span class=\"mqtt-publisher-token node-ui-token\">topic</span>");
        markup.ShouldNotContain("<span class=\"mqtt-publisher-token node-ui-token\">payload</span>");
        markup.ShouldNotContain("<span class=\"mqtt-publisher-token node-ui-token\">qos</span>");
        markup.ShouldNotContain("<span class=\"mqtt-publisher-token node-ui-token\">retain</span>");
        markup.ShouldContain("mqtt-publisher-editor");
        markup.ShouldContain("aria-label=\"Publisher settings\"");
        markup.ShouldContain("Label=\"Broker connection\"");
        markup.ShouldContain("Label=\"Connection name\"");
        markup.ShouldContain("mqtt-publisher-broker-cell");
        markup.ShouldNotContain("mqtt-publisher-field-note");
        markup.ShouldNotContain("Add a broker connection in the left panel to enable the dropdown.");
        markup.ShouldNotContain("HelperText=\"Add a broker connection in the left panel to enable the dropdown.\"");
        markup.ShouldContain("Label=\"Input buffer\"");
        markup.ShouldContain("Value=\"@_boundedCapacity\"");
        markup.ShouldContain("ValueChanged=\"@SetBoundedCapacity\"");
        markup.ShouldNotContain("@bind-Value=\"_boundedCapacity\"");
        markup.ShouldContain("Class=\"mqtt-publisher-broker-field\"");
        markup.ShouldContain("Class=\"mqtt-publisher-buffer-field\"");
        markup.ShouldContain("private async Task SetBoundedCapacity(int value)");
        markup.ShouldContain("private string? ValidateEditor()");
        markup.ShouldContain("Input buffer must be between 1 and 100000.");
        markup.ShouldNotContain("<Stat ");
        markup.ShouldNotContain("<MudChip");
        markup.ShouldNotContain("d-flex flex-wrap gap-1");
        markup.ShouldNotContain("Label=\"Broker resource\"");

        css.ShouldContain(".mqtt-publisher-summary");
        css.ShouldContain(".mqtt-publisher-meta");
        css.ShouldContain("grid-template-columns: minmax(0, 1.34fr) minmax(0, 0.66fr);");
        css.ShouldNotContain("grid-template-columns: minmax(0, 1fr) 84px;");
        css.ShouldNotContain(".mqtt-publisher-contract");
        css.ShouldNotContain(".mqtt-publisher-contract-label");
        css.ShouldNotContain(".mqtt-publisher-token");
        css.ShouldNotContain("grid-template-columns: minmax(0, 1fr) repeat(4, auto);");
        css.ShouldContain("white-space: normal;");
        css.ShouldContain("-webkit-line-clamp: 2;");
        css.ShouldContain("overflow-wrap: anywhere;");
        css.ShouldContain(".mqtt-publisher-editor");
        css.ShouldContain(".mqtt-publisher-broker-cell");
        css.ShouldNotContain(".mqtt-publisher-field-note");
        css.ShouldContain("gap: 7px;");
        css.ShouldNotContain("gap: 8px;");
        css.ShouldContain("grid-template-columns: minmax(0, 1fr) minmax(0, 0.36fr);");
        css.ShouldNotContain("grid-template-columns: minmax(0, 1fr) minmax(10rem, 0.36fr);");
        css.ShouldNotContain("grid-template-columns: minmax(0, 1fr) 160px;");
        css.ShouldContain(".mqtt-publisher-editor ::deep(.mud-input-control)");
        css.ShouldContain("min-height: 34px;");
        css.ShouldContain(".mqtt-publisher-editor ::deep(.mud-input-label)");
        css.ShouldContain("font-size: 0.72rem;");
        css.ShouldContain("@media (max-width: 640px)");
        css.ShouldNotContain(".flow-node-filters");
        css.ShouldNotContain("border: 1px solid var(--flux-border-soft);");
    }

    [Fact]
    public void MqttRecorderNodeWidget_UsesCompactSummaryAndFlatEditor()
    {
        var root = FindRepositoryRoot();
        var markup = File.ReadAllText(Path.Combine(
            root,
            "src",
            "FluxMq.UI",
            "Components",
            "Workspace",
            "Nodes",
            "Actors",
            "MqttRecorderNodeWidget.razor"));
        var css = File.ReadAllText(Path.Combine(
            root,
            "src",
            "FluxMq.UI",
            "Components",
            "Workspace",
            "Nodes",
            "Actors",
            "MqttRecorderNodeWidget.razor.css"));

        markup.ShouldContain("ShowHeaderIcon=\"false\"");
        markup.ShouldContain("ShowDisplayName=\"true\"");
        markup.ShouldContain("ShowCategoryToken=\"false\"");
        markup.ShouldContain("@inject NodeEditDialogRefreshService DialogRefresh");
        markup.ShouldContain("EditorValidationError=\"@ValidateEditor\"");
        markup.ShouldNotContain("EditDialogMaxWidth=");
        markup.ShouldContain("mqtt-recorder-summary");
        markup.ShouldContain("mqtt-recorder-meta");
        markup.ShouldContain("mqtt-recorder-meta-item target");
        markup.ShouldContain("Local sessions");
        markup.ShouldContain("ActorNodeConfiguration.NormalizeBoundedCapacity(Model.BoundedCapacity)");
        markup.ShouldNotContain("mqtt-recorder-contract");
        markup.ShouldNotContain("aria-label=\"Recording request fields\"");
        markup.ShouldNotContain("mqtt-recorder-token");
        markup.ShouldNotContain("<span class=\"mqtt-recorder-token node-ui-token\">sessionId</span>");
        markup.ShouldNotContain("<span class=\"mqtt-recorder-token node-ui-token\">envelope</span>");
        markup.ShouldContain("mqtt-recorder-editor");
        markup.ShouldContain("aria-label=\"Recorder settings\"");
        markup.ShouldContain("Label=\"Input buffer\"");
        markup.ShouldContain("Value=\"@_boundedCapacity\"");
        markup.ShouldContain("ValueChanged=\"@SetBoundedCapacity\"");
        markup.ShouldNotContain("@bind-Value=\"_boundedCapacity\"");
        markup.ShouldContain("Class=\"mqtt-recorder-buffer-field\"");
        markup.ShouldContain("private async Task SetBoundedCapacity(int value)");
        markup.ShouldContain("private string? ValidateEditor()");
        markup.ShouldContain("Input buffer must be between 1 and 100000.");
        markup.ShouldNotContain("<Stat ");
        markup.ShouldNotContain("<MudChip");
        markup.ShouldNotContain("d-flex flex-wrap gap-1");
        markup.ShouldNotContain("MqttRecordingRequest");

        css.ShouldContain(".mqtt-recorder-summary");
        css.ShouldContain(".mqtt-recorder-meta");
        css.ShouldContain("grid-template-columns: minmax(0, 1.34fr) minmax(0, 0.66fr);");
        css.ShouldNotContain("grid-template-columns: minmax(0, 1fr) 84px;");
        css.ShouldNotContain(".mqtt-recorder-contract");
        css.ShouldNotContain(".mqtt-recorder-contract-label");
        css.ShouldNotContain(".mqtt-recorder-token");
        css.ShouldNotContain("grid-template-columns: minmax(0, 1fr) repeat(2, auto);");
        css.ShouldContain("white-space: normal;");
        css.ShouldContain("-webkit-line-clamp: 2;");
        css.ShouldContain("overflow-wrap: anywhere;");
        css.ShouldContain(".mqtt-recorder-editor");
        css.ShouldContain("gap: 7px;");
        css.ShouldNotContain("gap: 8px;");
        css.ShouldContain("grid-template-columns: minmax(0, 0.36fr);");
        css.ShouldNotContain("grid-template-columns: minmax(10rem, 0.36fr);");
        css.ShouldNotContain("grid-template-columns: minmax(0, 180px);");
        css.ShouldContain(".mqtt-recorder-editor ::deep(.mud-input-control)");
        css.ShouldContain("min-height: 34px;");
        css.ShouldContain(".mqtt-recorder-editor ::deep(.mud-input-label)");
        css.ShouldContain("font-size: 0.72rem;");
        css.ShouldContain("@media (max-width: 640px)");
        css.ShouldNotContain(".flow-node-filters");
        css.ShouldNotContain("border: 1px solid var(--flux-border-soft);");
    }

    [Fact]
    public void FileWriterNodeWidget_UsesCompactSummaryAndFlatEditor()
    {
        var root = FindRepositoryRoot();
        var markup = File.ReadAllText(Path.Combine(
            root,
            "src",
            "FluxMq.UI",
            "Components",
            "Workspace",
            "Nodes",
            "Actors",
            "FileWriterNodeWidget.razor"));
        var css = File.ReadAllText(Path.Combine(
            root,
            "src",
            "FluxMq.UI",
            "Components",
            "Workspace",
            "Nodes",
            "Actors",
            "FileWriterNodeWidget.razor.css"));

        markup.ShouldContain("ShowHeaderIcon=\"false\"");
        markup.ShouldContain("ShowDisplayName=\"true\"");
        markup.ShouldContain("ShowCategoryToken=\"false\"");
        markup.ShouldContain("@inject NodeEditDialogRefreshService DialogRefresh");
        markup.ShouldContain("EditorValidationError=\"@ValidateEditor\"");
        markup.ShouldNotContain("EditDialogMaxWidth=");
        markup.ShouldContain("file-writer-summary");
        markup.ShouldContain("file-writer-meta");
        markup.ShouldContain("file-writer-meta-item target");
        markup.ShouldContain("Input path");
        markup.ShouldContain("ActorNodeConfiguration.NormalizeBoundedCapacity(Model.BoundedCapacity)");
        markup.ShouldNotContain("file-writer-contract");
        markup.ShouldNotContain("aria-label=\"File write request fields\"");
        markup.ShouldNotContain("file-writer-token");
        markup.ShouldNotContain("<span class=\"file-writer-token node-ui-token\">path</span>");
        markup.ShouldNotContain("<span class=\"file-writer-token node-ui-token\">content</span>");
        markup.ShouldNotContain("<span class=\"file-writer-token node-ui-token\">mode</span>");
        markup.ShouldNotContain("<span class=\"file-writer-token node-ui-token\">createDirectory</span>");
        markup.ShouldContain("file-writer-editor");
        markup.ShouldContain("aria-label=\"File writer settings\"");
        markup.ShouldContain("Label=\"Input buffer\"");
        markup.ShouldContain("Value=\"@_boundedCapacity\"");
        markup.ShouldContain("ValueChanged=\"@SetBoundedCapacity\"");
        markup.ShouldNotContain("@bind-Value=\"_boundedCapacity\"");
        markup.ShouldContain("Class=\"file-writer-buffer-field\"");
        markup.ShouldContain("private async Task SetBoundedCapacity(int value)");
        markup.ShouldContain("private string? ValidateEditor()");
        markup.ShouldContain("Input buffer must be between 1 and 100000.");
        markup.ShouldNotContain("<Stat ");
        markup.ShouldNotContain("<MudChip");
        markup.ShouldNotContain("d-flex flex-wrap gap-1");
        markup.ShouldNotContain("FileWriteRequest");

        css.ShouldContain(".file-writer-summary");
        css.ShouldContain(".file-writer-meta");
        css.ShouldContain("grid-template-columns: minmax(0, 1.34fr) minmax(0, 0.66fr);");
        css.ShouldNotContain("grid-template-columns: minmax(0, 1fr) 84px;");
        css.ShouldNotContain(".file-writer-contract");
        css.ShouldNotContain(".file-writer-contract-label");
        css.ShouldNotContain(".file-writer-token");
        css.ShouldNotContain("grid-template-columns: repeat(4, minmax(0, auto));");
        css.ShouldContain("white-space: normal;");
        css.ShouldContain("-webkit-line-clamp: 2;");
        css.ShouldContain("overflow-wrap: anywhere;");
        css.ShouldContain(".file-writer-editor");
        css.ShouldContain("gap: 7px;");
        css.ShouldNotContain("gap: 8px;");
        css.ShouldContain("grid-template-columns: minmax(0, 0.36fr);");
        css.ShouldNotContain("grid-template-columns: minmax(10rem, 0.36fr);");
        css.ShouldNotContain("grid-template-columns: minmax(0, 180px);");
        css.ShouldContain(".file-writer-editor ::deep(.mud-input-control)");
        css.ShouldContain("min-height: 34px;");
        css.ShouldNotContain("min-height: 36px;");
        css.ShouldContain(".file-writer-editor ::deep(.mud-input-label)");
        css.ShouldContain("font-size: 0.72rem;");
        css.ShouldContain("@media (max-width: 640px)");
        css.ShouldNotContain(".flow-node-filters");
        css.ShouldNotContain("border: 1px solid var(--flux-border-soft);");
    }

    [Fact]
    public void ConditionRouterNodeWidget_UsesCompactSummaryAndFlatEditor()
    {
        var root = FindRepositoryRoot();
        var markup = File.ReadAllText(Path.Combine(
            root,
            "src",
            "FluxMq.UI",
            "Components",
            "Workspace",
            "Nodes",
            "ConditionRouter",
            "ConditionRouterNodeWidget.razor"));
        var css = File.ReadAllText(Path.Combine(
            root,
            "src",
            "FluxMq.UI",
            "Components",
            "Workspace",
            "Nodes",
            "ConditionRouter",
            "ConditionRouterNodeWidget.razor.css"));

        markup.ShouldContain("EditDialogMaxWidth=\"MaxWidth.Large\"");
        markup.ShouldContain("@implements IDisposable");
        markup.ShouldContain("@inject AppThemeService ThemeService");
        markup.ShouldContain("@inject IJSRuntime JsRuntime");
        markup.ShouldContain("@inject NodeEditDialogRefreshService DialogRefresh");
        markup.ShouldContain("EditorValidationError=\"@ValidateEditor\"");
        markup.ShouldContain("ShowHeaderIcon=\"false\"");
        markup.ShouldContain("ShowDisplayName=\"true\"");
        markup.ShouldContain("ShowCategoryToken=\"false\"");
        markup.ShouldContain("condition-router-summary");
        markup.ShouldContain("condition-router-meta");
        markup.ShouldContain("InputTypeCaption");
        markup.ShouldContain("condition-router-expression");
        markup.ShouldContain("aria-label=\"Condition router expression\"");
        markup.ShouldContain("Route condition");
        markup.ShouldContain("When true");
        markup.ShouldContain("When false");
        markup.ShouldContain("ExpressionPreview");
        markup.ShouldContain("condition-router-variables");
        markup.ShouldContain("SummaryVariables");
        markup.ShouldContain("VariableOverflow");
        markup.ShouldContain("condition-router-token");
        markup.ShouldContain("condition-router-editor");
        markup.ShouldContain("aria-label=\"Condition router settings\"");
        markup.ShouldNotContain("condition-router-config-row");
        markup.ShouldNotContain("condition-router-rule-grid");
        markup.ShouldContain("condition-router-rule-row");
        markup.ShouldContain("aria-label=\"Condition router rule\"");
        markup.ShouldContain("condition-router-condition-workspace");
        markup.ShouldContain("condition-router-workspace-header");
        markup.ShouldContain("condition-router-editor-host");
        markup.ShouldContain("StandaloneCodeEditor");
        markup.ShouldContain("CssClass=\"condition-router-code-editor\"");
        markup.ShouldContain("ConstructionOptions=\"@EditorConstructionOptions\"");
        markup.ShouldContain("OnDidChangeModelContent=\"@OnExpressionContentChanged\"");
        markup.ShouldContain("condition-router-sidecar");
        markup.ShouldNotContain("condition-router-editor-surface");
        markup.ShouldNotContain("condition-router-editor-status");
        markup.ShouldNotContain("DraftInputTypeCaption");
        markup.ShouldNotContain("aria-label=\"Condition router draft status\"");
        markup.ShouldNotContain("aria-label=\"Condition router configuration\"");
        markup.ShouldNotContain("condition-router-expression-row");
        markup.ShouldNotContain("condition-router-expression-workspace");
        markup.ShouldNotContain("condition-router-condition-panel");
        markup.ShouldNotContain("condition-router-source-row");
        markup.ShouldContain("Label=\"Input type\"");
        markup.ShouldContain("aria-label=\"Condition input type\"");
        markup.ShouldContain("ValueChanged=\"@SetInputType\"");
        markup.ShouldContain("Class=\"condition-router-input-field\"");
        markup.ShouldNotContain("condition-router-output-map");
        markup.ShouldContain("aria-label=\"Condition expression\"");
        markup.ShouldNotContain("Label=\"Route condition\"");
        markup.ShouldNotContain("Value=\"@_expression\"");
        markup.ShouldNotContain("ValueChanged=\"@SetExpression\"");
        markup.ShouldNotContain("Immediate=\"true\"");
        markup.ShouldNotContain("@bind-Value=\"_expression\"");
        markup.ShouldNotContain("Lines=\"8\"");
        markup.ShouldNotContain("Class=\"condition-router-expression-field\"");
        markup.ShouldNotContain("condition-router-expression-cell");
        markup.ShouldNotContain("HelperText=\"@ExpressionHelper\"");
        markup.ShouldNotContain("ExpressionHelper");
        markup.ShouldNotContain("condition-router-field-note");
        markup.ShouldContain("condition-router-variable-reference");
        markup.ShouldContain("condition-router-variable-list");
        markup.ShouldNotContain("condition-router-variable-strip");
        markup.ShouldContain("condition-router-variable-label");
        markup.ShouldContain("condition-router-variable-token");
        markup.ShouldContain("Variables");
        markup.ShouldNotContain("condition-router-variable-reference-grid");
        markup.ShouldNotContain("condition-router-variable-reference-row");
        markup.ShouldNotContain("<Stat ");
        markup.ShouldNotContain("<MudStack");
        markup.ShouldNotContain("<MudChip");
        markup.ShouldNotContain("<MudGrid");
        markup.ShouldNotContain("<MudItem");
        markup.ShouldNotContain("<MudExpansionPanel");
        markup.ShouldNotContain("<MudSimpleTable");
        markup.ShouldNotContain("d-flex flex-wrap gap-1");
        markup.ShouldNotContain("node-expr-preview");
        markup.ShouldNotContain("node-expr-field");
        markup.ShouldNotContain("condition-router-route-strip");
        markup.ShouldNotContain("condition-router-variable-panel");
        markup.ShouldNotContain("condition-router-panel-title");
        markup.ShouldNotContain("condition-router-config-grid");
        markup.ShouldNotContain("condition-router-panel-header");
        markup.ShouldNotContain("condition-router-panel-kicker");
        markup.ShouldNotContain("condition-router-panel-token");
        markup.ShouldContain("private async Task SetInputType(string value)");
        markup.ShouldNotContain("private async Task SetExpression(string value)");
        markup.ShouldContain("private async Task OnExpressionContentChanged()");
        markup.ShouldContain("private StandaloneEditorConstructionOptions EditorConstructionOptions(StandaloneCodeEditor editor)");
        markup.ShouldContain("private async Task ConfigureCodeEditorAsync()");
        markup.ShouldContain("fluxmqMonaco.measureElement");
        markup.ShouldContain("await DialogRefresh.RefreshAsync(Node.NodeName);");
        markup.ShouldContain("private string? ValidateEditor()");
        markup.ShouldContain("Enter a route condition before saving.");

        css.ShouldContain(".condition-router-summary");
        css.ShouldContain(".condition-router-meta");
        css.ShouldContain("grid-template-columns: minmax(0, 1.18fr) minmax(0, 0.58fr) minmax(0, 0.62fr);");
        css.ShouldNotContain("grid-template-columns: minmax(0, 1fr) 76px 82px;");
        css.ShouldContain("display: -webkit-box;");
        css.ShouldContain("-webkit-line-clamp: 2;");
        css.ShouldContain(".condition-router-expression");
        css.ShouldContain(".condition-router-variables");
        css.ShouldContain("flex-wrap: wrap;");
        css.ShouldContain(".condition-router-expression strong");
        css.ShouldNotContain("grid-template-columns: repeat(4, minmax(0, auto));");
        css.ShouldContain("flex: 0 0 100%;");
        css.ShouldNotContain("grid-column: 1 / -1;");
        css.ShouldContain(".condition-router-token");
        css.ShouldContain("display: inline-flex;");
        css.ShouldContain("overflow-wrap: anywhere;");
        css.ShouldContain("white-space: normal;");
        css.ShouldNotContain("text-overflow: ellipsis;");
        css.ShouldContain(".condition-router-editor");
        css.ShouldNotContain("gap: 10px;");
        css.ShouldContain("gap: 7px;");
        css.ShouldNotContain("gap: 8px;");
        css.ShouldContain("gap: 4px;");
        css.ShouldContain("padding-top: 7px;");
        css.ShouldNotContain("padding-left: 8px;");
        css.ShouldNotContain("padding-left: 10px;");
        css.ShouldContain("min-height: 34px;");
        css.ShouldNotContain("min-height: 36px;");
        css.ShouldNotContain(".condition-router-config-row");
        css.ShouldNotContain(".condition-router-rule-grid");
        css.ShouldContain(".condition-router-rule-row");
        css.ShouldContain("grid-template-columns: minmax(0, 1.5fr) minmax(0, 0.5fr);");
        css.ShouldContain("height: clamp(420px, 58vh, 680px);");
        css.ShouldContain(".condition-router-condition-workspace");
        css.ShouldContain(".condition-router-workspace-header");
        css.ShouldContain(".condition-router-editor-host");
        css.ShouldContain(".condition-router-sidecar");
        css.ShouldContain(".condition-router-condition-workspace ::deep(.condition-router-code-editor)");
        css.ShouldNotContain(".condition-router-editor-surface");
        css.ShouldNotContain(".condition-router-editor-status");
        css.ShouldNotContain(".condition-router-editor-status > div");
        css.ShouldNotContain(".condition-router-expression-row");
        css.ShouldNotContain(".condition-router-expression-workspace");
        css.ShouldNotContain(".condition-router-condition-panel");
        css.ShouldNotContain("background: color-mix(in srgb, var(--flux-surface-2) 4%, transparent);");
        css.ShouldNotContain("border: 1px solid color-mix(in srgb, var(--flux-border-soft) 42%, transparent);");
        css.ShouldNotContain("padding: 12px;");
        css.ShouldNotContain("padding: 14px;");
        css.ShouldNotContain(".condition-router-source-row");
        css.ShouldContain("grid-template-columns: minmax(0, 1fr);");
        css.ShouldNotContain("grid-template-columns: minmax(0, 1fr) minmax(0, 0.42fr);");
        css.ShouldNotContain("grid-template-columns: minmax(0, 240px);");
        css.ShouldNotContain("grid-template-columns: minmax(0, 1fr) minmax(160px, 0.34fr);");
        css.ShouldNotContain("grid-template-columns: minmax(180px, 240px);");
        css.ShouldNotContain("grid-template-columns: minmax(0, 1fr) minmax(172px, 220px);");
        css.ShouldNotContain("grid-template-columns: minmax(180px, 1fr) minmax(230px, 0.72fr);");
        css.ShouldNotContain(".condition-router-output-map");
        css.ShouldNotContain("border-left: 2px solid");
        css.ShouldNotContain("border-left: 1px solid color-mix(in srgb, var(--flux-border-soft) 58%, transparent);");
        css.ShouldContain("border-top: 1px solid color-mix(in srgb, var(--flux-border-soft) 42%, transparent);");
        css.ShouldNotContain(".condition-router-expression-cell");
        css.ShouldContain(".condition-router-variable-reference");
        css.ShouldContain(".condition-router-variable-list");
        css.ShouldNotContain(".condition-router-variable-strip");
        css.ShouldContain(".condition-router-variable-label");
        css.ShouldContain(".condition-router-variable-token");
        css.ShouldContain("grid-template-columns: minmax(0, 1fr) minmax(0, 0.46fr);");
        css.ShouldNotContain("grid-template-columns: minmax(0, 1fr) auto;");
        css.ShouldNotContain("grid-template-columns: 74px minmax(0, 1fr);");
        css.ShouldNotContain(".condition-router-editor ::deep(.condition-router-expression-field textarea.mud-input-root)");
        css.ShouldNotContain("min-height: 168px;");
        css.ShouldContain(".condition-router-editor ::deep(.mud-input-control)");
        css.ShouldContain("@media (max-width: 760px)");
        css.ShouldContain("padding-top: 7px;");
        css.ShouldNotContain("padding-top: 8px;");
        css.ShouldNotContain(".flow-node-filters");
        css.ShouldNotContain(".condition-router-route-strip");
        css.ShouldNotContain(".condition-router-variable-panel");
        css.ShouldNotContain(".condition-router-variable-reference-grid");
        css.ShouldNotContain(".condition-router-variable-reference-row");
        css.ShouldNotContain(".condition-router-field-note");
        css.ShouldNotContain(".condition-router-panel-title");
        css.ShouldNotContain(".condition-router-config-grid");
        css.ShouldNotContain(".condition-router-panel-header");
        css.ShouldNotContain(".condition-router-panel-kicker");
        css.ShouldNotContain(".condition-router-panel-token");
        css.ShouldNotContain("border-radius: 999px;");
    }

    [Fact]
    public void RoutingSwitchNodeWidget_UsesCompactSummaryAndFlatEditor()
    {
        var root = FindRepositoryRoot();
        var markup = File.ReadAllText(Path.Combine(
            root,
            "src",
            "FluxMq.UI",
            "Components",
            "Workspace",
            "Nodes",
            "Routing",
            "RoutingSwitchNodeWidget.razor"));
        var css = File.ReadAllText(Path.Combine(
            root,
            "src",
            "FluxMq.UI",
            "Components",
            "Workspace",
            "Nodes",
            "Routing",
            "RoutingSwitchNodeWidget.razor.css"));

        markup.ShouldContain("@implements IDisposable");
        markup.ShouldContain("EditDialogMaxWidth=\"MaxWidth.Large\"");
        markup.ShouldContain("ShowHeaderIcon=\"false\"");
        markup.ShouldContain("ShowDisplayName=\"true\"");
        markup.ShouldContain("ShowCategoryToken=\"false\"");
        markup.ShouldContain("@inject AppThemeService ThemeService");
        markup.ShouldContain("@inject IJSRuntime JsRuntime");
        markup.ShouldContain("@inject NodeEditDialogRefreshService DialogRefresh");
        markup.ShouldContain("EditorValidationError=\"@ValidateEditor\"");
        markup.ShouldContain("routing-switch-summary");
        markup.ShouldContain("routing-switch-meta");
        markup.ShouldContain("InputTypeCaption");
        markup.ShouldContain("RouteCountCaption");
        markup.ShouldContain("EnvelopeCaption");
        markup.ShouldContain("BufferCaption");
        markup.ShouldContain("routing-switch-expression");
        markup.ShouldContain("aria-label=\"Routing switch expression\"");
        markup.ShouldContain("routing-switch-summary-label");
        markup.ShouldContain("ExpressionPreview");
        markup.ShouldContain("routing-switch-routes");
        markup.ShouldContain("aria-label=\"Routing switch routes\"");
        markup.ShouldContain("RoutePreview");
        markup.ShouldContain("RoutePreviewOverflow");
        markup.ShouldContain("routing-switch-token");
        markup.ShouldNotContain("routing-switch-contract-label");
        markup.ShouldContain("routing-switch-editor");
        markup.ShouldContain("aria-label=\"Routing switch settings\"");
        markup.ShouldContain("routing-switch-rule-row");
        markup.ShouldContain("aria-label=\"Routing switch rule\"");
        markup.ShouldContain("routing-switch-expression-workspace node-ui-code-surface");
        markup.ShouldContain("aria-label=\"Routing expression\"");
        markup.ShouldContain("routing-switch-workspace-header");
        markup.ShouldContain("routing-switch-kicker");
        markup.ShouldContain("Route match value");
        markup.ShouldContain("routing-switch-editor-host");
        markup.ShouldContain("@ref=\"_expressionEditorHost\"");
        markup.ShouldContain("<StandaloneCodeEditor @key=\"_editorVersion\"");
        markup.ShouldContain("@ref=\"_editor\"");
        markup.ShouldContain("Id=\"@EditorId\"");
        markup.ShouldContain("CssClass=\"routing-switch-code-editor\"");
        markup.ShouldContain("ConstructionOptions=\"@EditorConstructionOptions\"");
        markup.ShouldContain("OnDidChangeModelContent=\"@OnExpressionContentChanged\"");
        markup.ShouldContain("routing-switch-sidecar");
        markup.ShouldContain("routing-switch-config-row");
        markup.ShouldNotContain("routing-switch-editor-surface");
        markup.ShouldNotContain("aria-label=\"Routing switch configuration\"");
        markup.ShouldContain("Label=\"Input type\"");
        markup.ShouldContain("aria-label=\"Routing switch input type\"");
        markup.ShouldContain("@bind-Value=\"_inputType\"");
        markup.ShouldContain("Class=\"routing-switch-input-field\"");
        markup.ShouldContain("Label=\"Input buffer\"");
        markup.ShouldContain("aria-label=\"Routing switch input buffer\"");
        markup.ShouldContain("@bind-Value=\"_boundedCapacity\"");
        markup.ShouldContain("Class=\"routing-switch-buffer-field\"");
        markup.ShouldContain("Label=\"Emit envelope\"");
        markup.ShouldContain("aria-label=\"Emit route envelope\"");
        markup.ShouldContain("@bind-Value=\"_emitRouteEnvelope\"");
        markup.ShouldContain("Class=\"routing-switch-envelope-check\"");
        markup.ShouldNotContain("routing-switch-option-row");
        markup.ShouldNotContain("Label=\"Expression\"");
        markup.ShouldNotContain("Value=\"@_expression\"");
        markup.ShouldNotContain("ValueChanged=\"@SetExpression\"");
        markup.ShouldNotContain("@bind-Value=\"_expression\"");
        markup.ShouldNotContain("Lines=\"5\"");
        markup.ShouldNotContain("Class=\"routing-switch-expression-field\"");
        markup.ShouldNotContain("routing-switch-rule-grid");
        markup.ShouldNotContain("routing-switch-expression-stack");
        markup.ShouldNotContain("routing-switch-expression-row");
        markup.ShouldNotContain("aria-label=\"Routing switch rule expression\"");
        markup.ShouldContain("routing-switch-variable-reference");
        markup.ShouldContain("ExpressionVariables");
        markup.ShouldContain("routing-switch-variable-token");
        markup.ShouldContain("routing-switch-route-composer");
        markup.ShouldContain("aria-label=\"Routing switch route outputs\"");
        markup.ShouldContain("routing-switch-route-header");
        markup.ShouldContain("routing-switch-route-list");
        markup.ShouldContain("AddRoute");
        markup.ShouldContain("Class=\"routing-switch-route-add\"");
        markup.ShouldContain("Text=\"@AddRouteLabel\"");
        markup.ShouldContain("aria-label=\"@AddRouteLabel\"");
        markup.ShouldContain("private string AddRouteLabel => $\"Add route to {Node.NodeName}\";");
        markup.ShouldContain("routing-switch-route-row");
        markup.ShouldContain("aria-label=\"@($\"Route match key {index + 1}\")\"");
        markup.ShouldContain("aria-label=\"@($\"Route output port {index + 1}\")\"");
        markup.ShouldContain("Value=\"@route.Key\"");
        markup.ShouldContain("ValueChanged=\"@(value => SetRouteKeyAsync(route, value))\"");
        markup.ShouldContain("Value=\"@route.OutputPort\"");
        markup.ShouldContain("ValueChanged=\"@(value => SetRouteOutputAsync(route, value))\"");
        markup.ShouldNotContain("@bind-Value=\"route.Key\"");
        markup.ShouldNotContain("@bind-Value=\"route.OutputPort\"");
        markup.ShouldContain("Class=\"routing-switch-route-key-field\"");
        markup.ShouldContain("Class=\"routing-switch-route-output-field\"");
        markup.ShouldContain("RemoveRoute(route)");
        markup.ShouldContain("Text=\"@RemoveRouteLabel(route)\"");
        markup.ShouldContain("aria-label=\"@RemoveRouteLabel(route)\"");
        markup.ShouldContain("private string RemoveRouteLabel(RouteDraft route)");
        markup.ShouldContain("$\"Remove {target} from {Node.NodeName}\"");
        markup.ShouldNotContain("Text=\"Add route\"");
        markup.ShouldNotContain("aria-label=\"Add route\"");
        markup.ShouldNotContain("Text=\"Remove route\"");
        markup.ShouldNotContain("aria-label=\"@($\"Remove route {route.Key}\")\"");
        markup.ShouldContain("FormatRouteDrafts");
        markup.ShouldContain("private StandaloneCodeEditor? _editor;");
        markup.ShouldContain("private StandaloneCodeEditor? _initializedEditor;");
        markup.ShouldContain("private ElementReference _expressionEditorHost;");
        markup.ShouldContain("private int _editorVersion;");
        markup.ShouldContain("private bool _syncingEditor;");
        markup.ShouldContain("private string EditorId => $\"routing-switch-{Node.NodeName.Replace('.', '-').Replace(' ', '-')}\";");
        markup.ShouldContain("protected override void OnInitialized()");
        markup.ShouldContain("ThemeService.Changed += OnThemeChanged;");
        markup.ShouldContain("protected override async Task OnAfterRenderAsync(bool firstRender)");
        markup.ShouldContain("ConfigureCodeEditorAsync");
        markup.ShouldContain("SyncEditorAsync");
        markup.ShouldContain("LayoutEditorAfterRenderAsync");
        markup.ShouldContain("public void Dispose()");
        markup.ShouldContain("ThemeService.Changed -= OnThemeChanged;");
        markup.ShouldContain("_editorVersion++;");
        markup.ShouldContain("private StandaloneEditorConstructionOptions EditorConstructionOptions(StandaloneCodeEditor editor)");
        markup.ShouldContain("Language = \"csharp\"");
        markup.ShouldContain("Theme = ThemeService.IsDarkMode ? \"fluxmq-dark\" : \"fluxmq-light\"");
        markup.ShouldContain("fluxmqMonaco.ensureConfigured");
        markup.ShouldContain("fluxmqMonaco.measureElement");
        markup.ShouldContain("private async Task SaveAsync()");
        markup.ShouldContain("_expression = await _editor.GetValue();");
        markup.ShouldContain("private async Task AddRoute()");
        markup.ShouldContain("private async Task RemoveRoute(RouteDraft route)");
        markup.ShouldContain("private async Task OnExpressionContentChanged()");
        markup.ShouldContain("private async Task SetRouteKeyAsync(RouteDraft route, string value)");
        markup.ShouldContain("private async Task SetRouteOutputAsync(RouteDraft route, string value)");
        markup.ShouldContain("private async Task RefreshDialogAsync()");
        markup.ShouldContain("await DialogRefresh.RefreshAsync(Node.NodeName);");
        markup.ShouldContain("await LayoutEditorAfterRenderAsync();");
        markup.ShouldContain("private string? ValidateEditor()");
        markup.ShouldContain("Enter a routing expression before saving.");
        markup.ShouldContain("Add at least one complete route before saving.");
        markup.ShouldContain("Each route needs a match key.");
        markup.ShouldContain("Each route needs an output port.");
        markup.ShouldContain("Route match keys must be unique.");
        markup.ShouldContain("private static bool IsCompleteRoute(RouteDraft route)");
        markup.ShouldContain("private string? _loadedNodeName;");
        markup.ShouldContain("private FlowDiagramNodeModel? _loadedNode;");
        markup.ShouldContain("ReferenceEquals(_loadedNode, Node)");
        markup.ShouldContain("_loadedNode = Node;");
        markup.ShouldNotContain("routing-switch-panel-header");
        markup.ShouldNotContain("routing-switch-panel-kicker");
        markup.ShouldNotContain("routing-switch-panel-token");
        markup.ShouldNotContain("SwitchPanelCaption");
        markup.ShouldNotContain("routing-switch-rule-panel");
        markup.ShouldNotContain("routing-switch-source-row");
        markup.ShouldNotContain("routing-switch-expression-cell");
        markup.ShouldNotContain("routing-switch-route-editor");
        markup.ShouldNotContain("<Stat ");
        markup.ShouldNotContain("<MudStack");
        markup.ShouldNotContain("<MudChip");
        markup.ShouldNotContain("<MudGrid");
        markup.ShouldNotContain("<MudItem");
        markup.ShouldNotContain("d-flex flex-wrap gap-1");
        markup.ShouldNotContain("node-expr-preview");
        markup.ShouldNotContain("node-expr-field");
        markup.ShouldNotContain("@bind-Value=\"_routesText\"");
        markup.ShouldNotContain("Class=\"routing-switch-routes-field\"");
        markup.ShouldNotContain("Label=\"Match key\"");
        markup.ShouldNotContain("Label=\"Output port\"");
        markup.ShouldNotContain("routing-switch-route-table");
        markup.ShouldNotContain("routing-switch-route-toolbar");
        markup.ShouldNotContain("routing-switch-panel-title");
        markup.ShouldNotContain("routing-switch-config-grid");
        markup.ShouldNotContain("routing-switch-route-workspace");
        markup.ShouldNotContain("routing-switch-route-section");
        markup.ShouldNotContain("routing-switch-route-column-header");
        markup.ShouldNotContain("routing-switch-route-actions");
        markup.ShouldNotContain("routing-switch-route-title");
        markup.ShouldNotContain("routing-switch-route-index");
        markup.ShouldNotContain("Match result to output port");

        css.ShouldContain(".routing-switch-summary");
        css.ShouldContain(".routing-switch-meta");
        css.ShouldContain("grid-template-columns: minmax(0, 1.22fr) minmax(0, 0.58fr) minmax(0, 0.64fr) minmax(0, 0.6fr);");
        css.ShouldNotContain("grid-template-columns: minmax(0, 1fr) 62px 68px 64px;");
        css.ShouldContain("display: -webkit-box;");
        css.ShouldContain("-webkit-line-clamp: 2;");
        css.ShouldContain(".routing-switch-expression");
        css.ShouldContain(".routing-switch-routes");
        css.ShouldContain(".routing-switch-summary-label");
        css.ShouldNotContain(".routing-switch-contract-label");
        css.ShouldContain("display: flex;");
        css.ShouldNotContain("grid-template-columns: repeat(4, minmax(0, auto));");
        css.ShouldContain(".routing-switch-token");
        css.ShouldContain("display: inline-flex;");
        css.ShouldContain("overflow-wrap: anywhere;");
        css.ShouldContain("white-space: normal;");
        css.ShouldNotContain("text-overflow: ellipsis;");
        css.ShouldContain(".routing-switch-editor");
        css.ShouldNotContain("gap: 8px;");
        css.ShouldContain("gap: 7px;");
        css.ShouldContain("padding-top: 7px;");
        css.ShouldNotContain("padding-top: 8px;");
        css.ShouldNotContain("padding-top: 10px;");
        css.ShouldContain("min-height: 34px;");
        css.ShouldNotContain("min-height: 36px;");
        css.ShouldContain(".routing-switch-envelope-option ::deep(.mud-checkbox)");
        css.ShouldContain("display: inline-flex;");
        css.ShouldContain(".routing-switch-envelope-option ::deep(.mud-checkbox .mud-button-root)");
        css.ShouldContain("padding: 3px;");
        css.ShouldContain(".routing-switch-envelope-option ::deep(.mud-checkbox .mud-typography)");
        css.ShouldContain("margin: 0;");
        css.ShouldContain(".routing-switch-rule-row");
        css.ShouldContain("height: clamp(460px, 62vh, 720px);");
        css.ShouldContain("grid-template-columns: minmax(0, 1.45fr) minmax(0, 0.62fr);");
        css.ShouldContain(".routing-switch-expression-workspace");
        css.ShouldContain("grid-template-rows: auto minmax(0, 1fr);");
        css.ShouldContain(".routing-switch-workspace-header");
        css.ShouldContain(".routing-switch-kicker");
        css.ShouldContain(".routing-switch-editor-host");
        css.ShouldContain(".routing-switch-sidecar");
        css.ShouldContain("grid-template-rows: auto auto minmax(0, 1fr);");
        css.ShouldContain(".routing-switch-config-row");
        css.ShouldNotContain(".routing-switch-rule-grid");
        css.ShouldNotContain(".routing-switch-expression-stack");
        css.ShouldNotContain(".routing-switch-editor-surface");
        css.ShouldNotContain(".routing-switch-expression-row");
        css.ShouldContain(".routing-switch-route-composer");
        css.ShouldNotContain(".routing-switch-option-row");
        css.ShouldContain(".routing-switch-route-header");
        css.ShouldNotContain("background: color-mix(in srgb, var(--flux-surface-2) 4%, transparent);");
        css.ShouldNotContain("border: 1px solid color-mix(in srgb, var(--flux-border-soft) 42%, transparent);");
        css.ShouldNotContain("gap: 10px;");
        css.ShouldNotContain("padding: 12px;");
        css.ShouldNotContain(".routing-switch-panel-header");
        css.ShouldNotContain(".routing-switch-panel-kicker");
        css.ShouldNotContain(".routing-switch-panel-token");
        css.ShouldNotContain(".routing-switch-rule-panel");
        css.ShouldNotContain(".routing-switch-source-row");
        css.ShouldNotContain("grid-template-columns: minmax(0, 1fr) minmax(0, 0.52fr) minmax(0, 0.62fr);");
        css.ShouldNotContain("grid-template-columns: minmax(0, 1fr) minmax(130px, 170px) minmax(128px, 154px);");
        css.ShouldNotContain("grid-template-columns: minmax(0, 1fr) minmax(172px, 220px);");
        css.ShouldContain("grid-template-columns: minmax(0, 1fr);");
        css.ShouldContain(".routing-switch-variable-reference");
        css.ShouldContain(".routing-switch-variable-token");
        css.ShouldContain("grid-template-columns: minmax(0, 1fr) minmax(0, 0.44fr);");
        css.ShouldNotContain("border-left: 1px solid color-mix(in srgb, var(--flux-border-soft) 58%, transparent);");
        css.ShouldContain("flex-wrap: wrap;");
        css.ShouldNotContain("grid-template-columns: repeat(3, minmax(0, auto));");
        css.ShouldNotContain(".routing-switch-expression-cell");
        css.ShouldNotContain(".routing-switch-route-editor");
        css.ShouldContain(".routing-switch-route-list");
        css.ShouldContain(".routing-switch-route-row");
        css.ShouldNotContain(".routing-switch-route-index");
        css.ShouldContain("grid-template-columns: minmax(0, 1fr) minmax(0, 1fr) minmax(1.75rem, max-content);");
        css.ShouldContain("overflow-wrap: anywhere;");
        css.ShouldNotContain("grid-template-columns: minmax(0, 1fr) minmax(0, 1fr) 30px;");
        css.ShouldNotContain("grid-template-columns: minmax(0, 1fr) minmax(0, 1fr) 28px;");
        css.ShouldContain("min-height: 40px;");
        css.ShouldNotContain("min-height: 42px;");
        css.ShouldContain("align-items: center;");
        css.ShouldContain(".routing-switch-route-header ::deep(.routing-switch-route-add.mud-icon-button)");
        css.ShouldContain(".routing-switch-editor ::deep(.mud-input-control)");
        css.ShouldContain("align-self: center;");
        css.ShouldContain("justify-self: end;");
        css.ShouldContain("border-radius: 4px;");
        css.ShouldContain("@media (max-width: 720px)");
        css.ShouldContain(".routing-switch-expression-workspace ::deep(.routing-switch-code-editor)");
        css.ShouldContain(".routing-switch-expression-workspace ::deep(.routing-switch-code-editor > div)");
        css.ShouldContain(".routing-switch-expression-workspace ::deep(.routing-switch-code-editor .monaco-editor)");
        css.ShouldContain(".routing-switch-expression-workspace ::deep(.routing-switch-code-editor .overflow-guard)");
        css.ShouldContain("height: 100% !important;");
        css.ShouldContain("min-height: 380px;");
        css.ShouldNotContain(".routing-switch-editor ::deep(.routing-switch-expression-field textarea.mud-input-root)");
        css.ShouldNotContain(".flow-node-filters");
        css.ShouldNotContain(".routing-switch-routes-field");
        css.ShouldNotContain(".routing-switch-route-table");
        css.ShouldNotContain(".routing-switch-route-toolbar");
        css.ShouldNotContain(".routing-switch-panel-title");
        css.ShouldNotContain(".routing-switch-config-grid");
        css.ShouldNotContain(".routing-switch-route-workspace");
        css.ShouldNotContain(".routing-switch-route-section");
        css.ShouldNotContain(".routing-switch-route-column-header");
        css.ShouldNotContain(".routing-switch-route-actions");
        css.ShouldNotContain(".routing-switch-route-title");
        css.ShouldNotContain("grid-template-columns: minmax(0, 1fr) 150px minmax(0, 190px);");
        css.ShouldNotContain("padding: 14px;");
        css.ShouldNotContain("border-radius: 999px;");
    }

    [Fact]
    public void RoutingFanNodeWidgets_UseCompactSummaryAndFlatEditors()
    {
        var root = FindRepositoryRoot();
        var forkMarkup = File.ReadAllText(Path.Combine(
            root,
            "src",
            "FluxMq.UI",
            "Components",
            "Workspace",
            "Nodes",
            "Routing",
            "RoutingForkNodeWidget.razor"));
        var forkCss = File.ReadAllText(Path.Combine(
            root,
            "src",
            "FluxMq.UI",
            "Components",
            "Workspace",
            "Nodes",
            "Routing",
            "RoutingForkNodeWidget.razor.css"));
        var mergeMarkup = File.ReadAllText(Path.Combine(
            root,
            "src",
            "FluxMq.UI",
            "Components",
            "Workspace",
            "Nodes",
            "Routing",
            "RoutingMergeNodeWidget.razor"));
        var mergeCss = File.ReadAllText(Path.Combine(
            root,
            "src",
            "FluxMq.UI",
            "Components",
            "Workspace",
            "Nodes",
            "Routing",
            "RoutingMergeNodeWidget.razor.css"));

        forkMarkup.ShouldContain("EditDialogMaxWidth=\"MaxWidth.Medium\"");
        forkMarkup.ShouldContain("ShowHeaderIcon=\"false\"");
        forkMarkup.ShouldContain("ShowDisplayName=\"true\"");
        forkMarkup.ShouldContain("ShowCategoryToken=\"false\"");
        forkMarkup.ShouldContain("@inject NodeEditDialogRefreshService DialogRefresh");
        forkMarkup.ShouldContain("EditorValidationError=\"@ValidateEditor\"");
        forkMarkup.ShouldContain("routing-fork-summary");
        forkMarkup.ShouldContain("routing-fork-meta");
        forkMarkup.ShouldContain("InputTypeCaption");
        forkMarkup.ShouldContain("OutputCountCaption");
        forkMarkup.ShouldContain("BufferCaption");
        forkMarkup.ShouldContain("routing-fork-ports");
        forkMarkup.ShouldContain("aria-label=\"Routing fork outputs\"");
        forkMarkup.ShouldContain("routing-fork-summary-label");
        forkMarkup.ShouldContain("OutputPreview");
        forkMarkup.ShouldContain("OutputPreviewOverflow");
        forkMarkup.ShouldContain("routing-fork-token");
        forkMarkup.ShouldNotContain("routing-fork-contract-label");
        forkMarkup.ShouldContain("routing-fork-editor");
        forkMarkup.ShouldContain("aria-label=\"Routing fork settings\"");
        forkMarkup.ShouldContain("routing-fork-layout");
        forkMarkup.ShouldContain("aria-label=\"Routing fork editor layout\"");
        forkMarkup.ShouldContain("routing-fork-sidecar");
        forkMarkup.ShouldContain("aria-label=\"Routing fork support settings\"");
        forkMarkup.ShouldNotContain("routing-fork-editor-surface");
        forkMarkup.ShouldNotContain("routing-fork-config-row");
        forkMarkup.ShouldNotContain("aria-label=\"Routing fork configuration\"");
        forkMarkup.ShouldContain("Label=\"Input type\"");
        forkMarkup.ShouldContain("aria-label=\"Routing fork input type\"");
        forkMarkup.ShouldContain("@bind-Value=\"_inputType\"");
        forkMarkup.ShouldContain("Class=\"routing-fork-input-field\"");
        forkMarkup.ShouldContain("Label=\"Input buffer\"");
        forkMarkup.ShouldContain("aria-label=\"Routing fork input buffer\"");
        forkMarkup.ShouldContain("@bind-Value=\"_boundedCapacity\"");
        forkMarkup.ShouldContain("Class=\"routing-fork-buffer-field\"");
        forkMarkup.ShouldContain("routing-fork-port-composer");
        forkMarkup.ShouldContain("aria-label=\"Routing fork output ports\"");
        forkMarkup.ShouldContain("routing-fork-port-header");
        forkMarkup.ShouldContain("Class=\"routing-fork-add-port\"");
        forkMarkup.ShouldContain("Text=\"@AddOutputPortLabel\"");
        forkMarkup.ShouldContain("aria-label=\"@AddOutputPortLabel\"");
        forkMarkup.ShouldContain("private string AddOutputPortLabel => $\"Add output port to {Node.NodeName}\";");
        forkMarkup.ShouldContain("routing-fork-port-row");
        forkMarkup.ShouldContain("_outputDrafts");
        forkMarkup.ShouldContain("AddOutput");
        forkMarkup.ShouldContain("RemoveOutput");
        forkMarkup.ShouldContain("Value=\"@output.Name\"");
        forkMarkup.ShouldContain("ValueChanged=\"@(value => SetOutputNameAsync(output, value))\"");
        forkMarkup.ShouldContain("Immediate=\"true\"");
        forkMarkup.ShouldNotContain("@bind-Value=\"output.Name\"");
        forkMarkup.ShouldContain("aria-label=\"@($\"Output port {index + 1}\")\"");
        forkMarkup.ShouldContain("Text=\"@RemoveOutputPortLabel(output)\"");
        forkMarkup.ShouldContain("aria-label=\"@RemoveOutputPortLabel(output)\"");
        forkMarkup.ShouldContain("private string RemoveOutputPortLabel(PortDraft output)");
        forkMarkup.ShouldContain("$\"Keep at least one output port on {Node.NodeName}\"");
        forkMarkup.ShouldContain("$\"Remove {target} from {Node.NodeName}\"");
        forkMarkup.ShouldNotContain("Text=\"Add output port\"");
        forkMarkup.ShouldNotContain("aria-label=\"Add output port\"");
        forkMarkup.ShouldNotContain("Text=\"@(_outputDrafts.Count > 1 ? \"Remove output port\" : \"Keep at least one output\")\"");
        forkMarkup.ShouldNotContain("aria-label=\"@($\"Remove output port {output.Name}\")\"");
        forkMarkup.ShouldContain("Class=\"routing-fork-output-name-field\"");
        forkMarkup.ShouldContain("private async Task AddOutput()");
        forkMarkup.ShouldContain("private async Task RemoveOutput(PortDraft output)");
        forkMarkup.ShouldContain("private async Task SetOutputNameAsync(PortDraft output, string value)");
        forkMarkup.ShouldContain("await DialogRefresh.RefreshAsync(Node.NodeName);");
        forkMarkup.ShouldContain("private string? ValidateEditor()");
        forkMarkup.ShouldContain("Add at least one output port before saving.");
        forkMarkup.ShouldContain("Each output port needs a name.");
        forkMarkup.ShouldContain("Output port names cannot be Input or Errors.");
        forkMarkup.ShouldContain("Output port names must be unique.");
        forkMarkup.ShouldContain("Output port names can use letters, numbers, and underscores and cannot start with a number.");
        forkMarkup.ShouldContain("private static string? ValidatePortDrafts(");
        forkMarkup.ShouldContain("private static bool IsNamedPort(PortDraft draft)");
        forkMarkup.ShouldContain("private string? _loadedNodeName;");
        forkMarkup.ShouldContain("private FlowDiagramNodeModel? _loadedNode;");
        forkMarkup.ShouldContain("ReferenceEquals(_loadedNode, Node)");
        forkMarkup.ShouldContain("_loadedNode = Node;");
        forkMarkup.ShouldNotContain("routing-fork-panel-header");
        forkMarkup.ShouldNotContain("routing-fork-panel-kicker");
        forkMarkup.ShouldNotContain("routing-fork-panel-token");
        forkMarkup.ShouldNotContain("OutputPanelCaption");
        forkMarkup.ShouldNotContain("routing-fork-panel-title");
        forkMarkup.ShouldNotContain("routing-fork-config-grid");
        forkMarkup.ShouldNotContain("routing-fork-port-panel");
        forkMarkup.ShouldNotContain("routing-fork-source-row");
        forkMarkup.ShouldNotContain("routing-fork-port-editor");
        forkMarkup.ShouldNotContain("routing-fork-port-workspace");
        forkMarkup.ShouldNotContain("routing-fork-port-section");
        forkMarkup.ShouldNotContain("routing-fork-port-column-header");
        forkMarkup.ShouldNotContain("routing-fork-port-actions");
        forkMarkup.ShouldNotContain("routing-fork-port-title");
        forkMarkup.ShouldNotContain("routing-fork-port-index");
        forkMarkup.ShouldNotContain("Duplicate the input to each named port");
        forkMarkup.ShouldNotContain("aria-hidden=\"true\"");
        forkMarkup.ShouldNotContain("Label=\"@($\"Output {index + 1}\")\"");
        forkMarkup.ShouldNotContain("@bind-Value=\"_outputsText\"");
        forkMarkup.ShouldNotContain("Class=\"routing-fork-outputs-field\"");
        forkMarkup.ShouldNotContain("Lines=\"4\"");
        forkMarkup.ShouldNotContain("<MudStack");
        forkMarkup.ShouldNotContain("<MudChip");
        forkMarkup.ShouldNotContain("d-flex flex-wrap gap-1");
        forkMarkup.ShouldNotContain("HelperText=");

        forkCss.ShouldContain(".routing-fork-summary");
        forkCss.ShouldContain(".routing-fork-meta");
        forkCss.ShouldContain("grid-template-columns: minmax(0, 1.36fr) minmax(0, 0.72fr) minmax(0, 0.64fr);");
        forkCss.ShouldNotContain("grid-template-columns: minmax(0, 1fr) 72px 64px;");
        forkCss.ShouldContain("display: -webkit-box;");
        forkCss.ShouldContain("-webkit-line-clamp: 2;");
        forkCss.ShouldContain(".routing-fork-ports");
        forkCss.ShouldContain(".routing-fork-summary-label");
        forkCss.ShouldNotContain(".routing-fork-contract-label");
        forkCss.ShouldContain("display: flex;");
        forkCss.ShouldContain("flex-wrap: wrap;");
        forkCss.ShouldNotContain("grid-template-columns: repeat(4, minmax(0, auto));");
        forkCss.ShouldContain(".routing-fork-token");
        forkCss.ShouldContain("display: inline-flex;");
        forkCss.ShouldContain("overflow-wrap: anywhere;");
        forkCss.ShouldContain("white-space: normal;");
        forkCss.ShouldContain(".routing-fork-editor");
        forkCss.ShouldContain(".routing-fork-layout");
        forkCss.ShouldContain(".routing-fork-sidecar");
        forkCss.ShouldContain("grid-template-columns: minmax(0, 1fr) minmax(164px, 0.36fr);");
        forkCss.ShouldContain("border-left: 1px solid color-mix(in srgb, var(--flux-border-soft) 58%, transparent);");
        forkCss.ShouldContain("padding-left: 7px;");
        forkCss.ShouldContain(".routing-fork-port-composer");
        forkCss.ShouldContain(".routing-fork-port-header");
        forkCss.ShouldNotContain(".routing-fork-editor-surface");
        forkCss.ShouldNotContain("background: color-mix(in srgb, var(--flux-surface-2) 4%, transparent);");
        forkCss.ShouldNotContain("border: 1px solid color-mix(in srgb, var(--flux-border-soft) 42%, transparent);");
        forkCss.ShouldNotContain("gap: 10px;");
        forkCss.ShouldNotContain("gap: 8px;");
        forkCss.ShouldContain("gap: 7px;");
        forkCss.ShouldContain("gap: 6px;");
        forkCss.ShouldNotContain("padding: 12px;");
        forkCss.ShouldNotContain(".routing-fork-panel-header");
        forkCss.ShouldNotContain(".routing-fork-panel-kicker");
        forkCss.ShouldNotContain(".routing-fork-panel-token");
        forkCss.ShouldNotContain(".routing-fork-port-panel");
        forkCss.ShouldNotContain(".routing-fork-source-row");
        forkCss.ShouldNotContain(".routing-fork-config-row");
        forkCss.ShouldNotContain("grid-template-columns: minmax(0, 1fr) minmax(0, 0.54fr);");
        forkCss.ShouldNotContain("grid-template-columns: minmax(0, 1fr) 150px;");
        forkCss.ShouldNotContain(".routing-fork-port-editor");
        forkCss.ShouldNotContain("padding-top: 10px;");
        forkCss.ShouldContain("padding-top: 7px;");
        forkCss.ShouldNotContain("padding-top: 8px;");
        forkCss.ShouldNotContain("padding-top: 4px;");
        forkCss.ShouldNotContain(".routing-fork-port-section");
        forkCss.ShouldNotContain(".routing-fork-port-column-header");
        forkCss.ShouldNotContain(".routing-fork-port-actions");
        forkCss.ShouldNotContain(".routing-fork-port-title");
        forkCss.ShouldContain(".routing-fork-port-list");
        forkCss.ShouldContain(".routing-fork-port-row");
        forkCss.ShouldNotContain(".routing-fork-port-index");
        forkCss.ShouldContain("align-items: center;");
        forkCss.ShouldContain("grid-template-columns: minmax(0, 1fr) minmax(1.75rem, max-content);");
        forkCss.ShouldContain("overflow-wrap: anywhere;");
        forkCss.ShouldContain("white-space: normal;");
        forkCss.ShouldNotContain("text-overflow: ellipsis;");
        forkCss.ShouldNotContain("grid-template-columns: minmax(0, 1fr) 28px;");
        forkCss.ShouldNotContain("grid-template-columns: minmax(0, 1fr) 30px;");
        forkCss.ShouldContain("min-height: 34px;");
        forkCss.ShouldNotContain("min-height: 36px;");
        forkCss.ShouldNotContain("min-height: 40px;");
        forkCss.ShouldContain(".routing-fork-port-header ::deep(.routing-fork-add-port.mud-icon-button)");
        forkCss.ShouldContain(".routing-fork-editor ::deep(.mud-input-control)");
        forkCss.ShouldContain("align-self: center;");
        forkCss.ShouldContain("justify-self: end;");
        forkCss.ShouldContain("border-radius: 4px;");
        forkCss.ShouldContain(".routing-fork-port-header ::deep(.routing-fork-add-port .mud-icon-root)");
        forkCss.ShouldContain("font-size: 0.98rem;");
        forkCss.ShouldContain(".routing-fork-port-composer ::deep(.routing-fork-remove-port.mud-disabled)");
        forkCss.ShouldNotContain(".routing-fork-panel-title");
        forkCss.ShouldNotContain(".routing-fork-config-grid");
        forkCss.ShouldNotContain(".routing-fork-outputs-field");
        forkCss.ShouldNotContain(".routing-fork-port-workspace");
        forkCss.ShouldNotContain("padding: 14px;");
        forkCss.ShouldNotContain("textarea.mud-input-root");
        forkCss.ShouldContain("@media (max-width: 720px)");
        forkCss.ShouldNotContain(".flow-node-filters");
        forkCss.ShouldNotContain("border-radius: 999px;");

        mergeMarkup.ShouldContain("EditDialogMaxWidth=\"MaxWidth.Medium\"");
        mergeMarkup.ShouldContain("ShowHeaderIcon=\"false\"");
        mergeMarkup.ShouldContain("ShowDisplayName=\"true\"");
        mergeMarkup.ShouldContain("ShowCategoryToken=\"false\"");
        mergeMarkup.ShouldContain("@inject NodeEditDialogRefreshService DialogRefresh");
        mergeMarkup.ShouldContain("EditorValidationError=\"@ValidateEditor\"");
        mergeMarkup.ShouldContain("routing-merge-summary");
        mergeMarkup.ShouldContain("routing-merge-meta");
        mergeMarkup.ShouldContain("InputTypeCaption");
        mergeMarkup.ShouldContain("InputCountCaption");
        mergeMarkup.ShouldContain("BufferCaption");
        mergeMarkup.ShouldContain("Merged item");
        mergeMarkup.ShouldNotContain("FlowMergeItem");
        mergeMarkup.ShouldContain("routing-merge-ports");
        mergeMarkup.ShouldContain("aria-label=\"Routing merge inputs\"");
        mergeMarkup.ShouldContain("routing-merge-summary-label");
        mergeMarkup.ShouldContain("InputPreview");
        mergeMarkup.ShouldContain("InputPreviewOverflow");
        mergeMarkup.ShouldContain("routing-merge-token");
        mergeMarkup.ShouldNotContain("routing-merge-contract-label");
        mergeMarkup.ShouldContain("routing-merge-editor");
        mergeMarkup.ShouldContain("aria-label=\"Routing merge settings\"");
        mergeMarkup.ShouldContain("routing-merge-layout");
        mergeMarkup.ShouldContain("aria-label=\"Routing merge editor layout\"");
        mergeMarkup.ShouldContain("routing-merge-sidecar");
        mergeMarkup.ShouldContain("aria-label=\"Routing merge support settings\"");
        mergeMarkup.ShouldNotContain("routing-merge-editor-surface");
        mergeMarkup.ShouldNotContain("routing-merge-config-row");
        mergeMarkup.ShouldNotContain("aria-label=\"Routing merge configuration\"");
        mergeMarkup.ShouldContain("Label=\"Input type\"");
        mergeMarkup.ShouldContain("aria-label=\"Routing merge input type\"");
        mergeMarkup.ShouldContain("@bind-Value=\"_inputType\"");
        mergeMarkup.ShouldContain("Class=\"routing-merge-input-field\"");
        mergeMarkup.ShouldContain("Label=\"Input buffer\"");
        mergeMarkup.ShouldContain("aria-label=\"Routing merge input buffer\"");
        mergeMarkup.ShouldContain("@bind-Value=\"_boundedCapacity\"");
        mergeMarkup.ShouldContain("Class=\"routing-merge-buffer-field\"");
        mergeMarkup.ShouldContain("routing-merge-port-composer");
        mergeMarkup.ShouldContain("aria-label=\"Routing merge input ports\"");
        mergeMarkup.ShouldContain("routing-merge-port-header");
        mergeMarkup.ShouldContain("Class=\"routing-merge-add-port\"");
        mergeMarkup.ShouldContain("Text=\"@AddInputPortLabel\"");
        mergeMarkup.ShouldContain("aria-label=\"@AddInputPortLabel\"");
        mergeMarkup.ShouldContain("private string AddInputPortLabel => $\"Add input port to {Node.NodeName}\";");
        mergeMarkup.ShouldContain("routing-merge-port-row");
        mergeMarkup.ShouldContain("_inputDrafts");
        mergeMarkup.ShouldContain("AddInput");
        mergeMarkup.ShouldContain("RemoveInput");
        mergeMarkup.ShouldContain("Value=\"@input.Name\"");
        mergeMarkup.ShouldContain("ValueChanged=\"@(value => SetInputNameAsync(input, value))\"");
        mergeMarkup.ShouldContain("Immediate=\"true\"");
        mergeMarkup.ShouldNotContain("@bind-Value=\"input.Name\"");
        mergeMarkup.ShouldContain("aria-label=\"@($\"Input port {index + 1}\")\"");
        mergeMarkup.ShouldContain("Text=\"@RemoveInputPortLabel(input)\"");
        mergeMarkup.ShouldContain("aria-label=\"@RemoveInputPortLabel(input)\"");
        mergeMarkup.ShouldContain("private string RemoveInputPortLabel(PortDraft input)");
        mergeMarkup.ShouldContain("$\"Keep at least one input port on {Node.NodeName}\"");
        mergeMarkup.ShouldContain("$\"Remove {target} from {Node.NodeName}\"");
        mergeMarkup.ShouldNotContain("Text=\"Add input port\"");
        mergeMarkup.ShouldNotContain("aria-label=\"Add input port\"");
        mergeMarkup.ShouldNotContain("Text=\"@(_inputDrafts.Count > 1 ? \"Remove input port\" : \"Keep at least one input\")\"");
        mergeMarkup.ShouldNotContain("aria-label=\"@($\"Remove input port {input.Name}\")\"");
        mergeMarkup.ShouldContain("Class=\"routing-merge-input-name-field\"");
        mergeMarkup.ShouldContain("private async Task AddInput()");
        mergeMarkup.ShouldContain("private async Task RemoveInput(PortDraft input)");
        mergeMarkup.ShouldContain("private async Task SetInputNameAsync(PortDraft input, string value)");
        mergeMarkup.ShouldContain("await DialogRefresh.RefreshAsync(Node.NodeName);");
        mergeMarkup.ShouldContain("private string? ValidateEditor()");
        mergeMarkup.ShouldContain("Add at least one input port before saving.");
        mergeMarkup.ShouldContain("Each input port needs a name.");
        mergeMarkup.ShouldContain("Input port names cannot be Output or Errors.");
        mergeMarkup.ShouldContain("Input port names must be unique.");
        mergeMarkup.ShouldContain("Input port names can use letters, numbers, and underscores and cannot start with a number.");
        mergeMarkup.ShouldContain("private static string? ValidatePortDrafts(");
        mergeMarkup.ShouldContain("private static bool IsNamedPort(PortDraft draft)");
        mergeMarkup.ShouldContain("private string? _loadedNodeName;");
        mergeMarkup.ShouldContain("private FlowDiagramNodeModel? _loadedNode;");
        mergeMarkup.ShouldContain("ReferenceEquals(_loadedNode, Node)");
        mergeMarkup.ShouldContain("_loadedNode = Node;");
        mergeMarkup.ShouldNotContain("routing-merge-panel-header");
        mergeMarkup.ShouldNotContain("routing-merge-panel-kicker");
        mergeMarkup.ShouldNotContain("routing-merge-panel-token");
        mergeMarkup.ShouldNotContain("InputPanelCaption");
        mergeMarkup.ShouldNotContain("routing-merge-panel-title");
        mergeMarkup.ShouldNotContain("routing-merge-config-grid");
        mergeMarkup.ShouldNotContain("routing-merge-port-panel");
        mergeMarkup.ShouldNotContain("routing-merge-source-row");
        mergeMarkup.ShouldNotContain("routing-merge-port-editor");
        mergeMarkup.ShouldNotContain("routing-merge-port-workspace");
        mergeMarkup.ShouldNotContain("routing-merge-port-section");
        mergeMarkup.ShouldNotContain("routing-merge-port-column-header");
        mergeMarkup.ShouldNotContain("routing-merge-port-actions");
        mergeMarkup.ShouldNotContain("routing-merge-port-title");
        mergeMarkup.ShouldNotContain("routing-merge-port-index");
        mergeMarkup.ShouldNotContain("Accept events from each named input");
        mergeMarkup.ShouldNotContain("aria-hidden=\"true\"");
        mergeMarkup.ShouldNotContain("Label=\"@($\"Input {index + 1}\")\"");
        mergeMarkup.ShouldNotContain("@bind-Value=\"_inputsText\"");
        mergeMarkup.ShouldNotContain("Class=\"routing-merge-inputs-field\"");
        mergeMarkup.ShouldNotContain("Lines=\"4\"");
        mergeMarkup.ShouldNotContain("<MudStack");
        mergeMarkup.ShouldNotContain("<MudChip");
        mergeMarkup.ShouldNotContain("d-flex flex-wrap gap-1");
        mergeMarkup.ShouldNotContain("HelperText=");

        mergeCss.ShouldContain(".routing-merge-summary");
        mergeCss.ShouldContain(".routing-merge-meta");
        mergeCss.ShouldContain("grid-template-columns: minmax(0, 1.3fr) minmax(0, 0.62fr) minmax(0, 0.86fr) minmax(0, 0.62fr);");
        mergeCss.ShouldNotContain("grid-template-columns: minmax(0, 1fr) 66px 94px 64px;");
        mergeCss.ShouldContain("display: -webkit-box;");
        mergeCss.ShouldContain("-webkit-line-clamp: 2;");
        mergeCss.ShouldContain(".routing-merge-ports");
        mergeCss.ShouldContain(".routing-merge-summary-label");
        mergeCss.ShouldNotContain(".routing-merge-contract-label");
        mergeCss.ShouldContain("display: flex;");
        mergeCss.ShouldContain("flex-wrap: wrap;");
        mergeCss.ShouldNotContain("grid-template-columns: repeat(4, minmax(0, auto));");
        mergeCss.ShouldContain(".routing-merge-token");
        mergeCss.ShouldContain("display: inline-flex;");
        mergeCss.ShouldContain("overflow-wrap: anywhere;");
        mergeCss.ShouldContain("white-space: normal;");
        mergeCss.ShouldContain(".routing-merge-editor");
        mergeCss.ShouldContain(".routing-merge-layout");
        mergeCss.ShouldContain(".routing-merge-sidecar");
        mergeCss.ShouldContain("grid-template-columns: minmax(0, 1fr) minmax(164px, 0.36fr);");
        mergeCss.ShouldContain("border-left: 1px solid color-mix(in srgb, var(--flux-border-soft) 58%, transparent);");
        mergeCss.ShouldContain("padding-left: 7px;");
        mergeCss.ShouldContain(".routing-merge-port-composer");
        mergeCss.ShouldContain(".routing-merge-port-header");
        mergeCss.ShouldNotContain(".routing-merge-editor-surface");
        mergeCss.ShouldNotContain("background: color-mix(in srgb, var(--flux-surface-2) 4%, transparent);");
        mergeCss.ShouldNotContain("border: 1px solid color-mix(in srgb, var(--flux-border-soft) 42%, transparent);");
        mergeCss.ShouldNotContain("gap: 10px;");
        mergeCss.ShouldNotContain("gap: 8px;");
        mergeCss.ShouldContain("gap: 7px;");
        mergeCss.ShouldContain("gap: 6px;");
        mergeCss.ShouldNotContain("padding: 12px;");
        mergeCss.ShouldNotContain(".routing-merge-panel-header");
        mergeCss.ShouldNotContain(".routing-merge-panel-kicker");
        mergeCss.ShouldNotContain(".routing-merge-panel-token");
        mergeCss.ShouldNotContain(".routing-merge-port-panel");
        mergeCss.ShouldNotContain(".routing-merge-source-row");
        mergeCss.ShouldNotContain(".routing-merge-config-row");
        mergeCss.ShouldNotContain("grid-template-columns: minmax(0, 1fr) minmax(0, 0.54fr);");
        mergeCss.ShouldNotContain("grid-template-columns: minmax(0, 1fr) 150px;");
        mergeCss.ShouldNotContain(".routing-merge-port-editor");
        mergeCss.ShouldNotContain("padding-top: 10px;");
        mergeCss.ShouldContain("padding-top: 7px;");
        mergeCss.ShouldNotContain("padding-top: 8px;");
        mergeCss.ShouldNotContain("padding-top: 4px;");
        mergeCss.ShouldNotContain(".routing-merge-port-section");
        mergeCss.ShouldNotContain(".routing-merge-port-column-header");
        mergeCss.ShouldNotContain(".routing-merge-port-actions");
        mergeCss.ShouldNotContain(".routing-merge-port-title");
        mergeCss.ShouldContain(".routing-merge-port-list");
        mergeCss.ShouldContain(".routing-merge-port-row");
        mergeCss.ShouldNotContain(".routing-merge-port-index");
        mergeCss.ShouldContain("align-items: center;");
        mergeCss.ShouldContain("grid-template-columns: minmax(0, 1fr) minmax(1.75rem, max-content);");
        mergeCss.ShouldContain("overflow-wrap: anywhere;");
        mergeCss.ShouldContain("white-space: normal;");
        mergeCss.ShouldNotContain("text-overflow: ellipsis;");
        mergeCss.ShouldNotContain("grid-template-columns: minmax(0, 1fr) 28px;");
        mergeCss.ShouldNotContain("grid-template-columns: minmax(0, 1fr) 30px;");
        mergeCss.ShouldContain("min-height: 34px;");
        mergeCss.ShouldNotContain("min-height: 36px;");
        mergeCss.ShouldNotContain("min-height: 40px;");
        mergeCss.ShouldContain(".routing-merge-port-header ::deep(.routing-merge-add-port.mud-icon-button)");
        mergeCss.ShouldContain(".routing-merge-editor ::deep(.mud-input-control)");
        mergeCss.ShouldContain("align-self: center;");
        mergeCss.ShouldContain("justify-self: end;");
        mergeCss.ShouldContain("border-radius: 4px;");
        mergeCss.ShouldContain(".routing-merge-port-header ::deep(.routing-merge-add-port .mud-icon-root)");
        mergeCss.ShouldContain("font-size: 0.98rem;");
        mergeCss.ShouldContain(".routing-merge-port-composer ::deep(.routing-merge-remove-port.mud-disabled)");
        mergeCss.ShouldNotContain(".routing-merge-panel-title");
        mergeCss.ShouldNotContain(".routing-merge-config-grid");
        mergeCss.ShouldNotContain(".routing-merge-inputs-field");
        mergeCss.ShouldNotContain(".routing-merge-port-workspace");
        mergeCss.ShouldNotContain("padding: 14px;");
        mergeCss.ShouldNotContain("textarea.mud-input-root");
        mergeCss.ShouldContain("@media (max-width: 720px)");
        mergeCss.ShouldNotContain(".flow-node-filters");
        mergeCss.ShouldNotContain("border-radius: 999px;");
    }

    [Fact]
    public void RoutingWindowNodeWidget_UsesCompactSummaryAndFlatEditor()
    {
        var root = FindRepositoryRoot();
        var markup = File.ReadAllText(Path.Combine(
            root,
            "src",
            "FluxMq.UI",
            "Components",
            "Workspace",
            "Nodes",
            "Routing",
            "RoutingWindowNodeWidget.razor"));
        var css = File.ReadAllText(Path.Combine(
            root,
            "src",
            "FluxMq.UI",
            "Components",
            "Workspace",
            "Nodes",
            "Routing",
            "RoutingWindowNodeWidget.razor.css"));

        markup.ShouldContain("EditDialogMaxWidth=\"MaxWidth.Medium\"");
        markup.ShouldContain("ShowHeaderIcon=\"false\"");
        markup.ShouldContain("ShowDisplayName=\"true\"");
        markup.ShouldContain("ShowCategoryToken=\"false\"");
        markup.ShouldContain("@inject NodeEditDialogRefreshService DialogRefresh");
        markup.ShouldContain("EditorValidationError=\"@ValidateEditor\"");
        markup.ShouldContain("routing-window-summary");
        markup.ShouldContain("routing-window-meta");
        markup.ShouldContain("InputTypeCaption");
        markup.ShouldContain("BufferCaption");
        markup.ShouldContain("routing-window-boundary");
        markup.ShouldContain("aria-label=\"Routing window boundary\"");
        markup.ShouldContain("MaxItemsCaption");
        markup.ShouldContain("TimeCaption");
        markup.ShouldContain("PartialCaption");
        markup.ShouldContain("routing-window-token");
        markup.ShouldContain("routing-window-editor");
        markup.ShouldContain("aria-label=\"Routing window settings\"");
        markup.ShouldContain("routing-window-layout");
        markup.ShouldContain("aria-label=\"Routing window editor layout\"");
        markup.ShouldContain("routing-window-sidecar");
        markup.ShouldContain("aria-label=\"Routing window support settings\"");
        markup.ShouldContain("routing-window-boundary-editor");
        markup.ShouldContain("aria-label=\"Routing window boundary settings\"");
        markup.ShouldContain("Label=\"Input type\"");
        markup.ShouldContain("aria-label=\"Routing window input type\"");
        markup.ShouldContain("@bind-Value=\"_inputType\"");
        markup.ShouldContain("Class=\"routing-window-input-field\"");
        markup.ShouldContain("Label=\"Input buffer\"");
        markup.ShouldContain("aria-label=\"Routing window input buffer\"");
        markup.ShouldContain("Value=\"@_boundedCapacity\"");
        markup.ShouldContain("ValueChanged=\"@SetBoundedCapacity\"");
        markup.ShouldContain("Class=\"routing-window-buffer-field\"");
        markup.ShouldContain("Label=\"Max items\"");
        markup.ShouldContain("aria-label=\"Routing window max items\"");
        markup.ShouldContain("Value=\"@_maxItems\"");
        markup.ShouldContain("ValueChanged=\"@SetMaxItems\"");
        markup.ShouldContain("Class=\"routing-window-max-items-field\"");
        markup.ShouldContain("Label=\"Time window ms\"");
        markup.ShouldContain("aria-label=\"Routing window time in milliseconds\"");
        markup.ShouldContain("Value=\"@_timeMilliseconds\"");
        markup.ShouldContain("ValueChanged=\"@SetTimeMilliseconds\"");
        markup.ShouldContain("Class=\"routing-window-time-field\"");
        markup.ShouldContain("RefreshDialogAsync");
        markup.ShouldContain("DialogRefresh.RefreshAsync(Node.NodeName)");
        markup.ShouldContain("private string? ValidateEditor()");
        markup.ShouldContain("Input buffer must be between 1 and 100000.");
        markup.ShouldContain("Max items must be between 0 and 100000.");
        markup.ShouldContain("Time window must be between 0 and 86400000 ms.");
        markup.ShouldContain("Set max items or time window before saving.");
        markup.ShouldNotContain("@bind-Value=\"_boundedCapacity\"");
        markup.ShouldNotContain("@bind-Value=\"_maxItems\"");
        markup.ShouldNotContain("@bind-Value=\"_timeMilliseconds\"");
        markup.ShouldContain("Label=\"Emit partial\"");
        markup.ShouldContain("aria-label=\"Emit partial window on completion\"");
        markup.ShouldContain("@bind-Value=\"_emitPartialOnCompletion\"");
        markup.ShouldContain("Class=\"routing-window-partial-check\"");
        markup.ShouldContain("private string? _loadedNodeName;");
        markup.ShouldContain("private FlowDiagramNodeModel? _loadedNode;");
        markup.ShouldContain("ReferenceEquals(_loadedNode, Node)");
        markup.ShouldContain("_loadedNode = Node;");
        markup.ShouldNotContain("routing-window-panel-header");
        markup.ShouldNotContain("routing-window-panel-kicker");
        markup.ShouldNotContain("routing-window-panel-token");
        markup.ShouldNotContain("WindowPanelCaption");
        markup.ShouldNotContain("routing-window-panel-title");
        markup.ShouldNotContain("routing-window-window-panel");
        markup.ShouldNotContain("routing-window-source-row");
        markup.ShouldNotContain("routing-window-source-grid");
        markup.ShouldNotContain("routing-window-limit-panel");
        markup.ShouldNotContain("routing-window-limit-workspace");
        markup.ShouldNotContain("routing-window-editor-surface");
        markup.ShouldNotContain("routing-window-primary-row");
        markup.ShouldNotContain("routing-window-config-row");
        markup.ShouldNotContain("routing-window-limit-row");
        markup.ShouldNotContain("<span>Completion</span>");
        markup.ShouldNotContain("routing-window-form-grid");
        markup.ShouldNotContain("<MudStack");
        markup.ShouldNotContain("<MudChip");
        markup.ShouldNotContain("<MudGrid");
        markup.ShouldNotContain("<MudItem");
        markup.ShouldNotContain("d-flex flex-wrap gap-1");
        markup.ShouldNotContain("HelperText=");

        css.ShouldContain(".routing-window-summary");
        css.ShouldContain(".routing-window-meta");
        css.ShouldContain("grid-template-columns: minmax(0, 1.18fr) minmax(0, 0.7fr) minmax(0, 0.52fr);");
        css.ShouldNotContain("grid-template-columns: minmax(0, 1fr) 86px 62px;");
        css.ShouldContain("display: -webkit-box;");
        css.ShouldContain("-webkit-line-clamp: 2;");
        css.ShouldContain(".routing-window-boundary");
        css.ShouldContain("flex-wrap: wrap;");
        css.ShouldNotContain("grid-template-columns: repeat(3, minmax(0, auto));");
        css.ShouldContain("flex: 0 0 100%;");
        css.ShouldNotContain("grid-column: 1 / -1;");
        css.ShouldContain(".routing-window-token");
        css.ShouldContain("display: inline-flex;");
        css.ShouldContain("overflow-wrap: anywhere;");
        css.ShouldContain("white-space: normal;");
        css.ShouldContain(".routing-window-editor");
        css.ShouldContain(".routing-window-layout");
        css.ShouldContain(".routing-window-sidecar");
        css.ShouldNotContain("gap: 10px;");
        css.ShouldNotContain("gap: 8px;");
        css.ShouldContain("gap: 7px;");
        css.ShouldNotContain(".routing-window-editor-surface");
        css.ShouldNotContain("background: color-mix(in srgb, var(--flux-surface-2) 4%, transparent);");
        css.ShouldNotContain("border: 1px solid color-mix(in srgb, var(--flux-border-soft) 42%, transparent);");
        css.ShouldNotContain("padding: 12px;");
        css.ShouldNotContain(".routing-window-panel-header");
        css.ShouldNotContain(".routing-window-panel-kicker");
        css.ShouldNotContain(".routing-window-panel-token");
        css.ShouldNotContain(".routing-window-window-panel");
        css.ShouldNotContain(".routing-window-source-row");
        css.ShouldContain("grid-template-columns: minmax(0, 1fr) minmax(164px, 0.38fr);");
        css.ShouldContain("border-left: 1px solid color-mix(in srgb, var(--flux-border-soft) 58%, transparent);");
        css.ShouldContain("padding-left: 7px;");
        css.ShouldNotContain(".routing-window-primary-row");
        css.ShouldNotContain("grid-template-columns: minmax(0, 1fr) minmax(0, 0.42fr);");
        css.ShouldNotContain("grid-template-columns: minmax(0, 1fr) 160px;");
        css.ShouldContain(".routing-window-boundary-editor");
        css.ShouldContain("padding-top: 7px;");
        css.ShouldNotContain("padding-top: 8px;");
        css.ShouldContain("grid-template-columns: minmax(0, 1fr) minmax(0, 1fr);");
        css.ShouldNotContain("grid-template-columns: minmax(0, 1fr) minmax(0, 1fr) minmax(0, 0.68fr);");
        css.ShouldNotContain("grid-template-columns: minmax(120px, 1fr) minmax(150px, 1fr) minmax(124px, 0.6fr);");
        css.ShouldContain(".routing-window-partial-option");
        css.ShouldContain("align-items: center;");
        css.ShouldContain("align-self: center;");
        css.ShouldNotContain("align-self: end;");
        css.ShouldContain("justify-content: flex-start;");
        css.ShouldContain("min-height: 34px;");
        css.ShouldContain(".routing-window-partial-option ::deep(.mud-checkbox .mud-button-root)");
        css.ShouldContain("padding: 3px;");
        css.ShouldContain(".routing-window-editor ::deep(.mud-input-control)");
        css.ShouldContain("@media (max-width: 720px)");
        css.ShouldNotContain(".routing-window-panel-title");
        css.ShouldNotContain(".routing-window-source-grid");
        css.ShouldNotContain(".routing-window-limit-panel");
        css.ShouldNotContain(".routing-window-form-grid");
        css.ShouldNotContain(".routing-window-config-row");
        css.ShouldNotContain(".routing-window-limit-row");
        css.ShouldNotContain("grid-template-columns: minmax(220px, 1.4fr) repeat(3, minmax(116px, 0.72fr)) minmax(128px, 0.66fr);");
        css.ShouldNotContain(".routing-window-config-grid");
        css.ShouldNotContain(".routing-window-limit-grid");
        css.ShouldNotContain(".routing-window-limit-workspace");
        css.ShouldNotContain("padding: 14px;");
        css.ShouldNotContain(".flow-node-filters");
        css.ShouldNotContain("border-radius: 999px;");
    }

    [Fact]
    public void RoutingCorrelationAndJoinNodeWidgets_UseCompactSummaryAndFlatEditors()
    {
        var root = FindRepositoryRoot();
        var correlationMarkup = File.ReadAllText(Path.Combine(
            root,
            "src",
            "FluxMq.UI",
            "Components",
            "Workspace",
            "Nodes",
            "Routing",
            "RoutingCorrelationNodeWidget.razor"));
        var correlationCss = File.ReadAllText(Path.Combine(
            root,
            "src",
            "FluxMq.UI",
            "Components",
            "Workspace",
            "Nodes",
            "Routing",
            "RoutingCorrelationNodeWidget.razor.css"));
        var joinMarkup = File.ReadAllText(Path.Combine(
            root,
            "src",
            "FluxMq.UI",
            "Components",
            "Workspace",
            "Nodes",
            "Routing",
            "RoutingJoinNodeWidget.razor"));
        var joinCss = File.ReadAllText(Path.Combine(
            root,
            "src",
            "FluxMq.UI",
            "Components",
            "Workspace",
            "Nodes",
            "Routing",
            "RoutingJoinNodeWidget.razor.css"));

        correlationMarkup.ShouldContain("@implements IDisposable");
        correlationMarkup.ShouldContain("@inject AppThemeService ThemeService");
        correlationMarkup.ShouldContain("@inject IJSRuntime JsRuntime");
        correlationMarkup.ShouldContain("ShowHeaderIcon=\"false\"");
        correlationMarkup.ShouldContain("ShowDisplayName=\"true\"");
        correlationMarkup.ShouldContain("ShowCategoryToken=\"false\"");
        correlationMarkup.ShouldContain("EditDialogMaxWidth=\"MaxWidth.Large\"");
        correlationMarkup.ShouldContain("@inject NodeEditDialogRefreshService DialogRefresh");
        correlationMarkup.ShouldContain("EditorValidationError=\"@ValidateEditor\"");
        correlationMarkup.ShouldContain("routing-correlation-summary");
        correlationMarkup.ShouldContain("routing-correlation-meta");
        correlationMarkup.ShouldContain("InputTypeCaption");
        correlationMarkup.ShouldContain("SideFlowCaption");
        correlationMarkup.ShouldContain("TimeoutCaption");
        correlationMarkup.ShouldContain("BufferCaption");
        correlationMarkup.ShouldContain("routing-correlation-rules");
        correlationMarkup.ShouldContain("aria-label=\"Routing correlation rules\"");
        correlationMarkup.ShouldContain("routing-correlation-summary-label");
        correlationMarkup.ShouldContain("KeyExpressionCaption");
        correlationMarkup.ShouldContain("SideExpressionCaption");
        correlationMarkup.ShouldContain("CaseCaption");
        correlationMarkup.ShouldContain("PendingCaption");
        correlationMarkup.ShouldNotContain("routing-correlation-contract-label");
        correlationMarkup.ShouldContain("routing-correlation-editor");
        correlationMarkup.ShouldContain("aria-label=\"Routing correlation settings\"");
        correlationMarkup.ShouldContain("routing-correlation-rule-row");
        correlationMarkup.ShouldContain("routing-correlation-expression-stack");
        correlationMarkup.ShouldContain("routing-correlation-key-workspace node-ui-code-surface");
        correlationMarkup.ShouldContain("aria-label=\"Correlation key expression\"");
        correlationMarkup.ShouldContain("routing-correlation-side-workspace node-ui-code-surface");
        correlationMarkup.ShouldContain("aria-label=\"Correlation side expression\"");
        correlationMarkup.ShouldContain("routing-correlation-workspace-header");
        correlationMarkup.ShouldContain("routing-correlation-editor-host");
        correlationMarkup.ShouldContain("StandaloneCodeEditor");
        correlationMarkup.ShouldContain("CssClass=\"routing-correlation-code-editor\"");
        correlationMarkup.ShouldContain("ConstructionOptions=\"@KeyEditorConstructionOptions\"");
        correlationMarkup.ShouldContain("ConstructionOptions=\"@SideEditorConstructionOptions\"");
        correlationMarkup.ShouldContain("OnDidChangeModelContent=\"@OnKeyExpressionContentChanged\"");
        correlationMarkup.ShouldContain("OnDidChangeModelContent=\"@OnSideExpressionContentChanged\"");
        correlationMarkup.ShouldContain("routing-correlation-sidecar");
        correlationMarkup.ShouldContain("routing-correlation-config-row");
        correlationMarkup.ShouldContain("aria-label=\"Routing correlation input settings\"");
        correlationMarkup.ShouldContain("aria-label=\"Routing correlation matching rule\"");
        correlationMarkup.ShouldContain("aria-label=\"Routing correlation expressions\"");
        correlationMarkup.ShouldContain("routing-correlation-side-map-row");
        correlationMarkup.ShouldContain("aria-label=\"Routing correlation side mapping\"");
        correlationMarkup.ShouldContain("@bind-Value=\"_inputType\"");
        correlationMarkup.ShouldContain("Value=\"@_requestSide\"");
        correlationMarkup.ShouldContain("ValueChanged=\"@SetRequestSide\"");
        correlationMarkup.ShouldContain("Value=\"@_responseSide\"");
        correlationMarkup.ShouldContain("ValueChanged=\"@SetResponseSide\"");
        correlationMarkup.ShouldContain("Value=\"@_caseSensitive\"");
        correlationMarkup.ShouldContain("ValueChanged=\"@SetCaseSensitive\"");
        correlationMarkup.ShouldContain("Immediate=\"true\"");
        correlationMarkup.ShouldNotContain("@bind-Value=\"_keyExpression\"");
        correlationMarkup.ShouldNotContain("@bind-Value=\"_sideExpression\"");
        correlationMarkup.ShouldNotContain("@bind-Value=\"_requestSide\"");
        correlationMarkup.ShouldNotContain("@bind-Value=\"_responseSide\"");
        correlationMarkup.ShouldNotContain("@bind-Value=\"_caseSensitive\"");
        correlationMarkup.ShouldContain("@bind-Value=\"_timeoutMilliseconds\"");
        correlationMarkup.ShouldContain("@bind-Value=\"_maxPending\"");
        correlationMarkup.ShouldContain("@bind-Value=\"_boundedCapacity\"");
        correlationMarkup.ShouldContain("private StandaloneCodeEditor? _keyEditor;");
        correlationMarkup.ShouldContain("private StandaloneCodeEditor? _sideEditor;");
        correlationMarkup.ShouldContain("private ElementReference _keyEditorHost;");
        correlationMarkup.ShouldContain("private ElementReference _sideEditorHost;");
        correlationMarkup.ShouldContain("private string KeyEditorId");
        correlationMarkup.ShouldContain("private string SideEditorId");
        correlationMarkup.ShouldContain("private string? _loadedNodeName;");
        correlationMarkup.ShouldContain("private FlowDiagramNodeModel? _loadedNode;");
        correlationMarkup.ShouldContain("ReferenceEquals(_loadedNode, Node)");
        correlationMarkup.ShouldContain("_loadedNode = Node;");
        correlationMarkup.ShouldContain("private StandaloneEditorConstructionOptions KeyEditorConstructionOptions");
        correlationMarkup.ShouldContain("private StandaloneEditorConstructionOptions SideEditorConstructionOptions");
        correlationMarkup.ShouldContain("private async Task ConfigureCodeEditorsAsync()");
        correlationMarkup.ShouldContain("_keyExpression = await _keyEditor.GetValue();");
        correlationMarkup.ShouldContain("_sideExpression = await _sideEditor.GetValue();");
        correlationMarkup.ShouldContain("private async Task OnKeyExpressionContentChanged()");
        correlationMarkup.ShouldContain("private async Task OnSideExpressionContentChanged()");
        correlationMarkup.ShouldContain("private async Task LayoutEditorsAfterRenderAsync()");
        correlationMarkup.ShouldContain("private static bool IsNonCriticalEditorException");
        correlationMarkup.ShouldContain("code editor");
        correlationMarkup.ShouldContain("private async Task SetRequestSide(string value)");
        correlationMarkup.ShouldContain("private async Task SetResponseSide(string value)");
        correlationMarkup.ShouldContain("private async Task SetCaseSensitive(bool value)");
        correlationMarkup.ShouldContain("private string? ValidateEditor()");
        correlationMarkup.ShouldContain("Enter a correlation key expression before saving.");
        correlationMarkup.ShouldContain("Enter a correlation side expression before saving.");
        correlationMarkup.ShouldContain("Enter a request side name before saving.");
        correlationMarkup.ShouldContain("Enter a response side name before saving.");
        correlationMarkup.ShouldContain("Request and response side names must be different.");
        correlationMarkup.ShouldNotContain("routing-correlation-panel-header");
        correlationMarkup.ShouldNotContain("routing-correlation-panel-kicker");
        correlationMarkup.ShouldNotContain("routing-correlation-panel-token");
        correlationMarkup.ShouldNotContain("routing-correlation-config-grid");
        correlationMarkup.ShouldNotContain("routing-correlation-side-grid");
        correlationMarkup.ShouldNotContain("routing-correlation-form-grid");
        correlationMarkup.ShouldNotContain("routing-correlation-expression-row");
        correlationMarkup.ShouldNotContain("routing-correlation-match-composer");
        correlationMarkup.ShouldNotContain("routing-correlation-side-panel");
        correlationMarkup.ShouldNotContain("routing-correlation-limit-panel");
        correlationMarkup.ShouldNotContain("routing-correlation-rule-panel");
        correlationMarkup.ShouldNotContain("routing-correlation-editor-surface");
        correlationMarkup.ShouldNotContain("routing-correlation-match-title");
        correlationMarkup.ShouldNotContain("routing-correlation-matching-workspace");
        correlationMarkup.ShouldNotContain("routing-correlation-source-grid");
        correlationMarkup.ShouldNotContain("routing-correlation-expression-grid");
        correlationMarkup.ShouldNotContain("routing-correlation-side-row");
        correlationMarkup.ShouldNotContain("routing-correlation-limit-row");
        correlationMarkup.ShouldNotContain("routing-correlation-matching-section");
        correlationMarkup.ShouldNotContain("Value=\"@_keyExpression\"");
        correlationMarkup.ShouldNotContain("ValueChanged=\"@SetKeyExpression\"");
        correlationMarkup.ShouldNotContain("Value=\"@_sideExpression\"");
        correlationMarkup.ShouldNotContain("ValueChanged=\"@SetSideExpression\"");
        correlationMarkup.ShouldNotContain("private async Task SetKeyExpression(string value)");
        correlationMarkup.ShouldNotContain("private async Task SetSideExpression(string value)");
        correlationMarkup.ShouldNotContain("Class=\"routing-correlation-key-field\"");
        correlationMarkup.ShouldNotContain("Class=\"routing-correlation-side-field\"");
        correlationMarkup.ShouldNotContain("<span>Matching</span>");
        correlationMarkup.ShouldNotContain("<MudStack");
        correlationMarkup.ShouldNotContain("<MudChip");
        correlationMarkup.ShouldNotContain("<MudGrid");
        correlationMarkup.ShouldNotContain("<MudItem");
        correlationMarkup.ShouldNotContain("d-flex flex-wrap gap-1");

        correlationCss.ShouldContain(".routing-correlation-summary");
        correlationCss.ShouldContain(".routing-correlation-meta");
        correlationCss.ShouldContain("grid-template-columns: minmax(0, 1.08fr) minmax(0, 0.82fr) minmax(0, 0.58fr) minmax(0, 0.52fr);");
        correlationCss.ShouldNotContain("grid-template-columns: minmax(0, 1fr) 112px 76px 64px;");
        correlationCss.ShouldContain("display: -webkit-box;");
        correlationCss.ShouldContain("-webkit-line-clamp: 2;");
        correlationCss.ShouldContain(".routing-correlation-rules");
        correlationCss.ShouldContain(".routing-correlation-summary-label");
        correlationCss.ShouldNotContain(".routing-correlation-contract-label");
        correlationCss.ShouldContain("flex-wrap: wrap;");
        correlationCss.ShouldNotContain("grid-template-columns: repeat(4, minmax(0, auto));");
        correlationCss.ShouldContain("flex: 0 0 100%;");
        correlationCss.ShouldNotContain("grid-column: 1 / -1;");
        correlationCss.ShouldContain(".routing-correlation-token");
        correlationCss.ShouldContain("display: inline-flex;");
        correlationCss.ShouldContain("overflow-wrap: anywhere;");
        correlationCss.ShouldContain("white-space: normal;");
        correlationCss.ShouldContain(".routing-correlation-editor");
        correlationCss.ShouldContain(".routing-correlation-rule-row");
        correlationCss.ShouldContain("height: clamp(460px, 62vh, 720px);");
        correlationCss.ShouldContain("grid-template-columns: minmax(0, 1.38fr) minmax(0, 0.62fr);");
        correlationCss.ShouldContain(".routing-correlation-expression-stack");
        correlationCss.ShouldContain("grid-template-rows: minmax(0, 1fr) minmax(0, 1fr);");
        correlationCss.ShouldContain(".routing-correlation-key-workspace");
        correlationCss.ShouldContain(".routing-correlation-side-workspace");
        correlationCss.ShouldContain(".routing-correlation-workspace-header");
        correlationCss.ShouldContain(".routing-correlation-editor-host");
        correlationCss.ShouldContain(".routing-correlation-sidecar");
        correlationCss.ShouldContain(".routing-correlation-config-row");
        correlationCss.ShouldContain(".routing-correlation-side-map-row");
        correlationCss.ShouldContain(".routing-correlation-key-workspace ::deep(.routing-correlation-code-editor)");
        correlationCss.ShouldContain(".routing-correlation-side-workspace ::deep(.routing-correlation-code-editor)");
        correlationCss.ShouldContain(".routing-correlation-code-editor .monaco-editor");
        correlationCss.ShouldNotContain(".routing-correlation-editor-surface");
        correlationCss.ShouldNotContain(".routing-correlation-match-title");
        correlationCss.ShouldNotContain("background: color-mix(in srgb, var(--flux-surface-2) 4%, transparent);");
        correlationCss.ShouldNotContain("border: 1px solid color-mix(in srgb, var(--flux-border-soft) 42%, transparent);");
        correlationCss.ShouldNotContain("gap: 10px;");
        correlationCss.ShouldNotContain("gap: 8px;");
        correlationCss.ShouldContain("gap: 7px;");
        correlationCss.ShouldNotContain("padding: 12px;");
        correlationCss.ShouldNotContain(".routing-correlation-panel-header");
        correlationCss.ShouldNotContain(".routing-correlation-panel-kicker");
        correlationCss.ShouldNotContain(".routing-correlation-panel-token");
        correlationCss.ShouldNotContain(".routing-correlation-rule-panel");
        correlationCss.ShouldNotContain(".routing-correlation-source-grid");
        correlationCss.ShouldContain("grid-template-columns: minmax(0, 1fr);");
        correlationCss.ShouldNotContain("grid-template-columns: minmax(0, 1fr) 150px 150px 150px;");
        correlationCss.ShouldNotContain("background: color-mix(in srgb, var(--flux-surface-2) 7%, transparent);");
        correlationCss.ShouldNotContain("border: 1px solid color-mix(in srgb, var(--flux-border-soft) 24%, transparent);");
        correlationCss.ShouldNotContain(".routing-correlation-expression-grid");
        correlationCss.ShouldNotContain("grid-template-columns: minmax(0, 1fr) minmax(0, 1fr) minmax(132px, 0.72fr);");
        correlationCss.ShouldNotContain("padding-top: 10px;");
        correlationCss.ShouldContain("padding-top: 7px;");
        correlationCss.ShouldNotContain("padding-top: 8px;");
        correlationCss.ShouldNotContain(".routing-correlation-side-row");
        correlationCss.ShouldNotContain(".routing-correlation-limit-row");
        correlationCss.ShouldContain(".routing-correlation-case-option");
        correlationCss.ShouldContain("align-items: center;");
        correlationCss.ShouldContain("align-self: center;");
        correlationCss.ShouldNotContain("align-self: end;");
        correlationCss.ShouldContain("justify-content: flex-start;");
        correlationCss.ShouldContain("min-height: 34px;");
        correlationCss.ShouldContain(".routing-correlation-case-option ::deep(.mud-checkbox .mud-button-root)");
        correlationCss.ShouldContain("padding: 3px;");
        correlationCss.ShouldContain("@media (max-width: 720px)");
        correlationCss.ShouldNotContain(".routing-correlation-case-option > span");
        correlationCss.ShouldNotContain(".routing-correlation-config-grid");
        correlationCss.ShouldNotContain(".routing-correlation-side-grid");
        correlationCss.ShouldNotContain(".routing-correlation-form-grid");
        correlationCss.ShouldNotContain(".routing-correlation-side-panel");
        correlationCss.ShouldNotContain(".routing-correlation-limit-panel");
        correlationCss.ShouldNotContain(".routing-correlation-match-composer");
        correlationCss.ShouldNotContain(".routing-correlation-expression-row");
        correlationCss.ShouldNotContain(".routing-correlation-key-field");
        correlationCss.ShouldNotContain(".routing-correlation-side-field");
        correlationCss.ShouldNotContain(".routing-correlation-matching-workspace");
        correlationCss.ShouldNotContain(".routing-correlation-matching-section");
        correlationCss.ShouldNotContain("padding: 14px;");
        correlationCss.ShouldNotContain(".flow-node-filters");
        correlationCss.ShouldNotContain("border-radius: 999px;");

        joinMarkup.ShouldContain("@implements IDisposable");
        joinMarkup.ShouldContain("@inject AppThemeService ThemeService");
        joinMarkup.ShouldContain("@inject IJSRuntime JsRuntime");
        joinMarkup.ShouldContain("ShowHeaderIcon=\"false\"");
        joinMarkup.ShouldContain("ShowDisplayName=\"true\"");
        joinMarkup.ShouldContain("ShowCategoryToken=\"false\"");
        joinMarkup.ShouldContain("EditDialogMaxWidth=\"MaxWidth.Large\"");
        joinMarkup.ShouldContain("@inject NodeEditDialogRefreshService DialogRefresh");
        joinMarkup.ShouldContain("EditorValidationError=\"@ValidateEditor\"");
        joinMarkup.ShouldContain("routing-join-summary");
        joinMarkup.ShouldContain("routing-join-meta");
        joinMarkup.ShouldContain("LeftInputTypeCaption");
        joinMarkup.ShouldContain("RightInputTypeCaption");
        joinMarkup.ShouldContain("JoinExpressionCaption");
        joinMarkup.ShouldContain("TimeoutCaption");
        joinMarkup.ShouldContain("PendingCaption");
        joinMarkup.ShouldContain("BufferCaption");
        joinMarkup.ShouldContain("routing-join-rules");
        joinMarkup.ShouldContain("aria-label=\"Routing join rules\"");
        joinMarkup.ShouldContain("routing-join-summary-label");
        joinMarkup.ShouldNotContain("routing-join-contract-label");
        joinMarkup.ShouldContain("routing-join-editor");
        joinMarkup.ShouldContain("aria-label=\"Routing join settings\"");
        joinMarkup.ShouldContain("routing-join-rule-row");
        joinMarkup.ShouldContain("routing-join-expression-stack");
        joinMarkup.ShouldContain("routing-join-left-key-workspace node-ui-code-surface");
        joinMarkup.ShouldContain("aria-label=\"Routing join left key expression\"");
        joinMarkup.ShouldContain("routing-join-right-key-workspace node-ui-code-surface");
        joinMarkup.ShouldContain("aria-label=\"Routing join right key expression\"");
        joinMarkup.ShouldContain("routing-join-workspace-header");
        joinMarkup.ShouldContain("routing-join-editor-host");
        joinMarkup.ShouldContain("StandaloneCodeEditor");
        joinMarkup.ShouldContain("CssClass=\"routing-join-code-editor\"");
        joinMarkup.ShouldContain("ConstructionOptions=\"@LeftKeyEditorConstructionOptions\"");
        joinMarkup.ShouldContain("ConstructionOptions=\"@RightKeyEditorConstructionOptions\"");
        joinMarkup.ShouldContain("OnDidChangeModelContent=\"@OnLeftKeyExpressionContentChanged\"");
        joinMarkup.ShouldContain("OnDidChangeModelContent=\"@OnRightKeyExpressionContentChanged\"");
        joinMarkup.ShouldContain("routing-join-sidecar");
        joinMarkup.ShouldContain("routing-join-config-row");
        joinMarkup.ShouldContain("aria-label=\"Routing join input settings\"");
        joinMarkup.ShouldContain("aria-label=\"Routing join matching rule\"");
        joinMarkup.ShouldContain("aria-label=\"Routing join key expressions\"");
        joinMarkup.ShouldContain("routing-join-control-row");
        joinMarkup.ShouldContain("aria-label=\"Routing join limit settings\"");
        joinMarkup.ShouldContain("@bind-Value=\"_leftInputType\"");
        joinMarkup.ShouldContain("@bind-Value=\"_rightInputType\"");
        joinMarkup.ShouldNotContain("@bind-Value=\"_leftKeyExpression\"");
        joinMarkup.ShouldNotContain("@bind-Value=\"_rightKeyExpression\"");
        joinMarkup.ShouldContain("@bind-Value=\"_caseSensitive\"");
        joinMarkup.ShouldContain("@bind-Value=\"_timeoutMilliseconds\"");
        joinMarkup.ShouldContain("@bind-Value=\"_maxPending\"");
        joinMarkup.ShouldContain("@bind-Value=\"_boundedCapacity\"");
        joinMarkup.ShouldContain("private StandaloneCodeEditor? _leftKeyEditor;");
        joinMarkup.ShouldContain("private StandaloneCodeEditor? _rightKeyEditor;");
        joinMarkup.ShouldContain("private ElementReference _leftKeyEditorHost;");
        joinMarkup.ShouldContain("private ElementReference _rightKeyEditorHost;");
        joinMarkup.ShouldContain("private string LeftKeyEditorId");
        joinMarkup.ShouldContain("private string RightKeyEditorId");
        joinMarkup.ShouldContain("private string? _loadedNodeName;");
        joinMarkup.ShouldContain("private FlowDiagramNodeModel? _loadedNode;");
        joinMarkup.ShouldContain("ReferenceEquals(_loadedNode, Node)");
        joinMarkup.ShouldContain("_loadedNode = Node;");
        joinMarkup.ShouldContain("private StandaloneEditorConstructionOptions LeftKeyEditorConstructionOptions");
        joinMarkup.ShouldContain("private StandaloneEditorConstructionOptions RightKeyEditorConstructionOptions");
        joinMarkup.ShouldContain("private async Task ConfigureCodeEditorsAsync()");
        joinMarkup.ShouldContain("_leftKeyExpression = await _leftKeyEditor.GetValue();");
        joinMarkup.ShouldContain("_rightKeyExpression = await _rightKeyEditor.GetValue();");
        joinMarkup.ShouldContain("private async Task OnLeftKeyExpressionContentChanged()");
        joinMarkup.ShouldContain("private async Task OnRightKeyExpressionContentChanged()");
        joinMarkup.ShouldContain("private async Task LayoutEditorsAfterRenderAsync()");
        joinMarkup.ShouldContain("private static bool IsNonCriticalEditorException");
        joinMarkup.ShouldContain("code editor");
        joinMarkup.ShouldContain("private string? ValidateEditor()");
        joinMarkup.ShouldContain("Enter a left key expression before saving.");
        joinMarkup.ShouldContain("Enter a right key expression before saving.");
        joinMarkup.ShouldNotContain("routing-join-panel-header");
        joinMarkup.ShouldNotContain("routing-join-panel-kicker");
        joinMarkup.ShouldNotContain("routing-join-panel-token");
        joinMarkup.ShouldNotContain("routing-join-type-grid");
        joinMarkup.ShouldNotContain("routing-join-expression-grid");
        joinMarkup.ShouldNotContain("routing-join-limit-grid");
        joinMarkup.ShouldNotContain("routing-join-form-grid");
        joinMarkup.ShouldNotContain("routing-join-key-row");
        joinMarkup.ShouldNotContain("routing-join-match-composer");
        joinMarkup.ShouldNotContain("routing-join-limit-panel");
        joinMarkup.ShouldNotContain("routing-join-rule-panel");
        joinMarkup.ShouldNotContain("routing-join-editor-surface");
        joinMarkup.ShouldNotContain("routing-join-match-title");
        joinMarkup.ShouldNotContain("routing-join-matching-workspace");
        joinMarkup.ShouldNotContain("routing-join-input-grid");
        joinMarkup.ShouldNotContain("routing-join-key-grid");
        joinMarkup.ShouldNotContain("routing-join-matching-section");
        joinMarkup.ShouldNotContain("routing-join-limit-row");
        joinMarkup.ShouldNotContain("Value=\"@_leftKeyExpression\"");
        joinMarkup.ShouldNotContain("ValueChanged=\"@SetLeftKeyExpression\"");
        joinMarkup.ShouldNotContain("Value=\"@_rightKeyExpression\"");
        joinMarkup.ShouldNotContain("ValueChanged=\"@SetRightKeyExpression\"");
        joinMarkup.ShouldNotContain("private async Task SetLeftKeyExpression(string value)");
        joinMarkup.ShouldNotContain("private async Task SetRightKeyExpression(string value)");
        joinMarkup.ShouldNotContain("Class=\"routing-join-left-key-field\"");
        joinMarkup.ShouldNotContain("Class=\"routing-join-right-key-field\"");
        joinMarkup.ShouldNotContain("<span>Matching</span>");
        joinMarkup.ShouldNotContain("<MudStack");
        joinMarkup.ShouldNotContain("<MudChip");
        joinMarkup.ShouldNotContain("<MudGrid");
        joinMarkup.ShouldNotContain("<MudItem");
        joinMarkup.ShouldNotContain("d-flex flex-wrap gap-1");

        joinCss.ShouldContain(".routing-join-summary");
        joinCss.ShouldContain(".routing-join-meta");
        joinCss.ShouldContain("grid-template-columns: minmax(0, 1fr) minmax(0, 1fr) minmax(0, 0.74fr) minmax(0, 0.56fr);");
        joinCss.ShouldNotContain("grid-template-columns: minmax(0, 1fr) minmax(0, 1fr) 96px 64px;");
        joinCss.ShouldContain("display: -webkit-box;");
        joinCss.ShouldContain("-webkit-line-clamp: 2;");
        joinCss.ShouldContain(".routing-join-rules");
        joinCss.ShouldContain(".routing-join-summary-label");
        joinCss.ShouldNotContain(".routing-join-contract-label");
        joinCss.ShouldContain("display: flex;");
        joinCss.ShouldContain("flex-wrap: wrap;");
        joinCss.ShouldNotContain("grid-template-columns: repeat(4, minmax(0, auto));");
        joinCss.ShouldContain(".routing-join-token");
        joinCss.ShouldContain("display: inline-flex;");
        joinCss.ShouldContain("overflow-wrap: anywhere;");
        joinCss.ShouldContain("white-space: normal;");
        joinCss.ShouldContain(".routing-join-editor");
        joinCss.ShouldContain(".routing-join-rule-row");
        joinCss.ShouldContain("height: clamp(460px, 62vh, 720px);");
        joinCss.ShouldContain("grid-template-columns: minmax(0, 1.38fr) minmax(0, 0.62fr);");
        joinCss.ShouldContain(".routing-join-expression-stack");
        joinCss.ShouldContain("grid-template-rows: minmax(0, 1fr) minmax(0, 1fr);");
        joinCss.ShouldContain(".routing-join-left-key-workspace");
        joinCss.ShouldContain(".routing-join-right-key-workspace");
        joinCss.ShouldContain(".routing-join-workspace-header");
        joinCss.ShouldContain(".routing-join-editor-host");
        joinCss.ShouldContain(".routing-join-sidecar");
        joinCss.ShouldContain(".routing-join-config-row");
        joinCss.ShouldContain(".routing-join-control-row");
        joinCss.ShouldContain(".routing-join-left-key-workspace ::deep(.routing-join-code-editor)");
        joinCss.ShouldContain(".routing-join-right-key-workspace ::deep(.routing-join-code-editor)");
        joinCss.ShouldContain(".routing-join-code-editor .monaco-editor");
        joinCss.ShouldNotContain(".routing-join-editor-surface");
        joinCss.ShouldNotContain(".routing-join-match-title");
        joinCss.ShouldNotContain("background: color-mix(in srgb, var(--flux-surface-2) 4%, transparent);");
        joinCss.ShouldNotContain("border: 1px solid color-mix(in srgb, var(--flux-border-soft) 42%, transparent);");
        joinCss.ShouldNotContain("gap: 10px;");
        joinCss.ShouldNotContain("gap: 8px;");
        joinCss.ShouldContain("gap: 7px;");
        joinCss.ShouldNotContain("padding: 12px;");
        joinCss.ShouldNotContain(".routing-join-panel-header");
        joinCss.ShouldNotContain(".routing-join-panel-kicker");
        joinCss.ShouldNotContain(".routing-join-panel-token");
        joinCss.ShouldNotContain(".routing-join-rule-panel");
        joinCss.ShouldNotContain(".routing-join-input-grid");
        joinCss.ShouldNotContain(".routing-join-key-grid");
        joinCss.ShouldNotContain("background: color-mix(in srgb, var(--flux-surface-2) 7%, transparent);");
        joinCss.ShouldNotContain("border: 1px solid color-mix(in srgb, var(--flux-border-soft) 24%, transparent);");
        joinCss.ShouldNotContain(".routing-join-limit-row");
        joinCss.ShouldContain("grid-template-columns: minmax(0, 1fr);");
        joinCss.ShouldNotContain("grid-template-columns: minmax(0, 1fr) minmax(0, 1fr) 150px;");
        joinCss.ShouldNotContain("grid-template-columns: minmax(130px, 1fr) minmax(130px, 1fr) minmax(132px, 0.72fr);");
        joinCss.ShouldNotContain("padding-top: 10px;");
        joinCss.ShouldContain("padding-top: 7px;");
        joinCss.ShouldNotContain("padding-top: 8px;");
        joinCss.ShouldContain(".routing-join-case-option");
        joinCss.ShouldContain("align-items: center;");
        joinCss.ShouldContain("align-self: center;");
        joinCss.ShouldNotContain("align-self: end;");
        joinCss.ShouldContain("justify-content: flex-start;");
        joinCss.ShouldContain("min-height: 34px;");
        joinCss.ShouldContain(".routing-join-case-option ::deep(.mud-checkbox .mud-button-root)");
        joinCss.ShouldContain("padding: 3px;");
        joinCss.ShouldContain("@media (max-width: 720px)");
        joinCss.ShouldNotContain(".routing-join-case-option > span");
        joinCss.ShouldNotContain(".routing-join-type-grid");
        joinCss.ShouldNotContain(".routing-join-expression-grid");
        joinCss.ShouldNotContain(".routing-join-limit-grid");
        joinCss.ShouldNotContain(".routing-join-form-grid");
        joinCss.ShouldNotContain(".routing-join-limit-panel");
        joinCss.ShouldNotContain(".routing-join-match-composer");
        joinCss.ShouldNotContain(".routing-join-key-row");
        joinCss.ShouldNotContain(".routing-join-left-key-field");
        joinCss.ShouldNotContain(".routing-join-right-key-field");
        joinCss.ShouldNotContain(".routing-join-matching-workspace");
        joinCss.ShouldNotContain(".routing-join-matching-section");
        joinCss.ShouldNotContain("padding: 14px;");
        joinCss.ShouldNotContain(".flow-node-filters");
        joinCss.ShouldNotContain("border-radius: 999px;");
    }

    [Fact]
    public void HttpRequestNodeWidget_UsesHttpClientSummaryAndFlatEditor()
    {
        var root = FindRepositoryRoot();
        var markup = File.ReadAllText(Path.Combine(
            root,
            "src",
            "FluxMq.UI",
            "Components",
            "Workspace",
            "Nodes",
            "Http",
            "HttpRequestNodeWidget.razor"));
        var css = File.ReadAllText(Path.Combine(
            root,
            "src",
            "FluxMq.UI",
            "Components",
            "Workspace",
            "Nodes",
            "Http",
            "HttpRequestNodeWidget.razor.css"));

        markup.ShouldContain("EditDialogMaxWidth=\"MaxWidth.Medium\"");
        markup.ShouldContain("@inject NodeEditDialogRefreshService DialogRefresh");
        markup.ShouldContain("EditorValidationError=\"@ValidateEditor\"");
        markup.ShouldContain("ShowHeaderIcon=\"false\"");
        markup.ShouldContain("ShowDisplayName=\"true\"");
        markup.ShouldContain("ShowCategoryToken=\"false\"");
        markup.ShouldContain("http-client-summary");
        markup.ShouldContain("http-client-meta");
        markup.ShouldContain("http-client-meta-item target");
        markup.ShouldContain("TargetCaption");
        markup.ShouldContain("Per request");
        markup.ShouldContain("TimeoutCaption");
        markup.ShouldContain("InputBufferCaption");
        markup.ShouldContain("RedirectCaption");
        markup.ShouldContain("ErrorModeCaption");
        markup.ShouldContain("<span>Redirects</span>");
        markup.ShouldContain("<span>Errors</span>");
        markup.ShouldNotContain("http-client-contracts");
        markup.ShouldNotContain("aria-label=\"HTTP request fields\"");
        markup.ShouldNotContain("aria-label=\"HTTP response fields\"");
        markup.ShouldNotContain("http-client-token");
        markup.ShouldNotContain("Request fields");
        markup.ShouldNotContain("Response fields");
        markup.ShouldContain("http-client-editor");
        markup.ShouldContain("aria-label=\"HTTP client settings\"");
        markup.ShouldContain("Label=\"Base URL\"");
        markup.ShouldContain("@bind-Value=\"_baseUrl\"");
        markup.ShouldContain("Label=\"Timeout ms\"");
        markup.ShouldContain("Value=\"@_defaultTimeoutMilliseconds\"");
        markup.ShouldContain("ValueChanged=\"@SetDefaultTimeoutMilliseconds\"");
        markup.ShouldNotContain("@bind-Value=\"_defaultTimeoutMilliseconds\"");
        markup.ShouldContain("Label=\"Max body bytes\"");
        markup.ShouldContain("Value=\"@_maxResponseBodyBytes\"");
        markup.ShouldContain("ValueChanged=\"@SetMaxResponseBodyBytes\"");
        markup.ShouldNotContain("@bind-Value=\"_maxResponseBodyBytes\"");
        markup.ShouldContain("Label=\"Input buffer\"");
        markup.ShouldContain("Value=\"@_boundedCapacity\"");
        markup.ShouldContain("ValueChanged=\"@SetBoundedCapacity\"");
        markup.ShouldNotContain("@bind-Value=\"_boundedCapacity\"");
        markup.ShouldContain("Label=\"Follow redirects\"");
        markup.ShouldContain("@bind-Value=\"_followRedirects\"");
        markup.ShouldContain("Label=\"Non-success status emits error\"");
        markup.ShouldContain("@bind-Value=\"_treatNonSuccessStatusAsError\"");
        markup.ShouldContain("Label=\"Default headers\"");
        markup.ShouldContain("@bind-Value=\"_defaultHeadersText\"");
        markup.ShouldContain("Class=\"http-client-base-url-field\"");
        markup.ShouldContain("Class=\"http-client-buffer-field\"");
        markup.ShouldContain("private async Task SetDefaultTimeoutMilliseconds(int value)");
        markup.ShouldContain("private async Task SetMaxResponseBodyBytes(int value)");
        markup.ShouldContain("private async Task SetBoundedCapacity(int value)");
        markup.ShouldContain("private string? ValidateEditor()");
        markup.ShouldContain("Timeout must be between 1 and 600000 ms.");
        markup.ShouldContain("Max body bytes must be between 1 and 104857600.");
        markup.ShouldContain("Input buffer must be between 1 and 100000.");
        markup.ShouldNotContain("<Stat ");
        markup.ShouldNotContain("<MudChip");
        markup.ShouldNotContain("d-flex flex-wrap gap-1");
        markup.ShouldNotContain("HttpResponseOutput");
        markup.ShouldNotContain("HttpRequestInput");

        css.ShouldContain(".http-client-summary");
        css.ShouldContain(".http-client-meta");
        css.ShouldContain("grid-template-columns: minmax(0, 1fr) minmax(0, 1fr);");
        css.ShouldContain(".http-client-meta-item.target");
        css.ShouldContain("grid-column: 1 / -1;");
        css.ShouldContain("-webkit-line-clamp: 2;");
        css.ShouldContain("overflow-wrap: anywhere;");
        css.ShouldContain("white-space: normal;");
        css.ShouldNotContain("grid-template-columns: minmax(0, 1.26fr) minmax(0, 0.62fr) minmax(0, 0.72fr);");
        css.ShouldNotContain("grid-template-columns: minmax(0, 1fr) 70px 84px;");
        css.ShouldNotContain(".http-client-contracts");
        css.ShouldNotContain(".http-client-contract");
        css.ShouldNotContain(".http-client-contract-label");
        css.ShouldNotContain(".http-client-token");
        css.ShouldNotContain("grid-template-columns: repeat(4, minmax(0, auto));");
        css.ShouldContain(".http-client-editor");
        css.ShouldNotContain("gap: 10px;");
        css.ShouldNotContain("gap: 8px;");
        css.ShouldContain("gap: 7px;");
        css.ShouldContain("gap: 6px;");
        css.ShouldContain("gap: 6px 16px;");
        css.ShouldNotContain("gap: 7px 18px;");
        css.ShouldNotContain("gap: 8px 22px;");
        css.ShouldContain("min-height: 34px;");
        css.ShouldNotContain("min-height: 36px;");
        css.ShouldNotContain("min-height: 40px;");
        css.ShouldContain(".http-client-number-grid");
        css.ShouldContain("grid-template-columns: repeat(3, minmax(0, 1fr));");
        css.ShouldContain(".http-client-options");
        css.ShouldContain(".http-client-options ::deep(.mud-checkbox)");
        css.ShouldContain("display: inline-flex;");
        css.ShouldContain(".http-client-options ::deep(.mud-checkbox .mud-button-root)");
        css.ShouldContain("padding: 3px;");
        css.ShouldContain(".http-client-options ::deep(.mud-checkbox .mud-typography)");
        css.ShouldContain(".http-client-editor ::deep(.mud-input-control)");
        css.ShouldContain(".http-client-editor ::deep(.mud-input-control > .mud-input-control-input-container > .mud-input)");
        css.ShouldContain("@media (max-width: 640px)");
        css.ShouldNotContain(".flow-node-filters");
        css.ShouldNotContain("border: 1px solid var(--flux-border-soft);");
    }

    [Fact]
    public void PayloadInspectorNodeWidget_UsesCompactSummaryAndReadOnlyEditor()
    {
        var root = FindRepositoryRoot();
        var markup = File.ReadAllText(Path.Combine(
            root,
            "src",
            "FluxMq.UI",
            "Components",
            "Workspace",
            "Nodes",
            "PayloadInspector",
            "PayloadInspectorNodeWidget.razor"));
        var css = File.ReadAllText(Path.Combine(
            root,
            "src",
            "FluxMq.UI",
            "Components",
            "Workspace",
            "Nodes",
            "PayloadInspector",
            "PayloadInspectorNodeWidget.razor.css"));

        markup.ShouldContain("EditDialogMaxWidth=\"MaxWidth.Medium\"");
        markup.ShouldContain("ShowHeaderIcon=\"false\"");
        markup.ShouldContain("ShowDisplayName=\"true\"");
        markup.ShouldContain("ShowCategoryToken=\"false\"");
        markup.ShouldContain("payload-inspector-node-summary");
        markup.ShouldContain("payload-inspector-node-meta");
        markup.ShouldContain("payload-inspector-node-meta-item behavior");
        markup.ShouldContain("<span>Behavior</span>");
        markup.ShouldContain("<span>Formats</span>");
        markup.ShouldContain("Auto detect payload");
        markup.ShouldContain("JSON, XML, text, Base64, binary");
        markup.ShouldNotContain("<span>Contract</span>");
        markup.ShouldNotContain("MqttEnvelope to InspectedMqttMessage");
        markup.ShouldNotContain("payload-inspector-node-contracts");
        markup.ShouldNotContain("aria-label=\"Payload inspector result fields\"");
        markup.ShouldNotContain("payload-inspector-node-token");
        markup.ShouldNotContain("Result fields");
        markup.ShouldContain("<span class=\"payload-inspector-node-editor-label\">Input</span>");
        markup.ShouldContain("<span class=\"payload-inspector-node-editor-label\">Result</span>");
        markup.ShouldContain("<Editor>");
        markup.ShouldContain("payload-inspector-node-editor");
        markup.ShouldContain("aria-label=\"Payload inspector details\"");
        markup.ShouldContain("payload-inspector-node-capability");
        markup.ShouldContain("aria-label=\"Payload inspector decode behavior\"");
        markup.ShouldContain("payload-inspector-node-detail-list");
        markup.ShouldContain("aria-label=\"Payload inspector operation details\"");
        markup.ShouldContain("payload-inspector-node-detail-row");
        markup.ShouldContain("aria-label=\"Payload inspector input details\"");
        markup.ShouldContain("aria-label=\"Payload inspector result details\"");
        markup.ShouldContain("payload-inspector-node-editor-label");
        markup.ShouldContain("payload-inspector-node-editor-note");
        markup.ShouldContain("MQTT envelope payload");
        markup.ShouldContain("Inspected payload summary");
        markup.ShouldNotContain("payload-inspector-node-editor-tokens");
        markup.ShouldNotContain("aria-label=\"Payload inspector detection modes\"");
        markup.ShouldNotContain("aria-label=\"Payload inspector input contract\"");
        markup.ShouldNotContain("aria-label=\"Payload inspector output contract\"");
        markup.ShouldNotContain("payload-inspector-node-editor-table");
        markup.ShouldNotContain("aria-label=\"Payload inspector contract details\"");
        markup.ShouldNotContain("payload-inspector-node-editor-row");
        markup.ShouldNotContain("payload-inspector-node-editor-summary");
        markup.ShouldNotContain("payload-inspector-node-editor-cell");
        markup.ShouldNotContain("aria-label=\"Payload inspector contract summary\"");
        markup.ShouldNotContain("payload-inspector-node-editor-contract-list");
        markup.ShouldNotContain("payload-inspector-node-editor-contract-item");
        markup.ShouldNotContain("payload-inspector-node-editor-contract-label");
        markup.ShouldNotContain("payload-inspector-node-config-summary");
        markup.ShouldNotContain("payload-inspector-node-setting-line");
        markup.ShouldNotContain("payload-inspector-node-token-group");
        markup.ShouldNotContain("<span>Decode</span>");
        markup.ShouldNotContain("payload-inspector-node-readonly");
        markup.ShouldNotContain("payload-inspector-node-contract-table");
        markup.ShouldNotContain("payload-inspector-node-contract-head");
        markup.ShouldNotContain("payload-inspector-node-contract-line");
        markup.ShouldNotContain("payload-inspector-node-contract-panel");
        markup.ShouldNotContain("payload-inspector-node-editor-surface");
        markup.ShouldNotContain("payload-inspector-node-panel-header");
        markup.ShouldNotContain("payload-inspector-node-panel-kicker");
        markup.ShouldNotContain("payload-inspector-node-panel-token");
        markup.ShouldNotContain("payload-inspector-node-behavior");
        markup.ShouldNotContain("payload-inspector-node-editor-overview");
        markup.ShouldNotContain("payload-inspector-node-editor-meta");
        markup.ShouldNotContain("payload-inspector-node-editor-panels");
        markup.ShouldNotContain("payload-inspector-node-editor-panel");
        markup.ShouldNotContain("Detect from payload");
        markup.ShouldNotContain("Automatic");
        markup.ShouldNotContain("payload-inspector-node-section-heading");
        markup.ShouldNotContain("Envelope fields");
        markup.ShouldNotContain("Inspection fields");
        markup.ShouldNotContain("payload-inspector-node-mode");
        markup.ShouldNotContain("Automatic payload inspection");
        markup.ShouldNotContain("Configuration");
        markup.ShouldNotContain("Fixed");
        markup.ShouldNotContain("Read only");
        markup.ShouldNotContain("Decode envelope payloads");
        markup.ShouldNotContain("<span>Role</span>");
        markup.ShouldNotContain("<span>Fields</span>");
        markup.ShouldNotContain("<MudText");
        markup.ShouldNotContain("<MudChip");
        markup.ShouldNotContain("<MudStack");
        markup.ShouldNotContain("<MudGrid");
        markup.ShouldNotContain("d-flex");
        markup.ShouldNotContain("gap-1");

        css.ShouldContain(".payload-inspector-node-summary");
        css.ShouldContain(".payload-inspector-node-meta");
        css.ShouldContain("grid-template-columns: minmax(0, 1.34fr) minmax(0, 0.66fr);");
        css.ShouldNotContain(".payload-inspector-node-meta-item.output");
        css.ShouldNotContain("grid-column: 1 / -1;");
        css.ShouldContain("-webkit-line-clamp: 2;");
        css.ShouldNotContain("grid-template-columns: minmax(0, 0.72fr) minmax(0, 1.38fr) minmax(0, 0.9fr);");
        css.ShouldNotContain("grid-template-columns: 76px minmax(0, 1fr) 98px;");
        css.ShouldNotContain(".payload-inspector-node-contracts");
        css.ShouldNotContain(".payload-inspector-node-contract");
        css.ShouldNotContain(".payload-inspector-node-contract-label");
        css.ShouldNotContain(".payload-inspector-node-token");
        css.ShouldContain("overflow-wrap: anywhere;");
        css.ShouldContain("white-space: normal;");
        css.ShouldNotContain("text-overflow: ellipsis;");
        css.ShouldContain(".payload-inspector-node-editor");
        css.ShouldNotContain("gap: 10px;");
        css.ShouldContain("gap: 7px 10px;");
        css.ShouldContain("gap: 8px;");
        css.ShouldNotContain(".payload-inspector-node-editor-surface");
        css.ShouldNotContain("background: color-mix(in srgb, var(--flux-surface-2) 4%, transparent);");
        css.ShouldNotContain("border: 1px solid color-mix(in srgb, var(--flux-border-soft) 42%, transparent);");
        css.ShouldNotContain("padding: 12px;");
        css.ShouldContain(".payload-inspector-node-capability");
        css.ShouldContain("grid-template-columns: minmax(0, 0.46fr) minmax(0, 0.9fr) minmax(0, 1.64fr);");
        css.ShouldContain(".payload-inspector-node-detail-list");
        css.ShouldContain(".payload-inspector-node-detail-row");
        css.ShouldContain(".payload-inspector-node-detail-row + .payload-inspector-node-detail-row");
        css.ShouldNotContain(".payload-inspector-node-contract-grid");
        css.ShouldNotContain("grid-template-columns: repeat(2, minmax(0, 1fr));");
        css.ShouldNotContain(".payload-inspector-node-contract-card");
        css.ShouldNotContain(".payload-inspector-node-contract-card + .payload-inspector-node-contract-card");
        css.ShouldNotContain("border-left: 1px solid color-mix(in srgb, var(--flux-border-soft) 26%, transparent);");
        css.ShouldNotContain(".payload-inspector-node-editor-table");
        css.ShouldNotContain(".payload-inspector-node-editor-row");
        css.ShouldNotContain("grid-template-columns: minmax(0, 0.56fr) minmax(0, 0.84fr) minmax(0, 1.6fr);");
        css.ShouldNotContain("grid-template-columns: 84px minmax(150px, 0.42fr) minmax(0, 1fr);");
        css.ShouldContain("min-height: 38px;");
        css.ShouldContain("padding: 2px 2px 8px;");
        css.ShouldNotContain("padding: 9px 2px;");
        css.ShouldContain(".payload-inspector-node-editor-label");
        css.ShouldContain(".payload-inspector-node-editor-note");
        css.ShouldContain(".payload-inspector-node-capability > strong");
        css.ShouldContain(".payload-inspector-node-detail-row > strong");
        css.ShouldNotContain(".payload-inspector-node-editor-tokens");
        css.ShouldNotContain(".payload-inspector-node-editor-summary");
        css.ShouldNotContain(".payload-inspector-node-editor-cell");
        css.ShouldNotContain(".payload-inspector-node-editor-contract-list");
        css.ShouldNotContain(".payload-inspector-node-editor-contract-item");
        css.ShouldNotContain(".payload-inspector-node-editor-contract-label");
        css.ShouldNotContain(".payload-inspector-node-config-summary");
        css.ShouldNotContain(".payload-inspector-node-setting-line");
        css.ShouldNotContain("grid-template-columns: 68px minmax(172px, 0.9fr) minmax(0, 1.3fr);");
        css.ShouldNotContain(".payload-inspector-node-setting-line:first-child");
        css.ShouldNotContain(".payload-inspector-node-token-group");
        css.ShouldNotContain(".payload-inspector-node-readonly");
        css.ShouldNotContain(".payload-inspector-node-contract-table");
        css.ShouldNotContain(".payload-inspector-node-contract-head");
        css.ShouldNotContain(".payload-inspector-node-contract-line");
        css.ShouldNotContain(".payload-inspector-node-contract-panel");
        css.ShouldNotContain(".payload-inspector-node-panel-header");
        css.ShouldNotContain(".payload-inspector-node-panel-kicker");
        css.ShouldNotContain(".payload-inspector-node-panel-token");
        css.ShouldNotContain(".payload-inspector-node-behavior");
        css.ShouldNotContain(".payload-inspector-node-editor-overview");
        css.ShouldNotContain(".payload-inspector-node-editor-meta");
        css.ShouldNotContain(".payload-inspector-node-editor-panels");
        css.ShouldNotContain(".payload-inspector-node-editor-panel");
        css.ShouldNotContain(".payload-inspector-node-section-heading");
        css.ShouldNotContain(".payload-inspector-node-mode");
        css.ShouldContain("@media (max-width: 640px)");
        css.ShouldNotContain(".payload-inspector-node-editor-grid");
        css.ShouldNotContain("border-bottom: 1px solid color-mix(in srgb, var(--flux-border-soft) 46%, transparent);");
        css.ShouldNotContain("grid-template-columns: 74px minmax(160px, 1fr) minmax(0, 1.6fr);");
        css.ShouldNotContain(".flow-node-filters");
        css.ShouldNotContain("border-radius: 999px;");
    }

    [Fact]
    public void DefaultNodeWidget_UsesCompactFallbackSummary()
    {
        var root = FindRepositoryRoot();
        var markup = File.ReadAllText(Path.Combine(
            root,
            "src",
            "FluxMq.UI",
            "Components",
            "Diagram",
            "DefaultNodeWidget.razor"));
        var css = File.ReadAllText(Path.Combine(
            root,
            "src",
            "FluxMq.UI",
            "Components",
            "Diagram",
            "DefaultNodeWidget.razor.css"));

        markup.ShouldContain("EditDialogMaxWidth=\"MaxWidth.Medium\"");
        markup.ShouldContain("ShowHeaderIcon=\"false\"");
        markup.ShouldContain("ShowDisplayName=\"true\"");
        markup.ShouldContain("ShowCategoryToken=\"false\"");
        markup.ShouldContain("default-node-summary");
        markup.ShouldContain("default-node-description");
        markup.ShouldContain("SummaryCaption");
        markup.ShouldContain("default-node-meta");
        markup.ShouldContain("PortCountCaption");
        markup.ShouldContain("default-node-ports");
        markup.ShouldContain("aria-label=\"Fallback node ports\"");
        markup.ShouldContain("PortPreview");
        markup.ShouldContain("PortPreviewOverflow");
        markup.ShouldContain("default-node-token");
        markup.ShouldContain("default-node-editor");
        markup.ShouldContain("aria-label=\"Fallback component details\"");
        markup.ShouldContain("default-node-editor-table");
        markup.ShouldContain("default-node-editor-row");
        markup.ShouldContain("default-node-editor-label");
        markup.ShouldContain("default-node-editor-note");
        markup.ShouldNotContain("default-node-editor-summary");
        markup.ShouldNotContain("default-node-editor-cell");
        markup.ShouldNotContain("default-node-editor-description");
        markup.ShouldContain("default-node-port-list");
        markup.ShouldContain("aria-label=\"Fallback component port details\"");
        markup.ShouldNotContain("aria-label=\"Fallback component port contracts\"");
        markup.ShouldContain("SortedPortDescriptors");
        markup.ShouldContain("default-node-port-row");
        markup.ShouldNotContain("default-node-port-main");
        markup.ShouldContain("PortDirection(port)");
        markup.ShouldContain("PortDirectionClass(port)");
        markup.ShouldContain("default-node-empty");
        markup.ShouldNotContain("<MudText");
        markup.ShouldNotContain("<MudChip");
        markup.ShouldNotContain("flow-node-filters");
        markup.ShouldNotContain("default-node-editor-surface");
        markup.ShouldNotContain("category chip");
        markup.ShouldNotContain("Mirrors the pre-split behaviour");

        css.ShouldContain(".default-node-summary");
        css.ShouldContain(".default-node-description");
        css.ShouldContain("text-overflow: ellipsis;");
        css.ShouldContain(".default-node-meta");
        css.ShouldContain(".default-node-ports");
        css.ShouldContain("grid-template-columns: repeat(2, minmax(0, 1fr));");
        css.ShouldNotContain("grid-template-columns: repeat(3, minmax(0, auto));");
        css.ShouldNotContain("justify-content: start;");
        css.ShouldContain(".default-node-token");
        css.ShouldContain("overflow-wrap: anywhere;");
        css.ShouldContain("white-space: normal;");
        css.ShouldContain(".default-node-editor");
        css.ShouldNotContain("gap: 10px;");
        css.ShouldContain("gap: 7px;");
        css.ShouldNotContain("gap: 8px;");
        css.ShouldNotContain(".default-node-editor-surface");
        css.ShouldNotContain("background: color-mix(in srgb, var(--flux-surface-2) 4%, transparent);");
        css.ShouldNotContain("border: 1px solid color-mix(in srgb, var(--flux-border-soft) 42%, transparent);");
        css.ShouldNotContain("padding: 12px;");
        css.ShouldContain(".default-node-editor-table");
        css.ShouldContain(".default-node-editor-row");
        css.ShouldContain(".default-node-editor-label");
        css.ShouldContain(".default-node-editor-note");
        css.ShouldContain("grid-template-columns: minmax(0, 0.5fr) minmax(0, 1fr) minmax(0, 0.62fr);");
        css.ShouldNotContain("grid-template-columns: 84px minmax(0, 1fr) minmax(120px, 0.45fr);");
        css.ShouldContain("min-height: 38px;");
        css.ShouldNotContain(".default-node-editor-summary");
        css.ShouldNotContain(".default-node-editor-cell");
        css.ShouldNotContain(".default-node-editor-description");
        css.ShouldContain(".default-node-port-list");
        css.ShouldContain("border-top: 1px solid color-mix(in srgb, var(--flux-border-soft) 30%, transparent);");
        css.ShouldContain(".default-node-port-row");
        css.ShouldContain("grid-template-columns: minmax(0, 0.58fr) minmax(0, 1fr) minmax(0, 0.82fr);");
        css.ShouldNotContain("grid-template-columns: 82px minmax(0, 1fr) minmax(160px, 0.7fr);");
        css.ShouldContain("padding: 7px 2px;");
        css.ShouldNotContain("padding: 9px 2px;");
        css.ShouldNotContain(".default-node-port-main");
        css.ShouldContain(".default-node-empty");
        css.ShouldContain("@media (max-width: 640px)");
        css.ShouldNotContain("border-radius: 999px;");
    }

    [Fact]
    public void GenericFlowNodeWidget_UsesFlatFallbackSummary()
    {
        var root = FindRepositoryRoot();
        var markup = File.ReadAllText(Path.Combine(
            root,
            "src",
            "FluxMq.UI",
            "Components",
            "Workspace",
            "Nodes",
            "Generic",
            "GenericFlowNodeWidget.razor"));
        var css = File.ReadAllText(Path.Combine(
            root,
            "src",
            "FluxMq.UI",
            "Components",
            "Workspace",
            "Nodes",
            "Generic",
            "GenericFlowNodeWidget.razor.css"));

        markup.ShouldContain("ShowHeaderIcon=\"false\"");
        markup.ShouldContain("ShowDisplayName=\"true\"");
        markup.ShouldContain("ShowCategoryToken=\"false\"");
        markup.ShouldContain("generic-node-summary");
        markup.ShouldContain("generic-node-description");
        markup.ShouldContain("SummaryCaption");
        markup.ShouldContain("generic-node-meta");
        markup.ShouldContain("InputCount");
        markup.ShouldContain("OutputCount");
        markup.ShouldContain("generic-node-ports");
        markup.ShouldContain("aria-label=\"Generic node ports\"");
        markup.ShouldContain("generic-node-port-group output");
        markup.ShouldContain("generic-node-port-group input");
        markup.ShouldContain("generic-node-port-label");
        markup.ShouldContain("generic-node-port-token-list");
        markup.ShouldContain("OutputPortPreview");
        markup.ShouldContain("InputPortPreview");
        markup.ShouldContain("PortPreviewOverflow");
        markup.ShouldContain("+@PortPreviewOverflow more");
        markup.ShouldContain("generic-node-token");
        markup.ShouldContain("generic-node-editor");
        markup.ShouldContain("aria-label=\"Generic component details\"");
        markup.ShouldNotContain("generic-node-editor-surface");
        markup.ShouldNotContain("aria-label=\"Generic component contract\"");
        markup.ShouldNotContain("generic-node-editor-panel");
        markup.ShouldNotContain("generic-node-editor-panel-header");
        markup.ShouldNotContain("generic-node-editor-kicker");
        markup.ShouldNotContain("generic-node-editor-panel-token");
        markup.ShouldContain("generic-node-editor-table");
        markup.ShouldContain("generic-node-editor-row");
        markup.ShouldContain("generic-node-editor-label");
        markup.ShouldContain("generic-node-editor-note");
        markup.ShouldNotContain("generic-node-editor-meta-row");
        markup.ShouldNotContain("generic-node-editor-meta-cell");
        markup.ShouldContain("aria-label=\"Generic component summary\"");
        markup.ShouldContain("generic-node-editor-port-list");
        markup.ShouldNotContain("generic-node-editor-port-table");
        markup.ShouldContain("aria-label=\"Generic component port details\"");
        markup.ShouldNotContain("aria-label=\"Generic component port contracts\"");
        markup.ShouldContain("generic-node-editor-port-header");
        markup.ShouldContain("<span>Direction</span>");
        markup.ShouldContain("<span>Port</span>");
        markup.ShouldContain("<span>Value type</span>");
        markup.ShouldNotContain("<span>Contract</span>");
        markup.ShouldContain("SortedPortDescriptors");
        markup.ShouldContain("generic-node-editor-port-row");
        markup.ShouldNotContain("generic-node-editor-port-main");
        markup.ShouldContain("PortDirection(port)");
        markup.ShouldContain("PortDirectionClass(port)");
        markup.ShouldContain("generic-node-editor-empty");
        markup.ShouldNotContain("generic-node-editor-overview");
        markup.ShouldNotContain("generic-node-editor-meta-item");
        markup.ShouldNotContain("<MudChip");
        markup.ShouldNotContain("<MudStack");
        markup.ShouldNotContain("d-flex flex-wrap gap-1");
        markup.ShouldNotContain("flow-node-filters");

        css.ShouldContain(".generic-node-summary");
        css.ShouldContain(".generic-node-description");
        css.ShouldContain("display: -webkit-box;");
        css.ShouldContain("-webkit-line-clamp: 2;");
        css.ShouldContain(".generic-node-meta");
        css.ShouldContain("grid-template-columns: repeat(2, minmax(0, 1fr));");
        css.ShouldContain(".generic-node-ports");
        css.ShouldContain(".generic-node-port-group");
        css.ShouldContain(".generic-node-port-label");
        css.ShouldContain(".generic-node-port-token-list");
        css.ShouldContain("grid-template-columns: minmax(3.5rem, max-content) minmax(0, 1fr);");
        css.ShouldContain("display: flex;");
        css.ShouldContain("flex-wrap: wrap;");
        css.ShouldNotContain("grid-template-columns: repeat(3, minmax(0, auto));");
        css.ShouldNotContain("justify-content: start;");
        css.ShouldContain(".generic-node-token");
        css.ShouldContain("overflow-wrap: anywhere;");
        css.ShouldContain("white-space: normal;");
        css.ShouldContain(".generic-node-editor");
        css.ShouldNotContain("gap: 10px;");
        css.ShouldContain("gap: 7px;");
        css.ShouldNotContain("gap: 8px;");
        css.ShouldNotContain(".generic-node-editor-surface");
        css.ShouldNotContain(".generic-node-editor-panel");
        css.ShouldNotContain("background: color-mix(in srgb, var(--flux-surface-2) 4%, transparent);");
        css.ShouldNotContain("border: 1px solid color-mix(in srgb, var(--flux-border-soft) 42%, transparent);");
        css.ShouldNotContain("padding: 12px;");
        css.ShouldNotContain("padding: 14px;");
        css.ShouldNotContain(".generic-node-editor-panel-header");
        css.ShouldNotContain(".generic-node-editor-kicker");
        css.ShouldNotContain(".generic-node-editor-panel-token");
        css.ShouldContain(".generic-node-editor-table");
        css.ShouldContain(".generic-node-editor-row");
        css.ShouldContain(".generic-node-editor-label");
        css.ShouldContain(".generic-node-editor-note");
        css.ShouldContain("overflow-wrap: anywhere;");
        css.ShouldContain("white-space: normal;");
        css.ShouldContain("grid-template-columns: minmax(0, 0.5fr) minmax(0, 1fr) minmax(0, 0.62fr);");
        css.ShouldNotContain("grid-template-columns: 84px minmax(0, 1fr) minmax(120px, 0.45fr);");
        css.ShouldContain("min-height: 38px;");
        css.ShouldNotContain(".generic-node-editor-meta-row");
        css.ShouldNotContain(".generic-node-editor-meta-cell");
        css.ShouldNotContain("grid-template-columns: minmax(0, 1fr) minmax(110px, 0.35fr) minmax(120px, 0.35fr);");
        css.ShouldContain(".generic-node-editor-port-list");
        css.ShouldNotContain(".generic-node-editor-port-table");
        css.ShouldContain(".generic-node-editor-port-header");
        css.ShouldContain("border-top: 1px solid color-mix(in srgb, var(--flux-border-soft) 30%, transparent);");
        css.ShouldContain(".generic-node-editor-port-row");
        css.ShouldNotContain(".generic-node-editor-port-main");
        css.ShouldContain("grid-template-columns: minmax(0, 0.58fr) minmax(0, 1fr) minmax(0, 0.82fr);");
        css.ShouldNotContain("grid-template-columns: 82px minmax(0, 1fr) minmax(160px, 0.7fr);");
        css.ShouldNotContain("grid-template-columns: minmax(130px, 0.9fr) 82px minmax(160px, 1.1fr);");
        css.ShouldContain("padding: 7px 2px;");
        css.ShouldNotContain("padding: 9px 2px;");
        css.ShouldNotContain(".generic-node-editor-port-type");
        css.ShouldContain(".generic-node-editor-empty");
        css.ShouldContain("@media (max-width: 640px)");
        css.ShouldNotContain(".generic-node-editor-overview");
        css.ShouldNotContain(".generic-node-editor-meta-item");
        css.ShouldNotContain(".flow-node-filters");
        css.ShouldNotContain("border-radius: 999px;");
    }

    [Fact]
    public void MqttMetricsNodeWidget_UsesScopedDisplayAndFlatEditor()
    {
        var root = FindRepositoryRoot();
        var markup = File.ReadAllText(Path.Combine(
            root,
            "src",
            "FluxMq.UI",
            "Components",
            "Workspace",
            "Nodes",
            "MetricNode",
            "MqttMetricsNodeWidget.razor"));
        var css = File.ReadAllText(Path.Combine(
            root,
            "src",
            "FluxMq.UI",
            "Components",
            "Workspace",
            "Nodes",
            "MetricNode",
            "MqttMetricsNodeWidget.razor.css"));

        markup.ShouldContain("ShowHeaderIcon=\"false\"");
        markup.ShouldContain("ShowDisplayName=\"true\"");
        markup.ShouldContain("ShowCategoryToken=\"false\"");
        markup.ShouldContain("EditDialogMaxWidth=\"MaxWidth.Medium\"");
        markup.ShouldContain("@inject NodeEditDialogRefreshService DialogRefresh");
        markup.ShouldContain("EditorValidationError=\"@ValidateEditor\"");
        markup.ShouldContain("metrics-summary");
        markup.ShouldContain("metrics-runtime-facts");
        markup.ShouldContain("metrics-runtime-fact");
        markup.ShouldContain("aria-label=\"MQTT metrics runtime facts\"");
        markup.ShouldContain("RateWindowCaption");
        markup.ShouldContain("ReadoutLayoutCaption");
        markup.ShouldContain("metrics-readout-strip");
        markup.ShouldContain("--metric-readout-columns:@readoutColumns");
        markup.ShouldContain("MetricReadouts");
        markup.ShouldContain("metrics-readout-token @readout.CssClass");
        markup.ShouldContain("aria-label=\"MQTT metrics readouts\"");
        markup.ShouldContain("metrics-topic-list");
        markup.ShouldContain("metrics-topic-list-header");
        markup.ShouldContain("TopicSummaryCaption");
        markup.ShouldContain("aria-label=\"Top MQTT topics\"");
        markup.ShouldContain("metrics-section-label");
        markup.ShouldContain("metrics-empty");
        markup.ShouldContain("role=\"status\"");
        markup.ShouldContain("metrics-last-line");
        markup.ShouldNotContain("metrics-status-line");
        markup.ShouldNotContain("metrics-status-item");
        markup.ShouldNotContain("contract");
        markup.ShouldNotContain("Ready to save");
        markup.ShouldContain("metrics-editor");
        markup.ShouldContain("aria-label=\"MQTT metrics settings\"");
        markup.ShouldNotContain("metrics-editor-surface");
        markup.ShouldContain("metrics-config-row");
        markup.ShouldContain("aria-label=\"MQTT metrics configuration\"");
        markup.ShouldNotContain("metrics-field-label");
        markup.ShouldNotContain("<span class=\"metrics-field-label\">Buffer</span>");
        markup.ShouldNotContain("<span class=\"metrics-field-label\">Window</span>");
        markup.ShouldNotContain("<span class=\"metrics-field-label\">Columns</span>");
        markup.ShouldContain("Label=\"Input buffer\"");
        markup.ShouldContain("Value=\"@_boundedCapacity\"");
        markup.ShouldContain("ValueChanged=\"@SetBoundedCapacity\"");
        markup.ShouldContain("Class=\"metrics-buffer-field\"");
        markup.ShouldContain("Label=\"Rate window seconds\"");
        markup.ShouldContain("Value=\"@_rateWindowSeconds\"");
        markup.ShouldContain("ValueChanged=\"@SetRateWindowSeconds\"");
        markup.ShouldContain("Class=\"metrics-rate-window-field\"");
        markup.ShouldContain("Label=\"Readout columns\"");
        markup.ShouldContain("Value=\"@_metricCardColumns\"");
        markup.ShouldContain("ValueChanged=\"@SetMetricCardColumns\"");
        markup.ShouldContain("Class=\"metrics-readout-columns-field\"");
        markup.ShouldContain("RefreshDialogAsync");
        markup.ShouldContain("DialogRefresh.RefreshAsync(Node.NodeName)");
        markup.ShouldContain("private string? ValidateEditor()");
        markup.ShouldContain("Input buffer must be between 1 and 100000.");
        markup.ShouldContain("Rate window seconds must be greater than 0.");
        markup.ShouldContain("Readout columns must be between 1 and 4.");
        markup.ShouldNotContain("@bind-Value=\"_boundedCapacity\"");
        markup.ShouldNotContain("@bind-Value=\"_rateWindowSeconds\"");
        markup.ShouldNotContain("@bind-Value=\"_metricCardColumns\"");
        markup.ShouldContain("metrics-readout-selector");
        markup.ShouldContain("aria-label=\"Visible metric readouts\"");
        markup.ShouldContain("metrics-readout-selector-header");
        markup.ShouldContain("SelectedReadoutCaption");
        markup.ShouldContain("metrics-readout-options");
        markup.ShouldContain("_displayMetrics.Count");
        markup.ShouldContain("ReadoutOptionClass(selected, disabled)");
        markup.ShouldContain("metrics-readout-option-tile");
        markup.ShouldContain("metrics-readout-option-name");
        markup.ShouldContain("aria-label=\"@($\"Show {option.Label}\")\"");
        markup.ShouldContain("ToggleDisplayMetric");
        markup.ShouldContain("Class=\"metrics-readout-option-check\"");
        markup.ShouldNotContain("metrics-panel-header");
        markup.ShouldNotContain("metrics-panel-kicker");
        markup.ShouldNotContain("metrics-panel-token");
        markup.ShouldNotContain("metrics-readout-section");
        markup.ShouldNotContain("metrics-readout-section-header");
        markup.ShouldNotContain("metrics-display-list");
        markup.ShouldNotContain("metrics-readout-option-label");
        markup.ShouldNotContain("Select at least one");
        markup.ShouldNotContain("metrics-display-options");
        markup.ShouldNotContain("metrics-readout-picker");
        markup.ShouldNotContain("metrics-readout-picker-header");
        markup.ShouldNotContain("metrics-readout-workspace");
        markup.ShouldNotContain("metrics-readout-table-head");
        markup.ShouldNotContain("metrics-readout-table-body");
        markup.ShouldNotContain("metrics-readout-option-row");
        markup.ShouldNotContain("<style>");
        markup.ShouldNotContain("<MudStack");
        markup.ShouldNotContain("<MudGrid");
        markup.ShouldNotContain("<MudItem");
        markup.ShouldNotContain("<MudDivider");
        markup.ShouldNotContain("<MudChip");
        markup.ShouldNotContain("d-flex flex-wrap gap-1");
        markup.ShouldNotContain("metrics-readout-grid");
        markup.ShouldNotContain("metrics-top-topics");
        markup.ShouldNotContain("metrics-last\"");
        markup.ShouldNotContain("Icons.Material.Outlined.HourglassEmpty");
        markup.ShouldNotContain("Icons.Material.Filled.AccessTime");
        markup.ShouldNotContain("metrics-grid");
        markup.ShouldNotContain("metrics-tile");
        markup.ShouldNotContain("metrics-card-panel");
        markup.ShouldNotContain("metrics-card-option");
        markup.ShouldNotContain("metrics-config-panel");
        markup.ShouldNotContain("metrics-settings-grid");
        markup.ShouldNotContain("metrics-readout-table\" aria-label");

        css.ShouldContain(".metrics-summary");
        css.ShouldContain(".metrics-runtime-facts");
        css.ShouldContain(".metrics-runtime-fact");
        css.ShouldContain("grid-template-columns: repeat(2, minmax(0, 1fr));");
        css.ShouldNotContain(".metrics-status-line");
        css.ShouldNotContain(".metrics-status-item");
        css.ShouldNotContain("grid-template-columns: auto minmax(0, 1fr) auto minmax(0, 1fr);");
        css.ShouldContain(".metrics-readout-strip");
        css.ShouldContain("grid-template-columns: repeat(var(--metric-readout-columns), minmax(0, 1fr));");
        css.ShouldContain(".metrics-readout-token");
        css.ShouldContain(".metrics-readout-value");
        css.ShouldContain("display: grid;");
        css.ShouldContain("grid-template-columns: minmax(0, 1fr) minmax(2.4rem, max-content);");
        css.ShouldContain("overflow-wrap: anywhere;");
        css.ShouldContain("white-space: normal;");
        css.ShouldContain("background: color-mix(in srgb, var(--flux-surface-2) 20%, transparent);");
        css.ShouldNotContain("border-left: 1px solid");
        css.ShouldContain(".metrics-readout-retained");
        css.ShouldContain("color-mix(in srgb, var(--mud-palette-tertiary) 76%, var(--mud-palette-text-primary));");
        css.ShouldNotContain("color-mix(in srgb, var(--mud-palette-warning) 76%, var(--flux-border-soft));");
        css.ShouldNotContain("text-overflow: ellipsis;");
        css.ShouldNotContain("white-space: nowrap;");
        css.ShouldContain(".metrics-topic-list");
        css.ShouldContain(".metrics-topic-list-header");
        css.ShouldContain(".metrics-section-label");
        css.ShouldContain(".metrics-topic-row");
        css.ShouldContain("grid-template-columns: minmax(0, 1fr) minmax(2.25rem, 0.34fr) minmax(1.375rem, 0.18fr);");
        css.ShouldNotContain("grid-template-columns: minmax(0, 1fr) minmax(36px, 0.34fr) minmax(22px, 0.18fr);");
        css.ShouldNotContain("grid-template-columns: minmax(0, 1fr) 64px 28px;");
        css.ShouldContain("-webkit-line-clamp: 2;");
        css.ShouldContain("background: color-mix(in srgb, var(--mud-palette-tertiary) 76%, var(--mud-palette-primary));");
        css.ShouldNotContain("linear-gradient");
        css.ShouldContain(".metrics-last-line");
        css.ShouldContain(".metrics-editor");
        css.ShouldNotContain("gap: 10px;");
        css.ShouldNotContain("gap: 8px;");
        css.ShouldContain("gap: 7px;");
        css.ShouldNotContain(".metrics-editor-surface");
        css.ShouldContain(".metrics-config-row");
        css.ShouldNotContain(".metrics-field-label");
        css.ShouldContain(".metrics-readout-selector");
        css.ShouldContain("grid-template-columns: minmax(0, 1fr) minmax(0, 1fr) minmax(0, 0.72fr);");
        css.ShouldNotContain("grid-template-columns: minmax(0, 1fr) minmax(0, 1fr) minmax(112px, 150px);");
        css.ShouldContain(".metrics-readout-selector-header");
        css.ShouldContain(".metrics-readout-selector-header > div");
        css.ShouldContain(".metrics-readout-options");
        css.ShouldContain("gap: 0 7px;");
        css.ShouldNotContain("gap: 0 10px;");
        css.ShouldNotContain("background: color-mix(in srgb, var(--flux-surface-2) 4%, transparent);");
        css.ShouldNotContain("border: 1px solid color-mix(in srgb, var(--flux-border-soft) 42%, transparent);");
        css.ShouldNotContain("padding: 12px;");
        css.ShouldContain("border-top: 1px solid color-mix(in srgb, var(--flux-border-soft) 58%, transparent);");
        css.ShouldContain("padding-top: 7px;");
        css.ShouldNotContain("padding-top: 8px;");
        css.ShouldContain(".metrics-readout-option-tile");
        css.ShouldContain("grid-template-columns: repeat(3, minmax(0, 1fr));");
        css.ShouldContain("grid-template-columns: minmax(0, 1fr) minmax(1.75rem, max-content);");
        css.ShouldContain("grid-template-columns: minmax(0, 1fr) minmax(2.75rem, max-content) minmax(1.75rem, max-content);");
        css.ShouldNotContain("grid-template-columns: minmax(0, 1fr) 28px;");
        css.ShouldNotContain("grid-template-columns: minmax(0, 1fr) 44px 28px;");
        css.ShouldNotContain("grid-template-columns: minmax(0, 1fr) 30px;");
        css.ShouldContain(".metrics-readout-option-tile.selected");
        css.ShouldContain(".metrics-readout-option-tile.locked");
        css.ShouldContain(".metrics-readout-option-name");
        css.ShouldContain("min-height: 32px;");
        css.ShouldContain("align-self: center;");
        css.ShouldContain("justify-self: end;");
        css.ShouldContain(".metrics-readout-selector ::deep(.mud-checkbox)");
        css.ShouldContain("display: inline-flex;");
        css.ShouldContain("min-height: 1.75rem;");
        css.ShouldNotContain("min-height: 30px;");
        css.ShouldContain(".metrics-readout-selector ::deep(.mud-checkbox .mud-button-root)");
        css.ShouldContain("padding: 3px;");
        css.ShouldNotContain(".metrics-panel-header");
        css.ShouldNotContain(".metrics-panel-kicker");
        css.ShouldNotContain(".metrics-panel-token");
        css.ShouldNotContain(".metrics-readout-section");
        css.ShouldNotContain(".metrics-readout-section-header");
        css.ShouldNotContain(".metrics-display-list");
        css.ShouldNotContain(".metrics-readout-option-label");
        css.ShouldNotContain(".metrics-display-options");
        css.ShouldNotContain(".metrics-readout-picker");
        css.ShouldNotContain(".metrics-readout-picker-header");
        css.ShouldNotContain(".metrics-readout-grid");
        css.ShouldNotContain(".metrics-readout-workspace");
        css.ShouldNotContain(".metrics-readout-table-head");
        css.ShouldNotContain(".metrics-readout-table-body");
        css.ShouldNotContain(".metrics-readout-option-row");
        css.ShouldNotContain(".metrics-top-topics");
        css.ShouldNotContain(".metrics-last {");
        css.ShouldContain(".metrics-editor ::deep(.mud-input-control)");
        css.ShouldContain("@media (max-width: 720px)");
        css.ShouldNotContain(".flow-node-filters");
        css.ShouldNotContain(".metrics-grid");
        css.ShouldNotContain(".metrics-tile");
        css.ShouldNotContain(".metrics-card-panel");
        css.ShouldNotContain(".metrics-card-option");
        css.ShouldNotContain(".metrics-config-panel");
        css.ShouldNotContain(".metrics-settings-grid");
        css.ShouldNotContain(".metrics-readout-table {");
        css.ShouldNotContain("border-radius: 999px;");
    }

    [Fact]
    public void PayloadInspectNodeWidget_UsesCompactSummaryAndFlatEditor()
    {
        var root = FindRepositoryRoot();
        var markup = File.ReadAllText(Path.Combine(
            root,
            "src",
            "FluxMq.UI",
            "Components",
            "Workspace",
            "Nodes",
            "Payloads",
            "PayloadInspectNodeWidget.razor"));
        var css = File.ReadAllText(Path.Combine(
            root,
            "src",
            "FluxMq.UI",
            "Components",
            "Workspace",
            "Nodes",
            "Payloads",
            "PayloadInspectNodeWidget.razor.css"));

        markup.ShouldContain("EditDialogMaxWidth=\"MaxWidth.Medium\"");
        markup.ShouldContain("@inject NodeEditDialogRefreshService DialogRefresh");
        markup.ShouldContain("EditorValidationError=\"@ValidateEditor\"");
        markup.ShouldContain("ShowHeaderIcon=\"false\"");
        markup.ShouldContain("ShowDisplayName=\"true\"");
        markup.ShouldContain("ShowCategoryToken=\"false\"");
        markup.ShouldContain("payload-inspect-summary");
        markup.ShouldContain("payload-inspect-meta");
        markup.ShouldContain("payload-inspect-meta-item input");
        markup.ShouldContain("Payload request");
        markup.ShouldContain("PreviewCaption");
        markup.ShouldContain("FormatCapCaption");
        markup.ShouldContain("InputBufferCaption");
        markup.ShouldContain("Base64Caption");
        markup.ShouldContain("FormattersCaption");
        markup.ShouldContain("<span>Base64</span>");
        markup.ShouldContain("<span>Formatting</span>");
        markup.ShouldNotContain("payload-inspect-contracts");
        markup.ShouldNotContain("aria-label=\"Payload inspection request fields\"");
        markup.ShouldNotContain("aria-label=\"Payload inspection result fields\"");
        markup.ShouldNotContain("payload-inspect-token");
        markup.ShouldNotContain("Request fields");
        markup.ShouldNotContain("Result fields");
        markup.ShouldNotContain("encodingHint");
        markup.ShouldNotContain("byteCount");
        markup.ShouldContain("payload-inspect-editor");
        markup.ShouldContain("aria-label=\"Payload inspection settings\"");
        markup.ShouldContain("payload-inspect-number-grid");
        markup.ShouldContain("Label=\"Preview bytes\"");
        markup.ShouldContain("Value=\"@_maxPreviewBytes\"");
        markup.ShouldContain("ValueChanged=\"@SetMaxPreviewBytes\"");
        markup.ShouldNotContain("@bind-Value=\"_maxPreviewBytes\"");
        markup.ShouldContain("Label=\"Formatted chars\"");
        markup.ShouldContain("Value=\"@_maxFormattedChars\"");
        markup.ShouldContain("ValueChanged=\"@SetMaxFormattedChars\"");
        markup.ShouldNotContain("@bind-Value=\"_maxFormattedChars\"");
        markup.ShouldContain("Label=\"Input buffer\"");
        markup.ShouldContain("Value=\"@_boundedCapacity\"");
        markup.ShouldContain("ValueChanged=\"@SetBoundedCapacity\"");
        markup.ShouldNotContain("@bind-Value=\"_boundedCapacity\"");
        markup.ShouldContain("Label=\"Detect Base64\"");
        markup.ShouldContain("@bind-Value=\"_detectBase64\"");
        markup.ShouldContain("Label=\"Format JSON\"");
        markup.ShouldContain("@bind-Value=\"_formatJson\"");
        markup.ShouldContain("Label=\"Format XML\"");
        markup.ShouldContain("@bind-Value=\"_formatXml\"");
        markup.ShouldContain("Class=\"payload-inspect-preview-field\"");
        markup.ShouldContain("Class=\"payload-inspect-buffer-field\"");
        markup.ShouldContain("private async Task SetMaxPreviewBytes(int value)");
        markup.ShouldContain("private async Task SetMaxFormattedChars(int value)");
        markup.ShouldContain("private async Task SetBoundedCapacity(int value)");
        markup.ShouldContain("private string? ValidateEditor()");
        markup.ShouldContain("Preview bytes must be between 1 and 1048576.");
        markup.ShouldContain("Formatted chars must be between 1 and 1048576.");
        markup.ShouldContain("Input buffer must be between 1 and 100000.");
        markup.ShouldNotContain("<Stat ");
        markup.ShouldNotContain("<MudChip");
        markup.ShouldNotContain("d-flex flex-wrap gap-1");
        markup.ShouldNotContain("PayloadInspectionRequest");
        markup.ShouldNotContain("PayloadInspectionResult");

        css.ShouldContain(".payload-inspect-summary");
        css.ShouldContain(".payload-inspect-meta");
        css.ShouldContain("grid-template-columns: repeat(3, minmax(0, 1fr));");
        css.ShouldContain(".payload-inspect-meta-item.input");
        css.ShouldContain("grid-column: 1 / -1;");
        css.ShouldContain("-webkit-line-clamp: 2;");
        css.ShouldContain("overflow-wrap: anywhere;");
        css.ShouldContain("white-space: normal;");
        css.ShouldNotContain("grid-template-columns: minmax(0, 1.22fr) minmax(0, 0.64fr) minmax(0, 0.7fr) minmax(0, 0.76fr);");
        css.ShouldNotContain("grid-template-columns: minmax(0, 1fr) 72px 78px 84px;");
        css.ShouldNotContain(".payload-inspect-contracts");
        css.ShouldNotContain(".payload-inspect-contract");
        css.ShouldNotContain(".payload-inspect-contract-label");
        css.ShouldNotContain(".payload-inspect-token");
        css.ShouldNotContain("grid-template-columns: repeat(4, minmax(0, auto));");
        css.ShouldContain(".payload-inspect-editor");
        css.ShouldNotContain("gap: 10px;");
        css.ShouldNotContain("gap: 8px;");
        css.ShouldContain("gap: 7px;");
        css.ShouldContain("gap: 6px;");
        css.ShouldContain("gap: 6px 16px;");
        css.ShouldNotContain("gap: 7px 18px;");
        css.ShouldNotContain("gap: 8px 22px;");
        css.ShouldContain("min-height: 34px;");
        css.ShouldNotContain("min-height: 36px;");
        css.ShouldNotContain("min-height: 40px;");
        css.ShouldContain(".payload-inspect-number-grid");
        css.ShouldContain("grid-template-columns: repeat(3, minmax(0, 1fr));");
        css.ShouldContain(".payload-inspect-options");
        css.ShouldContain(".payload-inspect-options ::deep(.mud-checkbox)");
        css.ShouldContain("display: inline-flex;");
        css.ShouldContain(".payload-inspect-options ::deep(.mud-checkbox .mud-button-root)");
        css.ShouldContain("padding: 3px;");
        css.ShouldContain(".payload-inspect-options ::deep(.mud-checkbox .mud-typography)");
        css.ShouldContain(".payload-inspect-editor ::deep(.mud-input-control)");
        css.ShouldContain(".payload-inspect-editor ::deep(.mud-input-control > .mud-input-control-input-container > .mud-input)");
        css.ShouldContain("@media (max-width: 640px)");
        css.ShouldNotContain(".flow-node-filters");
        css.ShouldNotContain("border: 1px solid var(--flux-border-soft);");
    }

    [Fact]
    public void MetricSourceNodeWidget_UsesCompactSummaryAndFlatEditor()
    {
        var root = FindRepositoryRoot();
        var markup = File.ReadAllText(Path.Combine(
            root,
            "src",
            "FluxMq.UI",
            "Components",
            "Workspace",
            "Nodes",
            "MetricSource",
            "MetricSourceNodeWidget.razor"));
        var css = File.ReadAllText(Path.Combine(
            root,
            "src",
            "FluxMq.UI",
            "Components",
            "Workspace",
            "Nodes",
            "MetricSource",
            "MetricSourceNodeWidget.razor.css"));

        markup.ShouldContain("EditDialogMaxWidth=\"MaxWidth.Medium\"");
        markup.ShouldContain("ShowHeaderIcon=\"false\"");
        markup.ShouldContain("ShowDisplayName=\"true\"");
        markup.ShouldContain("ShowCategoryToken=\"false\"");
        markup.ShouldContain("@inject NodeEditDialogRefreshService DialogRefresh");
        markup.ShouldContain("EditorValidationError=\"@ValidateEditor\"");
        markup.ShouldContain("metric-source-summary");
        markup.ShouldContain("metric-source-meta");
        markup.ShouldContain("MetricCaption");
        markup.ShouldContain("LatestValue");
        markup.ShouldContain("StartModeCaption");
        markup.ShouldContain("BufferCaption");
        markup.ShouldContain("metric-source-parameters");
        markup.ShouldContain("aria-label=\"Metric source parameters\"");
        markup.ShouldContain("metric-source-parameter-label");
        markup.ShouldContain("ParameterPreview");
        markup.ShouldContain("ParameterPreviewOverflow");
        markup.ShouldContain("metric-source-token");
        markup.ShouldNotContain("metric-source-contract");
        markup.ShouldNotContain("aria-label=\"Metric source output fields\"");
        markup.ShouldNotContain("Output fields");
        markup.ShouldNotContain("NumberMetricReading");
        markup.ShouldNotContain("<span class=\"metric-source-token node-ui-token\">metricId</span>");
        markup.ShouldNotContain("<span class=\"metric-source-token node-ui-token\">timestamp</span>");
        markup.ShouldNotContain("<span class=\"metric-source-token node-ui-token\">value</span>");
        markup.ShouldNotContain("Ready to save");
        markup.ShouldContain("metric-source-editor");
        markup.ShouldContain("aria-label=\"Metric source settings\"");
        markup.ShouldNotContain("metric-source-settings-surface");
        markup.ShouldNotContain("metric-source-settings-title");
        markup.ShouldContain("metric-source-config-row");
        markup.ShouldContain("aria-label=\"Metric source configuration\"");
        markup.ShouldContain("Class=\"metric-source-metric-field\"");
        markup.ShouldContain("metric-source-description-row");
        markup.ShouldNotContain("metric-source-parameter-surface");
        markup.ShouldNotContain("metric-source-parameter-summary");
        markup.ShouldContain("metric-source-parameter-group");
        markup.ShouldContain("metric-source-parameter-heading");
        markup.ShouldContain("ParameterCountCaption");
        markup.ShouldContain("metric-source-parameter-composer");
        markup.ShouldNotContain("metric-source-parameter-grid");
        markup.ShouldContain("aria-label=\"Metric parameters\"");
        markup.ShouldContain("metric-source-parameter-cell");
        markup.ShouldContain("ParameterHelpTitle(parameter)");
        markup.ShouldNotContain("metric-source-parameter-note");
        markup.ShouldNotContain("ParameterHelpNote(parameter)");
        markup.ShouldContain("Class=\"metric-source-parameter-field\"");
        markup.ShouldNotContain("HelperText=\"@parameter.HelpText\"");
        markup.ShouldContain("metric-source-start-row");
        markup.ShouldContain("aria-label=\"Metric source start behavior\"");
        markup.ShouldContain("Class=\"metric-source-start-check\"");
        markup.ShouldContain("Label=\"Emit latest reading on start\"");
        markup.ShouldContain("Label=\"Output buffer\"");
        markup.ShouldNotContain("Label=\"Input buffer\"");
        markup.ShouldContain("Value=\"@_boundedCapacity\"");
        markup.ShouldContain("ValueChanged=\"@SetBoundedCapacity\"");
        markup.ShouldContain("Class=\"metric-source-buffer-field\"");
        markup.ShouldContain("RefreshDialogAsync");
        markup.ShouldContain("DialogRefresh.RefreshAsync(Node.NodeName)");
        markup.ShouldContain("private string? ValidateEditor()");
        markup.ShouldContain("Output buffer must be between 1 and 100000.");
        markup.ShouldNotContain("ValueChanged=\"@(value => _boundedCapacity = Math.Max(1, value))\"");
        markup.ShouldNotContain("metric-source-main-grid");
        markup.ShouldNotContain("metric-source-start-panel");
        markup.ShouldNotContain("metric-source-start-option");
        markup.ShouldNotContain("metric-source-config-grid");
        markup.ShouldNotContain("metric-source-description-panel");
        markup.ShouldNotContain("metric-source-parameter-panel");
        markup.ShouldNotContain("metric-source-panel-header");
        markup.ShouldNotContain("metric-source-panel-kicker");
        markup.ShouldNotContain("metric-source-panel-token");
        markup.ShouldNotContain("metric-source-parameter-section");
        markup.ShouldNotContain("metric-source-parameter-header");
        markup.ShouldNotContain("metric-source-parameter-workspace");
        markup.ShouldNotContain("metric-source-parameter-table-head");
        markup.ShouldNotContain("metric-source-config-panel");
        markup.ShouldNotContain("metric-source-source-grid");
        markup.ShouldNotContain("metric-source-parameter-table\" aria-label");
        markup.ShouldNotContain("<span>Parameter</span>");
        markup.ShouldNotContain("<span>Value</span>");
        markup.ShouldNotContain("<Stat ");
        markup.ShouldNotContain("<MudStack");
        markup.ShouldNotContain("<MudChip");
        markup.ShouldNotContain("<MudGrid");
        markup.ShouldNotContain("<MudAlert");
        markup.ShouldNotContain("d-flex flex-wrap gap-1");

        css.ShouldContain(".metric-source-summary");
        css.ShouldContain(".metric-source-meta");
        css.ShouldContain("grid-template-columns: repeat(3, minmax(0, 1fr));");
        css.ShouldContain(".metric-source-meta-item.metric");
        css.ShouldContain("grid-column: 1 / -1;");
        css.ShouldContain("-webkit-line-clamp: 2;");
        css.ShouldContain("overflow-wrap: anywhere;");
        css.ShouldContain("white-space: normal;");
        css.ShouldNotContain("grid-template-columns: minmax(0, 1.42fr) minmax(0, 0.72fr) minmax(0, 0.62fr) minmax(0, 0.72fr);");
        css.ShouldNotContain("grid-template-columns: minmax(0, 1fr) 64px 56px 64px;");
        css.ShouldContain(".metric-source-parameters");
        css.ShouldContain(".metric-source-parameter-label");
        css.ShouldNotContain(".metric-source-contract");
        css.ShouldNotContain(".metric-source-contract-label");
        css.ShouldContain("display: flex;");
        css.ShouldContain("flex-wrap: wrap;");
        css.ShouldContain("flex: 0 0 100%;");
        css.ShouldContain(".metric-source-token");
        css.ShouldContain("display: inline-flex;");
        css.ShouldContain("overflow-wrap: anywhere;");
        css.ShouldContain("white-space: normal;");
        css.ShouldContain(".metric-source-editor");
        css.ShouldNotContain("gap: 10px;");
        css.ShouldNotContain("gap: 8px;");
        css.ShouldContain("gap: 7px;");
        css.ShouldNotContain(".metric-source-settings-surface");
        css.ShouldNotContain(".metric-source-settings-title");
        css.ShouldContain(".metric-source-config-row");
        css.ShouldContain("grid-template-columns: minmax(0, 1fr) minmax(0, 0.54fr);");
        css.ShouldNotContain("grid-template-columns: minmax(0, 1fr) 150px;");
        css.ShouldContain(".metric-source-description-row");
        css.ShouldNotContain("grid-template-columns: 72px minmax(0, 1fr);");
        css.ShouldContain("display: flex;");
        css.ShouldContain("flex: 1 1 180px;");
        css.ShouldContain("min-width: 0;");
        css.ShouldContain("padding-top: 7px;");
        css.ShouldNotContain("padding-top: 8px;");
        css.ShouldNotContain(".metric-source-parameter-surface");
        css.ShouldNotContain(".metric-source-parameter-summary");
        css.ShouldContain(".metric-source-parameter-group");
        css.ShouldContain(".metric-source-parameter-heading");
        css.ShouldContain("grid-template-columns: minmax(0, 1fr) minmax(0, 0.34fr);");
        css.ShouldNotContain("grid-template-columns: minmax(0, 1fr) auto;");
        css.ShouldContain("overflow-wrap: anywhere;");
        css.ShouldContain("white-space: normal;");
        css.ShouldNotContain("background: color-mix(in srgb, var(--flux-surface-2) 4%, transparent);");
        css.ShouldNotContain("border: 1px solid color-mix(in srgb, var(--flux-border-soft) 42%, transparent);");
        css.ShouldNotContain("padding: 12px;");
        css.ShouldNotContain("padding: 14px;");
        css.ShouldContain(".metric-source-parameter-composer");
        css.ShouldNotContain(".metric-source-parameter-grid");
        css.ShouldContain("grid-template-columns: repeat(2, minmax(0, 1fr));");
        css.ShouldContain(".metric-source-parameter-cell");
        css.ShouldNotContain(".metric-source-parameter-note");
        css.ShouldContain(".metric-source-start-row");
        css.ShouldContain("min-height: 34px;");
        css.ShouldNotContain("min-height: 38px;");
        css.ShouldContain(".metric-source-start-row ::deep(.mud-checkbox)");
        css.ShouldContain("display: inline-flex;");
        css.ShouldContain(".metric-source-start-row ::deep(.mud-checkbox .mud-button-root)");
        css.ShouldContain("padding: 3px;");
        css.ShouldNotContain("padding: 4px;");
        css.ShouldContain(".metric-source-start-row ::deep(.mud-checkbox .mud-typography)");
        css.ShouldContain("margin: 0;");
        css.ShouldContain(".metric-source-editor ::deep(.mud-input-control)");
        css.ShouldContain("@media (max-width: 720px)");
        css.ShouldNotContain(".metric-source-main-grid");
        css.ShouldNotContain(".metric-source-start-panel");
        css.ShouldNotContain(".metric-source-config-grid");
        css.ShouldNotContain(".metric-source-start-option");
        css.ShouldNotContain(".metric-source-description-panel");
        css.ShouldNotContain(".metric-source-parameter-panel");
        css.ShouldNotContain(".metric-source-panel-header");
        css.ShouldNotContain(".metric-source-panel-kicker");
        css.ShouldNotContain(".metric-source-panel-token");
        css.ShouldNotContain(".metric-source-parameter-section");
        css.ShouldNotContain(".metric-source-parameter-header");
        css.ShouldNotContain(".metric-source-parameter-workspace");
        css.ShouldNotContain(".metric-source-parameter-table-head");
        css.ShouldNotContain(".metric-source-config-panel");
        css.ShouldNotContain(".metric-source-source-grid");
        css.ShouldNotContain(".metric-source-parameter-table {");
        css.ShouldNotContain(".flow-node-filters");
        css.ShouldNotContain("border-radius: 999px;");
    }

    [Fact]
    public void GeneratedSourceNodeWidget_UsesCompactSummaryAndFlatEditor()
    {
        var root = FindRepositoryRoot();
        var markup = File.ReadAllText(Path.Combine(
            root,
            "src",
            "FluxMq.UI",
            "Components",
            "Workspace",
            "Nodes",
            "Sources",
            "GeneratedSourceNodeWidget.razor"));
        var css = File.ReadAllText(Path.Combine(
            root,
            "src",
            "FluxMq.UI",
            "Components",
            "Workspace",
            "Nodes",
            "Sources",
            "GeneratedSourceNodeWidget.razor.css"));

        markup.ShouldContain("EditDialogMaxWidth=\"MaxWidth.Large\"");
        markup.ShouldContain("ShowHeaderIcon=\"false\"");
        markup.ShouldContain("ShowDisplayName=\"true\"");
        markup.ShouldContain("ShowCategoryToken=\"false\"");
        markup.ShouldContain("EditorValidationError=\"@ValidateEditor\"");
        markup.ShouldContain("generated-source-summary");
        markup.ShouldContain("generated-source-meta");
        markup.ShouldContain("MessageCountCaption");
        markup.ShouldContain("FirstTopicCaption");
        markup.ShouldContain("BufferCaption");
        markup.ShouldContain("generated-source-previews");
        markup.ShouldContain("aria-label=\"Generated message preview\"");
        markup.ShouldContain("MessagePreview");
        markup.ShouldContain("MessagePreviewOverflow");
        markup.ShouldNotContain("generated-source-contract");
        markup.ShouldNotContain("aria-label=\"Generated source output fields\"");
        markup.ShouldContain("generated-source-token");
        markup.ShouldNotContain("<span class=\"generated-source-token node-ui-token\">MqttEnvelope</span>");
        markup.ShouldNotContain("<span class=\"generated-source-token node-ui-token\">topic</span>");
        markup.ShouldNotContain("<span class=\"generated-source-token node-ui-token\">payload</span>");
        markup.ShouldNotContain("<span class=\"generated-source-token node-ui-token\">qos</span>");
        markup.ShouldContain("generated-source-editor");
        markup.ShouldContain("aria-label=\"Generated source settings\"");
        markup.ShouldContain("generated-source-layout");
        markup.ShouldContain("aria-label=\"Generated source editor layout\"");
        markup.ShouldContain("generated-source-sidecar");
        markup.ShouldContain("aria-label=\"Generated source support settings\"");
        markup.ShouldContain("Label=\"Output buffer\"");
        markup.ShouldContain("Value=\"@_boundedCapacity\"");
        markup.ShouldContain("ValueChanged=\"@SetBoundedCapacity\"");
        markup.ShouldNotContain("@bind-Value=\"_boundedCapacity\"");
        markup.ShouldNotContain("generated-source-editor-surface");
        markup.ShouldNotContain("generated-source-message-panel");
        markup.ShouldNotContain("generated-source-panel-header");
        markup.ShouldNotContain("generated-source-panel-kicker");
        markup.ShouldNotContain("generated-source-panel-token");
        markup.ShouldNotContain("MessagePanelCaption");
        markup.ShouldNotContain("generated-source-settings-row");
        markup.ShouldNotContain("generated-source-control-strip");
        markup.ShouldNotContain("generated-source-editor-toolbar");
        markup.ShouldNotContain("generated-source-action-row");
        markup.ShouldNotContain("aria-label=\"Generated source configuration\"");
        markup.ShouldNotContain("aria-label=\"Generated source controls\"");
        markup.ShouldContain("generated-source-message-table");
        markup.ShouldContain("aria-label=\"Generated message rows\"");
        markup.ShouldContain("generated-source-table-header");
        markup.ShouldNotContain("aria-hidden=\"true\"");
        markup.ShouldContain("generated-source-column-header");
        markup.ShouldContain("<span class=\"generated-source-column-header action\">");
        markup.ShouldContain("<span class=\"generated-source-column-header\">Topic</span>");
        markup.ShouldContain("<span class=\"generated-source-column-header\">Payload</span>");
        markup.ShouldContain("<span class=\"generated-source-column-header\">Retain</span>");
        markup.ShouldContain("<span class=\"generated-source-column-header\">Received at</span>");
        markup.ShouldContain("generated-source-message-row");
        markup.ShouldContain("generated-source-message-index");
        markup.ShouldNotContain("generated-source-message-fields");
        markup.ShouldContain("generated-source-topic-field");
        markup.ShouldContain("UpdateMessageTopicAsync(index, value)");
        markup.ShouldContain("Immediate=\"true\"");
        markup.ShouldNotContain("Label=\"Topic\"");
        markup.ShouldContain("Placeholder=\"@($\"Topic {index + 1}\")\"");
        markup.ShouldContain("aria-label=\"@($\"Generated message {index + 1} topic\")\"");
        markup.ShouldContain("generated-source-qos-field");
        markup.ShouldContain("Label=\"QoS\"");
        markup.ShouldContain("generated-source-retain-toggle");
        markup.ShouldNotContain("generated-source-options-cell");
        markup.ShouldNotContain("generated-source-retain-cell");
        markup.ShouldNotContain("Label=\"Retain\"");
        markup.ShouldContain("generated-source-payload-field");
        markup.ShouldNotContain("Label=\"Payload\"");
        markup.ShouldContain("Placeholder=\"Payload\"");
        markup.ShouldContain("generated-source-received-field");
        markup.ShouldNotContain("Label=\"Received at\"");
        markup.ShouldContain("Placeholder=\"Received at\"");
        markup.ShouldContain("AddMessageAsync");
        markup.ShouldContain("Text=\"@AddGeneratedMessageLabel\"");
        markup.ShouldContain("aria-label=\"@AddGeneratedMessageLabel\"");
        markup.ShouldContain("private string AddGeneratedMessageLabel => $\"Add generated message to {Node.NodeName}\";");
        markup.ShouldContain("RemoveMessage(index)");
        markup.ShouldContain("Text=\"@RemoveGeneratedMessageLabel(index)\"");
        markup.ShouldContain("aria-label=\"@RemoveGeneratedMessageLabel(index)\"");
        markup.ShouldContain("private string RemoveGeneratedMessageLabel(int index)");
        markup.ShouldContain("$\"Remove {target} from {Node.NodeName}\"");
        markup.ShouldNotContain("Text=\"Add message\"");
        markup.ShouldNotContain("aria-label=\"Add generated message\"");
        markup.ShouldNotContain("Text=\"Remove message\"");
        markup.ShouldNotContain("aria-label=\"@($\"Remove generated message {index + 1}\")\"");
        markup.ShouldContain("ValidateEditor");
        markup.ShouldContain("Add at least one generated message before saving.");
        markup.ShouldContain("Each generated message needs a topic.");
        markup.ShouldContain("Output buffer must be between 1 and 100000.");
        markup.ShouldContain("private async Task SetBoundedCapacity(int value)");
        markup.ShouldNotContain("generated-source-message-list");
        markup.ShouldNotContain("generated-source-message-header");
        markup.ShouldNotContain("generated-source-table-title");
        markup.ShouldNotContain("generated-source-table-actions");
        markup.ShouldNotContain("generated-source-message-detail");
        markup.ShouldNotContain("<Stat ");
        markup.ShouldNotContain("<MudChip");
        markup.ShouldNotContain("<MudStack");
        markup.ShouldNotContain("<MudGrid");
        markup.ShouldNotContain("<MudDivider");
        markup.ShouldNotContain("flow-node-filters");
        markup.ShouldNotContain("d-flex flex-wrap gap-1");

        css.ShouldContain(".generated-source-summary");
        css.ShouldContain(".generated-source-meta");
        css.ShouldContain("grid-template-columns: minmax(0, 1fr) minmax(0, 1fr);");
        css.ShouldContain(".generated-source-meta-item.topic");
        css.ShouldContain("grid-column: 1 / -1;");
        css.ShouldNotContain("grid-template-columns: minmax(0, 0.74fr) minmax(0, 1.32fr) minmax(0, 0.7fr);");
        css.ShouldNotContain("grid-template-columns: 74px minmax(0, 1fr) 70px;");
        css.ShouldContain("-webkit-line-clamp: 2;");
        css.ShouldContain(".generated-source-previews");
        css.ShouldContain(".generated-source-preview-row");
        css.ShouldContain("grid-template-columns: minmax(0, 1fr) auto auto;");
        css.ShouldNotContain("text-overflow: ellipsis;");
        css.ShouldNotContain(".generated-source-contract");
        css.ShouldNotContain(".generated-source-contract-label");
        css.ShouldNotContain("grid-template-columns: repeat(4, minmax(0, auto));");
        css.ShouldContain("white-space: normal;");
        css.ShouldContain(".generated-source-token");
        css.ShouldContain("display: inline-flex;");
        css.ShouldContain("overflow-wrap: anywhere;");
        css.ShouldContain("white-space: normal;");
        css.ShouldContain(".generated-source-editor");
        css.ShouldNotContain("gap: 10px;");
        css.ShouldNotContain("gap: 8px;");
        css.ShouldContain("gap: 7px;");
        css.ShouldNotContain(".generated-source-editor-surface");
        css.ShouldNotContain(".generated-source-message-panel");
        css.ShouldNotContain("background: color-mix(in srgb, var(--flux-surface-2) 4%, transparent);");
        css.ShouldNotContain("border: 1px solid color-mix(in srgb, var(--flux-border-soft) 42%, transparent);");
        css.ShouldNotContain("padding: 12px;");
        css.ShouldNotContain(".generated-source-panel-header");
        css.ShouldNotContain(".generated-source-panel-kicker");
        css.ShouldNotContain(".generated-source-panel-token");
        css.ShouldContain(".generated-source-layout");
        css.ShouldContain(".generated-source-sidecar");
        css.ShouldNotContain(".generated-source-settings-row");
        css.ShouldNotContain(".generated-source-control-strip");
        css.ShouldNotContain(".generated-source-editor-toolbar");
        css.ShouldNotContain(".generated-source-action-row");
        css.ShouldContain("grid-template-columns: minmax(0, 1fr) minmax(168px, 0.28fr);");
        css.ShouldNotContain("max-width: 220px;");
        css.ShouldNotContain("grid-template-columns: minmax(180px, 220px) 28px;");
        css.ShouldNotContain("grid-template-columns: minmax(180px, 220px);");
        css.ShouldContain(".generated-source-message-table");
        css.ShouldContain("overflow-x: auto;");
        css.ShouldContain(".generated-source-table-header");
        css.ShouldContain(".generated-source-column-header");
        css.ShouldContain(".generated-source-column-header.action");
        css.ShouldContain(".generated-source-column-header.action ::deep(.generated-source-add-button.mud-icon-button)");
        css.ShouldNotContain(".generated-source-message-list");
        css.ShouldNotContain(".generated-source-message-header");
        css.ShouldContain(".generated-source-message-row");
        css.ShouldContain(".generated-source-message-index");
        css.ShouldNotContain(".generated-source-message-fields");
        css.ShouldNotContain(".generated-source-message-detail");
        css.ShouldContain("grid-template-columns: minmax(1.75rem, max-content) minmax(0, 0.96fr) minmax(0, 1.54fr) minmax(0, 0.58fr) minmax(0, 0.48fr) minmax(0, 0.9fr) minmax(1.75rem, max-content);");
        css.ShouldNotContain("grid-template-columns: minmax(24px, 0.18fr) minmax(0, 0.96fr) minmax(0, 1.54fr) minmax(0, 0.58fr) minmax(0, 0.48fr) minmax(0, 0.9fr) minmax(24px, 0.18fr);");
        css.ShouldNotContain("grid-template-columns: 28px minmax(150px, 0.9fr) minmax(240px, 1.4fr) 104px 84px minmax(132px, 0.72fr) 28px;");
        css.ShouldNotContain("grid-template-columns: 30px minmax(150px, 0.9fr) minmax(240px, 1.4fr) 104px 84px minmax(132px, 0.72fr) 30px;");
        css.ShouldContain("height: 1.75rem;");
        css.ShouldContain("min-height: 1.75rem;");
        css.ShouldContain("min-width: 1.75rem;");
        css.ShouldContain("width: 1.75rem;");
        css.ShouldContain("padding: 6px 0;");
        css.ShouldNotContain("padding: 8px 0;");
        css.ShouldContain(".generated-source-retain-toggle");
        css.ShouldNotContain(".generated-source-options-cell");
        css.ShouldNotContain(".generated-source-retain-cell");
        css.ShouldContain("min-height: 34px;");
        css.ShouldContain(".generated-source-retain-toggle ::deep(.mud-checkbox)");
        css.ShouldContain("display: inline-flex;");
        css.ShouldContain(".generated-source-retain-toggle ::deep(.mud-checkbox .mud-button-root)");
        css.ShouldContain("padding: 3px;");
        css.ShouldNotContain("padding: 4px;");
        css.ShouldContain("grid-template-columns: minmax(0, 1fr) minmax(1.75rem, max-content);");
        css.ShouldContain("grid-template-columns: minmax(1.625rem, max-content) minmax(0, 1fr);");
        css.ShouldNotContain("grid-template-columns: minmax(0, 1fr) 28px;");
        css.ShouldNotContain("grid-template-columns: 26px minmax(0, 1fr);");
        css.ShouldNotContain("grid-template-columns: minmax(0, 1fr) 30px;");
        css.ShouldNotContain("min-height: 36px;");
        css.ShouldContain(".generated-source-editor ::deep(.mud-input-control)");
        css.ShouldContain("@media (max-width: 720px)");
        css.ShouldNotContain(".generated-source-table-title");
        css.ShouldNotContain(".generated-source-table-actions");
        css.ShouldNotContain(".flow-node-filters");
        css.ShouldNotContain("border-radius: 999px;");
    }

    [Fact]
    public void ReplaySourceNodeWidget_UsesCompactSummaryAndFlatEditor()
    {
        var root = FindRepositoryRoot();
        var markup = File.ReadAllText(Path.Combine(
            root,
            "src",
            "FluxMq.UI",
            "Components",
            "Workspace",
            "Nodes",
            "Sources",
            "ReplaySourceNodeWidget.razor"));
        var css = File.ReadAllText(Path.Combine(
            root,
            "src",
            "FluxMq.UI",
            "Components",
            "Workspace",
            "Nodes",
            "Sources",
            "ReplaySourceNodeWidget.razor.css"));

        markup.ShouldContain("EditDialogMaxWidth=\"MaxWidth.Medium\"");
        markup.ShouldContain("ShowHeaderIcon=\"false\"");
        markup.ShouldContain("ShowDisplayName=\"true\"");
        markup.ShouldContain("ShowCategoryToken=\"false\"");
        markup.ShouldContain("@inject NodeEditDialogRefreshService DialogRefresh");
        markup.ShouldContain("EditorValidationError=\"@ValidateEditor\"");
        markup.ShouldContain("private string? ValidateEditor()");
        markup.ShouldContain("Select or enter a replay session before saving.");
        markup.ShouldContain("Playback speed must be between 0.1 and 1000.");
        markup.ShouldContain("Output buffer must be between 1 and 100000.");
        markup.ShouldContain("replay-source-summary");
        markup.ShouldContain("replay-source-meta");
        markup.ShouldContain("SessionCaption");
        markup.ShouldContain("SpeedCaption");
        markup.ShouldContain("BufferCaption");
        markup.ShouldNotContain("replay-source-contract");
        markup.ShouldNotContain("aria-label=\"Replay source output fields\"");
        markup.ShouldNotContain("replay-source-token");
        markup.ShouldNotContain("<span class=\"replay-source-token node-ui-token\">MqttEnvelope</span>");
        markup.ShouldNotContain("<span class=\"replay-source-token node-ui-token\">topic</span>");
        markup.ShouldNotContain("<span class=\"replay-source-token node-ui-token\">payload</span>");
        markup.ShouldNotContain("<span class=\"replay-source-token node-ui-token\">qos</span>");
        markup.ShouldNotContain("<span class=\"replay-source-token node-ui-token\">receivedAt</span>");
        markup.ShouldContain("replay-source-editor");
        markup.ShouldContain("aria-label=\"Replay source settings\"");
        markup.ShouldNotContain("replay-source-editor-surface");
        markup.ShouldContain("replay-source-editor-grid");
        markup.ShouldContain("aria-label=\"Replay source configuration fields\"");
        markup.ShouldNotContain("replay-source-field-label");
        markup.ShouldNotContain("replay-source-session-state");
        markup.ShouldNotContain("aria-label=\"Replay source configuration\"");
        markup.ShouldNotContain("SessionOptionCountCaption");
        markup.ShouldNotContain("replay-source-panel-header");
        markup.ShouldNotContain("replay-source-panel-kicker");
        markup.ShouldNotContain("replay-source-panel-token");
        markup.ShouldNotContain("replay-source-playback-panel");
        markup.ShouldNotContain("replay-source-session-row");
        markup.ShouldNotContain("replay-source-playback-row");
        markup.ShouldNotContain("replay-source-source-row");
        markup.ShouldNotContain("replay-source-config-row");
        markup.ShouldContain("Class=\"replay-source-session-field\"");
        markup.ShouldContain("replay-source-session-option");
        markup.ShouldContain("Label=\"Session\"");
        markup.ShouldContain("aria-label=\"Replay session\"");
        markup.ShouldContain("Value=\"@_sessionId\"");
        markup.ShouldContain("ValueChanged=\"@SetSessionId\"");
        markup.ShouldContain("Label=\"Session ID\"");
        markup.ShouldContain("aria-label=\"Replay session ID\"");
        markup.ShouldContain("Immediate=\"true\"");
        markup.ShouldNotContain("replay-source-main-grid");
        markup.ShouldNotContain("replay-source-playback-grid");
        markup.ShouldNotContain("replay-source-speed-cell");
        markup.ShouldNotContain("replay-source-field-note");
        markup.ShouldNotContain("1x is real time");
        markup.ShouldContain("Label=\"Playback speed\"");
        markup.ShouldContain("aria-label=\"Replay playback speed\"");
        markup.ShouldContain("Value=\"@_speed\"");
        markup.ShouldContain("ValueChanged=\"@SetSpeed\"");
        markup.ShouldContain("Label=\"Output buffer\"");
        markup.ShouldContain("aria-label=\"Replay output buffer\"");
        markup.ShouldContain("Value=\"@_boundedCapacity\"");
        markup.ShouldContain("ValueChanged=\"@SetBoundedCapacity\"");
        markup.ShouldContain("private async Task SetSessionId(string value)");
        markup.ShouldContain("private async Task SetSpeed(double value)");
        markup.ShouldContain("private async Task SetBoundedCapacity(int value)");
        markup.ShouldNotContain("@bind-Value=\"_sessionId\"");
        markup.ShouldNotContain("@bind-Value=\"_speed\"");
        markup.ShouldNotContain("@bind-Value=\"_boundedCapacity\"");
        markup.ShouldNotContain("replay-source-config-grid");
        markup.ShouldNotContain("replay-source-number-grid");
        markup.ShouldNotContain("<Stat ");
        markup.ShouldNotContain("<MudStack");
        markup.ShouldNotContain("<MudChip");
        markup.ShouldNotContain("<MudGrid");
        markup.ShouldNotContain("d-flex flex-wrap gap-1");

        css.ShouldContain(".replay-source-summary");
        css.ShouldContain(".replay-source-meta");
        css.ShouldContain("grid-template-columns: minmax(0, 1fr) minmax(0, 1fr);");
        css.ShouldContain(".replay-source-meta-item.session");
        css.ShouldContain("grid-column: 1 / -1;");
        css.ShouldNotContain("grid-template-columns: minmax(0, 1.3fr) minmax(0, 0.56fr) minmax(0, 0.56fr);");
        css.ShouldNotContain("grid-template-columns: minmax(0, 1fr) 70px 70px;");
        css.ShouldContain("-webkit-line-clamp: 2;");
        css.ShouldNotContain(".replay-source-contract");
        css.ShouldNotContain(".replay-source-contract-label");
        css.ShouldNotContain(".replay-source-token");
        css.ShouldNotContain("grid-template-columns: repeat(5, minmax(0, auto));");
        css.ShouldContain("white-space: normal;");
        css.ShouldContain("overflow-wrap: anywhere;");
        css.ShouldContain(".replay-source-editor");
        css.ShouldContain("gap: 7px;");
        css.ShouldNotContain("gap: 8px;");
        css.ShouldNotContain(".replay-source-editor-surface");
        css.ShouldContain(".replay-source-editor-grid");
        css.ShouldNotContain(".replay-source-field-label");
        css.ShouldNotContain(".replay-source-session-state");
        css.ShouldNotContain("background: color-mix(in srgb, var(--flux-surface-2) 4%, transparent);");
        css.ShouldNotContain("border: 1px solid color-mix(in srgb, var(--flux-border-soft) 42%, transparent);");
        css.ShouldNotContain("padding: 12px;");
        css.ShouldNotContain("padding: 14px;");
        css.ShouldNotContain(".replay-source-panel-header");
        css.ShouldNotContain(".replay-source-panel-kicker");
        css.ShouldNotContain(".replay-source-panel-token");
        css.ShouldNotContain(".replay-source-playback-panel");
        css.ShouldNotContain(".replay-source-session-row");
        css.ShouldNotContain(".replay-source-playback-row");
        css.ShouldNotContain(".replay-source-source-row");
        css.ShouldNotContain(".replay-source-config-row");
        css.ShouldContain("grid-template-columns: minmax(0, 1fr) minmax(0, 1fr);");
        css.ShouldContain(".replay-source-session-field");
        css.ShouldContain("grid-column: 1 / -1;");
        css.ShouldNotContain("grid-template-columns: minmax(0, 1.24fr) minmax(0, 0.58fr) minmax(0, 0.58fr);");
        css.ShouldNotContain("grid-template-columns: minmax(0, 1fr) minmax(120px, 154px) minmax(120px, 154px);");
        css.ShouldNotContain("grid-template-columns: minmax(0, 1fr) 132px minmax(120px, 154px) minmax(120px, 154px);");
        css.ShouldNotContain(".replay-source-config-grid");
        css.ShouldNotContain(".replay-source-main-grid");
        css.ShouldNotContain(".replay-source-playback-grid");
        css.ShouldNotContain(".replay-source-speed-cell");
        css.ShouldNotContain(".replay-source-field-note");
        css.ShouldContain(".replay-source-session-option");
        css.ShouldContain("grid-template-columns: minmax(0, 1fr) minmax(2rem, max-content);");
        css.ShouldNotContain("grid-template-columns: minmax(0, 1fr) auto;");
        css.ShouldNotContain("text-overflow: ellipsis;");
        css.ShouldContain("overflow-wrap: anywhere;");
        css.ShouldContain("white-space: normal;");
        css.ShouldContain(".replay-source-editor ::deep(.mud-input-control)");
        css.ShouldContain("min-height: 34px;");
        css.ShouldNotContain("min-height: 36px;");
        css.ShouldContain(".replay-source-editor ::deep(.mud-input-label)");
        css.ShouldContain("font-size: 0.72rem;");
        css.ShouldContain("@media (max-width: 640px)");
        css.ShouldNotContain(".replay-source-number-grid");
        css.ShouldNotContain(".flow-node-filters");
        css.ShouldNotContain("border-radius: 999px;");
    }

    [Fact]
    public void StoredSessionSourceNodeWidget_UsesCompactSummaryAndFlatEditor()
    {
        var root = FindRepositoryRoot();
        var markup = File.ReadAllText(Path.Combine(
            root,
            "src",
            "FluxMq.UI",
            "Components",
            "Workspace",
            "Nodes",
            "SessionSource",
            "StoredSessionSourceNodeWidget.razor"));
        var css = File.ReadAllText(Path.Combine(
            root,
            "src",
            "FluxMq.UI",
            "Components",
            "Workspace",
            "Nodes",
            "SessionSource",
            "StoredSessionSourceNodeWidget.razor.css"));

        markup.ShouldContain("EditDialogMaxWidth=\"MaxWidth.Medium\"");
        markup.ShouldContain("ShowHeaderIcon=\"false\"");
        markup.ShouldContain("ShowDisplayName=\"true\"");
        markup.ShouldContain("ShowCategoryToken=\"false\"");
        markup.ShouldContain("@inject NodeEditDialogRefreshService DialogRefresh");
        markup.ShouldContain("EditorValidationError=\"@ValidateEditor\"");
        markup.ShouldContain("private string? ValidateEditor()");
        markup.ShouldContain("Select a stored session before saving.");
        markup.ShouldContain("Playback speed must be between 0.1 and 100.");
        markup.ShouldContain("Output buffer must be between 1 and 100000.");
        markup.ShouldContain("stored-session-source-summary");
        markup.ShouldContain("stored-session-source-meta");
        markup.ShouldContain("SessionCaption");
        markup.ShouldContain("TimingCaption");
        markup.ShouldContain("SpeedCaption");
        markup.ShouldContain("BufferCaption");
        markup.ShouldNotContain("stored-session-source-contract");
        markup.ShouldNotContain("aria-label=\"Stored session source output fields\"");
        markup.ShouldNotContain("stored-session-source-token");
        markup.ShouldNotContain("<span class=\"stored-session-source-token node-ui-token\">MqttEnvelope</span>");
        markup.ShouldNotContain("<span class=\"stored-session-source-token node-ui-token\">topic</span>");
        markup.ShouldNotContain("<span class=\"stored-session-source-token node-ui-token\">payload</span>");
        markup.ShouldNotContain("<span class=\"stored-session-source-token node-ui-token\">qos</span>");
        markup.ShouldNotContain("<span class=\"stored-session-source-token node-ui-token\">receivedAt</span>");
        markup.ShouldContain("stored-session-source-editor");
        markup.ShouldContain("aria-label=\"Stored session source settings\"");
        markup.ShouldNotContain("stored-session-source-editor-surface");
        markup.ShouldContain("stored-session-source-editor-grid");
        markup.ShouldNotContain("stored-session-source-field-label");
        markup.ShouldNotContain("stored-session-source-session-state");
        markup.ShouldNotContain("aria-label=\"Stored session source configuration\"");
        markup.ShouldContain("aria-label=\"Stored session source configuration fields\"");
        markup.ShouldNotContain("aria-label=\"Stored sessions\"");
        markup.ShouldNotContain("SessionOptionCountCaption");
        markup.ShouldNotContain("stored-session-source-playback-workspace");
        markup.ShouldNotContain("stored-session-source-panel-header");
        markup.ShouldNotContain("stored-session-source-panel-kicker");
        markup.ShouldNotContain("stored-session-source-panel-token");
        markup.ShouldNotContain("stored-session-source-playback-panel");
        markup.ShouldNotContain("stored-session-source-source-row");
        markup.ShouldNotContain("stored-session-source-config-row");
        markup.ShouldNotContain("stored-session-source-session-row");
        markup.ShouldNotContain("stored-session-source-playback-row");
        markup.ShouldContain("Class=\"stored-session-source-session-field\"");
        markup.ShouldContain("stored-session-source-session-option");
        markup.ShouldContain("stored-session-source-empty");
        markup.ShouldContain("role=\"status\"");
        markup.ShouldContain("Label=\"Session\"");
        markup.ShouldContain("aria-label=\"Stored session\"");
        markup.ShouldContain("Value=\"@_sessionId\"");
        markup.ShouldContain("ValueChanged=\"@SetSessionId\"");
        markup.ShouldContain("Label=\"Preserve timing\"");
        markup.ShouldContain("aria-label=\"Preserve original session timing\"");
        markup.ShouldContain("Value=\"@_preserveTiming\"");
        markup.ShouldContain("ValueChanged=\"@SetPreserveTiming\"");
        markup.ShouldNotContain("stored-session-source-timing-row");
        markup.ShouldContain("Label=\"Playback speed\"");
        markup.ShouldContain("aria-label=\"Stored session playback speed\"");
        markup.ShouldContain("Value=\"@_speed\"");
        markup.ShouldContain("ValueChanged=\"@SetSpeed\"");
        markup.ShouldContain("Disabled=\"@(!_preserveTiming)\"");
        markup.ShouldNotContain("stored-session-source-main-grid");
        markup.ShouldNotContain("stored-session-source-timing-grid");
        markup.ShouldNotContain("stored-session-source-speed-cell");
        markup.ShouldNotContain("stored-session-source-field-note");
        markup.ShouldNotContain("1x is real time");
        markup.ShouldNotContain("HelperText=\"1 = real-time\"");
        markup.ShouldContain("Label=\"Output buffer\"");
        markup.ShouldContain("aria-label=\"Stored session output buffer\"");
        markup.ShouldContain("Value=\"@_boundedCapacity\"");
        markup.ShouldContain("ValueChanged=\"@SetBoundedCapacity\"");
        markup.ShouldContain("private async Task SetSessionId(string value)");
        markup.ShouldContain("private async Task SetPreserveTiming(bool value)");
        markup.ShouldContain("private async Task SetSpeed(double value)");
        markup.ShouldContain("private async Task SetBoundedCapacity(int value)");
        markup.ShouldNotContain("@bind-Value=\"_sessionId\"");
        markup.ShouldNotContain("@bind-Value=\"_preserveTiming\"");
        markup.ShouldNotContain("@bind-Value=\"_speed\"");
        markup.ShouldNotContain("@bind-Value=\"_boundedCapacity\"");
        markup.ShouldNotContain("stored-session-source-config-grid");
        markup.ShouldNotContain("<Stat ");
        markup.ShouldNotContain("<MudStack");
        markup.ShouldNotContain("<MudChip");
        markup.ShouldNotContain("<MudGrid");
        markup.ShouldNotContain("<MudAlert");
        markup.ShouldNotContain("d-flex flex-wrap gap-1");

        css.ShouldContain(".stored-session-source-summary");
        css.ShouldContain(".stored-session-source-meta");
        css.ShouldContain("grid-template-columns: repeat(3, minmax(0, 1fr));");
        css.ShouldContain(".stored-session-source-meta-item.session");
        css.ShouldContain("grid-column: 1 / -1;");
        css.ShouldNotContain("grid-template-columns: minmax(0, 1.28fr) minmax(0, 0.62fr) minmax(0, 0.52fr) minmax(0, 0.52fr);");
        css.ShouldNotContain("grid-template-columns: minmax(0, 1fr) 76px 64px 64px;");
        css.ShouldContain("-webkit-line-clamp: 2;");
        css.ShouldNotContain(".stored-session-source-contract");
        css.ShouldNotContain(".stored-session-source-contract-label");
        css.ShouldNotContain(".stored-session-source-token");
        css.ShouldNotContain("grid-template-columns: repeat(5, minmax(0, auto));");
        css.ShouldContain("white-space: normal;");
        css.ShouldContain("overflow-wrap: anywhere;");
        css.ShouldContain(".stored-session-source-editor");
        css.ShouldContain("gap: 7px;");
        css.ShouldNotContain("gap: 8px;");
        css.ShouldNotContain(".stored-session-source-editor-surface");
        css.ShouldContain(".stored-session-source-editor-grid");
        css.ShouldNotContain(".stored-session-source-field-label");
        css.ShouldNotContain(".stored-session-source-session-state");
        css.ShouldNotContain(".stored-session-source-playback-workspace");
        css.ShouldNotContain("background: color-mix(in srgb, var(--flux-surface-2) 4%, transparent);");
        css.ShouldNotContain("border: 1px solid color-mix(in srgb, var(--flux-border-soft) 42%, transparent);");
        css.ShouldNotContain("padding: 12px;");
        css.ShouldNotContain(".stored-session-source-panel-header");
        css.ShouldNotContain(".stored-session-source-panel-kicker");
        css.ShouldNotContain(".stored-session-source-panel-token");
        css.ShouldNotContain(".stored-session-source-playback-panel");
        css.ShouldNotContain(".stored-session-source-source-row");
        css.ShouldNotContain(".stored-session-source-config-row");
        css.ShouldContain("grid-template-columns: repeat(3, minmax(0, 1fr));");
        css.ShouldContain(".stored-session-source-session-field");
        css.ShouldContain("grid-column: 1 / -1;");
        css.ShouldNotContain("grid-template-columns: minmax(0, 1.24fr) minmax(0, 0.66fr) minmax(0, 0.58fr) minmax(0, 0.58fr);");
        css.ShouldNotContain("grid-template-columns: minmax(0, 1fr) 142px minmax(120px, 148px) minmax(120px, 148px);");
        css.ShouldNotContain(".stored-session-source-session-row");
        css.ShouldNotContain(".stored-session-source-timing-row");
        css.ShouldNotContain(".stored-session-source-playback-row");
        css.ShouldNotContain(".stored-session-source-config-grid");
        css.ShouldContain(".stored-session-source-timing-option");
        css.ShouldContain("align-self: end;");
        css.ShouldContain("justify-content: flex-start;");
        css.ShouldContain("min-height: 34px;");
        css.ShouldNotContain("min-height: 36px;");
        css.ShouldContain(".stored-session-source-timing-option ::deep(.mud-checkbox)");
        css.ShouldContain("display: inline-flex;");
        css.ShouldContain(".stored-session-source-timing-option ::deep(.mud-checkbox .mud-button-root)");
        css.ShouldContain("padding: 4px;");
        css.ShouldNotContain(".stored-session-source-main-grid");
        css.ShouldNotContain(".stored-session-source-timing-grid");
        css.ShouldNotContain(".stored-session-source-speed-cell");
        css.ShouldNotContain(".stored-session-source-field-note");
        css.ShouldContain(".stored-session-source-session-option");
        css.ShouldContain("grid-template-columns: minmax(0, 1fr) minmax(2rem, max-content);");
        css.ShouldNotContain("grid-template-columns: minmax(0, 1fr) auto;");
        css.ShouldContain(".stored-session-source-empty");
        css.ShouldNotContain("text-overflow: ellipsis;");
        css.ShouldContain("overflow-wrap: anywhere;");
        css.ShouldContain("white-space: normal;");
        css.ShouldContain(".stored-session-source-editor ::deep(.mud-input-control)");
        css.ShouldContain(".stored-session-source-editor ::deep(.mud-input-label)");
        css.ShouldContain("font-size: 0.72rem;");
        css.ShouldContain("@media (max-width: 720px)");
        css.ShouldNotContain(".flow-node-filters");
        css.ShouldNotContain("border-radius: 999px;");
    }

    [Fact]
    public void TimerNodeWidget_UsesCompactSummaryAndFlatEditor()
    {
        var root = FindRepositoryRoot();
        var markup = File.ReadAllText(Path.Combine(
            root,
            "src",
            "FluxMq.UI",
            "Components",
            "Workspace",
            "Nodes",
            "Timers",
            "TimerNodeWidget.razor"));
        var css = File.ReadAllText(Path.Combine(
            root,
            "src",
            "FluxMq.UI",
            "Components",
            "Workspace",
            "Nodes",
            "Timers",
            "TimerNodeWidget.razor.css"));

        markup.ShouldContain("EditDialogMaxWidth=\"MaxWidth.Medium\"");
        markup.ShouldContain("CategoryColor=\"@TimerCategoryColor\"");
        markup.ShouldContain("TimerCategoryColor");
        markup.ShouldContain("Color.Secondary");
        markup.ShouldContain("Color.Primary");
        markup.ShouldNotContain("TimerTokenClass");
        markup.ShouldNotContain("CategoryColor=\"@Color.Warning\"");
        markup.ShouldContain("ShowHeaderIcon=\"false\"");
        markup.ShouldContain("ShowDisplayName=\"true\"");
        markup.ShouldContain("ShowCategoryToken=\"false\"");
        markup.ShouldContain("@inject NodeEditDialogRefreshService DialogRefresh");
        markup.ShouldContain("EditorValidationError=\"@ValidateEditor\"");
        markup.ShouldContain("timer-node-summary");
        markup.ShouldContain("timer-node-meta");
        markup.ShouldContain("ModeCaption");
        markup.ShouldContain("PrimaryCaption");
        markup.ShouldContain("SecondaryCaption");
        markup.ShouldContain("BoundedCapacityCaption");
        markup.ShouldNotContain("timer-node-contract");
        markup.ShouldNotContain("ContractAriaLabel");
        markup.ShouldNotContain("timer-node-token");
        markup.ShouldNotContain("class=\"@TimerTokenClass\"");
        markup.ShouldNotContain("TimerTick");
        markup.ShouldNotContain("ScheduleTick");
        markup.ShouldContain("timer-node-editor");
        markup.ShouldContain("aria-label=\"Timer settings\"");
        markup.ShouldContain("timer-node-mode interval");
        markup.ShouldContain("timer-node-mode schedule");
        markup.ShouldContain("timer-node-mode passthrough");
        markup.ShouldNotContain("timer-node-editor-surface");
        markup.ShouldContain("timer-node-primary-row interval");
        markup.ShouldContain("aria-label=\"Interval timer settings\"");
        markup.ShouldContain("timer-node-settings-row interval");
        markup.ShouldContain("aria-label=\"Interval timer limits\"");
        markup.ShouldContain("timer-node-primary-row schedule");
        markup.ShouldContain("aria-label=\"Scheduled timer settings\"");
        markup.ShouldContain("timer-node-settings-row schedule");
        markup.ShouldContain("aria-label=\"Scheduled timer limits\"");
        markup.ShouldContain("timer-node-primary-row passthrough");
        markup.ShouldContain("aria-label=\"Delay timer settings\"");
        markup.ShouldContain("timer-node-settings-row passthrough");
        markup.ShouldContain("aria-label=\"Delay timer buffer\"");
        markup.ShouldContain("aria-label=\"Debounce timer settings\"");
        markup.ShouldContain("aria-label=\"Debounce timer buffer\"");
        markup.ShouldContain("timer-node-primary-row passthrough throttle");
        markup.ShouldContain("aria-label=\"Throttle timer settings\"");
        markup.ShouldContain("timer-node-settings-row option");
        markup.ShouldContain("aria-label=\"Throttle timer options\"");
        markup.ShouldContain("timer-node-option-cell");
        markup.ShouldContain("Label=\"Interval ms\"");
        markup.ShouldContain("Value=\"@_intervalMilliseconds\"");
        markup.ShouldContain("ValueChanged=\"@SetIntervalMilliseconds\"");
        markup.ShouldContain("Label=\"Initial delay ms\"");
        markup.ShouldContain("Value=\"@_initialDelayMilliseconds\"");
        markup.ShouldContain("ValueChanged=\"@SetInitialDelayMilliseconds\"");
        markup.ShouldContain("Value=\"@_maxTicks\"");
        markup.ShouldContain("ValueChanged=\"@SetMaxTicks\"");
        markup.ShouldContain("Label=\"Cron\"");
        markup.ShouldContain("Value=\"@_cron\"");
        markup.ShouldContain("ValueChanged=\"@SetCron\"");
        markup.ShouldContain("Label=\"Time zone\"");
        markup.ShouldContain("Value=\"@_timeZoneId\"");
        markup.ShouldContain("ValueChanged=\"@SetTimeZoneId\"");
        markup.ShouldContain("Label=\"Input type\"");
        markup.ShouldContain("@bind-Value=\"_inputType\"");
        markup.ShouldContain("Label=\"Delay ms\"");
        markup.ShouldContain("Value=\"@_delayMilliseconds\"");
        markup.ShouldContain("ValueChanged=\"@SetDelayMilliseconds\"");
        markup.ShouldContain("Label=\"Quiet period ms\"");
        markup.ShouldContain("Value=\"@_quietPeriodMilliseconds\"");
        markup.ShouldContain("ValueChanged=\"@SetQuietPeriodMilliseconds\"");
        markup.ShouldContain("Value=\"@_throttleIntervalMilliseconds\"");
        markup.ShouldContain("ValueChanged=\"@SetThrottleIntervalMilliseconds\"");
        markup.ShouldContain("Label=\"Output buffer\"");
        markup.ShouldContain("Label=\"Input buffer\"");
        markup.ShouldContain("Value=\"@_boundedCapacity\"");
        markup.ShouldContain("ValueChanged=\"@SetBoundedCapacity\"");
        markup.ShouldContain("Label=\"Emit immediately\"");
        markup.ShouldContain("@bind-Value=\"_emitImmediately\"");
        markup.ShouldContain("Label=\"Emit first immediately\"");
        markup.ShouldContain("@bind-Value=\"_emitFirstImmediately\"");
        markup.ShouldContain("InputTypeSelect()");
        markup.ShouldContain("OutputBufferField()");
        markup.ShouldContain("InputBufferField()");
        markup.ShouldContain("private string? ValidateEditor()");
        markup.ShouldContain("private string? ValidateIntervalTimer()");
        markup.ShouldContain("private string? ValidateScheduledTimer()");
        markup.ShouldContain("private string? ValidateDelayTimer()");
        markup.ShouldContain("private string? ValidateDebounceTimer()");
        markup.ShouldContain("private string? ValidateThrottleTimer()");
        markup.ShouldContain("Interval must be greater than 0 ms.");
        markup.ShouldContain("Initial delay cannot be negative.");
        markup.ShouldContain("Max ticks cannot be negative.");
        markup.ShouldContain("Enter a cron expression before saving.");
        markup.ShouldContain("Enter a time zone before saving.");
        markup.ShouldContain("Delay cannot be negative.");
        markup.ShouldContain("Quiet period must be greater than 0 ms.");
        markup.ShouldContain("Throttle interval must be greater than 0 ms.");
        markup.ShouldContain("Choose an input type before saving.");
        markup.ShouldContain("Buffer must be greater than 0.");
        markup.ShouldNotContain("@bind-Value=\"_intervalMilliseconds\"");
        markup.ShouldNotContain("@bind-Value=\"_initialDelayMilliseconds\"");
        markup.ShouldNotContain("@bind-Value=\"_maxTicks\"");
        markup.ShouldNotContain("@bind-Value=\"_cron\"");
        markup.ShouldNotContain("@bind-Value=\"_timeZoneId\"");
        markup.ShouldNotContain("@bind-Value=\"_delayMilliseconds\"");
        markup.ShouldNotContain("@bind-Value=\"_quietPeriodMilliseconds\"");
        markup.ShouldNotContain("@bind-Value=\"_throttleIntervalMilliseconds\"");
        markup.ShouldNotContain("@bind-Value=\"_boundedCapacity\"");
        markup.ShouldNotContain("private RenderFragment BufferField()");
        markup.ShouldNotContain("HelperText=\"0 = unlimited\"");
        markup.ShouldNotContain("<span>Emission</span>");
        markup.ShouldNotContain("timer-node-config-row");
        markup.ShouldNotContain("timer-node-secondary-row");
        markup.ShouldNotContain("timer-node-config-grid");
        markup.ShouldNotContain("timer-node-limit-workspace");
        markup.ShouldNotContain("timer-node-option-workspace");
        markup.ShouldNotContain("timer-node-limit-panel");
        markup.ShouldNotContain("timer-node-option-panel");
        markup.ShouldNotContain("timer-node-check-row");
        markup.ShouldNotContain("timer-node-config-section");
        markup.ShouldNotContain("timer-node-timing-grid");
        markup.ShouldNotContain("timer-node-passthrough-grid");
        markup.ShouldNotContain("timer-node-limit-row");
        markup.ShouldNotContain("timer-node-option-row");
        markup.ShouldNotContain("timer-node-panel-header");
        markup.ShouldNotContain("timer-node-panel-kicker");
        markup.ShouldNotContain("timer-node-panel-token");
        markup.ShouldNotContain("<Stat ");
        markup.ShouldNotContain("<MudStack");
        markup.ShouldNotContain("<MudChip");
        markup.ShouldNotContain("<MudGrid");
        markup.ShouldNotContain("d-flex flex-wrap gap-1");

        css.ShouldContain(".timer-node-summary");
        css.ShouldContain(".timer-node-meta");
        css.ShouldContain("grid-template-columns: minmax(0, 1fr) minmax(0, 1fr);");
        css.ShouldContain("-webkit-line-clamp: 2;");
        css.ShouldContain("overflow-wrap: anywhere;");
        css.ShouldContain("white-space: normal;");
        css.ShouldNotContain("grid-template-columns: minmax(0, 0.72fr) minmax(0, 1.08fr) minmax(0, 0.9fr) minmax(0, 0.62fr);");
        css.ShouldNotContain("grid-template-columns: 72px minmax(0, 1fr) minmax(0, 0.82fr) 64px;");
        css.ShouldNotContain(".timer-node-contract");
        css.ShouldNotContain(".timer-node-contract-label");
        css.ShouldNotContain(".timer-node-token");
        css.ShouldNotContain("grid-template-columns: repeat(4, minmax(0, auto));");
        css.ShouldNotContain("grid-column: 1 / -1;");
        css.ShouldNotContain(".timer-node-token.passthrough");
        css.ShouldNotContain("color-mix(in srgb, var(--mud-palette-warning) 76%, var(--mud-palette-text-primary));");
        css.ShouldContain("overflow-wrap: anywhere;");
        css.ShouldContain("white-space: normal;");
        css.ShouldContain(".timer-node-editor");
        css.ShouldContain(".timer-node-mode");
        css.ShouldNotContain("gap: 10px;");
        css.ShouldContain("gap: 7px;");
        css.ShouldNotContain("gap: 8px;");
        css.ShouldNotContain(".timer-node-editor-surface");
        css.ShouldContain(".timer-node-primary-row");
        css.ShouldContain(".timer-node-settings-row");
        css.ShouldNotContain(".timer-node-config-row");
        css.ShouldNotContain(".timer-node-secondary-row");
        css.ShouldNotContain(".timer-node-limit-workspace");
        css.ShouldNotContain(".timer-node-option-workspace");
        css.ShouldContain("grid-template-columns: minmax(0, 1fr) minmax(0, 1fr);");
        css.ShouldContain("grid-template-columns: repeat(3, minmax(0, 1fr));");
        css.ShouldNotContain("grid-template-columns: minmax(0, 1fr) minmax(0, 1fr) minmax(170px, 1fr);");
        css.ShouldContain("grid-template-columns: minmax(0, 1fr);");
        css.ShouldContain("grid-template-columns: minmax(0, 1fr) minmax(0, 0.9fr);");
        css.ShouldNotContain("grid-template-columns: minmax(0, 1fr) minmax(180px, 1fr);");
        css.ShouldNotContain("grid-template-columns: minmax(118px, 150px) minmax(130px, 170px) minmax(170px, 1fr);");
        css.ShouldNotContain("grid-template-columns: minmax(118px, 150px) minmax(130px, 170px);");
        css.ShouldNotContain("grid-template-columns: minmax(130px, 170px) minmax(180px, 1fr);");
        css.ShouldNotContain("background: color-mix(in srgb, var(--flux-surface-2) 4%, transparent);");
        css.ShouldNotContain("border: 1px solid color-mix(in srgb, var(--flux-border-soft) 42%, transparent);");
        css.ShouldContain("border-top: 1px solid color-mix(in srgb, var(--flux-border-soft) 58%, transparent);");
        css.ShouldNotContain("padding: 12px;");
        css.ShouldContain("padding-top: 7px;");
        css.ShouldNotContain("padding-top: 8px;");
        css.ShouldNotContain("padding: 14px;");
        css.ShouldContain(".timer-node-option-cell");
        css.ShouldContain("align-items: center;");
        css.ShouldContain("align-self: center;");
        css.ShouldNotContain("align-self: end;");
        css.ShouldContain("justify-content: flex-start;");
        css.ShouldContain("min-height: 34px;");
        css.ShouldContain(".timer-node-option-cell ::deep(.mud-checkbox)");
        css.ShouldContain("display: inline-flex;");
        css.ShouldContain(".timer-node-option-cell ::deep(.mud-checkbox .mud-button-root)");
        css.ShouldContain("padding: 4px;");
        css.ShouldNotContain("min-height: 36px;");
        css.ShouldNotContain("min-height: 40px;");
        css.ShouldContain(".timer-node-editor ::deep(.mud-input-control)");
        css.ShouldContain("@media (max-width: 760px)");
        css.ShouldNotContain(".timer-node-config-grid");
        css.ShouldNotContain(".timer-node-limit-panel");
        css.ShouldNotContain(".timer-node-option-panel");
        css.ShouldNotContain(".timer-node-option-cell > span");
        css.ShouldNotContain(".timer-node-check-row");
        css.ShouldNotContain(".timer-node-config-section");
        css.ShouldNotContain(".timer-node-timing-grid");
        css.ShouldNotContain(".timer-node-passthrough-grid");
        css.ShouldNotContain(".timer-node-limit-row");
        css.ShouldNotContain(".timer-node-option-row");
        css.ShouldNotContain(".timer-node-panel-header");
        css.ShouldNotContain(".timer-node-panel-kicker");
        css.ShouldNotContain(".timer-node-panel-token");
        css.ShouldNotContain(".flow-node-filters");
        css.ShouldNotContain("border-radius: 999px;");
    }

    [Fact]
    public void NodeWidgets_PreserveEditorDraftsAcrossParentRenders()
    {
        var root = FindRepositoryRoot();
        var nodeDirectory = Path.Combine(
            root,
            "src",
            "FluxMq.UI",
            "Components",
            "Workspace",
            "Nodes");
        var nodeWidgets = Directory.GetFiles(nodeDirectory, "*NodeWidget.razor", SearchOption.AllDirectories);

        foreach (var file in nodeWidgets)
        {
            var markup = File.ReadAllText(file);
            if (!markup.Contains("OnCancel=\"@LoadDraft\"", StringComparison.Ordinal) ||
                !markup.Contains("protected override void OnParametersSet()", StringComparison.Ordinal))
            {
                continue;
            }

            markup.ShouldNotContain("protected override void OnParametersSet() => LoadDraft();");
            markup.ShouldContain("private string? _loadedNodeName;");
            markup.ShouldContain("private FlowDiagramNodeModel? _loadedNode;");
            markup.ShouldContain("ReferenceEquals(_loadedNode, Node)");
            markup.ShouldContain("_loadedNode = Node;");
            markup.ShouldContain("_loadedNodeName = Node.NodeName;");
        }
    }

    [Fact]
    public void NodeWidgetShell_UsesCompactNodeChrome()
    {
        var root = FindRepositoryRoot();
        var shellMarkup = File.ReadAllText(Path.Combine(
            root,
            "src",
            "FluxMq.UI",
            "Components",
            "Diagram",
            "NodeWidgetShell.razor"));
        var css = File.ReadAllText(Path.Combine(
            root,
            "src",
            "FluxMq.UI",
            "Components",
            "Workspace",
            "FlowDesigner.razor.css"));

        shellMarkup.ShouldContain("flow-node-action flow-node-toggle");
        shellMarkup.ShouldContain("aria-label=\"@NodeToggleLabel\"");
        shellMarkup.ShouldContain("private string NodeToggleLabel => Node.IsCollapsed ? \"Expand node\" : \"Collapse node\";");
        shellMarkup.ShouldContain("ShowHeaderIcon");
        shellMarkup.ShouldContain("ShowDisplayName");
        shellMarkup.ShouldContain("ShowCategoryToken");
        shellMarkup.ShouldContain("HeaderState");
        shellMarkup.ShouldNotContain("HeaderBadge");
        shellMarkup.ShouldContain("EditDialogContentClass");
        shellMarkup.ShouldContain("EditorValidationError");
        shellMarkup.ShouldNotContain("nameof(NodeEditDialog.CategoryColor)");
        shellMarkup.ShouldContain("flow-node-type-icon");
        shellMarkup.ShouldContain("Class=\"flow-node-type-icon\" aria-hidden=\"true\"");
        shellMarkup.ShouldContain("flow-node-name");
        shellMarkup.ShouldContain("flow-node-display-name");
        shellMarkup.ShouldNotContain("Color=\"Color.Secondary\" Class=\"flow-node-display-name\"");
        shellMarkup.ShouldContain("flow-node-action flow-node-edit");
        shellMarkup.ShouldContain("aria-label=\"Edit node\"");
        shellMarkup.ShouldContain("Icons.Material.Filled.Settings");
        shellMarkup.ShouldContain("role=\"img\"");
        shellMarkup.ShouldContain("aria-label=\"@DiagnosticAccessibilityLabel(diagnostic)\"");
        shellMarkup.ShouldContain("private static string DiagnosticAccessibilityLabel(WorkspaceDiagnostic diagnostic)");
        System.Text.RegularExpressions.Regex.Matches(
                shellMarkup,
                @"<MudIconButton\b(?:(?!/>).)*?/>",
                System.Text.RegularExpressions.RegexOptions.Singleline)
            .Cast<System.Text.RegularExpressions.Match>()
            .Select(static match => match.Value)
            .Where(static iconButton => !iconButton.Contains("aria-label=", StringComparison.Ordinal) &&
                !iconButton.Contains("AriaLabel=", StringComparison.Ordinal))
            .ToArray()
            .ShouldBeEmpty();
        System.Text.RegularExpressions.Regex.Matches(
                shellMarkup,
                @"<MudIcon\b(?:(?!/>).)*?/>",
                System.Text.RegularExpressions.RegexOptions.Singleline)
            .Cast<System.Text.RegularExpressions.Match>()
            .Select(static match => match.Value)
            .Where(static icon => !icon.Contains("aria-hidden=\"true\"", StringComparison.Ordinal) &&
                !icon.Contains("aria-label=", StringComparison.Ordinal) &&
                !icon.Contains("AriaLabel=", StringComparison.Ordinal))
            .ToArray()
            .ShouldBeEmpty();
        shellMarkup.ShouldContain("flow-node-category-token");
        shellMarkup.ShouldContain("flow-node-divider");
        shellMarkup.ShouldContain("flow-node-activity");
        shellMarkup.ShouldContain("flow-node-activity-dot");
        shellMarkup.ShouldContain("role=\"status\"");
        shellMarkup.ShouldContain("aria-live=\"polite\"");
        shellMarkup.IndexOf("@Body", StringComparison.Ordinal).ShouldBeLessThan(
            shellMarkup.IndexOf("flow-node-activity", StringComparison.Ordinal));
        shellMarkup.ShouldContain("flow-node-collapsed-activity");
        shellMarkup.ShouldNotContain("Color=\"Color.Secondary\" Class=\"flow-node-collapsed-activity\"");
        shellMarkup.ShouldNotContain("flow-node-activity-icon");
        shellMarkup.ShouldNotContain("<MudChip");
        shellMarkup.ShouldNotContain("<MudAlert");
        shellMarkup.ShouldNotContain("<MudDivider");

        css.ShouldContain(".flow-designer-root ::deep .flow-node-action");
        css.ShouldContain(".flow-designer-root ::deep .flow-node :is(");
        css.ShouldContain(".node-ui-summary");
        css.ShouldContain(".node-ui-facts");
        css.ShouldContain(".node-ui-fact");
        css.ShouldContain(".node-ui-token-group");
        css.ShouldContain(".node-ui-token-row");
        css.ShouldContain(".node-ui-token");
        css.ShouldContain(".node-ui-preview");
        css.ShouldContain("flex: 0 0 24px;");
        css.ShouldContain(".flow-designer-root ::deep .flow-node-action:focus-visible");
        css.ShouldContain("outline-offset: 2px;");
        css.ShouldContain(".flow-designer-root ::deep .flow-node-category-token");
        css.ShouldContain("max-width: 74px;");
        css.ShouldContain(".flow-designer-root ::deep .flow-node-display-name");
        css.ShouldContain("color: color-mix(in srgb, var(--mud-palette-text-secondary) 72%, var(--mud-palette-text-disabled));");
        css.ShouldContain("font-weight: 620;");
        css.ShouldContain("opacity: 0.9;");
        css.ShouldContain("overflow-wrap: anywhere;");
        css.ShouldContain("white-space: normal;");
        css.ShouldContain(".flow-designer-root ::deep .flow-node-divider");
        css.ShouldContain("height: 1px;");
        css.ShouldContain(".flow-designer-root ::deep .flow-node-activity");
        css.ShouldContain("align-items: center;");
        css.ShouldContain("grid-template-columns: 7px minmax(0, 1fr);");
        css.ShouldContain("min-height: 18px;");
        css.ShouldContain("border-top: 1px solid color-mix(in srgb, var(--flux-border-soft) 54%, transparent);");
        css.ShouldContain(".flow-designer-root ::deep .flow-node-activity-dot");
        css.ShouldContain("color: color-mix(in srgb, var(--mud-palette-text-secondary) 76%, var(--mud-palette-text-disabled));");
        css.ShouldContain(".flow-designer-root ::deep .flow-port-name");
        css.ShouldContain("-webkit-line-clamp: 2;");
        css.ShouldNotContain("grid-template-columns: 16px minmax(0, 1fr);");
        css.ShouldNotContain(".flow-designer-root ::deep .flow-node-activity-icon");
        css.ShouldNotContain(".flow-node-category-chip");
        css.ShouldNotContain(".mud-alert-message");
        css.ShouldNotContain(".mud-alert-icon");
        css.ShouldNotContain(".node-stat");
        css.ShouldNotContain(".node-stat-icon");
    }

    [Fact]
    public void PipelineNodeWidgets_UseSharedNodeUiPrimitives()
    {
        var root = FindRepositoryRoot();
        var nodeDirectory = Path.Combine(
            root,
            "src",
            "FluxMq.UI",
            "Components",
            "Workspace",
            "Nodes");
        var nodeWidgets = Directory.GetFiles(nodeDirectory, "*NodeWidget.razor", SearchOption.AllDirectories);

        nodeWidgets.ShouldNotBeEmpty();

        foreach (var file in nodeWidgets)
        {
            var markup = File.ReadAllText(file);

            markup.ShouldNotContain("<MudChip");
            markup.ShouldNotContain("<Stat");
            markup.ShouldNotContain("flow-node-filters");
            markup.ShouldNotContain("d-flex flex-wrap gap-1");
            markup.ShouldNotContain("ShowHeaderIcon=\"true\"");
            markup.ShouldNotContain("ShowCategoryToken=\"true\"");

            if (markup.Contains("<Body>", StringComparison.Ordinal))
            {
                markup.Contains("node-ui-summary", StringComparison.Ordinal).ShouldBeTrue(file);
            }

            if (markup.Contains("<Editor>", StringComparison.Ordinal))
            {
                markup.Contains("node-ui-editor", StringComparison.Ordinal).ShouldBeTrue(file);
            }
        }
    }

    [Fact]
    public void WorkspaceStatusMessages_UseExplicitPoliteLiveSemantics()
    {
        var root = FindRepositoryRoot();
        var componentDirectory = Path.Combine(
            root,
            "src",
            "FluxMq.UI",
            "Components");
        var violations = Directory.GetFiles(componentDirectory, "*.razor", SearchOption.AllDirectories)
            .SelectMany(file =>
            {
                var markup = File.ReadAllText(file);
                return System.Text.RegularExpressions.Regex.Matches(
                        markup,
                        @"<[^>]*\brole\s*=\s*""status""[^>]*>",
                        System.Text.RegularExpressions.RegexOptions.Singleline)
                    .Cast<System.Text.RegularExpressions.Match>()
                    .Select(match => (File: file, Tag: match.Value))
                    .Where(static item => !item.Tag.Contains("aria-live=\"polite\"", StringComparison.Ordinal));
            })
            .Select(item => $"{Path.GetRelativePath(root, item.File)}: {item.Tag.Replace('\r', ' ').Replace('\n', ' ').Trim()}")
            .ToArray();

        violations.ShouldBeEmpty();
    }

    [Fact]
    public void FlowDesigner_UsesCompactDiagnosticSurface()
    {
        var root = FindRepositoryRoot();
        var markup = File.ReadAllText(Path.Combine(
            root,
            "src",
            "FluxMq.UI",
            "Components",
            "Workspace",
            "FlowDesigner.razor"));
        var css = File.ReadAllText(Path.Combine(
            root,
            "src",
            "FluxMq.UI",
            "Components",
            "Workspace",
            "FlowDesigner.razor.css"));

        markup.ShouldContain("[Parameter] public EventCallback<WorkspaceLogQuery> LogsRequested { get; set; }");
        markup.ShouldContain("class=\"flow-canvas-stat flow-canvas-stat-button @DiagnosticSeverityClass\"");
        markup.ShouldContain("aria-label=\"@DiagnosticLogsAriaLabel\"");
        markup.ShouldContain("@onclick=\"@OpenDiagnosticLogsAsync\"");
        markup.ShouldContain("DiagnosticDisplayCount");
        markup.ShouldContain("HasActionableDiagnostics");
        markup.ShouldContain("@if (ShowRuntimeState)");
        markup.ShouldContain("private bool ShowRuntimeState => Flow.State != RuntimeWorkspaceState.Faulted || ErrorCount == 0;");
        markup.ShouldContain("LogsRequested.InvokeAsync(new WorkspaceLogQuery(");
        markup.ShouldContain("Severity: severity");
        markup.ShouldContain("Search: DiagnosticLogSearch");
        markup.ShouldContain("private string? DiagnosticLogSearch");
        markup.ShouldContain("string.IsNullOrWhiteSpace(diagnostic.WorkflowName) ||");
        markup.ShouldContain("<div class=\"flow-link-condition-panel\">");
        markup.ShouldNotContain("flow-diagnostic-panel");
        markup.ShouldNotContain("ShowDiagnosticPanel");
        markup.ShouldNotContain("VisibleDiagnostics");
        markup.ShouldNotContain("AdditionalDiagnosticCount");

        css.ShouldContain(".flow-canvas-stat-button");
        css.ShouldContain("text-underline-offset: 2px;");
        css.ShouldContain(".flow-canvas-title h2");
        css.ShouldContain("overflow-wrap: anywhere;");
        css.ShouldContain(".flow-canvas-meta-strip");
        css.ShouldContain("flex-wrap: wrap;");
        css.ShouldContain(".flow-link-condition-title strong");
        css.ShouldContain(".flow-designer-root ::deep .diagram-link div.default-link-label");
        css.ShouldNotContain("text-overflow: ellipsis;");
        css.ShouldNotContain("white-space: nowrap;");
        css.ShouldNotContain("flex-wrap: nowrap;");
        css.ShouldContain(".flow-designer-root ::deep .flow-node-diagnostic-error::before");
        css.ShouldContain("width: 3px;");
        css.ShouldNotContain(".flow-diagnostic-panel");
        css.ShouldNotContain(".flow-diagnostic-row");
        css.ShouldNotContain(".flow-link-condition-panel.with-diagnostics");
    }

    [Fact]
    public void WorkspacePage_RoutesPipelineDiagnosticsToFilteredLogs()
    {
        var root = FindRepositoryRoot();
        var markup = File.ReadAllText(Path.Combine(
            root,
            "src",
            "FluxMq.UI",
            "Components",
            "Pages",
            "WorkspacePage.razor"));
        var css = File.ReadAllText(Path.Combine(
            root,
            "src",
            "FluxMq.UI",
            "Components",
            "Pages",
            "WorkspacePage.razor.css"));

        markup.ShouldContain("@onclick=\"@(() => SelectLogs())\"");
        markup.ShouldContain("<AppJsonPanel Project=\"@active\" />");
        markup.ShouldContain("class=\"project-tab project-tab-json-view @(_jsonView ? \"project-tab-active\" : \"\")\"");
        markup.ShouldContain("role=\"button\"");
        markup.ShouldContain("tabindex=\"0\"");
        markup.ShouldContain("aria-current=\"@WorkspaceTabCurrent(isActive)\"");
        markup.ShouldContain("aria-current=\"@WorkspaceTabCurrent(metricsActive)\"");
        markup.ShouldContain("aria-current=\"@WorkspaceTabCurrent(topicsActive)\"");
        markup.ShouldContain("aria-current=\"@WorkspaceTabCurrent(logsActive)\"");
        markup.ShouldContain("aria-current=\"@WorkspaceTabCurrent(_jsonView)\"");
        System.Text.RegularExpressions.Regex.Matches(
                markup,
                "class=\"workspace-empty\" role=\"status\" aria-live=\"polite\"")
            .Count.ShouldBe(2);
        markup.ShouldContain("No app open");
        markup.ShouldContain("No artifacts");
        System.Text.RegularExpressions.Regex.Matches(markup, "aria-keyshortcuts=\"Enter Space\"").Count.ShouldBe(7);
        markup.ShouldContain("aria-label=\"@OpenPipelineLabel(w)\"");
        markup.ShouldContain("title=\"@PipelineTabTitle(w)\"");
        markup.ShouldContain("aria-label=\"@OpenMetricsTabLabel(active)\"");
        markup.ShouldContain("title=\"@OpenMetricsTabLabel(active)\"");
        markup.ShouldContain("aria-label=\"@OpenDashboardLabel(d)\"");
        markup.ShouldContain("title=\"@DashboardTabTitle(d)\"");
        markup.ShouldContain("aria-label=\"@OpenTestLabel(t)\"");
        markup.ShouldContain("title=\"@TestTabTitle(t)\"");
        markup.ShouldContain("aria-label=\"@OpenTopicsTabLabel(active)\"");
        markup.ShouldContain("title=\"@OpenTopicsTabLabel(active)\"");
        markup.ShouldContain("aria-label=\"@OpenLogsTabLabel(active)\"");
        markup.ShouldContain("title=\"@OpenLogsTabLabel(active)\"");
        markup.ShouldContain("aria-label=\"@OpenAppJsonTabLabel(active)\"");
        markup.ShouldContain("title=\"@OpenAppJsonTabLabel(active)\"");
        markup.ShouldContain("title=\"@DeleteArtifactTabLabel(\"pipeline\", w)\"");
        markup.ShouldContain("aria-label=\"@DeleteArtifactTabLabel(\"pipeline\", w)\"");
        markup.ShouldContain("title=\"@DeleteArtifactTabLabel(\"dashboard\", d)\"");
        markup.ShouldContain("aria-label=\"@DeleteArtifactTabLabel(\"dashboard\", d)\"");
        markup.ShouldContain("title=\"@DeleteArtifactTabLabel(\"test\", t)\"");
        markup.ShouldContain("aria-label=\"@DeleteArtifactTabLabel(\"test\", t)\"");
        markup.ShouldContain("private static string OpenMetricsTabLabel");
        markup.ShouldContain("private static string OpenPipelineLabel(string pipelineName)");
        markup.ShouldContain("private static string PipelineTabTitle(string pipelineName)");
        markup.ShouldContain("private static string OpenDashboardLabel(string dashboardName)");
        markup.ShouldContain("private static string DashboardTabTitle(string dashboardName)");
        markup.ShouldContain("private static string OpenTestLabel(string testName)");
        markup.ShouldContain("private static string TestTabTitle(string testName)");
        markup.ShouldNotContain("@($\"Open pipeline {w}\")");
        markup.ShouldNotContain("@($\"Pipeline {w}\")");
        markup.ShouldNotContain("@($\"Open dashboard {d}\")");
        markup.ShouldNotContain("@($\"Dashboard {d}\")");
        markup.ShouldNotContain("@($\"Open test {t}\")");
        markup.ShouldNotContain("@($\"Test {t}\")");
        markup.ShouldContain("private static string OpenTopicsTabLabel");
        markup.ShouldContain("private static string OpenLogsTabLabel");
        markup.ShouldContain("private static string OpenAppJsonTabLabel");
        markup.ShouldContain("private static string DeleteArtifactTabLabel");
        markup.ShouldContain("RunFromKeyboardAsync(args, () => { active.SetActiveWorkflow(w); _jsonView = false; })");
        markup.ShouldContain("RunFromKeyboardAsync(args, () => { active.SetActiveMetrics(); _jsonView = false; })");
        markup.ShouldContain("RunFromKeyboardAsync(args, () => { active.SetActiveDashboard(d); _jsonView = false; })");
        markup.ShouldContain("RunFromKeyboardAsync(args, () => { active.SetActiveTest(t); _jsonView = false; })");
        markup.ShouldContain("RunFromKeyboardAsync(args, () => { active.SetActiveTopics(); _jsonView = false; })");
        markup.ShouldContain("RunFromKeyboardAsync(args, SelectLogs)");
        markup.ShouldContain("RunFromKeyboardAsync(args, ToggleJsonView)");
        markup.ShouldContain("Icon=\"@Icons.Material.Filled.Code\"");
        markup.ShouldContain("AriaLabel=\"@AddArtifactMenuLabel(active)\"");
        markup.ShouldContain("private static string AddArtifactMenuLabel(FlowWorkspaceService app)");
        markup.ShouldNotContain("AriaLabel=\"Add artifact\"");
        markup.Split("Class=\"project-tab-icon\" aria-hidden=\"true\"", StringSplitOptions.None).Length.ShouldBe(8);
        markup.ShouldContain("<span class=\"project-tab-name\">App JSON</span>");
        markup.ShouldContain("<span class=\"project-tabbar-app-name\">@active.Name</span>");
        markup.ShouldContain("title=\"@CloseAppLabel(active)\"");
        markup.ShouldContain("aria-label=\"@CloseAppLabel(active)\"");
        markup.ShouldContain("private static string CloseAppLabel(FlowWorkspaceService app)");
        markup.ShouldNotContain("@($\"Close app {active.Name}\")");
        markup.ShouldContain("Icon=\"@Icons.Material.Filled.Close\"");
        markup.ShouldContain("<MudIcon Icon=\"@Icons.Material.Filled.Close\" Size=\"Size.Small\" aria-hidden=\"true\" />");
        markup.ShouldContain("<MudIcon Icon=\"@Icons.Material.Filled.DeleteOutline\" Size=\"Size.Small\" aria-hidden=\"true\" />");
        System.Text.RegularExpressions.Regex.Matches(
                markup,
                @"<MudIcon\b(?:(?!/>).)*?/>",
                System.Text.RegularExpressions.RegexOptions.Singleline)
            .Cast<System.Text.RegularExpressions.Match>()
            .Select(static match => match.Value)
            .Where(static icon => !icon.Contains("aria-hidden=\"true\"", StringComparison.Ordinal) &&
                !icon.Contains("aria-label=", StringComparison.Ordinal) &&
                !icon.Contains("AriaLabel=", StringComparison.Ordinal))
            .ToArray()
            .ShouldBeEmpty();
        markup.ShouldContain("@onclick:stopPropagation=\"true\"");
        markup.ShouldContain("@onmousedown:stopPropagation=\"true\"");
        markup.ShouldContain("@onclick=\"@ToggleJsonView\"");
        markup.ShouldNotContain(">x</button>");
        markup.ShouldContain("private void ToggleJsonView()");
        markup.ShouldContain("private void OpenJsonView()");
        markup.ShouldContain("private void CloseJsonView()");
        markup.ShouldContain("private static string? WorkspaceTabCurrent");
        markup.ShouldContain("private static bool IsActivationKey");
        markup.ShouldContain("private static Task RunFromKeyboardAsync");
        markup.ShouldContain("SyncActiveArtifactSelection();");
        markup.ShouldContain("private void SyncActiveArtifactSelection()");
        markup.ShouldNotContain("SyncActiveArtifactState");
        markup.ShouldNotContain("private string JsonToggleTitle");
        markup.ShouldNotContain("project-tabbar-app-toggle");
        markup.ShouldNotContain("project-tab-json-active");
        markup.ShouldContain("InitialQuery=\"@_pendingLogQuery\"");
        markup.ShouldContain("<FlowDesigner LogsRequested=\"@OpenLogs\" />");
        markup.ShouldContain("private WorkspaceLogQuery? _pendingLogQuery;");
        markup.ShouldContain("private void SelectLogs()");
        markup.ShouldContain("_pendingLogQuery = null;");
        markup.ShouldContain("private void OpenLogs(WorkspaceLogQuery query)");
        markup.ShouldContain("_pendingLogQuery = query;");
        markup.ShouldContain("active.SetActiveLogs();");
        markup.ShouldNotContain("aria-label=\"Open metrics\"");
        markup.ShouldNotContain("aria-label=\"Open topics\"");
        markup.ShouldNotContain("aria-label=\"Open logs\"");
        markup.ShouldNotContain("aria-label=\"Open app JSON\"");
        markup.ShouldNotContain("title=\"Delete pipeline\"");
        markup.ShouldNotContain("title=\"Delete dashboard\"");
        markup.ShouldNotContain("title=\"Delete test\"");
        markup.ShouldNotContain("aria-label=\"@($\"Delete pipeline {w}\")\"");
        markup.ShouldNotContain("aria-label=\"@($\"Delete dashboard {d}\")\"");
        markup.ShouldNotContain("aria-label=\"@($\"Delete test {t}\")\"");
        css.ShouldContain(".project-tabbar {");
        css.ShouldContain("position: relative;");
        css.ShouldContain("z-index: 12;");
        css.ShouldContain(".project-tab-json-view .project-tab-icon");
        css.ShouldNotContain(".project-tabbar-app-toggle");
        css.ShouldNotContain(".project-tab-json {");
        css.ShouldNotContain(".project-tab-json:hover");
        css.ShouldNotContain(".project-tab-json-active");
    }

    [Fact]
    public void WorkspaceRoleButtons_UseExplicitAccessibleNames()
    {
        var root = FindRepositoryRoot();
        var componentDirectory = Path.Combine(
            root,
            "src",
            "FluxMq.UI",
            "Components");
        var violations = Directory.GetFiles(componentDirectory, "*.razor", SearchOption.AllDirectories)
            .SelectMany(file =>
            {
                var markup = File.ReadAllText(file);
                return System.Text.RegularExpressions.Regex.Matches(
                        markup,
                        @"<[^>]*\brole\s*=\s*""button""[^>]*>",
                        System.Text.RegularExpressions.RegexOptions.Singleline)
                    .Cast<System.Text.RegularExpressions.Match>()
                    .Select(match => (File: file, Tag: match.Value))
                    .Where(static item => !item.Tag.Contains("aria-label=", StringComparison.Ordinal) &&
                        !item.Tag.Contains("aria-labelledby=", StringComparison.Ordinal));
            })
            .Select(item => $"{Path.GetRelativePath(root, item.File)}: {item.Tag.Replace('\r', ' ').Replace('\n', ' ').Trim()}")
            .ToArray();

        violations.ShouldBeEmpty();
    }

    [Fact]
    public void WorkspaceListboxes_ExposeActiveOptionRelationships()
    {
        var root = FindRepositoryRoot();
        var componentDirectory = Path.Combine(
            root,
            "src",
            "FluxMq.UI",
            "Components");
        var listboxViolations = Directory.GetFiles(componentDirectory, "*.razor", SearchOption.AllDirectories)
            .SelectMany(file =>
            {
                var markup = File.ReadAllText(file);
                return System.Text.RegularExpressions.Regex.Matches(
                        markup,
                        @"<[^>]*\brole\s*=\s*""listbox""[^>]*>",
                        System.Text.RegularExpressions.RegexOptions.Singleline)
                    .Cast<System.Text.RegularExpressions.Match>()
                    .Select(match => (File: file, Tag: match.Value))
                    .Where(static item => !item.Tag.Contains("aria-activedescendant=", StringComparison.Ordinal));
            })
            .Select(item => $"{Path.GetRelativePath(root, item.File)}: {item.Tag.Replace('\r', ' ').Replace('\n', ' ').Trim()}")
            .ToArray();
        var optionViolations = Directory.GetFiles(componentDirectory, "*.razor", SearchOption.AllDirectories)
            .SelectMany(file =>
            {
                var markup = File.ReadAllText(file);
                return System.Text.RegularExpressions.Regex.Matches(
                        markup,
                        @"<[^>]*\brole\s*=\s*""option""[^>]*>",
                        System.Text.RegularExpressions.RegexOptions.Singleline)
                    .Cast<System.Text.RegularExpressions.Match>()
                    .Select(match => (File: file, Tag: match.Value))
                    .Where(static item => !item.Tag.Contains("id=", StringComparison.Ordinal));
            })
            .Select(item => $"{Path.GetRelativePath(root, item.File)}: {item.Tag.Replace('\r', ' ').Replace('\n', ' ').Trim()}")
            .ToArray();

        listboxViolations.Concat(optionViolations).ToArray().ShouldBeEmpty();
    }

    [Fact]
    public void WorkspacePreformattedBlocks_ExposeAccessibleNames()
    {
        var root = FindRepositoryRoot();
        var componentDirectory = Path.Combine(
            root,
            "src",
            "FluxMq.UI",
            "Components");
        var violations = Directory.GetFiles(componentDirectory, "*.razor", SearchOption.AllDirectories)
            .SelectMany(file =>
            {
                var markup = File.ReadAllText(file);
                return System.Text.RegularExpressions.Regex.Matches(
                        markup,
                        @"<pre\b[^>]*>",
                        System.Text.RegularExpressions.RegexOptions.Singleline)
                    .Cast<System.Text.RegularExpressions.Match>()
                    .Select(match => (File: file, Tag: match.Value))
                    .Where(static item => !item.Tag.Contains("aria-label=", StringComparison.Ordinal) &&
                        !item.Tag.Contains("aria-labelledby=", StringComparison.Ordinal));
            })
            .Select(item => $"{Path.GetRelativePath(root, item.File)}: {item.Tag.Replace('\r', ' ').Replace('\n', ' ').Trim()}")
            .ToArray();

        violations.ShouldBeEmpty();
    }

    [Fact]
    public void NodeEditDialog_UsesCompactEditorShell()
    {
        var root = FindRepositoryRoot();
        var markup = File.ReadAllText(Path.Combine(
            root,
            "src",
            "FluxMq.UI",
            "Components",
            "Workspace",
            "Dialogs",
            "NodeEditDialog.razor"));
        var css = File.ReadAllText(Path.Combine(
            root,
            "src",
            "FluxMq.UI",
            "Components",
            "Workspace",
            "Dialogs",
            "NodeEditDialog.razor.css"));

        markup.ShouldContain("node-edit-dialog-title");
        markup.ShouldContain("node-edit-dialog-heading");
        markup.ShouldContain("node-edit-dialog-validation");
        markup.ShouldContain("node-edit-dialog-content");
        markup.ShouldContain("ContentClassName");
        markup.ShouldContain("[Parameter] public string? ContentClass");
        markup.ShouldContain("role=\"form\" aria-label=\"Edit node\"");
        markup.ShouldContain("node-edit-dialog-section node-edit-dialog-identity");
        markup.ShouldContain("aria-label=\"Node identity\"");
        markup.ShouldContain("node-edit-dialog-editor");
        markup.ShouldContain("aria-describedby=\"@ValidationElementId\"");
        markup.ShouldContain("role=\"status\"");
        markup.ShouldContain("aria-live=\"polite\"");
        markup.ShouldContain("OnNodeIdKeyDown");
        markup.ShouldContain("@if (!CanSubmit)");
        markup.ShouldContain("ValidationElementId");
        markup.ShouldContain("SubmitValidationText");
        markup.ShouldContain("<span id=\"node-edit-dialog-validation\"");
        markup.ShouldContain("class=\"node-edit-dialog-validation-message\"");
        markup.ShouldContain("[Parameter] public Func<string?>? EditorValidationError");
        markup.ShouldContain("private string? _editorError;");
        markup.ShouldContain("private string? EditorError => _editorError;");
        markup.ShouldContain("private void RefreshValidationState()");
        markup.ShouldContain("RefreshValidationState();");
        markup.ShouldContain("string.IsNullOrWhiteSpace(EditorError)");
        markup.ShouldContain("NodeIdError ?? EditorError ?? \"Review required\"");
        markup.ShouldNotContain("Ready to save");
        markup.ShouldNotContain("StatusElementId");
        markup.ShouldNotContain("SubmitStatusText");
        markup.ShouldNotContain("node-edit-dialog-action-status");
        markup.ShouldNotContain("SubmitStatusClass");
        markup.ShouldContain("node-edit-dialog-actions");
        markup.ShouldContain("aria-label=\"@CancelNodeEditLabel\"");
        markup.ShouldContain("aria-label=\"@SaveNodeEditLabel\"");
        markup.ShouldContain("private string CancelNodeEditLabel => $\"Cancel editing {NodeEditTargetLabel}\"");
        markup.ShouldContain("private string SaveNodeEditLabel => $\"Save edits for {NodeEditTargetLabel}\"");
        markup.ShouldContain("private string NodeEditTargetLabel => string.IsNullOrWhiteSpace(NodeDisplayName)");
        markup.ShouldNotContain("aria-label=\"Cancel node edit\"");
        markup.ShouldNotContain("aria-label=\"Save node edit\"");
        markup.ShouldContain("Color=\"Color.Primary\"");
        markup.ShouldNotContain("[Parameter] public Color CategoryColor");
        markup.ShouldNotContain("Color=\"@CategoryColor\"");
        markup.ShouldNotContain("HelperText=");
        markup.ShouldNotContain("ErrorText=");
        markup.ShouldNotContain("node-edit-dialog-meta-strip");
        markup.ShouldNotContain("node-edit-dialog-title-meta");
        markup.ShouldNotContain("node-edit-dialog-section-title");
        markup.ShouldNotContain("node-edit-dialog-section-head");
        markup.ShouldNotContain("node-edit-dialog-section node-edit-dialog-editor");
        markup.ShouldNotContain("Icons.Material.Filled.Badge");
        markup.ShouldNotContain("Icons.Material.Filled.Settings");
        markup.ShouldNotContain("node-edit-dialog-title-icon");
        markup.ShouldNotContain("Must be unique within the workflow.");
        markup.ShouldNotContain("@key=\"SubmitStatusKey\"");

        css.ShouldContain(".node-edit-dialog-content");
        css.ShouldContain("background: var(--flux-surface);");
        css.ShouldContain("border: 1px solid var(--flux-border-soft);");
        css.ShouldContain("padding: 12px;");
        css.ShouldContain("max-height: min(82vh, 760px);");
        css.ShouldContain(".node-edit-dialog-section");
        css.ShouldContain("padding: 0;");
        css.ShouldContain("color: color-mix(in srgb, var(--mud-palette-text-primary) 78%, var(--mud-palette-text-secondary));");
        css.ShouldContain(".node-edit-dialog-heading");
        css.ShouldContain("display: -webkit-box;");
        css.ShouldContain("-webkit-line-clamp: 2;");
        css.ShouldContain("overflow-wrap: anywhere;");
        css.ShouldContain("white-space: normal;");
        css.ShouldContain(".node-edit-dialog-editor");
        css.ShouldContain("display: contents;");
        css.ShouldContain("grid-template-columns: minmax(0, 1fr);");
        css.ShouldContain(".node-edit-dialog-content ::deep(:is(");
        css.ShouldContain(".node-ui-editor");
        css.ShouldContain(".node-ui-form-grid");
        css.ShouldContain(".node-ui-stack");
        css.ShouldContain(".node-ui-checkbox-row");
        css.ShouldContain(".node-ui-table-wrap");
        css.ShouldContain(".node-ui-code-surface");
        css.ShouldContain(".node-edit-dialog-content.dynamic-mapper-dialog");
        css.ShouldContain("grid-template-areas:");
        css.ShouldContain("\"identity config\"");
        css.ShouldContain("\"workbench workbench\"");
        css.ShouldContain("height: calc(100vh - 112px);");
        css.ShouldContain("height: calc(100vh - 168px);");
        css.ShouldContain("height: 100% !important;");
        css.ShouldContain("overflow-x: hidden;");
        css.ShouldContain(".node-edit-dialog-content ::deep(.mud-input-control)");
        css.ShouldContain(".node-edit-dialog-content ::deep(.mud-input-label)");
        css.ShouldContain(".node-edit-dialog-content ::deep(.mud-input-root)");
        css.ShouldContain("color: color-mix(in srgb, var(--mud-palette-text-primary) 80%, var(--mud-palette-text-secondary));");
        css.ShouldContain(".node-edit-dialog-content.dynamic-mapper-dialog ::deep(.dynamic-mapper-workspace)");
        css.ShouldContain(".node-edit-dialog-content.dynamic-mapper-dialog ::deep(.dynamic-mapper-control-row)");
        css.ShouldContain(".node-edit-dialog-content.dynamic-mapper-dialog .dynamic-mapper-editor");
        css.ShouldContain(".node-edit-dialog-content.dynamic-mapper-dialog .dynamic-mapper-workspace");
        css.ShouldContain(".node-edit-dialog-content.dynamic-mapper-dialog .dynamic-mapper-workspace .dynamic-mapper-monaco-editor .overflow-guard");
        css.ShouldContain("align-self: stretch;");
        css.ShouldContain(".node-edit-dialog-content.dynamic-mapper-dialog ::deep(:is(");
        css.ShouldContain(".node-edit-dialog-content.dynamic-mapper-dialog :is(");
        css.ShouldContain(".dynamic-mapper-input-workspace,");
        css.ShouldContain(".dynamic-mapper-result-workspace))");
        css.ShouldContain(".node-edit-dialog-editor ::deep(.dynamic-mapper-workspace .dynamic-mapper-monaco-editor)");
        css.ShouldContain(".node-edit-dialog-content.json-schema-validator-dialog");
        css.ShouldContain("\"schema schema\"");
        css.ShouldContain("height: calc(100vh - 112px);");
        css.ShouldContain("height: calc(100vh - 168px);");
        css.ShouldContain(".node-edit-dialog-content.json-schema-validator-dialog ::deep(.json-schema-validator-editor)");
        css.ShouldContain(".node-edit-dialog-content.json-schema-validator-dialog ::deep(.json-schema-validator-config-row)");
        css.ShouldContain(".node-edit-dialog-content.json-schema-validator-dialog ::deep(.json-schema-validator-schema-area)");
        css.ShouldContain(".node-edit-dialog-content.json-schema-validator-dialog ::deep(.json-schema-validator-inline-source .schema-monaco-editor .overflow-guard)");
        css.ShouldNotContain(".node-edit-dialog-editor ::deep(.dynamic-mapper-workbench .dynamic-mapper-panel)");
        css.ShouldNotContain(".node-edit-dialog-editor ::deep(.mud-chip)");
        css.ShouldNotContain("height: 96px;");
        css.ShouldNotContain("height: 280px;");
        css.ShouldNotContain("height: 570px;");
        css.ShouldContain(".node-edit-dialog-validation-message");
        css.ShouldNotContain(".node-edit-dialog-action-status");
        css.ShouldNotContain(".node-edit-dialog-validation-message.ready");
        css.ShouldContain("min-height: 28px;");
        css.ShouldNotContain("text-overflow: ellipsis;");
        css.ShouldNotContain("white-space: nowrap;");
        css.ShouldContain(".node-edit-dialog-actions");
        css.ShouldNotContain("background: color-mix(in srgb, var(--flux-surface-2) 72%, var(--flux-surface));");
        css.ShouldNotContain("padding: 8px 10px;");
        css.ShouldContain("@media (max-width: 700px)");
        css.ShouldNotContain(".node-edit-dialog-status");
        css.ShouldNotContain(".node-edit-dialog-section-title");
        css.ShouldNotContain(".node-edit-dialog-section-head");
        css.ShouldNotContain(".node-edit-dialog-title-icon");
        css.ShouldNotContain(".node-edit-dialog-title-meta");
        css.ShouldNotContain(".node-edit-dialog-content ::deep(.mud-input-slot)");
        css.ShouldNotContain(".node-edit-dialog-meta-strip");
    }

    [Fact]
    public void WorkspacePage_UsesPipelineSpecificDesignerShell()
    {
        var root = FindRepositoryRoot();
        var markup = File.ReadAllText(Path.Combine(
            root,
            "src",
            "FluxMq.UI",
            "Components",
            "Pages",
            "WorkspacePage.razor"));
        var css = File.ReadAllText(Path.Combine(
            root,
            "src",
            "FluxMq.UI",
            "Components",
            "Pages",
            "WorkspacePage.razor.css"));

        markup.ShouldContain("workspace-artifact-shell workspace-artifact-shell-pipeline");
        markup.ShouldContain("ComponentCatalogPanel ArtifactKind=\"@WorkspaceArtifactKind.Pipeline\"");
        markup.ShouldContain("<FlowDesigner LogsRequested=\"@OpenLogs\" />");
        markup.ShouldContain("<TopicExplorerPanel />");
        markup.ShouldNotContain("_standaloneTopicExplorer");
        markup.ShouldNotContain("OpenStandaloneTopicExplorer");
        markup.ShouldNotContain("Topic explorer</MudButton>");

        css.ShouldContain(".workspace-artifact-shell-pipeline");
        css.ShouldContain("grid-template-columns: minmax(212px, 236px) minmax(0, 1fr);");
        css.ShouldContain(".workspace-artifact-shell-pipeline .workspace-artifact-tools");
        css.ShouldContain("padding: 6px;");
        css.ShouldContain(".workspace-artifact-tools:focus-within");
        css.ShouldContain(".project-tab-close ::deep .mud-icon-root");
        css.ShouldNotContain(".workspace-artifact-region:focus-within");
        css.ShouldNotContain(".workspace-designer-region:focus-within");
        css.ShouldNotContain(".artifact-workspace-state");
        css.ShouldNotContain(".artifact-workspace-icon");
        css.ShouldNotContain(".artifact-workspace-title");
        css.ShouldNotContain(".artifact-workspace-meta");
        css.ShouldNotContain("box-shadow: inset 0 0 0 1px color-mix(in srgb, var(--mud-palette-primary) 34%, var(--flux-border));");
    }

    [Fact]
    public void AppTreePanel_UsesCompactTestManagementRows()
    {
        var root = FindRepositoryRoot();
        var markup = File.ReadAllText(Path.Combine(
            root,
            "src",
            "FluxMq.UI",
            "Components",
            "Workspace",
            "AppTreePanel.razor"));
        var css = File.ReadAllText(Path.Combine(
            root,
            "src",
            "FluxMq.UI",
            "Components",
            "Workspace",
            "AppTreePanel.razor.css"));

        markup.ShouldContain("tree-empty\" role=\"status\" aria-live=\"polite\"");
        markup.ShouldContain("<MudIcon Icon=\"@Icons.Material.Filled.AccountTree\" Size=\"Size.Medium\" aria-hidden=\"true\" />");
        markup.ShouldContain("app-tree\" aria-label=\"@AppTreeLabel\"");
        markup.ShouldContain("private string AppTreeLabel");
        markup.ShouldContain("App structure tree for {active.Name}, {CountLabel(Projects.Projects.Count, \"open app\")}");
        markup.ShouldContain("App structure tree with {CountLabel(Projects.Projects.Count, \"open app\")}");
        markup.ShouldNotContain("aria-label=\"App structure tree\"");
        markup.ShouldContain("aria-label=\"@AppRowLabel(a, isActive)\"");
        markup.ShouldContain("aria-current=\"@TreeItemCurrent(isActive)\"");
        markup.ShouldContain("title=\"@AppRowLabel(a, isActive)\"");
        System.Text.RegularExpressions.Regex.Matches(
                markup,
                @"<MudIcon\b(?:(?!/>).)*?/>",
                System.Text.RegularExpressions.RegexOptions.Singleline)
            .Cast<System.Text.RegularExpressions.Match>()
            .Select(static match => match.Value)
            .Where(static icon => !icon.Contains("aria-hidden=\"true\"", StringComparison.Ordinal))
            .ToArray()
            .ShouldBeEmpty();
        System.Text.RegularExpressions.Regex.IsMatch(markup, @"Class=""app-row-icon""\s+aria-hidden=""true""").ShouldBeTrue();
        markup.ShouldContain("private static string AppRowLabel");
        markup.ShouldContain("private static string? TreeItemCurrent");
        System.Text.RegularExpressions.Regex.Matches(markup, "aria-keyshortcuts=\"Enter Space\"").Count.ShouldBe(7);
        markup.ShouldContain("Text=\"@CloseAppLabel(a)\"");
        markup.ShouldContain("aria-label=\"@CloseAppLabel(a)\"");
        markup.ShouldContain("private static string CloseAppLabel(FlowWorkspaceService app)");
        markup.ShouldNotContain("@($\"Close app {a.Name}\")");
        markup.ShouldNotContain("Text=\"Close app\"");
        markup.ShouldContain("tree-empty-artifact-row tests");
        markup.ShouldContain("role=\"button\"");
        markup.ShouldContain("aria-label=\"@CreateTestScenarioLabel(a)\"");
        markup.ShouldContain("private static string CreateTestScenarioLabel(FlowWorkspaceService app)");
        markup.ShouldContain("=> $\"Create test scenario for {app.Name}, {CountLabel(app.TestNames.Count, \"test\")}\";");
        markup.ShouldNotContain("aria-label=\"Create test scenario\"");
        markup.ShouldContain("AddTestFromKeyboardAsync(args, a)");
        markup.ShouldContain("if (IsActivationKey(args))");
        System.Text.RegularExpressions.Regex.IsMatch(markup, @"Class=""tree-empty-artifact-icon""\s+aria-hidden=""true""").ShouldBeTrue();
        System.Text.RegularExpressions.Regex.IsMatch(markup, @"Class=""tree-empty-artifact-add""\s+aria-hidden=""true""").ShouldBeTrue();
        markup.ShouldContain("tree-empty-artifact-copy");
        markup.ShouldContain("tree-empty-artifact-cues");
        markup.ShouldContain("@ConnectionDotClass(c.State)");
        markup.ShouldContain("Text=\"@ConnectConnectionLabel(c)\"");
        markup.ShouldContain("aria-label=\"@ConnectConnectionLabel(c)\"");
        markup.ShouldContain("Text=\"@DisconnectConnectionLabel(c)\"");
        markup.ShouldContain("aria-label=\"@DisconnectConnectionLabel(c)\"");
        markup.ShouldContain("Text=\"@RemoveConnectionLabel(c)\"");
        markup.ShouldContain("aria-label=\"@RemoveConnectionLabel(c)\"");
        markup.ShouldContain("private static string ConnectConnectionLabel(ManagedConnection connection)");
        markup.ShouldContain("private static string DisconnectConnectionLabel(ManagedConnection connection)");
        markup.ShouldContain("private static string RemoveConnectionLabel(ManagedConnection connection)");
        markup.ShouldNotContain("@($\"Connect {c.ResourceName}\")");
        markup.ShouldNotContain("@($\"Disconnect {c.ResourceName}\")");
        markup.ShouldNotContain("@($\"Remove connection {c.ResourceName}\")");
        markup.ShouldNotContain("Text=\"Connect\"");
        markup.ShouldNotContain("Text=\"Disconnect\"");
        markup.ShouldNotContain("Text=\"Remove\"");
        markup.ShouldContain("private static string ConnectionDotClass");
        markup.ShouldNotContain("StateDotClass");
        markup.ShouldContain("TreeSectionHeader(");
        markup.ShouldContain("private RenderFragment TreeSectionHeader");
        markup.ShouldContain("BrokerSection");
        markup.ShouldContain("PipelineSection");
        markup.ShouldContain("DashboardSection");
        markup.ShouldContain("TestSection");
        markup.ShouldContain("SelectAppFromKeyboardAsync(args, a)");
        markup.ShouldContain("ToggleSectionFromKeyboardAsync(args, app, section)");
        markup.ShouldContain("SelectPipelineFromKeyboardAsync(args, a, w)");
        markup.ShouldContain("aria-label=\"@OpenPipelineLabel(w)\"");
        markup.ShouldContain("private static string OpenPipelineLabel(string pipelineName)");
        markup.ShouldNotContain("@($\"Open pipeline {w}\")");
        markup.ShouldContain("aria-current=\"@TreeItemCurrent(isPipeActive)\"");
        System.Text.RegularExpressions.Regex.IsMatch(markup, @"Class=""pipe-icon""\s+aria-hidden=""true""").ShouldBeTrue();
        markup.ShouldContain("SelectMetricsFromKeyboardAsync(args, a)");
        markup.ShouldContain("var isMetricsActive = a == Projects.ActiveProject");
        markup.ShouldContain("aria-label=\"@MetricDesignerRowLabel(a, isMetricsActive)\"");
        markup.ShouldContain("title=\"@MetricDesignerRowLabel(a, isMetricsActive)\"");
        markup.ShouldContain("private static string MetricDesignerRowLabel");
        markup.ShouldContain("return $\"{app.Name} metric designer, {state}, {CountLabel(app.MetricNames.Count, \"metric\")}\";");
        markup.ShouldNotContain("aria-label=\"Open metric designer\"");
        markup.ShouldContain("aria-current=\"@TreeItemCurrent(isMetricsActive)\"");
        System.Text.RegularExpressions.Regex.Matches(markup, @"Class=""artifact-icon dashboard""\s+aria-hidden=""true""").Count.ShouldBe(2);
        markup.ShouldContain("SelectDashboardFromKeyboardAsync(args, a, d)");
        markup.ShouldContain("aria-label=\"@OpenDashboardLabel(d)\"");
        markup.ShouldContain("private static string OpenDashboardLabel(string dashboardName)");
        markup.ShouldNotContain("@($\"Open dashboard {d}\")");
        markup.ShouldContain("aria-current=\"@TreeItemCurrent(isDashboardActive)\"");
        markup.ShouldContain("SelectTestFromKeyboardAsync(args, a, t)");
        markup.ShouldContain("aria-current=\"@TreeItemCurrent(isTestActive)\"");
        markup.ShouldContain("aria-expanded=\"@AriaExpanded(isCollapsed)\"");
        markup.ShouldContain("aria-controls=\"@TreeSectionBodyId(app, section)\"");
        System.Text.RegularExpressions.Regex.IsMatch(markup, @"Class=""tree-section-chevron""\s+aria-hidden=""true""").ShouldBeTrue();
        System.Text.RegularExpressions.Regex.IsMatch(markup, @"Class=""@\(\$""tree-section-icon \{iconClass\}""\)""\s+aria-hidden=""true""").ShouldBeTrue();
        markup.ShouldContain("id=\"@TreeSectionBodyId(a, BrokerSection)\"");
        markup.ShouldContain("id=\"@TreeSectionBodyId(a, PipelineSection)\"");
        markup.ShouldContain("id=\"@TreeSectionBodyId(a, MetricSection)\"");
        markup.ShouldContain("id=\"@TreeSectionBodyId(a, DashboardSection)\"");
        markup.ShouldContain("id=\"@TreeSectionBodyId(a, TestSection)\"");
        markup.ShouldContain("private static string TreeSectionBodyId");
        markup.ShouldContain("private static string SanitizeIdToken");
        markup.ShouldContain("aria-label=\"@addTooltip\"");
        markup.ShouldContain("private static bool IsActivationKey");
        markup.ShouldContain("private static Task RunFromKeyboardAsync");
        markup.ShouldContain("TestArtifactItemClass(isTestActive, latestTestRun)");
        markup.ShouldContain("LatestTestRun(a, t)");
        markup.ShouldContain("test-artifact-icon-frame");
        markup.ShouldContain("test-artifact-copy");
        markup.ShouldContain("TestRunMeta(latestTestRun)");
        markup.ShouldContain("TestRunSummaryClass(latestTestRun)");
        markup.ShouldContain("aria-label=\"@TestRunSummaryLabel(latestTestRun)\"");
        markup.ShouldContain("private static string TestRunSummaryLabel");
        markup.ShouldContain("TestRunDetailText(latestTestRun)");
        markup.ShouldContain("TestRunMarkerClass(latestTestRun)");
        markup.ShouldContain("No run yet");
        markup.ShouldNotContain("TestRunStateLabel");
        markup.ShouldNotContain("TestRunStateClass(latestTestRun)");
        markup.ShouldNotContain("test-run-state");
        markup.ShouldContain("tree-item-actions");
        markup.ShouldContain("tree-delete-button");
        markup.ShouldContain("Text=\"@DeleteTestLabel(t)\"");
        markup.ShouldContain("aria-label=\"@DeleteTestLabel(t)\"");
        markup.ShouldContain("private static string DeleteTestLabel(string testName)");
        markup.ShouldNotContain("@($\"Delete test {t}\")");
        markup.ShouldNotContain("Text=\"Delete test\"");
        markup.ShouldContain("RemoveTestAsync(a, t)");
        markup.ShouldContain("ShowMessageBoxAsync(");
        markup.ShouldContain("private static string TestArtifactTitle");
        markup.ShouldContain("private static string TestRunIssueText");
        markup.ShouldNotContain("TestRunPillClass(latestTestRun)");
        markup.ShouldNotContain("test-run-pill");
        markup.ShouldNotContain("Ready for first run");

        css.ShouldContain(".tree-empty-artifact-row");
        css.ShouldContain(".tree-empty ::deep .mud-icon-root");
        css.ShouldContain("grid-template-columns: minmax(0, min(300px, 100%));");
        css.ShouldContain("flex: 1 1 auto;");
        css.ShouldContain("justify-items: center;");
        css.ShouldContain("text-align: center;");
        css.ShouldContain("overflow-wrap: anywhere;");
        css.ShouldContain(".app-row:focus-visible");
        css.ShouldContain("grid-template-columns: 18px minmax(0, 1fr) 34px;");
        css.ShouldContain("min-height: 38px;");
        css.ShouldContain(".tree-section-header:focus-visible");
        css.ShouldContain("min-height: 24px;");
        css.ShouldContain(".pipe-item:focus-visible");
        css.ShouldContain(".artifact-item:focus-visible");
        css.ShouldContain("grid-template-columns: 16px minmax(0, 1fr) auto 18px;");
        css.ShouldContain("margin: 3px 0 0 30px;");
        css.ShouldContain(".tree-empty-artifact-copy");
        css.ShouldContain(".tree-empty-artifact-cues");
        css.ShouldContain(".tree-empty-artifact-cues span");
        css.ShouldContain(".test-artifact-item");
        css.ShouldContain("grid-template-columns: 22px minmax(0, 1fr) minmax(82px, auto) 24px;");
        css.ShouldContain("min-height: 42px;");
        css.ShouldContain(".test-artifact-item::before");
        css.ShouldContain(".test-artifact-icon-frame");
        css.ShouldContain(".test-artifact-copy small");
        css.ShouldContain(".test-run-summary");
        css.ShouldContain(".test-run-summary-meta");
        css.ShouldContain(".test-run-marker");
        css.ShouldNotContain(".test-run-state");
        css.ShouldContain(".tree-item-actions");
        css.ShouldContain("opacity: 0.58;");
        css.ShouldContain(".tree-item-actions ::deep .tree-delete-button");
        css.ShouldNotContain(".test-run-pill");
        css.ShouldNotContain("border-radius: 999px;");
    }

    [Fact]
    public void AppStructureMenu_UsesCompactInlineArtifactActions()
    {
        var root = FindRepositoryRoot();
        var markup = File.ReadAllText(Path.Combine(
            root,
            "src",
            "FluxMq.UI",
            "Components",
            "Workspace",
            "AppStructureMenu.razor"));
        var css = File.ReadAllText(Path.Combine(
            root,
            "src",
            "FluxMq.UI",
            "Components",
            "Workspace",
            "AppStructureMenu.razor.css"));
        var appCss = File.ReadAllText(Path.Combine(
            root,
            "src",
            "FluxMq.UI",
            "wwwroot",
            "app.css"));

        markup.ShouldContain("aria-label=\"@StructureNavigationLabel(active)\"");
        markup.ShouldContain("private static string StructureNavigationLabel(FlowWorkspaceService app)");
        markup.ShouldContain("$\"{app.Name} structure navigation, {BuildAppMeta(app)}\"");
        markup.ShouldNotContain("aria-label=\"App structure navigation\"");
        markup.Split('\n')
            .Where(static line => line.Contains("<MudIcon ", StringComparison.Ordinal) &&
                !line.Contains("aria-hidden=\"true\"", StringComparison.Ordinal))
            .ToArray()
            .ShouldBeEmpty();
        markup.ShouldContain("StructureMenuLabel(\"Brokers\", conns.Count)");
        markup.ShouldContain("StructureMenuAriaLabel(active, \"Brokers\", conns.Count, \"broker connection\")");
        markup.ShouldContain("StructureMenuAriaLabel(active, \"Pipelines\", active.WorkflowNames.Count, \"pipeline\")");
        markup.ShouldContain("StructureMenuAriaLabel(active, \"Dashboards\", active.DashboardNames.Count, \"dashboard\")");
        markup.ShouldContain("StructureMenuAriaLabel(active, \"Metrics\", active.MetricNames.Count, \"metric\")");
        markup.ShouldContain("StructureMenuAriaLabel(active, \"Tests\", active.TestNames.Count, \"test\")");
        markup.ShouldContain("private static string StructureMenuAriaLabel(FlowWorkspaceService app, string sectionLabel, int count, string singular)");
        markup.ShouldContain("$\"{app.Name} {sectionLabel.ToLowerInvariant()} menu, {CountLabel(count, singular)}\"");
        (markup.Split("AriaLabel=\"@StructureMenuAriaLabel", StringSplitOptions.None).Length - 1).ShouldBe(5);
        markup.ShouldNotContain("AriaLabel=\"Brokers\"");
        markup.ShouldNotContain("AriaLabel=\"Pipelines\"");
        markup.ShouldNotContain("AriaLabel=\"Dashboards\"");
        markup.ShouldNotContain("AriaLabel=\"Metrics\"");
        markup.ShouldNotContain("AriaLabel=\"Tests\"");
        markup.ShouldContain("<span class=\"app-structure-name\">@active.Name</span>");
        (markup.Split("Modal=\"false\"", StringSplitOptions.None).Length - 1).ShouldBe(5);
        markup.ShouldContain("app-menu-artifact-row");
        markup.ShouldContain("app-menu-artifact-name");
        markup.ShouldContain("app-menu-empty");
        markup.ShouldContain("app-structure-empty\" role=\"status\" aria-live=\"polite\"");
        markup.ShouldContain("app-menu-empty-row");
        markup.ShouldContain("app-menu-empty-row\" role=\"status\" aria-live=\"polite\"");
        markup.ShouldContain("app-menu-empty-icon");
        markup.ShouldContain("app-menu-empty-copy");
        markup.ShouldContain("app-menu-empty-text");
        markup.ShouldContain("app-menu-command-item");
        markup.ShouldContain("app-menu-command-row");
        markup.ShouldContain("app-menu-command-icon");
        markup.ShouldContain("app-menu-command-copy");
        markup.ShouldContain("app-menu-command-cue");
        markup.ShouldContain("MenuEmptyRow(");
        markup.ShouldContain("MenuCommandRow(");
        markup.ShouldContain("private static RenderFragment MenuEmptyRow");
        markup.ShouldContain("private static RenderFragment MenuCommandRow");
        markup.ShouldContain("AddCommandLabel(active, \"broker connection\")");
        markup.ShouldContain("AddCommandLabel(active, \"pipeline\")");
        markup.ShouldContain("AddCommandLabel(active, \"dashboard\")");
        markup.ShouldContain("AddCommandLabel(active, \"metric\")");
        markup.ShouldContain("AddCommandLabel(active, \"test\")");
        markup.ShouldContain("private static string AddCommandLabel(FlowWorkspaceService app, string artifactLabel)");
        markup.ShouldContain("$\"Add {artifactLabel} to {app.Name}\"");
        (markup.Split("aria-label=\"@AddCommandLabel", StringSplitOptions.None).Length - 1).ShouldBe(5);
        (markup.Split("title=\"@AddCommandLabel", StringSplitOptions.None).Length - 1).ShouldBe(5);
        markup.ShouldNotContain("aria-label=\"Add connection\"");
        markup.ShouldNotContain("aria-label=\"Add pipeline\"");
        markup.ShouldNotContain("aria-label=\"Add dashboard\"");
        markup.ShouldNotContain("aria-label=\"Add metric\"");
        markup.ShouldNotContain("aria-label=\"Add test\"");
        markup.ShouldContain("app-menu-broker-item");
        markup.ShouldContain("app-menu-broker-row");
        markup.ShouldContain("app-menu-broker-icon-frame");
        markup.ShouldContain("app-menu-broker-copy");
        markup.ShouldContain("app-menu-broker-connection");
        markup.ShouldContain("BrokerEndpointLabel(c)");
        markup.ShouldContain("BrokerItemTitle(c)");
        markup.ShouldContain("BrokerRowClass(c)");
        markup.ShouldContain("BrokerConnectionClass(c)");
        markup.ShouldContain("BrokerConnectionText(c)");
        markup.ShouldContain("private static string BrokerEndpointLabel");
        markup.ShouldContain("private static string BrokerConnectionText");
        markup.ShouldContain("Class=\"@ArtifactMenuItemClass(active, WorkspaceArtifactKind.Pipeline, w)\"");
        markup.ShouldContain("aria-current=\"@ArtifactMenuItemCurrent(active, WorkspaceArtifactKind.Pipeline, w)\"");
        markup.ShouldContain("Class=\"@ArtifactMenuItemClass(active, WorkspaceArtifactKind.Dashboard, d)\"");
        markup.ShouldContain("aria-current=\"@ArtifactMenuItemCurrent(active, WorkspaceArtifactKind.Dashboard, d)\"");
        markup.ShouldContain("Class=\"@ArtifactMenuItemClass(active, WorkspaceArtifactKind.Metrics, \"Metrics\")\"");
        markup.ShouldContain("aria-current=\"@ArtifactMenuItemCurrent(active, WorkspaceArtifactKind.Metrics, \"Metrics\")\"");
        markup.ShouldContain("title=\"@MetricDesignerItemLabel(active)\"");
        markup.ShouldContain("aria-label=\"@MetricDesignerItemLabel(active)\"");
        markup.ShouldContain("private static string MetricDesignerItemLabel(FlowWorkspaceService app)");
        markup.ShouldContain("$\"Open {app.Name} metric designer, {CountLabel(app.MetricNames.Count, \"metric\")}\"");
        markup.ShouldNotContain("title=\"Open metric designer\"");
        markup.ShouldNotContain("aria-label=\"Open metric designer\"");
        markup.ShouldContain("app-menu-artifact-item");
        markup.ShouldContain("app-menu-compact-artifact-row");
        markup.ShouldContain("app-menu-artifact-icon-frame pipeline");
        markup.ShouldContain("app-menu-artifact-icon-frame dashboard");
        markup.ShouldContain("app-menu-artifact-icon-frame metrics");
        markup.ShouldContain("app-menu-metrics-row");
        markup.ShouldContain("app-menu-artifact-action");
        markup.ShouldContain("@CountLabel(active.MetricNames.Count, \"metric\")");
        markup.ShouldContain("private string ArtifactMenuItemClass");
        markup.ShouldContain("Broker profile");
        markup.ShouldContain("Processing flow");
        markup.ShouldContain("Metric view");
        markup.ShouldContain("Dashboard signal");
        markup.ShouldContain("Scenario check");
        markup.ShouldContain("app-menu-test-row");
        markup.ShouldContain("Class=\"@TestArtifactItemClass(active, t, latestTestRun)\"");
        markup.ShouldContain("title=\"@TestArtifactTitle(t, latestTestRun)\"");
        markup.ShouldContain("app-menu-test-icon-frame");
        markup.ShouldContain("app-menu-artifact-copy");
        markup.ShouldContain("app-menu-artifact-meta");
        markup.ShouldContain("TestRunMenuMeta(latestTestRun)");
        markup.ShouldContain("TestRunMenuRowClass(latestTestRun)");
        markup.ShouldContain("TestRunMenuSummaryClass(latestTestRun)");
        markup.ShouldContain("TestRunMenuSummaryLabel(latestTestRun)");
        markup.ShouldContain("TestRunMenuDetailText(latestTestRun)");
        markup.ShouldContain("TestRunMenuMarkerClass(latestTestRun)");
        markup.ShouldContain("app-menu-inline-action");
        markup.ShouldContain("app-menu-delete-button");
        markup.ShouldContain("aria-label=\"@DeleteLabel(");
        markup.ShouldContain("Text=\"@DeleteLabel(\"pipeline\", w)\"");
        markup.ShouldContain("Text=\"@DeleteLabel(\"dashboard\", d)\"");
        markup.ShouldContain("Text=\"@DeleteLabel(\"test\", t)\"");
        markup.ShouldContain("aria-current=\"@ArtifactMenuItemCurrent(active, WorkspaceArtifactKind.Test, t)\"");
        markup.ShouldContain("@onclick:stopPropagation=\"true\"");
        markup.ShouldContain("@onmousedown:stopPropagation=\"true\"");
        markup.ShouldContain("private static string DeleteLabel");
        markup.ShouldContain("private static ScenarioRunResult? LatestTestRun");
        markup.ShouldContain("private string TestArtifactItemClass");
        markup.ShouldContain("private string? ArtifactMenuItemCurrent");
        markup.ShouldContain("private bool IsArtifactActive");
        markup.ShouldContain("private static string TestRunMenuIssueText");
        markup.ShouldContain("No run yet");
        markup.ShouldContain("No run");
        markup.ShouldContain("No history");
        markup.ShouldNotContain("app-structure-current");
        markup.ShouldNotContain("CurrentArtifactLabel");
        markup.ShouldNotContain("app-structure-meta");
        markup.ShouldNotContain("BuildActiveMeta");
        markup.ShouldNotContain("StructureMenuClass");
        markup.ShouldNotContain("app-structure-menu active");
        markup.ShouldNotContain("app-menu-danger");
        markup.ShouldNotContain("Class=\"app-menu-muted\">No");
        markup.ShouldNotContain("Text=\"Delete pipeline\"");
        markup.ShouldNotContain("Text=\"Delete dashboard\"");
        markup.ShouldNotContain("Text=\"Delete test\"");
        css.ShouldNotContain(".app-structure-current");
        css.ShouldNotContain(".app-structure-meta");
        css.ShouldNotContain(".app-structure-menu.active");
        markup.ShouldNotContain("<MudMenuItem Icon=\"@BrokerActionIcon(c)\"");
        markup.ShouldNotContain("<MudMenuItem Icon=\"@Icons.Material.Filled.Timeline\"");
        markup.ShouldNotContain("<MudMenuItem Icon=\"@Icons.Material.Filled.Dashboard\"");
        markup.ShouldNotContain("<MudMenuItem Icon=\"@Icons.Material.Filled.QueryStats\"");
        markup.ShouldNotContain("Delete @w");
        markup.ShouldNotContain("Delete @d");
        markup.ShouldNotContain("Delete @t");
        markup.ShouldNotContain("app-menu-state-row");
        markup.ShouldNotContain("app-menu-state-icon");
        markup.ShouldNotContain("app-menu-state-copy");
        markup.ShouldNotContain("app-menu-state-text");
        markup.ShouldNotContain("MenuStateRow");
        markup.ShouldNotContain("app-menu-state-token");
        markup.ShouldNotContain("app-menu-broker-state");
        markup.ShouldNotContain("app-menu-artifact-state");
        markup.ShouldNotContain("app-menu-test-state");
        markup.ShouldNotContain("BrokerStateClass");
        markup.ShouldNotContain("BrokerStateText");
        markup.ShouldNotContain("TestRunMenuStateClass");
        markup.ShouldNotContain("TestRunMenuStateLabel");
        markup.ShouldNotContain("app-menu-broker-pill");
        markup.ShouldNotContain("app-menu-artifact-token");
        markup.ShouldNotContain("app-menu-test-pill");
        markup.ShouldNotContain("Ready for first run");

        css.ShouldContain("height: 28px;");
        css.ShouldContain("max-width: 132px;");
        css.ShouldContain(".app-structure-app {");
        css.ShouldContain(".app-structure-empty {");
        css.ShouldContain("color: inherit;");
        css.ShouldContain("font-size: inherit;");
        css.ShouldContain("font-weight: inherit;");
        css.ShouldContain("padding: 0 2px;");
        css.ShouldNotContain("border: 1px solid var(--flux-border);");
        css.ShouldNotContain("border-radius: 7px;");
        css.ShouldNotContain("height: 30px;");
        css.ShouldNotContain("padding: 0 7px;");
        css.ShouldNotContain("font-size: 11.5px;");
        css.ShouldNotContain(".app-structure-app,\r\n.app-structure-empty");
        css.ShouldNotContain(".app-structure-app,\n.app-structure-empty");
        css.ShouldContain(".app-menu-empty-row,");
        css.ShouldContain(".app-menu-command-row");
        css.ShouldContain("grid-template-columns: 22px minmax(0, 1fr) auto;");
        css.ShouldContain(".app-menu-empty-icon,");
        css.ShouldContain(".app-menu-command-icon");
        css.ShouldContain(".app-menu-empty-copy,");
        css.ShouldContain(".app-menu-command-copy");
        css.ShouldContain(".app-menu-empty-text");
        css.ShouldContain(".app-menu-command-cue");
        css.ShouldContain(".app-menu-broker-row");
        css.ShouldContain(".app-menu-broker-icon-frame");
        css.ShouldContain(".app-menu-broker-copy");
        css.ShouldContain(".app-menu-broker-connection");
        css.ShouldContain(".app-menu-broker-connection.live");
        css.ShouldContain(".app-menu-broker-connection.faulted");
        css.ShouldContain(".app-menu-broker-connection.pending");
        css.ShouldContain(".app-menu-artifact-row");
        css.ShouldContain("grid-template-columns: minmax(0, 1fr) 24px;");
        css.ShouldContain(".app-menu-compact-artifact-row");
        css.ShouldContain("grid-template-columns: 22px minmax(0, 1fr) 24px;");
        css.ShouldContain(".app-menu-metrics-row");
        css.ShouldContain(".app-menu-artifact-icon-frame");
        css.ShouldContain(".app-menu-artifact-icon-frame.pipeline");
        css.ShouldContain(".app-menu-artifact-icon-frame.dashboard");
        css.ShouldContain(".app-menu-artifact-icon-frame.metrics");
        css.ShouldContain(".app-menu-artifact-action");
        css.ShouldContain(".app-menu-test-row");
        css.ShouldContain("grid-template-columns: 22px minmax(0, 1fr) minmax(94px, auto) 24px;");
        css.ShouldContain(".app-menu-test-icon-frame");
        css.ShouldContain(".app-menu-test-row.canceled .app-menu-test-icon-frame");
        css.ShouldContain(".app-menu-artifact-meta");
        css.ShouldContain(".app-menu-test-summary");
        css.ShouldContain(".app-menu-test-summary-meta");
        css.ShouldContain(".app-menu-test-marker");
        css.ShouldContain("max-width: 92px;");
        css.ShouldContain(".app-menu-inline-action");
        css.ShouldContain("opacity: 0;");
        css.ShouldContain(".app-menu-child:hover .app-menu-inline-action");
        css.ShouldContain(".app-menu-inline-action ::deep .app-menu-delete-button");
        css.ShouldContain("height: 24px;");
        css.ShouldNotContain(".app-structure-menu ::deep .app-menu-danger");
        css.ShouldNotContain(".app-menu-state-row");
        css.ShouldNotContain(".app-menu-state-icon");
        css.ShouldNotContain(".app-menu-state-copy");
        css.ShouldNotContain(".app-menu-state-text");
        css.ShouldNotContain(".app-menu-state-token");
        css.ShouldNotContain(".app-menu-broker-state");
        css.ShouldNotContain(".app-menu-artifact-state");
        css.ShouldNotContain(".app-menu-test-state");
        css.ShouldNotContain(".app-menu-broker-pill");
        css.ShouldNotContain(".app-menu-artifact-token");
        css.ShouldNotContain(".app-menu-test-pill");
        css.ShouldNotContain("border-radius: 999px;");

        appCss.ShouldContain(".app-structure-popover .app-menu-test-item");
        appCss.ShouldContain(".app-structure-popover .app-menu-command-item");
        appCss.ShouldContain(".app-structure-popover .app-menu-artifact-item");
        appCss.ShouldContain(".app-structure-popover .app-menu-broker-item");
        appCss.ShouldContain(".app-structure-popover .app-menu-empty");
        appCss.ShouldContain("padding-left: 6px;");
        appCss.ShouldContain("z-index: 1250 !important;");
        appCss.ShouldContain("border: 0;");
        appCss.ShouldContain("box-shadow: 0 12px 28px -22px rgba(0, 0, 0, 0.78);");
        appCss.ShouldContain(".flux-theme-dark .mud-overlay.mud-overlay-dialog .mud-overlay-scrim.mud-overlay-dark");
        appCss.ShouldContain(".flux-theme-light .mud-overlay.mud-overlay-dialog .mud-overlay-scrim.mud-overlay-light");
        appCss.ShouldContain(".flux-theme-dark .mud-overlay.mud-overlay-popover,");
        appCss.ShouldContain(".flux-theme-light .mud-overlay.mud-overlay-popover .mud-overlay-scrim");
        appCss.ShouldContain("background-color: transparent !important;");
        appCss.ShouldContain(".flux-theme-scope .mud-dialog .mud-dialog-actions");
        appCss.ShouldContain("border: 1px solid var(--flux-border-soft);");
        appCss.ShouldContain("margin: 0 20px 18px;");
        appCss.ShouldContain("padding: 12px;");
        appCss.ShouldContain("width: auto;");
        appCss.ShouldContain(".flux-theme-scope .mud-dialog .mud-dialog-actions > *");
        appCss.ShouldContain(".flux-theme-scope .mud-dialog .mud-dialog-title .mud-button-close");
        appCss.ShouldContain(".flux-theme-scope .mud-dialog .mud-dialog-title .mud-button-close:hover");
        appCss.ShouldContain(".mud-dialog.dashboard-query-dialog-modal .mud-dialog-actions");
        appCss.ShouldNotContain("padding: 14px 20px 18px;");
        appCss.ShouldNotContain("border-top: 1px solid var(--flux-border, #1d232e);");
        appCss.ShouldNotContain(".flux-theme-dark .mud-overlay {");
        appCss.ShouldNotContain(".flux-theme-light .mud-overlay {");
        appCss.ShouldContain("max-height: min(56vh, 380px);");
        appCss.ShouldContain("min-width: 224px;");
        appCss.ShouldContain("overflow-y: auto;");
        appCss.ShouldNotContain("z-index: 1600 !important;");
        appCss.ShouldNotContain("border: 1px solid var(--flux-popover-border);");
        appCss.ShouldNotContain("min-width: 280px;");
        appCss.ShouldNotContain("box-shadow: 0 18px 44px");
        appCss.ShouldNotContain("box-shadow: 0 10px 24px -18px");
        appCss.ShouldNotContain(".broker-live .mud-list-item-icon");
    }

    [Fact]
    public void AppsPanel_UsesFlatCompactOpenAppRows()
    {
        var root = FindRepositoryRoot();
        var markup = File.ReadAllText(Path.Combine(
            root,
            "src",
            "FluxMq.UI",
            "Components",
            "Workspace",
            "AppsPanel.razor"));
        var css = File.ReadAllText(Path.Combine(
            root,
            "src",
            "FluxMq.UI",
            "Components",
            "Workspace",
            "AppsPanel.razor.css"));

        markup.ShouldContain("aria-label=\"@AppsPanelLabel\"");
        markup.ShouldContain("private string AppsPanelLabel => $\"Open apps, {ProjectCountLabel}\";");
        markup.ShouldNotContain("aria-label=\"Open apps panel\"");
        markup.ShouldContain("apps-panel-title-icon");
        System.Text.RegularExpressions.Regex.Matches(
                markup,
                @"<MudIcon\b(?:(?!/>).)*?/>",
                System.Text.RegularExpressions.RegexOptions.Singleline)
            .Cast<System.Text.RegularExpressions.Match>()
            .Select(static match => match.Value)
            .Where(static icon => !icon.Contains("aria-hidden=\"true\"", StringComparison.Ordinal))
            .ToArray()
            .ShouldBeEmpty();
        markup.ShouldContain("<strong>Open Apps</strong>");
        markup.ShouldContain("@ProjectCountLabel");
        markup.ShouldContain("apps-empty\" role=\"status\" aria-live=\"polite\"");
        markup.ShouldContain("apps-empty-title");
        markup.ShouldContain("role=\"list\"");
        markup.ShouldContain("role=\"button\"");
        markup.ShouldContain("tabindex=\"0\"");
        markup.ShouldContain("aria-label=\"@AppTileLabel(a, isActive)\"");
        markup.ShouldContain("aria-current=\"@AppTileCurrent(isActive)\"");
        markup.ShouldContain("aria-keyshortcuts=\"Enter Space\"");
        markup.ShouldContain("title=\"@AppTileLabel(a, isActive)\"");
        markup.ShouldContain("SelectAppFromKeyboard(args, a)");
        markup.ShouldContain("private static bool IsActivationKey");
        markup.ShouldContain("\"Spacebar\"");
        markup.ShouldContain("app-tile-meta");
        markup.ShouldContain("app-tile-markers");
        markup.ShouldContain("app-marker active");
        markup.ShouldContain("app-marker unsaved");
        markup.ShouldNotContain("app-tile-states");
        markup.ShouldNotContain("app-state active");
        markup.ShouldNotContain("app-state unsaved");
        markup.ShouldContain("Text=\"@CloseLabel(a)\"");
        markup.ShouldContain("aria-label=\"@CloseLabel(a)\"");
        markup.ShouldNotContain("Text=\"Close app\"");
        markup.ShouldContain("private static string? AppTileCurrent");
        markup.ShouldContain("private static string BuildAppMeta");
        markup.ShouldContain("private static string FileLabel");
        markup.ShouldContain("private static string AppTileLabel");
        markup.ShouldNotContain("MudList");
        markup.ShouldNotContain("MudListItem");
        markup.ShouldNotContain("flux-app-item");

        css.ShouldContain(".apps-panel");
        css.ShouldContain("background: var(--flux-canvas);");
        css.ShouldContain("border-bottom: 1px solid var(--flux-border-soft);");
        css.ShouldContain(".apps-list");
        css.ShouldContain("grid-template-columns: minmax(0, min(280px, 100%));");
        css.ShouldContain("flex: 1 1 auto;");
        css.ShouldContain("justify-items: center;");
        css.ShouldContain("text-align: center;");
        css.ShouldContain("grid-template-columns: minmax(0, 1fr) 26px;");
        css.ShouldContain("min-height: 50px;");
        css.ShouldContain(".app-tile-main");
        css.ShouldContain("grid-template-columns: 22px minmax(0, 1fr) 64px;");
        css.ShouldContain(".app-tile-main:focus-visible");
        css.ShouldContain("inset 0 0 0 1px color-mix(in srgb, var(--flux-accent) 40%, var(--flux-border))");
        css.ShouldContain(".app-tile-close");
        css.ShouldContain("opacity: 0.58;");
        css.ShouldContain(".app-tile:hover .app-tile-close");
        css.ShouldContain(".app-tile-markers");
        css.ShouldContain(".app-marker.unsaved");
        css.ShouldNotContain(".app-tile-states");
        css.ShouldNotContain(".app-state");
        css.ShouldContain("max-width: 64px;");
        css.ShouldContain("overflow-wrap: anywhere;");
        css.ShouldContain("@media (max-width: 760px)");
        css.ShouldNotContain(".flux-app-item");
    }

    [Fact]
    public void ConnectionPanel_UsesFlatCompactConnectionRows()
    {
        var root = FindRepositoryRoot();
        var markup = File.ReadAllText(Path.Combine(
            root,
            "src",
            "FluxMq.UI",
            "Components",
            "Workspace",
            "ConnectionPanel.razor"));
        var css = File.ReadAllText(Path.Combine(
            root,
            "src",
            "FluxMq.UI",
            "Components",
            "Workspace",
            "ConnectionPanel.razor.css"));

        markup.ShouldContain("aria-label=\"@ConnectionsPanelLabel\"");
        markup.ShouldContain("private string ConnectionsPanelLabel => $\"Connections, {ConnectionCountLabel}\";");
        markup.ShouldNotContain("aria-label=\"Connections panel\"");
        markup.ShouldContain("Text=\"@AddConnectionLabel\"");
        markup.ShouldContain("aria-label=\"@AddConnectionLabel\"");
        markup.ShouldContain("private string AddConnectionLabel => VisibleConnections.Count == 0");
        markup.ShouldContain("? \"Add first broker connection\"");
        markup.ShouldContain(": $\"Add broker connection, {ConnectionCountLabel} configured\"");
        markup.ShouldNotContain("Text=\"Add connection\"");
        markup.ShouldNotContain("aria-label=\"Add connection\"");
        markup.ShouldContain("connections-title-icon");
        markup.ShouldContain("class=\"connections-title-icon\" aria-hidden=\"true\"");
        markup.ShouldContain("<MudIcon Icon=\"@Icons.Material.Filled.Cable\" Size=\"Size.Small\" aria-hidden=\"true\" />");
        markup.ShouldNotContain("<MudIcon Icon=\"@Icons.Material.Filled.Cable\" Size=\"Size.Small\" />");
        markup.ShouldContain("<strong>Connections</strong>");
        markup.ShouldContain("@ConnectionCountLabel");
        markup.ShouldContain("VisibleConnections");
        markup.ShouldContain("!LiveMqttWorkspaceService.IsTopicMonitorConnection(connection)");
        markup.ShouldContain("connections-empty\" role=\"status\" aria-live=\"polite\"");
        markup.ShouldContain("connections-empty-title");
        markup.ShouldContain("connections-list");
        markup.ShouldContain("role=\"list\"");
        markup.ShouldContain("aria-label=\"@ConnectionRowLabel(conn)\"");
        markup.ShouldContain("connection-name-line");
        markup.ShouldContain("connection-endpoint");
        markup.ShouldContain("connection-meta");
        markup.ShouldContain("connection-error");
        markup.ShouldContain("ToggleConnectionAsync(conn)");
        markup.ShouldContain("PrimaryActionIcon(conn)");
        markup.ShouldContain("PrimaryActionClass(conn)");
        markup.ShouldContain("ConnectionMarkerClass");
        markup.ShouldContain("ConnectionMarkerLabel");
        markup.ShouldContain("ConnectionDotClass");
        markup.ShouldContain("private static string ConnectionRowLabel");
        markup.ShouldContain("aria-label=\"@PrimaryActionLabel(conn)\"");
        markup.ShouldContain("Text=\"@RemoveLabel(conn)\"");
        markup.ShouldContain("aria-label=\"@RemoveLabel(conn)\"");
        markup.ShouldNotContain("Text=\"Remove connection\"");
        markup.ShouldNotContain("StateClass");
        markup.ShouldNotContain("StateLabel");
        markup.ShouldNotContain("StateDotClass");
        markup.ShouldNotContain("StatePillClass");
        markup.ShouldNotContain("MudTreeView");
        markup.ShouldNotContain("MudTreeViewItem");
        markup.ShouldNotContain("StateColor");
        markup.ShouldNotContain("SeverityFor");

        css.ShouldContain(".connections-panel");
        css.ShouldContain(".connections-list");
        css.ShouldContain(".connection-row");
        css.ShouldContain("border-bottom: 1px solid var(--flux-border-soft);");
        css.ShouldContain("grid-template-columns: minmax(0, min(280px, 100%));");
        css.ShouldContain("flex: 1 1 auto;");
        css.ShouldContain("justify-items: center;");
        css.ShouldContain("text-align: center;");
        css.ShouldContain("grid-template-columns: 8px minmax(0, 1fr) 52px;");
        css.ShouldContain("min-height: 54px;");
        css.ShouldContain("opacity: 0.62;");
        css.ShouldContain("overflow-wrap: anywhere;");
        css.ShouldContain("white-space: normal;");
        css.ShouldContain(".connection-marker.live");
        css.ShouldContain(".connection-marker.pending");
        css.ShouldContain(".connection-marker.faulted");
        css.ShouldNotContain(".connection-state");
        css.ShouldContain(".connection-row:hover .connection-actions");
        css.ShouldContain(".connection-row:focus-within");
        css.ShouldContain("inset 2px 0 0 var(--mud-palette-info)");
        css.ShouldContain(".connection-actions ::deep .connection-action-button.connect");
        css.ShouldContain(".connection-actions ::deep .connection-remove-button");
        css.ShouldContain("@media (max-width: 760px)");
    }

    [Fact]
    public void SessionPanel_UsesFlatGroupedSessionRows()
    {
        var root = FindRepositoryRoot();
        var markup = File.ReadAllText(Path.Combine(
            root,
            "src",
            "FluxMq.UI",
            "Components",
            "Workspace",
            "SessionPanel.razor"));
        var css = File.ReadAllText(Path.Combine(
            root,
            "src",
            "FluxMq.UI",
            "Components",
            "Workspace",
            "SessionPanel.razor.css"));

        markup.ShouldContain("aria-label=\"@SessionsPanelLabel\"");
        markup.ShouldContain("private string SessionsPanelLabel => Live.IsRecording");
        markup.ShouldContain("$\"Recordings, {SessionCountLabel}, recording {RecordingName}\"");
        markup.ShouldContain("$\"Recordings, {SessionCountLabel}\"");
        markup.ShouldNotContain("aria-label=\"Recorded sessions panel\"");
        markup.ShouldContain("session-recording-strip");
        markup.ShouldContain("session-recording-strip\" role=\"status\" aria-live=\"polite\"");
        markup.ShouldContain("<MudIcon Icon=\"@Icons.Material.Filled.FiberManualRecord\" Size=\"Size.Small\" aria-hidden=\"true\" />");
        markup.ShouldNotContain("<MudIcon Icon=\"@Icons.Material.Filled.FiberManualRecord\" Size=\"Size.Small\" />");
        markup.ShouldContain("Text=\"@StopRecordingLabel\"");
        markup.ShouldContain("aria-label=\"@StopRecordingLabel\"");
        markup.ShouldContain("private string StopRecordingLabel => $\"Stop recording {RecordingName}\"");
        markup.ShouldNotContain("Text=\"Stop recording\"");
        markup.ShouldNotContain("aria-label=\"Stop recording\"");
        markup.ShouldContain("sessions-title-icon");
        markup.ShouldContain("class=\"sessions-title-icon\" aria-hidden=\"true\"");
        markup.ShouldContain("<MudIcon Icon=\"@Icons.Material.Filled.History\" Size=\"Size.Small\" aria-hidden=\"true\" />");
        markup.ShouldNotContain("<MudIcon Icon=\"@Icons.Material.Filled.History\" Size=\"Size.Small\" />");
        markup.ShouldContain("<strong>Recordings</strong>");
        markup.ShouldContain("@SessionCountLabel");
        markup.ShouldContain("FilteredSessionCount");
        markup.ShouldContain("role=\"search\" aria-label=\"@SessionsToolbarLabel\"");
        markup.ShouldContain("private string SessionsToolbarLabel => HasSearch");
        markup.ShouldContain("$\"Recording filters, {FilteredSessionCount} of {AllSessions.Count} shown\"");
        markup.ShouldContain("aria-label=\"@SessionSearchLabel\"");
        markup.ShouldContain("private string SessionSearchLabel => AllSessions.Count switch");
        markup.ShouldContain("$\"Search recorded sessions, {FilteredSessionCount} of {AllSessions.Count} shown\"");
        markup.ShouldNotContain("role=\"search\" aria-label=\"Session filters\"");
        markup.ShouldNotContain("aria-label=\"Search recorded sessions\"");
        markup.ShouldContain("Search sessions");
        markup.ShouldContain("session-live-strip");
        markup.ShouldContain("<MudIcon Icon=\"@Icons.Material.Filled.Inventory2\" Size=\"Size.Small\" aria-hidden=\"true\" />");
        markup.ShouldNotContain("<MudIcon Icon=\"@Icons.Material.Filled.Inventory2\" Size=\"Size.Small\" />");
        markup.ShouldContain("Text=\"@SwitchToLiveTrafficLabel\"");
        markup.ShouldContain("aria-label=\"@SwitchToLiveTrafficLabel\"");
        markup.ShouldContain("private string SwitchToLiveTrafficLabel => Live.SelectedStoredSession is { } session");
        markup.ShouldContain("$\"Switch from {session.Name} to live traffic\"");
        markup.ShouldNotContain("Text=\"Switch to live traffic\"");
        markup.ShouldNotContain("aria-label=\"Switch to live traffic\"");
        markup.ShouldContain("Class=\"session-live-button\"");
        markup.ShouldContain("OnClick=\"@Live.ClearStoredSessionSelection\"");
        markup.ShouldContain("Icon=\"@Icons.Material.Filled.Sensors\"");
        markup.ShouldNotContain("@onclick=\"@Live.ClearStoredSessionSelection\"");
        markup.ShouldContain("sessions-empty\" role=\"status\" aria-live=\"polite\"");
        markup.ShouldContain("<MudIcon Icon=\"@EmptyIcon\" Size=\"Size.Medium\" aria-hidden=\"true\" />");
        markup.ShouldNotContain("<MudIcon Icon=\"@EmptyIcon\" Size=\"Size.Medium\" />");
        markup.ShouldContain("sessions-empty-title");
        markup.ShouldContain("sessions-list");
        markup.ShouldContain("role=\"list\"");
        markup.ShouldContain("session-project-group");
        markup.ShouldContain("session-project-head");
        markup.ShouldContain("SessionRowClass(session)");
        markup.ShouldContain("SessionDotClass(session)");
        markup.ShouldContain("SessionMarkerClass(session)");
        markup.ShouldNotContain("SessionStateClass(session)");
        markup.ShouldContain("SessionRowLabel(session)");
        markup.ShouldContain("session-row-name-line");
        markup.ShouldContain("DurationLabel(session)");
        markup.ShouldContain("StartedLabel(session)");
        markup.ShouldContain("SelectSessionAsync(session)");
        markup.ShouldNotContain("session-recording-pulse");
        markup.ShouldNotContain("session-row-side");
        markup.ShouldNotContain("session-row-time");
        markup.ShouldNotContain("MudTreeView");
        markup.ShouldNotContain("MudTreeViewItem");
        markup.ShouldNotContain("px-2");
        markup.ShouldNotContain("pt-1");

        css.ShouldContain(".sessions-panel");
        css.ShouldContain(".session-recording-strip");
        css.ShouldContain(".sessions-list");
        css.ShouldContain(".session-project-group");
        css.ShouldContain(".session-row");
        css.ShouldContain("grid-template-columns: 7px minmax(0, 1fr);");
        css.ShouldContain("min-height: 44px;");
        css.ShouldContain(".session-row.selected");
        css.ShouldContain(".session-row.recording");
        css.ShouldContain(".session-row:focus-visible");
        css.ShouldContain(".session-row.selected:focus-visible");
        css.ShouldContain("inset 2px 0 0 var(--flux-accent)");
        css.ShouldContain("flex-wrap: nowrap;");
        css.ShouldContain(".session-row-name-line");
        css.ShouldContain(".session-row-meta span:first-child");
        css.ShouldContain(".session-row-meta span:not(:last-child)::after");
        css.ShouldContain("max-width: 100%;");
        css.ShouldContain("text-overflow: ellipsis;");
        css.ShouldContain("grid-template-columns: minmax(0, min(300px, 100%));");
        css.ShouldContain("flex: 1 1 auto;");
        css.ShouldContain("justify-items: center;");
        css.ShouldContain("text-align: center;");
        css.ShouldContain("overflow-wrap: anywhere;");
        css.ShouldContain(".session-marker.selected");
        css.ShouldContain(".session-marker.recording");
        css.ShouldContain(".session-live-strip ::deep .session-live-button");
        css.ShouldContain(".session-live-strip ::deep .session-live-button .mud-icon-root");
        css.ShouldNotContain(".session-live-strip button");
        css.ShouldNotContain(".session-state");
        css.ShouldContain(".session-search ::deep .mud-input-outlined-border");
        css.ShouldContain("@media (max-width: 760px)");
        css.ShouldNotContain(".session-recording-pulse");
        css.ShouldNotContain(".session-row-side");
        css.ShouldNotContain(".session-row-time");
        css.ShouldNotContain("border-radius: 999px;");
        css.ShouldNotContain("box-shadow: 0 0 0");
    }

    [Fact]
    public void WorkspaceFocusableCustomElements_ExposeRoles()
    {
        var root = FindRepositoryRoot();
        var componentsRoot = Path.Combine(root, "src", "FluxMq.UI", "Components");
        var nativeFocusTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "a",
            "button",
            "input",
            "select",
            "textarea"
        };
        var violations = new List<string>();

        foreach (var file in Directory.EnumerateFiles(componentsRoot, "*.razor", SearchOption.AllDirectories))
        {
            var markup = File.ReadAllText(file);
            foreach (System.Text.RegularExpressions.Match match in System.Text.RegularExpressions.Regex.Matches(
                markup,
                @"<[A-Za-z][A-Za-z0-9:.]*\b[^>]*\btabindex\s*=\s*""0""[^>]*>",
                System.Text.RegularExpressions.RegexOptions.Singleline))
            {
                var tag = match.Value;
                var tagNameMatch = System.Text.RegularExpressions.Regex.Match(tag, @"^<([A-Za-z][A-Za-z0-9:.]*)");
                if (!tagNameMatch.Success || nativeFocusTags.Contains(tagNameMatch.Groups[1].Value))
                {
                    continue;
                }

                if (System.Text.RegularExpressions.Regex.IsMatch(tag, @"\brole\s*="))
                {
                    continue;
                }

                var line = markup.Take(match.Index).Count(static value => value == '\n') + 1;
                violations.Add($"{Path.GetRelativePath(root, file)}:{line}: {tag.ReplaceLineEndings(" ").Trim()}");
            }
        }

        violations.ShouldBeEmpty();
    }

    [Fact]
    public void WorkspaceExpandedElements_ReferenceControlledContent()
    {
        var root = FindRepositoryRoot();
        var componentsRoot = Path.Combine(root, "src", "FluxMq.UI", "Components");
        var violations = new List<string>();

        foreach (var file in Directory.EnumerateFiles(componentsRoot, "*.razor", SearchOption.AllDirectories))
        {
            var markup = File.ReadAllText(file);
            foreach (System.Text.RegularExpressions.Match match in System.Text.RegularExpressions.Regex.Matches(
                markup,
                @"<[A-Za-z][A-Za-z0-9:.]*\b[^>]*\baria-expanded\s*=\s*""[^""]*""[^>]*>",
                System.Text.RegularExpressions.RegexOptions.Singleline))
            {
                var tag = match.Value;
                if (System.Text.RegularExpressions.Regex.IsMatch(tag, @"\baria-controls\s*="))
                {
                    continue;
                }

                var line = markup.Take(match.Index).Count(static value => value == '\n') + 1;
                violations.Add($"{Path.GetRelativePath(root, file)}:{line}: {tag.ReplaceLineEndings(" ").Trim()}");
            }
        }

        violations.ShouldBeEmpty();
    }

    [Fact]
    public void WorkspaceGroups_ExposeAccessibleNames()
    {
        var root = FindRepositoryRoot();
        var componentsRoot = Path.Combine(root, "src", "FluxMq.UI", "Components");
        var violations = new List<string>();

        foreach (var file in Directory.EnumerateFiles(componentsRoot, "*.razor", SearchOption.AllDirectories))
        {
            var markup = File.ReadAllText(file);
            foreach (System.Text.RegularExpressions.Match match in System.Text.RegularExpressions.Regex.Matches(
                markup,
                @"<[A-Za-z][A-Za-z0-9:.]*\b[^>]*\brole\s*=\s*""group""[^>]*>",
                System.Text.RegularExpressions.RegexOptions.Singleline))
            {
                var tag = match.Value;
                if (System.Text.RegularExpressions.Regex.IsMatch(tag, @"\baria-label\s*=") ||
                    System.Text.RegularExpressions.Regex.IsMatch(tag, @"\baria-labelledby\s*="))
                {
                    continue;
                }

                var line = markup.Take(match.Index).Count(static value => value == '\n') + 1;
                violations.Add($"{Path.GetRelativePath(root, file)}:{line}: {tag.ReplaceLineEndings(" ").Trim()}");
            }
        }

        violations.ShouldBeEmpty();
    }

    [Fact]
    public void WorkspaceTreeItems_ExposeNavigationSemantics()
    {
        var root = FindRepositoryRoot();
        var componentsRoot = Path.Combine(root, "src", "FluxMq.UI", "Components");
        var violations = new List<string>();

        foreach (var file in Directory.EnumerateFiles(componentsRoot, "*.razor", SearchOption.AllDirectories))
        {
            var markup = File.ReadAllText(file);
            foreach (System.Text.RegularExpressions.Match match in System.Text.RegularExpressions.Regex.Matches(
                markup,
                @"<[A-Za-z][A-Za-z0-9:.]*\b[^>]*\brole\s*=\s*""treeitem""[^>]*>",
                System.Text.RegularExpressions.RegexOptions.Singleline))
            {
                var tag = match.Value;
                if ((System.Text.RegularExpressions.Regex.IsMatch(tag, @"\baria-label\s*=") ||
                        System.Text.RegularExpressions.Regex.IsMatch(tag, @"\baria-labelledby\s*=")) &&
                    System.Text.RegularExpressions.Regex.IsMatch(tag, @"\baria-level\s*=") &&
                    System.Text.RegularExpressions.Regex.IsMatch(tag, @"\baria-selected\s*=") &&
                    System.Text.RegularExpressions.Regex.IsMatch(tag, @"\btabindex\s*=") &&
                    System.Text.RegularExpressions.Regex.IsMatch(tag, @"\baria-keyshortcuts\s*=") &&
                    System.Text.RegularExpressions.Regex.IsMatch(tag, @"@onkeydown\s*="))
                {
                    continue;
                }

                var line = markup.Take(match.Index).Count(static value => value == '\n') + 1;
                violations.Add($"{Path.GetRelativePath(root, file)}:{line}: {tag.ReplaceLineEndings(" ").Trim()}");
            }
        }

        violations.ShouldBeEmpty();
    }

    [Fact]
    public void WorkspaceTabs_ExposeKeyboardNavigation()
    {
        var root = FindRepositoryRoot();
        var componentsRoot = Path.Combine(root, "src", "FluxMq.UI", "Components");
        var violations = new List<string>();

        foreach (var file in Directory.EnumerateFiles(componentsRoot, "*.razor", SearchOption.AllDirectories))
        {
            var lines = File.ReadAllLines(file);
            for (var index = 0; index < lines.Length; index++)
            {
                if (!lines[index].Contains("role=\"tab\"", StringComparison.Ordinal))
                {
                    continue;
                }

                var start = index;
                while (start > 0 && !lines[start].TrimStart().StartsWith("<", StringComparison.Ordinal))
                {
                    start--;
                }

                var end = index;
                while (end < lines.Length - 1 && !lines[end].TrimEnd().EndsWith(">", StringComparison.Ordinal))
                {
                    end++;
                }

                var tag = string.Join('\n', lines[start..(end + 1)]);
                if (tag.Contains("aria-controls=", StringComparison.Ordinal) &&
                    tag.Contains("aria-selected=", StringComparison.Ordinal) &&
                    tag.Contains("aria-keyshortcuts=\"Enter Space ArrowLeft ArrowRight Home End\"", StringComparison.Ordinal) &&
                    tag.Contains("@onkeydown=", StringComparison.Ordinal))
                {
                    continue;
                }

                violations.Add($"{Path.GetRelativePath(root, file)}:{start + 1}: {tag.ReplaceLineEndings(" ").Trim()}");
            }
        }

        violations.ShouldBeEmpty();
    }

    [Fact]
    public void WorkspaceCommandTitles_UseSpecificLabels()
    {
        var root = FindRepositoryRoot();
        var componentsRoot = Path.Combine(root, "src", "FluxMq.UI", "Components");
        var genericTitles = new[]
        {
            "Add",
            "Remove",
            "Reset",
            "Copy",
            "Export",
            "Open",
            "Close",
            "Delete",
            "Run",
            "Stop",
            "Save",
            "Apply",
            "Cancel",
            "Edit",
            "Start",
            "Clear",
            "Refresh",
            "Reload",
            "Search",
            "Move up",
            "Move down",
            "Edit widget settings",
            "Simulate widget data",
            "Duplicate widget",
            "Delete widget"
        };
        var violations = new List<string>();

        foreach (var file in Directory.EnumerateFiles(componentsRoot, "*.razor", SearchOption.AllDirectories))
        {
            var markup = File.ReadAllText(file);
            foreach (var title in genericTitles)
            {
                var pattern = $"title=\"{title}\"";
                var index = markup.IndexOf(pattern, StringComparison.Ordinal);
                while (index >= 0)
                {
                    var line = markup.Take(index).Count(static value => value == '\n') + 1;
                    violations.Add($"{Path.GetRelativePath(root, file)}:{line}: {pattern}");
                    index = markup.IndexOf(pattern, index + pattern.Length, StringComparison.Ordinal);
                }
            }
        }

        violations.ShouldBeEmpty();
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
        var configuredRoot = Environment.GetEnvironmentVariable("FLUXMQ_REPOSITORY_ROOT");
        if (!string.IsNullOrWhiteSpace(configuredRoot) &&
            File.Exists(Path.Combine(configuredRoot, "FluxMq.sln")))
        {
            return Path.GetFullPath(configuredRoot);
        }

        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "FluxMq.sln")))
        {
            current = current.Parent;
        }

        current.ShouldNotBeNull("Could not locate FluxMq.sln from the test output directory.");
        return current.FullName;
    }
}
