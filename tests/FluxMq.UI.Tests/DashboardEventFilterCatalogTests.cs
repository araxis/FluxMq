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
        css.ShouldContain("grid-template-columns: 34px minmax(0, 1fr) 28px;");
        css.ShouldContain("background-color: var(--property-grid-color-value);");
        css.ShouldContain("font-size: 17px;");
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
        var rowCss = File.ReadAllText(Path.Combine(
            root,
            "src",
            "FluxMq.UI",
            "Components",
            "Workspace",
            "PropertyGridRow.razor.css"));
        var selectCss = File.ReadAllText(Path.Combine(
            root,
            "src",
            "FluxMq.UI",
            "Components",
            "Workspace",
            "PropertyGridSelect.razor.css"));
        var inspectorCss = File.ReadAllText(Path.Combine(
            root,
            "src",
            "FluxMq.UI",
            "Components",
            "Workspace",
            "DashboardInspector.razor.css"));
        var visualMetricRows = File.ReadAllText(Path.Combine(
            root,
            "src",
            "FluxMq.UI",
            "Components",
            "Workspace",
            "DashboardInspectorVisualMetricRows.razor"));

        propertyGrid.ShouldContain("DefaultNameColumnWidth = 124");
        propertyGrid.ShouldContain("MinNameColumnWidth = 88");
        propertyGrid.ShouldContain("MaxNameColumnWidth = 190");
        propertyGrid.ShouldContain("--property-grid-name-width: min({_nameColumnWidth.ToString(\"0\", CultureInfo.InvariantCulture)}px, 42%);");
        propertyGridCss.ShouldContain("min-height: 27px;");
        rowCss.ShouldContain("min-height: 28px;");
        rowCss.ShouldContain("min-height: 24px;");
        selectCss.ShouldContain("max-height: 204px;");
        inspectorCss.ShouldContain("flex: 0 0 344px;");
        visualMetricRows.ShouldContain("KeyboardArrowUp");
        visualMetricRows.ShouldContain("KeyboardArrowDown");
        visualMetricRows.ShouldContain("Icons.Material.Filled.Close");
        visualMetricRows.ShouldContain("aria-label=\"@($\"Move {VisualMetricLabel(currentMetric)} up\")\"");
        visualMetricRows.ShouldContain("aria-label=\"Add metric card\"");
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
        css.ShouldContain("@container dashboard-layout (max-width: 540px)");
        css.ShouldContain("--dashboard-grid-column-min: 132px;");
        css.ShouldContain("--dashboard-grid-row-min: 128px;");
        css.ShouldContain("--dashboard-grid-column-min: 0px;");
        css.ShouldContain("--dashboard-grid-row-min: 112px;");
        css.ShouldContain("grid-template-columns: repeat(2, minmax(var(--dashboard-grid-column-min, 156px), 1fr)) !important;");
        css.ShouldContain("grid-column: span var(--dashboard-cell-tablet-span, 1) !important;");
        css.ShouldContain("grid-column: span var(--dashboard-cell-mobile-span, 1) !important;");
        css.ShouldContain(".dashboard-live-grid");
        css.ShouldContain("grid-auto-rows: minmax(var(--dashboard-grid-row-min, 136px), 1fr);");
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
        razor.ShouldContain("dashboard-drop-status");
        razor.ShouldContain("DashboardDragStatusText");
        razor.ShouldContain("drop-ready");
        razor.ShouldContain("move-target");
        razor.ShouldContain("dashboard-cell-drop-mark");
        css.ShouldContain("grid-template-columns: 54px minmax(0, 1fr);");
        css.ShouldContain("grid-template-rows: 40px minmax(0, 1fr);");
        css.ShouldContain("overscroll-behavior: contain;");
        css.ShouldContain(".dashboard-grid-frame.adding-widget .dashboard-grid");
        css.ShouldContain(".dashboard-drop-status");
        css.ShouldContain(".dashboard-track-handle:focus-visible");
        css.ShouldContain(".dashboard-cell.drop-ready");
        css.ShouldContain(".dashboard-cell.move-target");
        css.ShouldContain(".dashboard-cell.selected::after");
        css.ShouldContain(".dashboard-cell.dropping::after");
        css.ShouldContain("border: 1px dashed color-mix(in srgb, var(--flux-accent) 78%, transparent);");
        css.ShouldContain(".dashboard-cell.moving-source::before");
        css.ShouldContain(".dashboard-cell-drop-mark");
        css.ShouldContain(".dashboard-cell:hover .dashboard-cell-placeholder");
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
        razor.ShouldContain("\"empty-grid\"");
        razor.ShouldContain("Ready for the first widget.");
        css.ShouldContain(".dashboard-grid-frame.empty-grid");
        css.ShouldContain(".dashboard-grid-empty-note");
        css.ShouldContain("position: absolute;");
        css.ShouldContain("min-height: clamp(360px, 58vh, 520px);");
        css.ShouldContain("overflow-x: auto;");
        css.ShouldContain(".dashboard-empty-actions");
        css.ShouldContain("--dashboard-grid-row-min: 86px;");
        inspectorCss.ShouldContain("max-height: 320px;");
        inspectorCss.ShouldContain("min-height: 188px;");
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
        // Only app-metric tiles show a data group now; the multi-metric binding editor was retired.
        inspector.ShouldContain("DashboardInspectorAppMetricRows");
        inspector.ShouldNotContain("DashboardInspectorMetricBindingRows");
        inspector.ShouldContain("DashboardInspectorMetricBindingState.Initialize");
        inspector.ShouldContain("DashboardInspectorMetricBindingState.Current");
        inspector.ShouldNotContain("<PropertyGridRow Name=\"Metric query\">");
        inspector.ShouldNotContain("private RenderFragment RenderMetricParameterField");
        inspector.ShouldNotContain("CurrentBindingMetrics(");
        appRows.ShouldContain("Open metric");
        appRows.ShouldContain("ParameterChanged");
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

        // The dashboard-local metric-query authoring path is retired: charts/topic/payload widgets
        // edit their window inline on the widget draft instead of a separate option-rows component.
        inspector.ShouldNotContain("DashboardInspectorMetricQueryOptionRows");
        inspector.ShouldNotContain("AggregationChanged");
        inspector.ShouldContain("PropertyGridRow Name=\"@InspectorLabels.WindowRow\"");
        inspector.ShouldContain("draft.Window");
        inspector.ShouldContain("SetMetricWindowAsync");
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
        displayRows.ShouldContain("PropertyGridRow Name=\"Line width\"");
        displayRows.ShouldContain("PropertyGridRow Name=\"@Labels.TopicSystemRow\"");
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
        markup.ShouldContain("catalog-item-badges");
        markup.ShouldContain("RequirementLabel(requirement)");
        markup.ShouldContain("CatalogItemClass(item)");
        markup.ShouldContain("aria-grabbed=\"@CatalogItemGrabbed(item)\"");
        markup.ShouldContain("CatalogItemAriaLabel(item)");
        markup.ShouldContain("catalog-item-affordance");
        markup.ShouldContain("catalog-drag-grip");
        markup.ShouldContain("DragIndicator");
        markup.ShouldContain("IsDraggingItem(CatalogEntry item)");
        css.ShouldContain("grid-template-areas:");
        css.ShouldContain("\"description badges\"");
        css.ShouldContain(".component-catalog.dashboard .catalog-item-badge:nth-child(n+2)");
        css.ShouldContain("min-height: 46px;");
        css.ShouldContain(".catalog-item.dragging");
        css.ShouldContain(".catalog-item-affordance");
        css.ShouldContain(".catalog-drag-grip");
        css.ShouldContain("box-shadow: inset 2px 0 0 var(--flux-accent);");
        layout.ShouldContain("DragPreviewIcon(activeDrag.TargetKind)");
        layout.ShouldContain("WorkspaceArtifactKind.Dashboard => Icons.Material.Filled.Widgets");
        layout.ShouldContain("DragPreviewTargetClass");
        appCss.ShouldContain(".flux-drag-preview.dashboard");
        appCss.ShouldContain(".flux-drag-preview.over-designer .mud-icon-root");
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
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "FluxMq.sln")))
        {
            current = current.Parent;
        }

        current.ShouldNotBeNull("Could not locate FluxMq.sln from the test output directory.");
        return current.FullName;
    }
}
