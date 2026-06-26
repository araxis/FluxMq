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
        var selectCss = File.ReadAllText(Path.Combine(
            root,
            "src",
            "FluxMq.UI",
            "Components",
            "Workspace",
            "PropertyGridSelect.razor.css"));
        var iconSegmentCss = File.ReadAllText(Path.Combine(
            root,
            "src",
            "FluxMq.UI",
            "Components",
            "Workspace",
            "PropertyGridIconSegment.razor.css"));
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
        var visualMetricRows = File.ReadAllText(Path.Combine(
            root,
            "src",
            "FluxMq.UI",
            "Components",
            "Workspace",
            "DashboardInspectorVisualMetricRows.razor"));

        propertyGrid.ShouldContain("DefaultNameColumnWidth = 116");
        propertyGrid.ShouldContain("MinNameColumnWidth = 78");
        propertyGrid.ShouldContain("MaxNameColumnWidth = 176");
        propertyGrid.ShouldContain("--property-grid-name-width: min({_nameColumnWidth.ToString(\"0\", CultureInfo.InvariantCulture)}px, 39%);");
        propertyGrid.ShouldContain("aria-label=\"Dashboard property editor\"");
        propertyGrid.ShouldContain("role=\"rowgroup\"");
        propertyGrid.ShouldContain("aria-label=\"@GroupAriaLabel(group, collapsed)\"");
        propertyGrid.ShouldContain("aria-label=\"@GroupHeaderAriaLabel(group, collapsed)\"");
        propertyGrid.ShouldContain("title=\"@group.Title\"");
        propertyGrid.ShouldContain("aria-label=\"@FormatSettingCount(group.SettingCount)\"");
        propertyGrid.ShouldContain("private static string GroupAriaLabel");
        propertyGrid.ShouldContain("private static string GroupHeaderAriaLabel");
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
        selectCss.ShouldContain("max-height: 160px;");
        selectCss.ShouldContain("right: 5px;");
        iconSegmentCss.ShouldContain("min-height: 19px;");
        iconSegmentCss.ShouldContain("width: calc(100% - 4px);");
        iconSegmentCss.ShouldContain(".property-grid-icon-segment.show-labels .property-grid-icon-segment-button");
        iconSegmentCss.ShouldContain(".property-grid-icon-segment.show-labels .property-grid-icon-segment-label");
        colorPickerCss.ShouldContain("grid-template-columns: 26px minmax(0, 1fr) 24px;");
        colorPickerCss.ShouldContain("width: calc(100% - 4px);");
        alignmentPadCss.ShouldContain("grid-template-columns: repeat(3, 16px);");
        inspector.ShouldContain("role=\"complementary\" aria-label=\"Dashboard inspector\"");
        inspector.ShouldContain("var propertyGroups = PropertyGroups;");
        inspector.ShouldContain("class=\"@InspectorHeaderClass\"");
        inspector.ShouldContain("dashboard-inspector-meta-strip");
        inspector.ShouldContain("dashboard-inspector-live-strip dashboard-inspector-status-chip");
        inspector.ShouldContain("role=\"status\"");
        inspector.ShouldContain("title=\"@InspectorStatusLabel\"");
        inspector.ShouldContain("dashboard-inspector-reset-command");
        inspector.ShouldContain("@InspectorModeLabel");
        inspector.ShouldContain("@InspectorStatusIcon");
        inspector.ShouldContain("@InspectorStatusLabel");
        inspector.ShouldContain("@InspectorGroupCountLabel(propertyGroups.Count)");
        inspector.ShouldContain("@InspectorPropertyCountLabel(propertyGroups)");
        inspector.ShouldContain("dashboard-inspector-property-shell");
        inspector.ShouldContain("dashboard-inspector-empty-icon");
        inspector.ShouldContain("dashboard-inspector-empty-card");
        inspector.ShouldContain("private string InspectorModeClass");
        inspector.ShouldContain("Widget edits apply immediately");
        inspector.ShouldContain("Cell edits apply immediately");
        inspector.ShouldContain("private static string InspectorPropertyCountLabel");
        inspectorCss.ShouldContain("flex: 0 0 324px;");
        inspectorCss.ShouldContain("grid-template-columns: 24px minmax(0, 1fr) auto;");
        inspectorCss.ShouldContain(".dashboard-inspector-header.widget");
        inspectorCss.ShouldContain(".dashboard-inspector-reset-command");
        inspectorCss.ShouldContain(".dashboard-inspector-status-chip");
        inspectorCss.ShouldContain(".dashboard-inspector-heading");
        inspectorCss.ShouldContain("flex-wrap: nowrap;");
        inspectorCss.ShouldContain(".dashboard-inspector-meta-strip span");
        inspectorCss.ShouldContain("flex: 0 1 auto;");
        inspectorCss.ShouldContain(".dashboard-inspector-live-strip");
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
        inspectorCss.ShouldContain(".dashboard-inspector-reset-command span,");
        visualMetricRows.ShouldContain("KeyboardArrowUp");
        visualMetricRows.ShouldContain("KeyboardArrowDown");
        visualMetricRows.ShouldContain("Icons.Material.Filled.Close");
        visualMetricRows.ShouldContain("aria-label=\"@($\"Move {VisualMetricLabel(currentMetric)} up\")\"");
        visualMetricRows.ShouldContain("aria-label=\"Add metric card\"");
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
        markup.ShouldContain("MQTT client");
        markup.ShouldContain("MQTT status");
        markup.ShouldContain("ActiveAppLabel");
        markup.ShouldContain("ClientCountLabel");
        markup.ShouldContain("ConnectionBadgeClass");
        markup.ShouldContain("EnsureLiveConnectionsForActiveProject");
        markup.ShouldContain("Live.AddConnectionIfAbsent(profile, subscription, name)");
        markup.ShouldContain("Live.ConnectAsync(connection.Id)");
        markup.ShouldContain("Live.PublishAsync(");
        markup.ShouldContain("RecordManualMqttPublish");
        markup.ShouldContain("diagnostics-panel");
        markup.ShouldContain("publish-form-grid");
        markup.ShouldContain("publish-field broker");
        markup.ShouldContain("publish-field topic");
        markup.ShouldContain("publish-field payload");
        markup.ShouldContain("publish-qos-select");
        markup.ShouldContain("publish-retain-toggle");
        markup.ShouldContain("publish-submit");
        markup.ShouldContain("No MQTT clients");
        markup.ShouldNotContain("Selected client");
        markup.ShouldNotContain("ConnectSelectedAsync");
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

        css.ShouldContain(".publisher-header");
        css.ShouldContain(".publisher-title-lockup");
        css.ShouldContain(".publisher-icon");
        css.ShouldContain(".connection-badge.connected");
        css.ShouldContain(".connection-badge.pending");
        css.ShouldContain(".connection-badge.faulted");
        css.ShouldContain(".publisher-panel");
        css.ShouldContain(".publish-form-grid");
        css.ShouldContain("grid-template-columns: minmax(0, 1fr);");
        css.ShouldNotContain("grid-template-columns: minmax(0, 1fr) minmax(0, 1fr);");
        css.ShouldContain(".publish-field.payload,");
        css.ShouldContain("grid-column: 1 / -1;");
        css.ShouldContain("text-transform: none;");
        css.ShouldContain("height: 30px;");
        css.ShouldContain("min-height: 88px;");
        css.ShouldContain("max-height: 150px;");
        css.ShouldContain(".publish-qos-select");
        css.ShouldContain(".publish-retain-toggle.active");
        css.ShouldContain(".diagnostics-panel");
        css.ShouldContain(".status-line.info");
        css.ShouldContain(".status-line.warning");
        css.ShouldContain(".status-line.error");
        css.ShouldNotContain(".client-panel");
        css.ShouldNotContain(".client-summary");
        css.ShouldNotContain(".client-state-dot");
        css.ShouldNotContain(".client-action");
        css.ShouldNotContain(".client-error");
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
        markup.ShouldContain("no-active-project");
        markup.ShouldContain("Hide MQTT publisher");
        markup.ShouldContain("Show MQTT publisher");
        markup.ShouldNotContain("Workspace navigation");
        markup.ShouldNotContain("No active project");
        markup.ShouldNotContain("flux-rail");
        markup.ShouldNotContain("flux-left-panel");
        markup.ShouldNotContain("left-collapsed");
        markup.ShouldNotContain("_leftOpen");
        markup.ShouldNotContain("<SessionPanel />");

        css.ShouldContain("--flux-right-width: 360px;");
        css.ShouldContain("\"top top\"");
        css.ShouldContain("\"main right\"");
        css.ShouldContain("\"status status\"");
        css.ShouldContain(".flux-breadcrumb");
        css.ShouldContain("font-size: 12.5px;");
        css.ShouldContain("font-weight: 650;");
        css.ShouldContain(".flux-shell.right-collapsed");
        css.ShouldContain(".flux-shell.no-active-project");
        css.ShouldContain("grid-template-areas: \"main\";");
        css.ShouldContain("grid-template-rows: minmax(0, 1fr);");
        css.ShouldContain("\"main\"");
        css.ShouldContain("grid-template-columns: minmax(0, 1fr) var(--flux-right-width);");
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

        markup.ShouldContain("aria-label=\"Topic tree\"");
        markup.ShouldContain("aria-label=\"Topic last state and history\"");
        markup.ShouldContain("aria-label=\"Latest topic message\"");
        markup.ShouldContain("aria-label=\"Topic message history\"");
        markup.ShouldContain("<h2>Topics</h2>");
        markup.ShouldContain("@implements IDisposable");
        markup.ShouldContain("@inject ProjectManagerService Projects");
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
        markup.ShouldContain("topic-broker-edit");
        markup.ShouldContain("Icons.Material.Filled.Settings");
        markup.ShouldContain("OpenBrokerMonitorEditorAsync");
        markup.ShouldContain("Broker monitor settings");
        markup.ShouldContain("Open broker monitor settings");
        markup.ShouldContain("PreserveCandidateExplorerNames");
        markup.ShouldContain("topic-broker-tree");
        markup.ShouldContain("BrokerGroups.Count brokers");
        markup.ShouldContain("VisibleBrokerGroups");
        markup.ShouldContain("BrokerLabel(LastMessage)");
        markup.ShouldContain("<MudTh>Broker</MudTh>");
        markup.ShouldContain("<MudTd DataLabel=\"Broker\">@BrokerLabel(context)</MudTd>");
        markup.ShouldContain("Compact=\"true\"");
        markup.ShouldContain("Class=\"topic-message-grid\"");
        markup.ShouldContain("Items=\"@HistoryMessages\"");
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
        markup.ShouldContain("aria-label=\"Selected MQTT message details\"");
        markup.ShouldContain("Message details");
        markup.ShouldContain("SelectedHistoryPayloadPreview");
        markup.ShouldContain("SelectedHistoryReceivedLabel");
        markup.ShouldContain("Select a history row to inspect MQTT metadata and payload.");
        markup.ShouldContain("LastMessage is null");
        markup.ShouldContain("topic-last-state");
        markup.ShouldContain("topic-last-payload");
        markup.ShouldContain("topic-last-meta");
        markup.ShouldContain("topic-no-traffic");
        markup.ShouldContain("topic-monitor-list");
        markup.ShouldContain("NoTrafficBrokerGroups");
        markup.ShouldContain("MonitorRowClass");
        markup.ShouldContain("One broker monitor is subscribed to #.");
        markup.ShouldContain("No history for the current selection.");
        markup.ShouldContain("topic-history-panel");
        markup.ShouldContain("role=\"status\" aria-live=\"polite\"");
        markup.ShouldContain("PayloadInspector.Inspect(LastMessage.Payload)");
        markup.ShouldContain("LastPayloadPreview");
        markup.ShouldContain("PayloadInspector.Inspect(SelectedHistoryMessage.Payload)");
        markup.ShouldContain("FormatPayloadPreview");
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
        css.ShouldContain(".topic-broker-row.live .topic-broker-state");
        css.ShouldContain(".topic-broker-tree");
        css.ShouldContain(".topic-broker-empty");
        css.ShouldContain(".topic-last-state");
        css.ShouldContain("flex: 0 0 clamp(218px, 38%, 360px);");
        css.ShouldContain(".topic-last-body");
        css.ShouldContain("grid-template-columns: minmax(0, 1fr) minmax(320px, 360px);");
        css.ShouldContain(".topic-last-payload pre");
        css.ShouldContain("white-space: pre-wrap;");
        css.ShouldContain(".topic-last-meta");
        css.ShouldContain("align-self: stretch;");
        css.ShouldContain("flex-direction: column;");
        css.ShouldContain("grid-template-columns: minmax(72px, 0.24fr) minmax(0, 1fr);");
        css.ShouldContain(".topic-last-meta div:last-child");
        css.ShouldContain(".topic-no-traffic");
        css.ShouldContain(".topic-no-traffic-copy");
        css.ShouldContain(".topic-monitor-list");
        css.ShouldContain(".topic-monitor-row");
        css.ShouldContain("grid-template-columns: 8px minmax(92px, 0.3fr) minmax(140px, 1fr) auto auto;");
        css.ShouldContain(".topic-monitor-row.live .topic-monitor-state");
        css.ShouldContain(".topic-history-panel");
        css.ShouldContain(".topic-history-header");
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
        css.ShouldContain(".topic-message-table ::deep col.topic-col-bytes");
        css.ShouldContain(".topic-message-table ::deep th:nth-child(1),");
        css.ShouldContain(".topic-message-table ::deep td:nth-child(1)");
        css.ShouldContain(".topic-message-table ::deep th:nth-child(4),");
        css.ShouldContain(".topic-message-table ::deep td:nth-child(5)");
        css.ShouldContain("min-width: 100%;");
        css.ShouldContain("table-layout: fixed;");
        css.ShouldContain("width: 100%;");
        css.ShouldContain(".topic-message-table ::deep th,");
        css.ShouldContain(".topic-message-table ::deep .topic-history-row.selected td");
        css.ShouldContain(".topic-history-detail");
        css.ShouldContain(".topic-history-detail-header");
        css.ShouldContain(".topic-history-detail-meta");
        css.ShouldContain(".topic-history-detail-meta div:last-child");
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
        css.ShouldContain(".topic-empty-state ::deep .mud-icon-root");
        css.ShouldContain("overflow-wrap: anywhere;");
        css.ShouldContain("flex-basis: clamp(252px, 40vh, 360px);");
        css.ShouldNotContain(".topic-payload-frame");
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

        markup.ShouldContain("aria-label=\"Test studio workspace\"");
        markup.ShouldContain("test-studio-title-icon");
        markup.ShouldContain("@TestCountLabel");
        markup.ShouldContain("@ActiveScenarioLabel");
        markup.ShouldContain("@RunCountLabel");
        markup.ShouldContain("test-studio-mode-switch");
        markup.ShouldContain("role=\"tablist\"");
        markup.ShouldContain("ModeButtonClass(TestStudioMode.Designer)");
        markup.ShouldContain("ModeButtonClass(TestStudioMode.Runner)");
        markup.ShouldContain("Icons.Material.Filled.EditNote");
        markup.ShouldContain("Icons.Material.Filled.PlayCircle");
        markup.ShouldContain("TestScenarioDesigner Project=\"@Project\"");
        markup.ShouldContain("TestRunnerConsole Project=\"@Project\"");
        markup.ShouldNotContain("MudToggleGroup");
        markup.ShouldNotContain("MudToggleItem");

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

        markup.ShouldContain("aria-label=\"Test runner console\"");
        markup.ShouldContain("test-runner-title-icon");
        markup.ShouldContain("test-runner-meta-strip");
        markup.ShouldContain("@NoTestEmptyTitle");
        markup.ShouldContain("@NoTestSelectionHint");
        markup.ShouldContain("test-runner-empty-cues");
        markup.ShouldContain("@ScenarioStepLabel");
        markup.ShouldContain("@ScenarioPhaseLabel");
        markup.ShouldContain("@RunHistorySummaryLabel");
        markup.ShouldContain("RunStatusClass(result.Status)");
        markup.ShouldContain("ActiveRunStateClass");
        markup.ShouldContain("test-run-history-panel");
        markup.ShouldContain("No run history");
        markup.ShouldContain("test-run-history-row");
        markup.ShouldContain("RunHistoryItemClass(historyRun)");
        markup.ShouldContain("RunHistoryAriaLabel(historyRun)");
        markup.ShouldContain("RunHistoryStatusClass(historyRun)");
        markup.ShouldContain("RunHistoryIssueLabel(historyRun)");
        markup.ShouldContain("test-runner-report-actions");
        markup.ShouldContain("Class=\"test-runner-icon-action\"");
        markup.ShouldContain("aria-label=\"@ViewReportTooltip\"");
        markup.ShouldContain("aria-label=\"@CopyReportTooltip\"");
        markup.ShouldContain("aria-label=\"@SaveReportTooltip\"");
        markup.ShouldContain("Class=\"test-runner-run-action\"");
        markup.ShouldContain("test-runner-workspace");
        markup.ShouldContain("test-runner-status-strip");
        markup.ShouldContain("PreflightItemClass");
        markup.ShouldContain("test-runner-result-strip");
        markup.ShouldContain("@FirstRunStripClass");
        markup.ShouldContain("@FirstRunAriaLabel");
        markup.ShouldContain("@FirstRunIcon");
        markup.ShouldContain("@FirstRunStateLabel");
        markup.ShouldContain("@FirstRunTitle");
        markup.ShouldContain("@FirstRunDescription");
        markup.ShouldContain("@FirstRunEventModeLabel");
        markup.ShouldContain("test-runner-first-run-cues");
        markup.ShouldContain("RunSummaryClass(latest)");
        markup.ShouldContain("RunSummaryAriaLabel(latest)");
        markup.ShouldContain("test-runner-result-scope");
        markup.ShouldContain("RunResultScopeLabel");
        markup.ShouldContain("test-runner-main");
        markup.ShouldContain("test-runner-section timeline");
        markup.ShouldContain("test-runner-section activity");
        markup.ShouldContain("test-runner-activity-grid");
        markup.ShouldContain("test-runner-stream-block");
        markup.ShouldContain("TimelineStepLabel(step, stepResult)");
        markup.ShouldContain("TimelineStepMeta(stepResult)");
        markup.ShouldContain("test-runner-step-copy");
        markup.ShouldContain("StepStatusIcon(stepResult)");
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
        markup.ShouldNotContain("MudChip");
        markup.ShouldNotContain("RunStatusPillClass(result.Status)");
        markup.ShouldNotContain("ActiveRunPillClass");
        markup.ShouldNotContain("test-runner-status-pill");

        css.ShouldContain(".test-runner-title-icon");
        css.ShouldContain(".test-runner-empty-cues");
        css.ShouldContain(".test-runner-empty-cues span");
        css.ShouldContain(".test-runner-meta-strip span,");
        css.ShouldContain(".test-runner-status-state");
        css.ShouldContain(".test-run-history-panel");
        css.ShouldContain(".test-run-history-empty strong");
        css.ShouldContain(".test-run-history-empty small");
        css.ShouldContain(".test-run-history-row");
        css.ShouldContain(".test-run-history-status");
        css.ShouldContain("::deep .test-run-history-item.selected .test-run-history-row");
        css.ShouldContain(".test-runner-report-actions");
        css.ShouldContain(".test-runner-report-actions ::deep .mud-icon-button");
        css.ShouldContain("grid-template-columns: minmax(0, 1fr) auto;");
        css.ShouldContain("min-height: 42px;");
        css.ShouldContain("flex-wrap: nowrap;");
        css.ShouldContain(".test-runner-workspace");
        css.ShouldContain("grid-template-rows: auto auto minmax(0, 1fr);");
        css.ShouldContain(".test-runner-status-strip");
        css.ShouldContain("grid-template-columns: repeat(3, minmax(0, 1fr));");
        css.ShouldContain(".test-runner-result-strip");
        css.ShouldContain(".test-runner-result-strip.empty.ready");
        css.ShouldContain(".test-runner-result-strip.empty.warning");
        css.ShouldContain(".test-runner-result-strip.history");
        css.ShouldContain(".test-runner-result-scope");
        css.ShouldContain(".test-runner-result-scope.first-run");
        css.ShouldContain(".test-runner-first-run-cues");
        css.ShouldContain(".test-runner-first-run-cues span");
        css.ShouldContain(".test-runner-main");
        css.ShouldContain("grid-template-columns: minmax(320px, 0.9fr) minmax(0, 1.35fr);");
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
        css.ShouldNotContain("border-radius: 999px;");
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

        markup.ShouldContain("aria-label=\"Test scenario designer\"");
        markup.ShouldContain("test-scenario-heading-icon");
        markup.ShouldContain("test-scenario-title-copy");
        markup.ShouldContain("test-scenario-meta-strip");
        markup.ShouldContain("@NoTestEmptyTitle");
        markup.ShouldContain("@NoTestSelectionHint");
        markup.ShouldContain("test-scenario-empty-cues");
        markup.ShouldContain("@NoStepsEmptyTitle");
        markup.ShouldContain("@NoStepsEmptyText");
        markup.ShouldContain("@ScenarioStepTypeCountText");
        markup.ShouldContain("@PhaseCountText");
        markup.ShouldContain("@RunModeText");
        markup.ShouldContain("@RecentRunCountText");
        markup.ShouldContain("test-scenario-workspace");
        markup.ShouldContain("test-scenario-builder-strip");
        markup.ShouldContain("BuilderMetricClass");
        markup.ShouldContain("@ActivePhaseCountText");
        markup.ShouldContain("@RunnerStateText");
        markup.ShouldContain("RunContextClass(result)");
        markup.ShouldContain("RunContextAriaLabel(result)");
        markup.ShouldContain("test-run-context-status");
        markup.ShouldContain("test-run-context-meta");
        markup.ShouldContain("test-run-context-reset");
        markup.ShouldContain("ReportActionsClass");
        markup.ShouldContain("ReportActionsLabel");
        markup.ShouldContain("test-run-history-panel");
        markup.ShouldContain("No run history");
        markup.ShouldContain("test-run-history-row");
        markup.ShouldContain("RunHistoryItemClass(historyRun)");
        markup.ShouldContain("RunHistoryAriaLabel(historyRun)");
        markup.ShouldContain("RunHistoryStatusClass(historyRun)");
        markup.ShouldContain("RunHistoryIssueLabel(historyRun)");
        markup.ShouldContain("PhaseLanesClass");
        markup.ShouldContain("PhaseLaneClass(phase)");
        markup.ShouldContain("test-scenario-report-actions");
        markup.ShouldContain("test-scenario-build-actions");
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
        markup.ShouldContain("StepCardLabel(step, stepResult)");
        markup.ShouldContain("tabindex=\"0\"");
        markup.ShouldContain("StepStatusIcon(stepResult)");
        markup.ShouldContain("test-step-result-strip");
        markup.ShouldContain("StepResultMetaLabel(stepResult)");
        markup.ShouldContain("StepResultScopeLabel");
        markup.ShouldContain("StepResultEventLabel(stepResult)");
        markup.ShouldContain("test-step-status idle");
        markup.ShouldNotContain("MudChip");
        markup.ShouldNotContain("test-step-badges");

        css.ShouldContain(".test-scenario-heading-icon");
        css.ShouldContain(".test-scenario-meta-strip span,");
        css.ShouldContain(".test-run-status");
        css.ShouldContain(".test-run-context");
        css.ShouldContain(".test-run-context.latest");
        css.ShouldContain(".test-run-context.history");
        css.ShouldContain(".test-run-context-status");
        css.ShouldContain(".test-run-context-reset");
        css.ShouldContain(".test-run-history-panel");
        css.ShouldContain(".test-run-history-empty strong");
        css.ShouldContain(".test-run-history-empty small");
        css.ShouldContain(".test-run-history-row");
        css.ShouldContain(".test-run-history-status");
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
        css.ShouldNotContain(".test-step-badges");
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
        markup.ShouldContain("aria-label=\"Cancel step edit\"");
        markup.ShouldContain("aria-label=\"Apply step edit\"");
        markup.ShouldContain("ValidationStateClass");
        markup.ShouldContain("ValidationStateText");
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
        css.ShouldContain(".scenario-step-editor-state");
        css.ShouldContain(".scenario-step-editor-state.ready");
        css.ShouldContain("display: none;");
        css.ShouldContain(".scenario-step-editor-state.invalid");
        css.ShouldContain(".scenario-step-editor-action-buttons");
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
        markup.ShouldContain("scenario-report-export-state");
        markup.ShouldContain("role=\"status\"");
        markup.ShouldContain("aria-live=\"polite\"");
        markup.ShouldContain("scenario-report-summary-grid");
        markup.ShouldContain("IssueMetricClass");
        markup.ShouldContain("scenario-report-viewer");
        markup.ShouldContain("HasSummaryReport");
        markup.ShouldContain("HasJsonReport");
        markup.ShouldContain("scenario-report-empty");
        markup.ShouldContain("<pre>@TextReport</pre>");
        markup.ShouldContain("<pre>@JsonReport</pre>");
        markup.ShouldContain("scenario-report-action-group");
        markup.ShouldContain("Disabled=\"@(!HasSummaryReport)\"");
        markup.ShouldContain("Disabled=\"@(!HasJsonReport)\"");
        markup.ShouldContain("scenario-report-close");
        markup.ShouldContain("aria-label=\"Close scenario report\"");
        markup.ShouldNotContain("MudChip");
        markup.ShouldNotContain("MudTextField");

        css.ShouldContain(".scenario-report-title");
        css.ShouldContain(".scenario-report-toolbar");
        css.ShouldContain(".scenario-report-meta-strip");
        css.ShouldContain(".scenario-report-export-state");
        css.ShouldContain("height: 24px;");
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
        markup.ShouldContain("new-app-dialog-status");
        markup.ShouldContain("role=\"status\"");
        markup.ShouldContain("aria-live=\"polite\"");
        markup.ShouldContain("new-app-dialog-section");
        markup.ShouldContain("new-app-dialog-grid connection");
        markup.ShouldContain("new-app-dialog-security-row");
        markup.ShouldContain("new-app-dialog-actions");
        markup.ShouldContain("aria-label=\"Create app\"");
        markup.ShouldContain("_port is >= 1 and <= 65535");
        markup.ShouldNotContain("MudDivider");
        markup.ShouldNotContain("HelperText=");

        css.ShouldContain(".new-app-dialog-title");
        css.ShouldContain(".new-app-dialog-status");
        css.ShouldContain(".new-app-dialog-status.ready");
        css.ShouldContain("display: none;");
        css.ShouldContain(".new-app-dialog-section");
        css.ShouldContain("border: 1px solid var(--flux-border-soft);");
        css.ShouldContain(".new-app-dialog-grid.connection");
        css.ShouldContain("grid-template-columns: minmax(0, 1.1fr) minmax(0, 1.6fr) 92px;");
        css.ShouldContain(".new-app-dialog-security-row");
        css.ShouldContain("::deep(.new-app-dialog-create)");
        css.ShouldContain("@media (max-width: 760px)");
        css.ShouldContain("grid-template-columns: minmax(0, 1fr);");
        css.ShouldContain("@media (max-width: 480px)");
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
        markup.ShouldContain("add-connection-dialog-status");
        markup.ShouldContain("role=\"status\"");
        markup.ShouldContain("aria-live=\"polite\"");
        markup.ShouldContain("add-connection-dialog-section");
        markup.ShouldContain("add-connection-dialog-grid broker");
        markup.ShouldContain("add-connection-dialog-checkbox-cell");
        markup.ShouldContain("add-connection-dialog-actions");
        markup.ShouldContain("aria-label=\"Add connection\"");
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
        markup.ShouldNotContain("Label=\"TLS\"");
        markup.ShouldNotContain("<input");

        css.ShouldContain(".add-connection-dialog-title");
        css.ShouldContain(".add-connection-dialog-status");
        css.ShouldContain(".add-connection-dialog-status.ready");
        css.ShouldContain("display: none;");
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

        markup.ShouldContain("metric-create-title-icon");
        markup.ShouldContain("metric-create-title-copy");
        markup.ShouldContain("metric-create-status");
        markup.ShouldContain("role=\"status\"");
        markup.ShouldContain("aria-live=\"polite\"");
        markup.ShouldContain("role=\"form\" aria-label=\"Create metric\"");
        markup.ShouldContain("role=\"search\"");
        markup.ShouldContain("aria-label=\"Create metric\"");
        markup.ShouldContain("Color=\"Color.Primary\"");
        markup.ShouldContain("Variant=\"Variant.Filled\"");
        markup.ShouldContain("aria-invalid=\"@(!CanCreate)\"");
        markup.ShouldContain("metric-create-empty-defaults");
        markup.ShouldNotContain("Class=\"metric-create-submit\"");
        markup.ShouldContain("CreateStatusClass");
        markup.ShouldContain("CreateStatusText");
        markup.ShouldNotContain("MudStack");
        markup.ShouldNotContain("MudGrid");
        markup.ShouldNotContain("MudDivider");
        markup.ShouldNotContain("HelperText=");

        css.ShouldContain(".metric-create-title-icon");
        css.ShouldContain(".metric-create-title-copy");
        css.ShouldContain(".metric-create-status");
        css.ShouldContain(".metric-create-status.ready");
        css.ShouldContain("display: none;");
        css.ShouldContain("grid-template-columns: 26px minmax(0, 1fr) auto;");
        css.ShouldContain("border: 1px solid var(--flux-border-soft);");
        css.ShouldContain("border-radius: 6px;");
        css.ShouldContain("height: min(304px, calc(100vh - 220px));");
        css.ShouldContain("min-height: 28px;");
        css.ShouldContain(".metric-create-empty-defaults");
        css.ShouldContain("@media (max-width: 760px)");
        css.ShouldContain("@media (max-width: 520px)");
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
            markup.ShouldContain($"{prefix}-status");
            markup.ShouldContain("role=\"status\"");
            markup.ShouldContain("aria-live=\"polite\"");
            markup.ShouldContain("aria-label=");
            markup.ShouldNotContain("MudStack");
            markup.ShouldNotContain("MudGrid");
            markup.ShouldNotContain("MudDivider");
            markup.ShouldNotContain("HelperText=");

            css.ShouldContain($".{prefix}-title");
            css.ShouldContain($".{prefix}-title-icon");
            css.ShouldContain($".{prefix}-title-copy");
            css.ShouldContain($".{prefix}-status");
            css.ShouldContain("grid-template-columns: 26px minmax(0, 1fr) auto;");
            css.ShouldContain("border: 1px solid var(--flux-border-soft);");
            css.ShouldContain("border-radius: 6px;");
            css.ShouldContain("min-height: 28px;");
            css.ShouldContain("@media (max-width:");
            css.ShouldNotContain("border-radius: 999px;");
            css.ShouldNotContain("box-shadow: 0 ");
        }

        File.ReadAllText(Path.Combine(dialogPath, "MetricConfirmDialog.razor"))
            .ShouldContain("role=\"alert\"");
        File.ReadAllText(Path.Combine(dialogPath, "MetricDeleteDialog.razor"))
            .ShouldContain("role=\"alert\"");
        File.ReadAllText(Path.Combine(dialogPath, "MetricRenameDialog.razor.css"))
            .ShouldContain(".metric-rename-status.ready");
        File.ReadAllText(Path.Combine(dialogPath, "MetricRenameDialog.razor.css"))
            .ShouldContain("display: none;");
        File.ReadAllText(Path.Combine(dialogPath, "MetricDuplicateDialog.razor.css"))
            .ShouldContain(".metric-duplicate-status.ready");
        File.ReadAllText(Path.Combine(dialogPath, "MetricDuplicateDialog.razor.css"))
            .ShouldContain("display: none;");
        File.ReadAllText(Path.Combine(dialogPath, "MetricTypeChangeDialog.razor.css"))
            .ShouldContain(".metric-type-change-status.ready");
        File.ReadAllText(Path.Combine(dialogPath, "MetricTypeChangeDialog.razor.css"))
            .ShouldContain("display: none;");
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
        markup.ShouldContain("new-pipeline-dialog-status");
        markup.ShouldContain("role=\"status\"");
        markup.ShouldContain("aria-live=\"polite\"");
        markup.ShouldContain("new-pipeline-dialog-section");
        markup.ShouldContain("new-pipeline-dialog-actions");
        markup.ShouldContain("Disabled=\"@(!IsValid)\"");
        markup.ShouldContain("CancelAriaLabel");
        markup.ShouldContain("SubmitAriaLabel");
        markup.ShouldContain("DialogResult.Ok(_name.Trim())");
        markup.ShouldNotContain("MudGrid");
        markup.ShouldNotContain("MudStack");

        css.ShouldContain(".new-pipeline-dialog-title");
        css.ShouldContain(".new-pipeline-dialog-status");
        css.ShouldContain(".new-pipeline-dialog-status.ready");
        css.ShouldContain("display: none;");
        css.ShouldContain(".new-pipeline-dialog-section");
        css.ShouldContain("border: 1px solid var(--flux-border-soft);");
        css.ShouldContain(".new-pipeline-dialog-field");
        css.ShouldContain("::deep(.new-pipeline-dialog-submit)");
        css.ShouldContain("@media (max-width: 480px)");
        css.ShouldContain("grid-template-columns: repeat(2, minmax(0, 1fr));");
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
        markup.ShouldContain("save-as-dialog-status");
        markup.ShouldContain("role=\"status\"");
        markup.ShouldContain("aria-live=\"polite\"");
        markup.ShouldContain("save-as-dialog-section");
        markup.ShouldContain("save-as-dialog-helper");
        markup.ShouldContain("save-as-dialog-actions");
        markup.ShouldContain("Disabled=\"@(!IsValid)\"");
        markup.ShouldContain("OnKeyDown");
        markup.ShouldContain("DialogResult.Ok(_path.Trim())");
        markup.ShouldNotContain("HelperText=");
        markup.ShouldNotContain("MudStack");

        css.ShouldContain(".save-as-dialog-title");
        css.ShouldContain(".save-as-dialog-status");
        css.ShouldContain(".save-as-dialog-status.ready");
        css.ShouldContain("display: none;");
        css.ShouldContain(".save-as-dialog-section");
        css.ShouldContain("border: 1px solid var(--flux-border-soft);");
        css.ShouldContain(".save-as-dialog-helper");
        css.ShouldContain("overflow-wrap: anywhere;");
        css.ShouldContain("::deep(.save-as-dialog-submit)");
        css.ShouldContain("@media (max-width: 480px)");
        css.ShouldContain("grid-template-columns: repeat(2, minmax(0, 1fr));");
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
        markup.ShouldContain("start-recording-status");
        markup.ShouldContain("role=\"status\"");
        markup.ShouldContain("aria-live=\"polite\"");
        markup.ShouldContain("role=\"form\" aria-label=\"Start recording\"");
        markup.ShouldContain("start-recording-section");
        markup.ShouldContain("start-recording-fields");
        markup.ShouldContain("start-recording-summary");
        markup.ShouldContain("aria-label=\"Recording project\"");
        markup.ShouldContain("aria-label=\"Recording session name\"");
        markup.ShouldContain("aria-label=\"Start recording\"");
        markup.ShouldContain("DefaultSessionName");
        markup.ShouldContain("ProjectSummaryText");
        markup.ShouldContain("OnKeyDown");
        markup.ShouldContain("StartRecordingResult(project, session)");
        markup.ShouldNotContain("MudStack");
        markup.ShouldNotContain("<MudText Typo=\"Typo.h6\">Start Recording</MudText>");

        css.ShouldContain(".start-recording-title");
        css.ShouldContain(".start-recording-title-icon");
        css.ShouldContain(".start-recording-title-copy");
        css.ShouldContain(".start-recording-status");
        css.ShouldContain("display: none;");
        css.ShouldContain(".start-recording-section");
        css.ShouldContain("border: 1px solid var(--flux-border-soft);");
        css.ShouldContain("border-radius: 6px;");
        css.ShouldContain(".start-recording-field ::deep(.mud-input-root)");
        css.ShouldContain(".start-recording-summary");
        css.ShouldContain(".start-recording-actions");
        css.ShouldContain("min-height: 28px;");
        css.ShouldContain("@media (max-width: 520px)");
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
        markup.ShouldContain("payload-format-badge @FormatClass");
        markup.ShouldContain("payload-meta-strip");
        markup.ShouldContain("payload-view-switch");
        markup.ShouldContain("role=\"tab\"");
        markup.ShouldContain("aria-selected=\"@IsActiveView(FormattedView)\"");
        markup.ShouldContain("payload-inspector-meta-list");
        markup.ShouldContain("private const string FormattedView = \"formatted\";");
        markup.ShouldContain("private string FormatClass");
        markup.ShouldContain("private string FormatIcon");
        markup.ShouldNotContain("MudToggleGroup");
        markup.ShouldNotContain("MudChip");
        markup.ShouldNotContain("<MudPaper");

        css.ShouldContain("border-radius: 5px;");
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
        markup.ShouldContain("role=\"search\" aria-label=\"Log filters\"");
        markup.ShouldContain("workspace-log-stats");
        markup.ShouldContain("WorkspaceLogFilter.Problems");
        markup.ShouldContain("workspace-log-segment");
        markup.ShouldContain("[Parameter] public WorkspaceLogQuery? InitialQuery { get; set; }");
        markup.ShouldContain("InitialQuery.Equals(_appliedInitialQuery)");
        markup.ShouldContain("_severity = string.IsNullOrWhiteSpace(InitialQuery.Severity)");
        markup.ShouldContain("_search = InitialQuery.Search");
        markup.ShouldContain("aria-label=\"@($\"{SeverityLabel(entry)} log at {FormatLogTime(entry)} from {SourceCode(entry)}\")\"");
        markup.ShouldContain("workspace-log-row-icon");
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

        markup.ShouldContain("aria-label=\"Application JSON toolbar\"");
        markup.ShouldContain("app-json-title-icon");
        markup.ShouldContain("<strong>App JSON</strong>");
        markup.ShouldContain("@FileLabel");
        markup.ShouldContain("@JsonLineCount lines");
        markup.ShouldContain("@JsonSizeLabel");
        markup.ShouldContain("app-json-state unsaved");
        markup.ShouldContain("role=\"status\" aria-live=\"polite\">Unsaved</span>");
        markup.ShouldContain("aria-label=\"Copy JSON\"");
        markup.ShouldContain("Disabled=\"@string.IsNullOrWhiteSpace(_fullJson)\"");
        markup.ShouldContain("app-json-editor-shell");
        markup.ShouldContain("app-json-empty");
        markup.ShouldContain("No JSON available");
        markup.ShouldContain("aria-label=\"Application definition JSON\"");
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
        markup.ShouldNotContain("ReturnToVisualViewAsync");

        css.ShouldContain("grid-template-columns: minmax(0, 1fr) auto auto auto;");
        css.ShouldContain("min-height: 38px;");
        css.ShouldContain("padding: 5px 8px;");
        css.ShouldContain("height: 26px;");
        css.ShouldContain(".app-json-meta span,");
        css.ShouldContain(".app-json-state.unsaved");
        css.ShouldContain(".app-json-state.unsaved::before");
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

        razor.ShouldContain("role=\"region\" aria-label=\"Dashboard designer\"");
        razor.ShouldContain("class=\"dashboard-toolbar\" role=\"toolbar\" aria-label=\"Dashboard toolbar\"");
        razor.ShouldContain("dashboard-meta-strip");
        razor.ShouldContain("@DashboardStatusLabel");
        razor.ShouldContain("@GridSizeLabel");
        razor.ShouldContain("@CellCountLabel");
        razor.ShouldContain("@WidgetCountLabel");
        razor.ShouldContain("dashboard-mode-shell");
        razor.ShouldContain("DashboardModeStateClass");
        razor.ShouldContain("dashboard-tool-group dashboard-tool-group-grid");
        razor.ShouldContain("dashboard-tool-group dashboard-tool-group-selection");
        razor.ShouldContain("aria-label=\"Dashboard layout editor\"");
        razor.ShouldContain("role=\"grid\" aria-label=\"Dashboard layout grid\"");
        razor.ShouldContain("aria-label=\"Live dashboard grid\"");
        razor.ShouldContain("aria-label=\"@CellAriaLabel(currentCell)\"");
        razor.ShouldContain("title=\"@CellAriaLabel(currentCell)\"");
        razor.ShouldContain("tabindex=\"0\"");
        razor.ShouldContain("SelectCellFromKeyboard");
        razor.ShouldContain("private static bool IsActivationKey");
        razor.ShouldContain("aria-label=\"@GridPickerButtonLabel\"");
        razor.ShouldContain("aria-label=\"@SplitPickerButtonLabel\"");
        razor.ShouldContain("GridPickerCellAriaLabel");
        razor.ShouldContain("SplitPickerCellAriaLabel");
        razor.ShouldContain("disabled=\"@IsSplitPickerCellDisabled(r, c)\"");
        razor.ShouldContain("private string CellAriaLabel");

        css.ShouldContain("grid-template-columns: minmax(0, 1fr) auto;");
        css.ShouldContain(".dashboard-meta-strip span");
        css.ShouldContain(".dashboard-mode-shell");
        css.ShouldContain(".dashboard-mode-state.edit");
        css.ShouldContain(".dashboard-mode-state.live");
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
        razor.ShouldContain("dashboard-live-summary");
        razor.ShouldContain("@LivePreviewSubtitle");
        razor.ShouldContain("@LivePreviewStateLabel");
        razor.ShouldContain("SwitchToEditMode");
        razor.ShouldContain("dashboard-live-viewport");
        razor.ShouldContain("dashboard-live-empty-note");
        razor.ShouldContain("No widgets in live preview");
        razor.ShouldContain("Read-only runtime view without layout controls.");

        css.ShouldContain(".dashboard-live-head");
        css.ShouldContain("grid-template-columns: minmax(0, 1fr) auto auto;");
        css.ShouldContain(".dashboard-live-summary .ready");
        css.ShouldContain(".dashboard-live-summary .empty");
        css.ShouldContain("::deep .dashboard-live-edit-button");
        css.ShouldContain(".dashboard-live-viewport");
        css.ShouldContain(".dashboard-live-empty-note");
        css.ShouldContain("grid-template-columns: minmax(0, min(320px, 100%));");
        css.ShouldContain("transform: translate(-50%, -50%);");
        css.ShouldContain("text-align: center;");
        css.ShouldContain("max-width: calc(100% - 16px);");
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
        razor.ShouldContain("role=\"status\" aria-live=\"polite\"");
        razor.ShouldContain("dashboard-grid-empty-icon");
        razor.ShouldContain("@EmptyGridHint");
        css.ShouldContain("grid-template-columns: 42px minmax(max-content, 1fr);");
        css.ShouldContain("grid-template-rows: 32px minmax(0, 1fr);");
        css.ShouldContain("overscroll-behavior: contain;");
        css.ShouldContain("border-right: 1px solid var(--flux-border-soft);");
        css.ShouldContain(".dashboard-grid-frame.adding-widget .dashboard-grid");
        css.ShouldContain(".dashboard-drop-status");
        css.ShouldContain(".dashboard-grid-empty-icon");
        css.ShouldContain("left: 8px;");
        css.ShouldContain("max-width: min(340px, calc(100% - 16px));");
        css.ShouldContain(".dashboard-track-handle:focus-visible");
        css.ShouldContain(".dashboard-cell:focus-visible");
        css.ShouldContain(".dashboard-cell.drop-ready");
        css.ShouldContain(".dashboard-grid-frame.adding-widget .dashboard-cell.drop-ready");
        css.ShouldContain(".dashboard-grid-frame.adding-widget .dashboard-cell.drop-ready .dashboard-cell-drop-mark");
        css.ShouldContain(".dashboard-cell.move-target");
        css.ShouldContain(".dashboard-cell.selected::after");
        css.ShouldContain(".dashboard-cell.dropping::after");
        css.ShouldContain("opacity: 0.5;");
        css.ShouldContain("border: 1px dashed color-mix(in srgb, var(--flux-accent) 78%, transparent);");
        css.ShouldContain(".dashboard-cell.moving-source::before");
        css.ShouldContain(".dashboard-cell-drop-mark");
        css.ShouldContain(".dashboard-cell:hover .dashboard-cell-placeholder");
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
        razor.ShouldContain("<MudToggleGroup T=\"string\"");
        razor.ShouldContain("SelectionMode=\"SelectionMode.SingleSelection\"");
        razor.ShouldContain("@TrackCode");
        razor.ShouldContain("@CurrentSummary");
        razor.ShouldContain("@ResultSize");
        razor.ShouldContain("@ModeDescription");
        razor.ShouldContain("StartIcon=\"@Icons.Material.Filled.RestartAlt\"");
        razor.ShouldContain("Disabled=\"@(!CanSubmit)\"");

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

        catalog.ShouldContain("Click to place in the selected cell, or drag to choose a cell");
        catalog.ShouldContain("Use the edit action on the placed widget to configure it.");
        catalogCss.ShouldContain(".component-catalog.dashboard .catalog-add-button");
        catalogCss.ShouldContain(".component-catalog.dashboard .catalog-drag-grip");

        designer.ShouldContain("Drag to move widget; use toolbar to edit");
        designer.ShouldContain("class=\"dashboard-cell-widget-action edit\"");
        designer.ShouldContain("Edit {WidgetLabel(currentCell.Widget)} settings");
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
        styleRows.ShouldContain("Reset cell style");
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
        appRows.ShouldContain("Open metric");
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
        markup.ShouldContain("catalog-item-meta");
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
        markup.ShouldContain("role=\"form\" aria-label=\"@EditorAriaLabel\"");
        markup.ShouldContain("@EditorModeLabel");
        markup.ShouldContain("@EditorDetailLabel");
        markup.ShouldContain("Rounded=\"false\"");
        markup.ShouldContain("dashboard-widget-editor-section-head");
        markup.ShouldContain("dashboard-widget-editor-action-status");
        markup.ShouldContain("dashboard-widget-editor-action-spacer");
        markup.ShouldContain("dashboard-widget-editor-actions");
        markup.ShouldContain("StartIcon=\"@Icons.Material.Filled.RestartAlt\"");
        markup.ShouldContain("Disabled=\"@(!HasChanges)\"");
        markup.ShouldContain("ActionStatusLabel");
        markup.ShouldContain("ConfigurationEquals");
        markup.ShouldContain("FilterAltOff");
        markup.ShouldNotContain("<MudDivider />");

        css.ShouldContain(".dashboard-widget-editor-title-icon");
        css.ShouldContain(".dashboard-widget-editor-title-copy");
        css.ShouldContain(".dashboard-widget-editor-meta-strip span");
        css.ShouldContain(".dashboard-widget-editor-shell");
        css.ShouldContain("max-height: min(68vh, 620px);");
        css.ShouldContain("overflow-y: auto;");
        css.ShouldContain("grid-template-columns: 30px minmax(0, 1fr);");
        css.ShouldContain("position: sticky;");
        css.ShouldContain(".dashboard-widget-editor-section-head");
        css.ShouldContain(".dashboard-widget-editor-action-status");
        css.ShouldContain(".dashboard-widget-editor-action-spacer");
        css.ShouldContain(".dashboard-widget-editor-actions");
        css.ShouldContain("grid-template-columns: repeat(3, minmax(0, 1fr));");
        css.ShouldContain("@media (max-width: 700px)");
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
        markup.ShouldContain("catalog-meta-strip");
        markup.ShouldContain("@CatalogModeLabel");
        markup.ShouldContain("@CatalogUseStateLabel");
        markup.ShouldContain("@CatalogSearchStateLabel");
        markup.ShouldContain("CatalogUseStateClass");
        markup.ShouldContain("aria-label=\"@SearchPlaceholder\"");
        markup.ShouldContain("aria-label=\"@CatalogListLabel\"");
        markup.ShouldContain("private string CatalogListLabel");
        markup.ShouldContain("role=\"status\" aria-live=\"polite\"");
        markup.ShouldContain("@EmptyIcon");
        markup.ShouldContain("@EmptyHintLabel");
        markup.ShouldContain("catalog-item-affordance");
        markup.ShouldContain("CatalogItemAriaLabel(item)");
        markup.ShouldContain("aria-grabbed=\"@CatalogItemGrabbed(item)\"");
        markup.ShouldContain("ShouldShowStepMetadata(item)");
        markup.ShouldContain("catalog-step-meta");
        markup.ShouldContain("StepPhaseMetaClass(item)");
        markup.ShouldContain("StepKindLabel(item)");
        markup.ShouldContain("StepParameterLabel(item)");
        markup.ShouldContain("descriptor.DefaultPhase");
        markup.ShouldContain("descriptor.Fields.Count");
        markup.ShouldNotContain("catalog-step-badges");
        markup.ShouldNotContain("StepPhaseBadgeClass(item)");
        markup.ShouldNotContain("catalog-item-badge");

        css.ShouldContain(".catalog-title-copy");
        css.ShouldContain(".catalog-title-label");
        css.ShouldContain(".catalog-meta-strip span,");
        css.ShouldContain("background: var(--flux-canvas);");
        css.ShouldContain("border-bottom: 1px solid var(--flux-border-soft);");
        css.ShouldContain(".catalog-use-state.ready");
        css.ShouldContain(".catalog-use-state.inactive");
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

        markup.ShouldContain("aria-label=\"Flow designer canvas\"");
        markup.ShouldContain("aria-label=\"Pipeline diagram canvas\"");
        markup.ShouldContain("flow-canvas-title-copy");
        markup.ShouldContain("flow-canvas-meta-strip");
        markup.ShouldContain("@WorkflowModeLabel");
        markup.ShouldContain("@WorkflowSelectionLabel");
        markup.ShouldContain("flow-canvas-metrics");
        markup.ShouldContain("flow-canvas-stat");
        markup.ShouldContain("flow-canvas-command-group");
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
        markup.ShouldContain("aria-label=\"Apply link condition\"");
        markup.ShouldContain("aria-label=\"Clear link condition\"");
        markup.ShouldNotContain("ViewStrokeColor=\"#FBBF24\"");
        markup.ShouldNotContain("flow-link-condition-meta");
        markup.ShouldNotContain("Icons.Material.Filled.Link");
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
        css.ShouldContain(".flow-canvas-metrics");
        css.ShouldContain(".flow-canvas-stat");
        css.ShouldContain(".flow-canvas-stat:not(:last-child)::after");
        css.ShouldContain(".flow-canvas-command-group");
        css.ShouldContain("min-height: 46px;");
        css.ShouldContain("flex-wrap: nowrap;");
        css.ShouldContain("height: 24px;");
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
        css.ShouldContain("max-width: min(100%, 340px);");
        css.ShouldContain("grid-row: auto;");
        css.ShouldContain("@media (max-width: 720px)");
        css.ShouldContain("grid-template-columns: minmax(0, 1fr);");
        css.ShouldContain("margin: 8px 8px 0;");
        css.ShouldNotContain("top: 10px;");
        markup.ShouldNotContain("flow-canvas-chip");
        css.ShouldNotContain(".flow-canvas-chip");
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

        markup.ShouldContain("EditDialogMaxWidth=\"@MaxWidth.Medium\"");
        markup.ShouldContain("ShowHeaderIcon=\"false\"");
        markup.ShouldContain("ShowDisplayName=\"true\"");
        markup.ShouldContain("ShowCategoryChip=\"false\"");
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
        markup.ShouldContain("mqtt-trigger-editor-section");
        markup.ShouldContain("aria-label=\"Broker settings\"");
        markup.ShouldContain("aria-label=\"Subscriptions\"");
        markup.ShouldNotContain("mqtt-trigger-section-title");
        markup.ShouldNotContain("mqtt-trigger-subscription-head");
        markup.ShouldNotContain("Icons.Material.Filled.Dns");
        markup.ShouldNotContain("Icons.Material.Filled.Topic");
        markup.ShouldContain("mqtt-trigger-editor-grid");
        markup.ShouldContain("mqtt-trigger-broker-cell");
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
        markup.ShouldContain("aria-label=\"Add subscription\"");
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
        css.ShouldContain(".mqtt-trigger-editor-section");
        css.ShouldContain(".mqtt-trigger-broker-cell");
        css.ShouldNotContain(".mqtt-trigger-field-note");
        css.ShouldContain("padding: 0;");
        css.ShouldContain("gap: 7px;");
        css.ShouldNotContain("gap: 8px;");
        css.ShouldNotContain("background: var(--flux-surface);");
        css.ShouldNotContain("border: 1px solid var(--flux-border-soft);");
        css.ShouldNotContain(".mqtt-trigger-section-title");
        css.ShouldNotContain(".mqtt-trigger-subscription-head");
        css.ShouldContain("grid-template-columns: minmax(0, 1fr) minmax(1.65rem, max-content) minmax(2.15rem, max-content) minmax(2.35rem, max-content);");
        css.ShouldNotContain("grid-template-columns: minmax(0, 1fr) 26px 34px 38px;");
        css.ShouldContain("display: inline-flex;");
        css.ShouldContain("overflow-wrap: anywhere;");
        css.ShouldContain("white-space: normal;");
        css.ShouldContain("grid-template-columns: minmax(0, 1fr) minmax(10rem, 0.36fr);");
        css.ShouldNotContain("grid-template-columns: minmax(0, 1fr) 190px;");
        css.ShouldContain(".mqtt-trigger-editor-grid ::deep(.mud-input-label)");
        css.ShouldContain("font-size: 0.72rem;");
        css.ShouldContain(".mqtt-trigger-editor-grid ::deep(.mud-input > input.mud-input-root)");
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
        markup.ShouldContain("ShowCategoryChip=\"false\"");
        markup.ShouldContain("connection-state-trigger-summary");
        markup.ShouldContain("connection-state-trigger-meta");
        markup.ShouldContain("ConnectionCaption");
        markup.ShouldContain("<span>Output</span>");
        markup.ShouldContain("Client state");
        markup.ShouldContain("State changes");
        markup.ShouldContain("connection-state-trigger-contracts");
        markup.ShouldContain("aria-label=\"Connection state trigger output fields\"");
        markup.ShouldContain("connection-state-trigger-token");
        markup.ShouldContain("<span class=\"connection-state-trigger-contract-label\">Fields</span>");
        markup.ShouldContain("profileId");
        markup.ShouldContain("state");
        markup.ShouldContain("errors");
        markup.ShouldContain("connection-state-trigger-editor");
        markup.ShouldContain("aria-label=\"Connection state trigger settings\"");
        markup.ShouldContain("Label=\"Broker connection\"");
        markup.ShouldContain("@bind-Value=\"_connection\"");
        markup.ShouldContain("Label=\"Connection name\"");
        markup.ShouldContain("Class=\"connection-state-trigger-broker-field\"");
        markup.ShouldContain("Flow.SyncConnectionAndUpdateNode");
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
        css.ShouldContain("grid-template-columns: minmax(0, 1.18fr) minmax(0, 0.72fr) minmax(0, 0.78fr);");
        css.ShouldNotContain("grid-template-columns: minmax(0, 1fr) 86px 92px;");
        css.ShouldContain(".connection-state-trigger-contracts");
        css.ShouldContain(".connection-state-trigger-contract");
        css.ShouldContain(".connection-state-trigger-contract-label");
        css.ShouldContain("flex: 0 0 100%;");
        css.ShouldContain("white-space: normal;");
        css.ShouldContain(".connection-state-trigger-token");
        css.ShouldContain("display: inline-flex;");
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
        markup.ShouldContain("ShowHeaderIcon=\"false\"");
        markup.ShouldContain("ShowDisplayName=\"true\"");
        markup.ShouldContain("ShowCategoryChip=\"false\"");
        markup.ShouldContain("state-reducer-summary");
        markup.ShouldContain("state-reducer-meta");
        markup.ShouldContain("EngineCaption");
        markup.ShouldContain("KeyCaption");
        markup.ShouldContain("MaxKeysCaption");
        markup.ShouldContain("BufferCaption");
        markup.ShouldContain("state-reducer-contracts");
        markup.ShouldContain("aria-label=\"State reducer contract fields\"");
        markup.ShouldContain("state-reducer-token");
        markup.ShouldContain("StateReducerInput");
        markup.ShouldContain("StateReducerResult");
        markup.ShouldContain("state-reducer-expression-preview");
        markup.ShouldContain("state-reducer-editor");
        markup.ShouldContain("aria-label=\"State reducer settings\"");
        markup.ShouldContain("state-reducer-config-row");
        markup.ShouldContain("aria-label=\"State reducer configuration\"");
        markup.ShouldContain("state-reducer-rule-row");
        markup.ShouldContain("aria-label=\"State reducer rule\"");
        markup.ShouldContain("state-reducer-rule-sidecar");
        markup.ShouldNotContain("state-reducer-editor-surface");
        markup.ShouldNotContain("state-reducer-rule-surface");
        markup.ShouldNotContain("state-reducer-rule-title");
        markup.ShouldNotContain("Transform the current input and existing state into the next state");
        markup.ShouldNotContain("state-reducer-rule-grid");
        markup.ShouldNotContain("state-reducer-rule-aside");
        markup.ShouldNotContain("state-reducer-rule-panel");
        markup.ShouldNotContain("state-reducer-expression-workspace");
        markup.ShouldNotContain("state-reducer-panel-header");
        markup.ShouldNotContain("state-reducer-panel-kicker");
        markup.ShouldNotContain("state-reducer-panel-token");
        markup.ShouldNotContain("state-reducer-source-row");
        markup.ShouldContain("Label=\"Engine\"");
        markup.ShouldContain("@bind-Value=\"_engine\"");
        markup.ShouldContain("EngineOptionLabel(engine)");
        markup.ShouldContain("Label=\"Expression name\"");
        markup.ShouldContain("@bind-Value=\"_expressionName\"");
        markup.ShouldContain("Label=\"Input buffer\"");
        markup.ShouldContain("@bind-Value=\"_boundedCapacity\"");
        markup.ShouldContain("Label=\"Max keys\"");
        markup.ShouldContain("@bind-Value=\"_maxKeys\"");
        markup.ShouldNotContain("state-reducer-expression-row");
        markup.ShouldNotContain("state-reducer-key-cell");
        markup.ShouldNotContain("aria-label=\"State key expression\"");
        markup.ShouldNotContain("state-reducer-reducer-cell");
        markup.ShouldNotContain("aria-label=\"State reducer expression\"");
        markup.ShouldNotContain("state-reducer-config-grid");
        markup.ShouldNotContain("state-reducer-expression-grid");
        markup.ShouldNotContain("state-reducer-workbench");
        markup.ShouldNotContain("state-reducer-key-panel");
        markup.ShouldNotContain("state-reducer-reducer-panel");
        markup.ShouldNotContain("state-reducer-section-heading");
        markup.ShouldContain("Label=\"Key expression\"");
        markup.ShouldContain("@bind-Value=\"_keyExpression\"");
        markup.ShouldContain("Placeholder=\"topic or blank\"");
        markup.ShouldContain("Label=\"Reducer\"");
        markup.ShouldContain("@bind-Value=\"_reducer\"");
        markup.ShouldContain("Lines=\"4\"");
        markup.ShouldContain("Lines=\"12\"");
        markup.ShouldNotContain("state-reducer-field-note");
        markup.ShouldNotContain("Blank keeps one shared state.");
        markup.ShouldContain("state-reducer-reference");
        markup.ShouldContain("state-reducer-reference-label");
        markup.ShouldContain("state-reducer-variable-list");
        markup.ShouldContain("ExpressionVariables");
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
        css.ShouldContain(".state-reducer-contracts");
        css.ShouldContain(".state-reducer-contract");
        css.ShouldContain(".state-reducer-contract-label");
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
        css.ShouldNotContain("grid-template-columns: minmax(0, 1.52fr) minmax(240px, 0.48fr);");
        css.ShouldContain(".state-reducer-rule-sidecar");
        css.ShouldNotContain(".state-reducer-rule-grid");
        css.ShouldNotContain(".state-reducer-rule-aside");
        css.ShouldNotContain("border-left: 1px solid color-mix(in srgb, var(--flux-border-soft) 36%, transparent);");
        css.ShouldNotContain(".state-reducer-expression-workspace");
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
        css.ShouldContain(".state-reducer-editor ::deep(.state-reducer-reducer-field textarea.mud-input-root)");
        css.ShouldContain("min-height: 268px;");
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
        markup.ShouldContain("ShowCategoryChip=\"false\"");
        markup.ShouldContain("flow-assertion-summary");
        markup.ShouldContain("flow-assertion-meta");
        markup.ShouldContain("AssertionCaption");
        markup.ShouldContain("InputTypeCaption");
        markup.ShouldContain("BufferCaption");
        markup.ShouldContain("flow-assertion-contracts");
        markup.ShouldContain("aria-label=\"Assertion output fields\"");
        markup.ShouldContain("flow-assertion-token");
        markup.ShouldContain("result");
        markup.ShouldContain("passed");
        markup.ShouldContain("failed");
        markup.ShouldContain("flow-assertion-expression-preview");
        markup.ShouldContain("flow-assertion-editor");
        markup.ShouldContain("aria-label=\"Flow assertion settings\"");
        markup.ShouldContain("flow-assertion-config-row");
        markup.ShouldContain("aria-label=\"Flow assertion configuration\"");
        markup.ShouldContain("flow-assertion-rule-stack");
        markup.ShouldContain("aria-label=\"Flow assertion rule\"");
        markup.ShouldNotContain("flow-assertion-rule-row");
        markup.ShouldNotContain("flow-assertion-rule-sidecar");
        markup.ShouldNotContain("flow-assertion-editor-surface");
        markup.ShouldNotContain("flow-assertion-rule-surface");
        markup.ShouldNotContain("flow-assertion-rule-title");
        markup.ShouldNotContain("flow-assertion-rule-composer");
        markup.ShouldNotContain("flow-assertion-rule-grid");
        markup.ShouldNotContain("flow-assertion-rule-aside");
        markup.ShouldNotContain("flow-assertion-rule-panel");
        markup.ShouldNotContain("flow-assertion-expression-workspace");
        markup.ShouldNotContain("flow-assertion-source-row");
        markup.ShouldContain("Label=\"Assertion name\"");
        markup.ShouldContain("@bind-Value=\"_assertionName\"");
        markup.ShouldContain("Label=\"Input type\"");
        markup.ShouldContain("@bind-Value=\"_inputType\"");
        markup.ShouldContain("Label=\"Input buffer\"");
        markup.ShouldContain("@bind-Value=\"_boundedCapacity\"");
        markup.ShouldNotContain("flow-assertion-expression-row");
        markup.ShouldNotContain("flow-assertion-expression-cell");
        markup.ShouldNotContain("aria-label=\"Assertion expression\"");
        markup.ShouldNotContain("flow-assertion-message-cell");
        markup.ShouldNotContain("aria-label=\"Assertion failure output\"");
        markup.ShouldContain("Label=\"Assertion\"");
        markup.ShouldNotContain("Label=\"Expression\"");
        markup.ShouldContain("@bind-Value=\"_expression\"");
        markup.ShouldContain("Lines=\"12\"");
        markup.ShouldContain("Label=\"Failure message\"");
        markup.ShouldContain("@bind-Value=\"_failureMessage\"");
        markup.ShouldContain("Lines=\"3\"");
        markup.ShouldNotContain("Lines=\"4\"");
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

        css.ShouldContain(".flow-assertion-summary");
        css.ShouldContain(".flow-assertion-meta");
        css.ShouldContain("grid-template-columns: minmax(0, 1.18fr) minmax(0, 0.6fr) minmax(0, 0.42fr);");
        css.ShouldContain("-webkit-line-clamp: 2;");
        css.ShouldContain(".flow-assertion-contracts");
        css.ShouldContain(".flow-assertion-contract");
        css.ShouldContain(".flow-assertion-contract-label");
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
        css.ShouldContain("text-overflow: ellipsis;");
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
        css.ShouldContain(".flow-assertion-rule-stack");
        css.ShouldContain("grid-template-columns: minmax(0, 1fr);");
        css.ShouldNotContain(".flow-assertion-rule-row");
        css.ShouldNotContain("grid-template-columns: minmax(0, 1.52fr) minmax(240px, 0.48fr);");
        css.ShouldNotContain(".flow-assertion-rule-sidecar");
        css.ShouldNotContain(".flow-assertion-editor-surface");
        css.ShouldNotContain(".flow-assertion-rule-surface");
        css.ShouldNotContain(".flow-assertion-rule-title");
        css.ShouldNotContain(".flow-assertion-rule-composer");
        css.ShouldNotContain(".flow-assertion-rule-grid");
        css.ShouldNotContain(".flow-assertion-rule-aside");
        css.ShouldNotContain("border-left: 1px solid color-mix(in srgb, var(--flux-border-soft) 36%, transparent);");
        css.ShouldNotContain(".flow-assertion-expression-workspace");
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
        css.ShouldContain("grid-template-columns: minmax(4.5rem, max-content) minmax(0, 1fr);");
        css.ShouldNotContain("grid-template-columns: 72px minmax(0, 1fr);");
        css.ShouldNotContain("background: color-mix(in srgb, var(--flux-surface-2) 7%, transparent);");
        css.ShouldNotContain("border: 1px solid color-mix(in srgb, var(--flux-border-soft) 24%, transparent);");
        css.ShouldNotContain("border-radius: 5px;");
        css.ShouldNotContain("padding: 7px;");
        css.ShouldContain(".flow-assertion-editor ::deep(.flow-assertion-expression-field textarea.mud-input-root)");
        css.ShouldContain("min-height: 292px;");
        css.ShouldContain(".flow-assertion-editor ::deep(.flow-assertion-message-field textarea.mud-input-root)");
        css.ShouldContain("min-height: 72px;");
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
        markup.ShouldContain("ShowCategoryChip=\"false\"");
        markup.ShouldContain("message-filter-summary");
        markup.ShouldContain("message-filter-meta");
        markup.ShouldContain("ModeCaption");
        markup.ShouldContain("RuleCountCaption");
        markup.ShouldContain("message-filter-patterns");
        markup.ShouldContain("aria-label=\"Topic filter patterns\"");
        markup.ShouldContain("SummaryPatterns");
        markup.ShouldContain("PatternOverflow");
        markup.ShouldContain("message-filter-expression-preview");
        markup.ShouldContain("message-filter-token");
        markup.ShouldContain("message-filter-editor");
        markup.ShouldContain("aria-label=\"Flow filter settings\"");
        markup.ShouldNotContain("message-filter-rules-surface");
        markup.ShouldContain("aria-label=\"Filter rules\"");
        markup.ShouldNotContain("message-filter-editor-status");
        markup.ShouldNotContain("DraftModeCaption");
        markup.ShouldNotContain("DraftPatternCountCaption");
        markup.ShouldNotContain("message-filter-rule-composer");
        markup.ShouldContain("message-filter-rule-layout");
        markup.ShouldNotContain("message-filter-rule-grid");
        markup.ShouldContain("message-filter-expression-row");
        markup.ShouldContain("message-filter-pattern-table");
        markup.ShouldContain("message-filter-pattern-list");
        markup.ShouldContain("message-filter-pattern-table-header");
        markup.ShouldNotContain("aria-hidden=\"true\"");
        markup.ShouldContain("aria-label=\"Topic pattern filters\"");
        markup.ShouldContain("message-filter-pattern-row");
        markup.ShouldContain("aria-label=\"@($\"Topic pattern {index + 1}\")\"");
        markup.ShouldContain("ValueChanged=\"@(v => _draftPatterns[index] = v ?? string.Empty)\"");
        markup.ShouldContain("aria-label=\"Add topic pattern\"");
        markup.ShouldContain("AddPattern");
        markup.ShouldContain("aria-label=\"@($\"Remove topic pattern {index + 1}\")\"");
        markup.ShouldContain("RemovePattern(index)");
        markup.ShouldContain("role=\"status\"");
        markup.ShouldNotContain("message-filter-rules-panel");
        markup.ShouldNotContain("message-filter-pattern-header");
        markup.ShouldNotContain("message-filter-expression-area");
        markup.ShouldContain("aria-label=\"Expression filter\"");
        markup.ShouldContain("Label=\"Filter expression\"");
        markup.ShouldContain("aria-label=\"Filter expression\"");
        markup.ShouldContain("@bind-Value=\"_expression\"");
        markup.ShouldContain("Lines=\"6\"");
        markup.ShouldContain("message-filter-reference");
        markup.ShouldContain("message-filter-reference-label");
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
        markup.ShouldNotContain("PatternLabel");

        css.ShouldContain(".message-filter-summary");
        css.ShouldContain(".message-filter-meta");
        css.ShouldContain("grid-template-columns: minmax(0, 1.3fr) minmax(0, 0.7fr);");
        css.ShouldNotContain("grid-template-columns: minmax(0, 1fr) 88px;");
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
        css.ShouldContain("display: -webkit-box;");
        css.ShouldContain("-webkit-line-clamp: 2;");
        css.ShouldContain("white-space: normal;");
        css.ShouldContain("text-overflow: ellipsis;");
        css.ShouldContain(".message-filter-editor");
        css.ShouldNotContain(".message-filter-rules-surface");
        css.ShouldNotContain("background: color-mix(in srgb, var(--flux-surface-2) 4%, transparent);");
        css.ShouldNotContain("border: 1px solid color-mix(in srgb, var(--flux-border-soft) 42%, transparent);");
        css.ShouldNotContain("padding: 12px;");
        css.ShouldNotContain("padding: 14px;");
        css.ShouldNotContain(".message-filter-editor-status");
        css.ShouldNotContain(".message-filter-editor-status > div");
        css.ShouldNotContain(".message-filter-rule-composer");
        css.ShouldContain(".message-filter-rule-layout");
        css.ShouldContain("grid-template-columns: minmax(0, 1.1fr) minmax(14rem, 0.9fr);");
        css.ShouldNotContain("grid-template-columns: minmax(0, 1fr) 310px;");
        css.ShouldNotContain(".message-filter-rule-grid");
        css.ShouldContain(".message-filter-expression-row");
        css.ShouldContain(".message-filter-pattern-table");
        css.ShouldContain(".message-filter-pattern-table-header");
        css.ShouldNotContain("grid-template-columns: repeat(2, minmax(0, 1fr));");
        css.ShouldNotContain("border-left: 1px solid color-mix(in srgb, var(--flux-border-soft) 36%, transparent);");
        css.ShouldContain(".message-filter-pattern-list");
        css.ShouldContain(".message-filter-pattern-table-header ::deep(.message-filter-add.mud-icon-button)");
        css.ShouldContain("justify-self: end;");
        css.ShouldContain("border-radius: 4px;");
        css.ShouldContain(".message-filter-pattern-table-header ::deep(.message-filter-add .mud-icon-root)");
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
        markup.ShouldContain("ShowCategoryChip=\"false\"");
        markup.ShouldContain("json-schema-validator-summary");
        markup.ShouldContain("json-schema-validator-meta");
        markup.ShouldContain("SchemaTargetCaption");
        markup.ShouldContain("SchemaIdCaption");
        markup.ShouldContain("json-schema-validator-contracts");
        markup.ShouldContain("aria-label=\"Validator input fields\"");
        markup.ShouldContain("aria-label=\"Validator output fields\"");
        markup.ShouldContain("json-schema-validator-token");
        markup.ShouldContain("MqttEnvelope");
        markup.ShouldContain("result");
        markup.ShouldContain("valid");
        markup.ShouldContain("invalid");
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
        markup.ShouldContain("@bind-Value=\"_schemaPath\"");
        markup.ShouldContain("aria-label=\"Select JSON Schema file\"");
        markup.ShouldContain("PickSchemaFileAsync");
        markup.ShouldContain("json-schema-validator-file-source");
        markup.ShouldContain("aria-label=\"JSON schema file source\"");
        markup.ShouldNotContain("json-schema-validator-field-label");
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
        css.ShouldContain(".json-schema-validator-contracts");
        css.ShouldContain(".json-schema-validator-contract");
        css.ShouldContain(".json-schema-validator-contract-label");
        css.ShouldContain("flex: 0 0 100%;");
        css.ShouldContain("white-space: normal;");
        css.ShouldNotContain("grid-template-columns: repeat(3, minmax(0, auto));");
        css.ShouldContain("overflow-wrap: anywhere;");
        css.ShouldContain(".json-schema-validator-token");
        css.ShouldContain("display: inline-flex;");
        css.ShouldContain(".json-schema-validator-editor");
        css.ShouldNotContain("gap: 10px;");
        css.ShouldNotContain("gap: 8px;");
        css.ShouldContain("gap: 7px;");
        css.ShouldNotContain(".json-schema-validator-editor-surface");
        css.ShouldContain(".json-schema-validator-config-row");
        css.ShouldNotContain(".json-schema-validator-field-label");
        css.ShouldContain(".json-schema-validator-schema-area");
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
        css.ShouldNotContain("grid-template-columns: minmax(0, 1fr) 30px;");
        css.ShouldContain("min-height: 34px;");
        css.ShouldNotContain("height: clamp(520px, 68vh, 760px);");
        css.ShouldNotContain("min-height: 520px;");
        css.ShouldContain("min-height: 360px;");
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
        markup.ShouldContain("ShowDisplayName=\"true\"");
        markup.ShouldContain("ShowCategoryChip=\"false\"");
        markup.ShouldContain("dynamic-mapper-summary");
        markup.ShouldContain("dynamic-mapper-meta");
        markup.ShouldContain("dynamic-mapper-meta-item input");
        markup.ShouldContain("EngineCaption");
        markup.ShouldContain("OutputContractCaption");
        markup.ShouldContain("dynamic-mapper-contracts");
        markup.ShouldContain("aria-label=\"Mapper input variables\"");
        markup.ShouldContain("aria-label=\"Mapper output fields\"");
        markup.ShouldContain("SummaryVariables");
        markup.ShouldContain("SummaryOutputFields");
        markup.ShouldContain("dynamic-mapper-token");
        markup.ShouldContain("dynamic-mapper-editor");
        markup.ShouldContain("aria-label=\"Dynamic mapper settings\"");
        markup.ShouldContain("dynamic-mapper-control-row");
        markup.ShouldContain("aria-label=\"Mapper configuration\"");
        markup.ShouldContain("Label=\"Input schema\"");
        markup.ShouldContain("ValueChanged=\"@SetInputType\"");
        markup.ShouldContain("Label=\"Engine\"");
        markup.ShouldContain("ValueChanged=\"@SetEngine\"");
        markup.ShouldContain("Label=\"Result contract\"");
        markup.ShouldContain("ValueChanged=\"@SetOutputContract\"");
        markup.ShouldContain("Label=\"Typed schema\"");
        markup.ShouldContain("ValueChanged=\"@SetOutputType\"");
        markup.ShouldContain("Label=\"JSON Schema file\"");
        markup.ShouldContain("ValueChanged=\"@SetOutputSchemaPath\"");
        markup.ShouldContain("PickOutputSchemaFileAsync");
        markup.ShouldContain("dynamic-mapper-workspace");
        markup.ShouldContain("dynamic-mapper-expression-workspace");
        markup.ShouldNotContain("WorkspaceClass");
        markup.ShouldContain("ShouldRenderSamplePanel");
        markup.ShouldContain("SamplePanelClass");
        markup.ShouldContain("dynamic-mapper-sample-popover");
        markup.ShouldContain("dynamic-mapper-workspace-header");
        markup.ShouldContain("dynamic-mapper-workspace-actions");
        markup.ShouldContain("dynamic-mapper-sample-popover-header");
        markup.ShouldContain("dynamic-mapper-heading-token");
        markup.ShouldContain("dynamic-mapper-sample-popover-actions");
        markup.ShouldNotContain("OutputShapeLabel");
        markup.ShouldContain("SampleToggleText");
        markup.ShouldContain("SampleToggleIcon");
        markup.ShouldContain("ToggleSampleEditorAsync");
        markup.ShouldContain("ReloadWorkspaceSample");
        markup.ShouldContain("dynamic-mapper-input-error");
        markup.ShouldContain("role=\"alert\"");
        markup.ShouldContain("Id=\"@InputEditorId\"");
        markup.ShouldContain("Id=\"@EditorId\"");
        markup.ShouldContain("ConstructionOptions=\"@InputEditorConstructionOptions\"");
        markup.ShouldContain("ConstructionOptions=\"@EditorConstructionOptions\"");
        markup.ShouldContain("CssClass=\"dynamic-mapper-monaco-editor dynamic-mapper-input-editor\"");
        markup.ShouldContain("CssClass=\"dynamic-mapper-monaco-editor dynamic-mapper-expression-editor\"");
        markup.ShouldNotContain("dynamic-mapper-drawer-grid");
        markup.ShouldNotContain("dynamic-mapper-config-grid");
        markup.ShouldNotContain("dynamic-mapper-source-drawer");
        markup.ShouldNotContain("dynamic-mapper-result-drawer");
        markup.ShouldNotContain("dynamic-mapper-sample-drawer");
        markup.ShouldNotContain("dynamic-mapper-sample-strip");
        markup.ShouldNotContain("Source Fields");
        markup.ShouldNotContain("Output Preview");
        markup.ShouldNotContain("Preview JSON");
        markup.ShouldNotContain("dynamic-mapper-variable-list");
        markup.ShouldNotContain("dynamic-mapper-shape-list");
        markup.ShouldNotContain("CssClass=\"dynamic-mapper-monaco-editor dynamic-mapper-result-editor\"");
        markup.ShouldNotContain("dynamic-mapper-sample-workspace");
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
        css.ShouldContain(".dynamic-mapper-contracts");
        css.ShouldContain(".dynamic-mapper-token");
        css.ShouldContain("flex-wrap: wrap;");
        css.ShouldNotContain("grid-template-columns: repeat(4, minmax(0, auto));");
        css.ShouldContain("flex: 0 0 100%;");
        css.ShouldNotContain("grid-column: 1 / -1;");
        css.ShouldContain("display: inline-flex;");
        css.ShouldContain("overflow-wrap: anywhere;");
        css.ShouldContain(".dynamic-mapper-editor");
        css.ShouldContain(".dynamic-mapper-control-row");
        css.ShouldContain("grid-template-columns: minmax(0, 0.74fr) minmax(0, 0.56fr) minmax(0, 0.64fr) minmax(0, 0.9fr);");
        css.ShouldNotContain("grid-template-columns: minmax(128px, 0.74fr) minmax(108px, 0.56fr) minmax(128px, 0.64fr) minmax(160px, 0.9fr);");
        css.ShouldContain("grid-template-columns: minmax(0, 1fr) minmax(1.5rem, max-content);");
        css.ShouldNotContain("grid-template-columns: minmax(0, 1fr) 28px;");
        css.ShouldContain(".dynamic-mapper-workspace");
        css.ShouldContain("grid-template-rows: minmax(0, 1fr);");
        css.ShouldNotContain(".dynamic-mapper-workspace.has-sample");
        css.ShouldNotContain("grid-template-rows: minmax(0, 1fr) minmax(128px, 0.2fr);");
        css.ShouldContain("height: 100%;");
        css.ShouldNotContain("height: clamp(760px, 82vh, 980px);");
        css.ShouldContain("overflow: hidden;");
        css.ShouldContain(".dynamic-mapper-expression-workspace");
        css.ShouldContain(".dynamic-mapper-sample-popover");
        css.ShouldContain(".dynamic-mapper-sample-popover.has-editor");
        css.ShouldContain("position: absolute;");
        css.ShouldContain("top: 44px;");
        css.ShouldContain("width: min(460px, calc(100% - 16px));");
        css.ShouldContain(".dynamic-mapper-sample-popover-header");
        css.ShouldContain(".dynamic-mapper-sample-popover-actions");
        css.ShouldContain(".dynamic-mapper-workspace-actions");
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
        css.ShouldContain(".dynamic-mapper-workspace ::deep(.dynamic-mapper-input-editor)");
        css.ShouldContain(".dynamic-mapper-input-error");
        css.ShouldNotContain(".dynamic-mapper-workspace ::deep(.dynamic-mapper-result-editor)");
        css.ShouldNotContain(".dynamic-mapper-drawer-grid");
        css.ShouldNotContain(".dynamic-mapper-config-grid");
        css.ShouldNotContain(".dynamic-mapper-drawer");
        css.ShouldNotContain(".dynamic-mapper-sample-drawer");
        css.ShouldNotContain(".dynamic-mapper-result-grid");
        css.ShouldNotContain(".dynamic-mapper-variable");
        css.ShouldNotContain(".dynamic-mapper-sample-workspace");
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
        markup.ShouldContain("ShowCategoryChip=\"false\"");
        markup.ShouldNotContain("EditDialogMaxWidth=");
        markup.ShouldContain("mqtt-publisher-summary");
        markup.ShouldContain("mqtt-publisher-meta");
        markup.ShouldContain("mqtt-publisher-meta-item broker");
        markup.ShouldContain("ActorNodeConfiguration.NormalizeBoundedCapacity(Model.BoundedCapacity)");
        markup.ShouldContain("mqtt-publisher-contract");
        markup.ShouldContain("aria-label=\"Publish request fields\"");
        markup.ShouldContain("mqtt-publisher-token");
        markup.ShouldContain("topic");
        markup.ShouldContain("payload");
        markup.ShouldContain("qos");
        markup.ShouldContain("retain");
        markup.ShouldContain("mqtt-publisher-editor");
        markup.ShouldContain("aria-label=\"Publisher settings\"");
        markup.ShouldContain("Label=\"Broker connection\"");
        markup.ShouldContain("Label=\"Connection name\"");
        markup.ShouldContain("mqtt-publisher-broker-cell");
        markup.ShouldNotContain("mqtt-publisher-field-note");
        markup.ShouldNotContain("Add a broker connection in the left panel to enable the dropdown.");
        markup.ShouldNotContain("HelperText=\"Add a broker connection in the left panel to enable the dropdown.\"");
        markup.ShouldContain("Label=\"Input buffer\"");
        markup.ShouldContain("@bind-Value=\"_boundedCapacity\"");
        markup.ShouldContain("Class=\"mqtt-publisher-broker-field\"");
        markup.ShouldContain("Class=\"mqtt-publisher-buffer-field\"");
        markup.ShouldNotContain("<Stat ");
        markup.ShouldNotContain("<MudChip");
        markup.ShouldNotContain("d-flex flex-wrap gap-1");
        markup.ShouldNotContain("Label=\"Broker resource\"");

        css.ShouldContain(".mqtt-publisher-summary");
        css.ShouldContain(".mqtt-publisher-meta");
        css.ShouldContain("grid-template-columns: minmax(0, 1.34fr) minmax(0, 0.66fr);");
        css.ShouldNotContain("grid-template-columns: minmax(0, 1fr) 84px;");
        css.ShouldContain(".mqtt-publisher-contract");
        css.ShouldContain("display: flex;");
        css.ShouldContain("flex-wrap: wrap;");
        css.ShouldNotContain("grid-template-columns: minmax(0, 1fr) repeat(4, auto);");
        css.ShouldContain(".mqtt-publisher-contract-label");
        css.ShouldContain("flex: 0 0 100%;");
        css.ShouldContain("white-space: normal;");
        css.ShouldContain(".mqtt-publisher-token");
        css.ShouldContain("display: inline-flex;");
        css.ShouldContain("overflow-wrap: anywhere;");
        css.ShouldContain(".mqtt-publisher-editor");
        css.ShouldContain(".mqtt-publisher-broker-cell");
        css.ShouldNotContain(".mqtt-publisher-field-note");
        css.ShouldContain("gap: 7px;");
        css.ShouldNotContain("gap: 8px;");
        css.ShouldContain("grid-template-columns: minmax(0, 1fr) minmax(10rem, 0.36fr);");
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
        markup.ShouldContain("ShowCategoryChip=\"false\"");
        markup.ShouldNotContain("EditDialogMaxWidth=");
        markup.ShouldContain("mqtt-recorder-summary");
        markup.ShouldContain("mqtt-recorder-meta");
        markup.ShouldContain("mqtt-recorder-meta-item target");
        markup.ShouldContain("Local sessions");
        markup.ShouldContain("ActorNodeConfiguration.NormalizeBoundedCapacity(Model.BoundedCapacity)");
        markup.ShouldContain("mqtt-recorder-contract");
        markup.ShouldContain("aria-label=\"Recording request fields\"");
        markup.ShouldContain("mqtt-recorder-token");
        markup.ShouldContain("sessionId");
        markup.ShouldContain("envelope");
        markup.ShouldContain("mqtt-recorder-editor");
        markup.ShouldContain("aria-label=\"Recorder settings\"");
        markup.ShouldContain("Label=\"Input buffer\"");
        markup.ShouldContain("@bind-Value=\"_boundedCapacity\"");
        markup.ShouldContain("Class=\"mqtt-recorder-buffer-field\"");
        markup.ShouldNotContain("<Stat ");
        markup.ShouldNotContain("<MudChip");
        markup.ShouldNotContain("d-flex flex-wrap gap-1");
        markup.ShouldNotContain("MqttRecordingRequest");

        css.ShouldContain(".mqtt-recorder-summary");
        css.ShouldContain(".mqtt-recorder-meta");
        css.ShouldContain("grid-template-columns: minmax(0, 1.34fr) minmax(0, 0.66fr);");
        css.ShouldNotContain("grid-template-columns: minmax(0, 1fr) 84px;");
        css.ShouldContain(".mqtt-recorder-contract");
        css.ShouldContain("display: flex;");
        css.ShouldContain("flex-wrap: wrap;");
        css.ShouldNotContain("grid-template-columns: minmax(0, 1fr) repeat(2, auto);");
        css.ShouldContain(".mqtt-recorder-contract-label");
        css.ShouldContain("flex: 0 0 100%;");
        css.ShouldContain("white-space: normal;");
        css.ShouldContain(".mqtt-recorder-token");
        css.ShouldContain("display: inline-flex;");
        css.ShouldContain("overflow-wrap: anywhere;");
        css.ShouldContain(".mqtt-recorder-editor");
        css.ShouldContain("gap: 7px;");
        css.ShouldNotContain("gap: 8px;");
        css.ShouldContain("grid-template-columns: minmax(10rem, 0.36fr);");
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
        markup.ShouldContain("ShowCategoryChip=\"false\"");
        markup.ShouldNotContain("EditDialogMaxWidth=");
        markup.ShouldContain("file-writer-summary");
        markup.ShouldContain("file-writer-meta");
        markup.ShouldContain("file-writer-meta-item target");
        markup.ShouldContain("Input path");
        markup.ShouldContain("ActorNodeConfiguration.NormalizeBoundedCapacity(Model.BoundedCapacity)");
        markup.ShouldContain("file-writer-contract");
        markup.ShouldContain("aria-label=\"File write request fields\"");
        markup.ShouldContain("file-writer-token");
        markup.ShouldContain("path");
        markup.ShouldContain("content");
        markup.ShouldContain("mode");
        markup.ShouldContain("createDirectory");
        markup.ShouldContain("file-writer-editor");
        markup.ShouldContain("aria-label=\"File writer settings\"");
        markup.ShouldContain("Label=\"Input buffer\"");
        markup.ShouldContain("@bind-Value=\"_boundedCapacity\"");
        markup.ShouldContain("Class=\"file-writer-buffer-field\"");
        markup.ShouldNotContain("<Stat ");
        markup.ShouldNotContain("<MudChip");
        markup.ShouldNotContain("d-flex flex-wrap gap-1");
        markup.ShouldNotContain("FileWriteRequest");

        css.ShouldContain(".file-writer-summary");
        css.ShouldContain(".file-writer-meta");
        css.ShouldContain("grid-template-columns: minmax(0, 1.34fr) minmax(0, 0.66fr);");
        css.ShouldNotContain("grid-template-columns: minmax(0, 1fr) 84px;");
        css.ShouldContain(".file-writer-contract");
        css.ShouldContain("display: flex;");
        css.ShouldContain("flex-wrap: wrap;");
        css.ShouldNotContain("grid-template-columns: repeat(4, minmax(0, auto));");
        css.ShouldContain(".file-writer-contract-label");
        css.ShouldContain("flex: 0 0 100%;");
        css.ShouldContain("white-space: normal;");
        css.ShouldContain(".file-writer-token");
        css.ShouldContain("display: inline-flex;");
        css.ShouldContain("overflow-wrap: anywhere;");
        css.ShouldContain("white-space: normal;");
        css.ShouldContain(".file-writer-editor");
        css.ShouldContain("gap: 7px;");
        css.ShouldNotContain("gap: 8px;");
        css.ShouldContain("grid-template-columns: minmax(10rem, 0.36fr);");
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

        markup.ShouldContain("EditDialogMaxWidth=\"MaxWidth.Medium\"");
        markup.ShouldContain("ShowHeaderIcon=\"false\"");
        markup.ShouldContain("ShowDisplayName=\"true\"");
        markup.ShouldContain("ShowCategoryChip=\"false\"");
        markup.ShouldContain("condition-router-summary");
        markup.ShouldContain("condition-router-meta");
        markup.ShouldContain("InputTypeCaption");
        markup.ShouldContain("condition-router-expression");
        markup.ShouldContain("aria-label=\"Condition router expression\"");
        markup.ShouldContain("ExpressionPreview");
        markup.ShouldContain("condition-router-variables");
        markup.ShouldContain("SummaryVariables");
        markup.ShouldContain("VariableOverflow");
        markup.ShouldContain("condition-router-token");
        markup.ShouldContain("condition-router-editor");
        markup.ShouldContain("aria-label=\"Condition router settings\"");
        markup.ShouldContain("condition-router-config-row");
        markup.ShouldContain("condition-router-rule-grid");
        markup.ShouldContain("aria-label=\"Condition router rule\"");
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
        markup.ShouldContain("Label=\"Condition\"");
        markup.ShouldContain("aria-label=\"Condition expression\"");
        markup.ShouldContain("@bind-Value=\"_expression\"");
        markup.ShouldContain("Lines=\"8\"");
        markup.ShouldContain("Class=\"condition-router-expression-field\"");
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
        css.ShouldContain(".condition-router-editor");
        css.ShouldNotContain("gap: 10px;");
        css.ShouldContain("gap: 7px;");
        css.ShouldNotContain("gap: 8px;");
        css.ShouldContain("gap: 4px;");
        css.ShouldContain("padding-left: 7px;");
        css.ShouldNotContain("padding-left: 8px;");
        css.ShouldNotContain("padding-left: 10px;");
        css.ShouldContain("min-height: 34px;");
        css.ShouldNotContain("min-height: 36px;");
        css.ShouldContain(".condition-router-config-row");
        css.ShouldContain(".condition-router-rule-grid");
        css.ShouldNotContain(".condition-router-editor-surface");
        css.ShouldNotContain(".condition-router-editor-status");
        css.ShouldNotContain(".condition-router-editor-status > div");
        css.ShouldNotContain(".condition-router-expression-row");
        css.ShouldNotContain(".condition-router-expression-workspace");
        css.ShouldNotContain(".condition-router-condition-panel");
        css.ShouldNotContain("background: color-mix(in srgb, var(--flux-surface-2) 4%, transparent);");
        css.ShouldNotContain("border: 1px solid color-mix(in srgb, var(--flux-border-soft) 42%, transparent);");
        css.ShouldNotContain("border-top: 1px solid color-mix(in srgb, var(--flux-border-soft) 42%, transparent);");
        css.ShouldNotContain("padding: 12px;");
        css.ShouldNotContain("padding: 14px;");
        css.ShouldNotContain(".condition-router-source-row");
        css.ShouldContain("grid-template-columns: minmax(0, 1fr);");
        css.ShouldContain("grid-template-columns: minmax(0, 1fr) minmax(0, 0.42fr);");
        css.ShouldNotContain("grid-template-columns: minmax(0, 240px);");
        css.ShouldNotContain("grid-template-columns: minmax(0, 1fr) minmax(160px, 0.34fr);");
        css.ShouldNotContain("grid-template-columns: minmax(180px, 240px);");
        css.ShouldNotContain("grid-template-columns: minmax(0, 1fr) minmax(172px, 220px);");
        css.ShouldNotContain("grid-template-columns: minmax(180px, 1fr) minmax(230px, 0.72fr);");
        css.ShouldNotContain(".condition-router-output-map");
        css.ShouldNotContain("border-left: 2px solid");
        css.ShouldContain("border-left: 1px solid color-mix(in srgb, var(--flux-border-soft) 58%, transparent);");
        css.ShouldNotContain(".condition-router-expression-cell");
        css.ShouldContain(".condition-router-variable-reference");
        css.ShouldContain(".condition-router-variable-list");
        css.ShouldNotContain(".condition-router-variable-strip");
        css.ShouldContain(".condition-router-variable-label");
        css.ShouldContain(".condition-router-variable-token");
        css.ShouldContain("grid-template-columns: minmax(0, 1fr) minmax(0, 0.46fr);");
        css.ShouldNotContain("grid-template-columns: minmax(0, 1fr) auto;");
        css.ShouldNotContain("grid-template-columns: 74px minmax(0, 1fr);");
        css.ShouldContain(".condition-router-editor ::deep(.condition-router-expression-field textarea.mud-input-root)");
        css.ShouldContain("min-height: 168px;");
        css.ShouldContain(".condition-router-editor ::deep(.mud-input-control)");
        css.ShouldContain("@media (max-width: 720px)");
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

        markup.ShouldContain("EditDialogMaxWidth=\"MaxWidth.Medium\"");
        markup.ShouldContain("ShowHeaderIcon=\"false\"");
        markup.ShouldContain("ShowDisplayName=\"true\"");
        markup.ShouldContain("ShowCategoryChip=\"false\"");
        markup.ShouldContain("routing-switch-summary");
        markup.ShouldContain("routing-switch-meta");
        markup.ShouldContain("InputTypeCaption");
        markup.ShouldContain("RouteCountCaption");
        markup.ShouldContain("EnvelopeCaption");
        markup.ShouldContain("BufferCaption");
        markup.ShouldContain("routing-switch-expression");
        markup.ShouldContain("aria-label=\"Routing switch expression\"");
        markup.ShouldContain("ExpressionPreview");
        markup.ShouldContain("routing-switch-routes");
        markup.ShouldContain("aria-label=\"Routing switch routes\"");
        markup.ShouldContain("RoutePreview");
        markup.ShouldContain("RoutePreviewOverflow");
        markup.ShouldContain("routing-switch-token");
        markup.ShouldContain("routing-switch-editor");
        markup.ShouldContain("aria-label=\"Routing switch settings\"");
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
        markup.ShouldContain("Label=\"Expression\"");
        markup.ShouldContain("aria-label=\"Routing switch expression\"");
        markup.ShouldContain("@bind-Value=\"_expression\"");
        markup.ShouldContain("Lines=\"5\"");
        markup.ShouldContain("Class=\"routing-switch-expression-field\"");
        markup.ShouldContain("routing-switch-rule-grid");
        markup.ShouldContain("routing-switch-expression-stack");
        markup.ShouldNotContain("routing-switch-expression-row");
        markup.ShouldContain("aria-label=\"Routing switch rule expression\"");
        markup.ShouldContain("routing-switch-variable-reference");
        markup.ShouldContain("ExpressionVariables");
        markup.ShouldContain("routing-switch-variable-token");
        markup.ShouldContain("routing-switch-route-composer");
        markup.ShouldContain("aria-label=\"Routing switch route outputs\"");
        markup.ShouldContain("routing-switch-route-header");
        markup.ShouldContain("routing-switch-route-list");
        markup.ShouldContain("AddRoute");
        markup.ShouldContain("Class=\"routing-switch-route-add\"");
        markup.ShouldContain("routing-switch-route-row");
        markup.ShouldContain("aria-label=\"@($\"Route match key {index + 1}\")\"");
        markup.ShouldContain("aria-label=\"@($\"Route output port {index + 1}\")\"");
        markup.ShouldContain("@bind-Value=\"route.Key\"");
        markup.ShouldContain("@bind-Value=\"route.OutputPort\"");
        markup.ShouldContain("Class=\"routing-switch-route-key-field\"");
        markup.ShouldContain("Class=\"routing-switch-route-output-field\"");
        markup.ShouldContain("RemoveRoute(route)");
        markup.ShouldContain("FormatRouteDrafts");
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
        markup.ShouldNotContain("routing-switch-expression-workspace");
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
        css.ShouldContain("display: flex;");
        css.ShouldNotContain("grid-template-columns: repeat(4, minmax(0, auto));");
        css.ShouldContain(".routing-switch-token");
        css.ShouldContain("display: inline-flex;");
        css.ShouldContain("overflow-wrap: anywhere;");
        css.ShouldContain("white-space: normal;");
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
        css.ShouldContain(".routing-switch-config-row");
        css.ShouldContain(".routing-switch-rule-grid");
        css.ShouldContain(".routing-switch-expression-stack");
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
        css.ShouldContain("grid-template-columns: minmax(0, 1fr) minmax(0, 0.52fr) minmax(0, 0.62fr);");
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
        css.ShouldNotContain(".flow-node-filters");
        css.ShouldNotContain(".routing-switch-routes-field");
        css.ShouldNotContain(".routing-switch-route-table");
        css.ShouldNotContain(".routing-switch-route-toolbar");
        css.ShouldNotContain(".routing-switch-panel-title");
        css.ShouldNotContain(".routing-switch-config-grid");
        css.ShouldNotContain(".routing-switch-expression-workspace");
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
        forkMarkup.ShouldContain("ShowCategoryChip=\"false\"");
        forkMarkup.ShouldContain("routing-fork-summary");
        forkMarkup.ShouldContain("routing-fork-meta");
        forkMarkup.ShouldContain("InputTypeCaption");
        forkMarkup.ShouldContain("OutputCountCaption");
        forkMarkup.ShouldContain("BufferCaption");
        forkMarkup.ShouldContain("routing-fork-ports");
        forkMarkup.ShouldContain("aria-label=\"Routing fork outputs\"");
        forkMarkup.ShouldContain("OutputPreview");
        forkMarkup.ShouldContain("OutputPreviewOverflow");
        forkMarkup.ShouldContain("routing-fork-token");
        forkMarkup.ShouldContain("routing-fork-editor");
        forkMarkup.ShouldContain("aria-label=\"Routing fork settings\"");
        forkMarkup.ShouldContain("routing-fork-config-row");
        forkMarkup.ShouldNotContain("routing-fork-editor-surface");
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
        forkMarkup.ShouldContain("aria-label=\"Add output port\"");
        forkMarkup.ShouldContain("routing-fork-port-row");
        forkMarkup.ShouldContain("_outputDrafts");
        forkMarkup.ShouldContain("AddOutput");
        forkMarkup.ShouldContain("RemoveOutput");
        forkMarkup.ShouldContain("@bind-Value=\"output.Name\"");
        forkMarkup.ShouldContain("aria-label=\"@($\"Output port {index + 1}\")\"");
        forkMarkup.ShouldContain("aria-label=\"@($\"Remove output port {output.Name}\")\"");
        forkMarkup.ShouldContain("Class=\"routing-fork-output-name-field\"");
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
        forkCss.ShouldContain("display: flex;");
        forkCss.ShouldContain("flex-wrap: wrap;");
        forkCss.ShouldNotContain("grid-template-columns: repeat(4, minmax(0, auto));");
        forkCss.ShouldContain(".routing-fork-token");
        forkCss.ShouldContain("display: inline-flex;");
        forkCss.ShouldContain("overflow-wrap: anywhere;");
        forkCss.ShouldContain("white-space: normal;");
        forkCss.ShouldContain(".routing-fork-editor");
        forkCss.ShouldContain(".routing-fork-config-row");
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
        forkCss.ShouldContain("grid-template-columns: minmax(0, 1fr) minmax(0, 0.54fr);");
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
        mergeMarkup.ShouldContain("ShowCategoryChip=\"false\"");
        mergeMarkup.ShouldContain("routing-merge-summary");
        mergeMarkup.ShouldContain("routing-merge-meta");
        mergeMarkup.ShouldContain("InputTypeCaption");
        mergeMarkup.ShouldContain("InputCountCaption");
        mergeMarkup.ShouldContain("BufferCaption");
        mergeMarkup.ShouldContain("Merged item");
        mergeMarkup.ShouldNotContain("FlowMergeItem");
        mergeMarkup.ShouldContain("routing-merge-ports");
        mergeMarkup.ShouldContain("aria-label=\"Routing merge inputs\"");
        mergeMarkup.ShouldContain("InputPreview");
        mergeMarkup.ShouldContain("InputPreviewOverflow");
        mergeMarkup.ShouldContain("routing-merge-token");
        mergeMarkup.ShouldContain("routing-merge-editor");
        mergeMarkup.ShouldContain("aria-label=\"Routing merge settings\"");
        mergeMarkup.ShouldContain("routing-merge-config-row");
        mergeMarkup.ShouldNotContain("routing-merge-editor-surface");
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
        mergeMarkup.ShouldContain("aria-label=\"Add input port\"");
        mergeMarkup.ShouldContain("routing-merge-port-row");
        mergeMarkup.ShouldContain("_inputDrafts");
        mergeMarkup.ShouldContain("AddInput");
        mergeMarkup.ShouldContain("RemoveInput");
        mergeMarkup.ShouldContain("@bind-Value=\"input.Name\"");
        mergeMarkup.ShouldContain("aria-label=\"@($\"Input port {index + 1}\")\"");
        mergeMarkup.ShouldContain("aria-label=\"@($\"Remove input port {input.Name}\")\"");
        mergeMarkup.ShouldContain("Class=\"routing-merge-input-name-field\"");
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
        mergeCss.ShouldContain("display: flex;");
        mergeCss.ShouldContain("flex-wrap: wrap;");
        mergeCss.ShouldNotContain("grid-template-columns: repeat(4, minmax(0, auto));");
        mergeCss.ShouldContain(".routing-merge-token");
        mergeCss.ShouldContain("display: inline-flex;");
        mergeCss.ShouldContain("overflow-wrap: anywhere;");
        mergeCss.ShouldContain("white-space: normal;");
        mergeCss.ShouldContain(".routing-merge-editor");
        mergeCss.ShouldContain(".routing-merge-config-row");
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
        mergeCss.ShouldContain("grid-template-columns: minmax(0, 1fr) minmax(0, 0.54fr);");
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
        markup.ShouldContain("ShowCategoryChip=\"false\"");
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
        markup.ShouldContain("routing-window-primary-row");
        markup.ShouldContain("aria-label=\"Routing window input settings\"");
        markup.ShouldContain("routing-window-boundary-editor");
        markup.ShouldContain("aria-label=\"Routing window boundary settings\"");
        markup.ShouldContain("Label=\"Input type\"");
        markup.ShouldContain("aria-label=\"Routing window input type\"");
        markup.ShouldContain("@bind-Value=\"_inputType\"");
        markup.ShouldContain("Class=\"routing-window-input-field\"");
        markup.ShouldContain("Label=\"Input buffer\"");
        markup.ShouldContain("aria-label=\"Routing window input buffer\"");
        markup.ShouldContain("@bind-Value=\"_boundedCapacity\"");
        markup.ShouldContain("Class=\"routing-window-buffer-field\"");
        markup.ShouldContain("Label=\"Max items\"");
        markup.ShouldContain("aria-label=\"Routing window max items\"");
        markup.ShouldContain("@bind-Value=\"_maxItems\"");
        markup.ShouldContain("Class=\"routing-window-max-items-field\"");
        markup.ShouldContain("Label=\"Time window ms\"");
        markup.ShouldContain("aria-label=\"Routing window time in milliseconds\"");
        markup.ShouldContain("@bind-Value=\"_timeMilliseconds\"");
        markup.ShouldContain("Class=\"routing-window-time-field\"");
        markup.ShouldContain("Label=\"Emit partial\"");
        markup.ShouldContain("aria-label=\"Emit partial window on completion\"");
        markup.ShouldContain("@bind-Value=\"_emitPartialOnCompletion\"");
        markup.ShouldContain("Class=\"routing-window-partial-check\"");
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
        css.ShouldContain(".routing-window-primary-row");
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
        css.ShouldContain("grid-template-columns: minmax(0, 1fr) minmax(0, 0.42fr);");
        css.ShouldNotContain("grid-template-columns: minmax(0, 1fr) 160px;");
        css.ShouldContain(".routing-window-boundary-editor");
        css.ShouldContain("border-top: 1px solid color-mix(in srgb, var(--flux-border-soft) 58%, transparent);");
        css.ShouldContain("padding-top: 7px;");
        css.ShouldNotContain("padding-top: 8px;");
        css.ShouldContain("grid-template-columns: minmax(0, 1fr) minmax(0, 1fr) minmax(0, 0.68fr);");
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

        correlationMarkup.ShouldContain("ShowHeaderIcon=\"false\"");
        correlationMarkup.ShouldContain("ShowDisplayName=\"true\"");
        correlationMarkup.ShouldContain("ShowCategoryChip=\"false\"");
        correlationMarkup.ShouldContain("routing-correlation-summary");
        correlationMarkup.ShouldContain("routing-correlation-meta");
        correlationMarkup.ShouldContain("InputTypeCaption");
        correlationMarkup.ShouldContain("TimeoutCaption");
        correlationMarkup.ShouldContain("BufferCaption");
        correlationMarkup.ShouldContain("routing-correlation-rules");
        correlationMarkup.ShouldContain("aria-label=\"Routing correlation rules\"");
        correlationMarkup.ShouldContain("KeyExpressionCaption");
        correlationMarkup.ShouldContain("SideExpressionCaption");
        correlationMarkup.ShouldContain("SideFlowCaption");
        correlationMarkup.ShouldContain("CaseCaption");
        correlationMarkup.ShouldContain("PendingCaption");
        correlationMarkup.ShouldContain("routing-correlation-editor");
        correlationMarkup.ShouldContain("aria-label=\"Routing correlation settings\"");
        correlationMarkup.ShouldContain("routing-correlation-config-row");
        correlationMarkup.ShouldContain("aria-label=\"Routing correlation input settings\"");
        correlationMarkup.ShouldContain("routing-correlation-match-composer");
        correlationMarkup.ShouldContain("aria-label=\"Routing correlation matching rule\"");
        correlationMarkup.ShouldContain("routing-correlation-expression-row");
        correlationMarkup.ShouldContain("aria-label=\"Routing correlation expressions\"");
        correlationMarkup.ShouldContain("routing-correlation-side-map-row");
        correlationMarkup.ShouldContain("aria-label=\"Routing correlation side mapping\"");
        correlationMarkup.ShouldContain("@bind-Value=\"_inputType\"");
        correlationMarkup.ShouldContain("@bind-Value=\"_keyExpression\"");
        correlationMarkup.ShouldContain("@bind-Value=\"_sideExpression\"");
        correlationMarkup.ShouldContain("@bind-Value=\"_requestSide\"");
        correlationMarkup.ShouldContain("@bind-Value=\"_responseSide\"");
        correlationMarkup.ShouldContain("@bind-Value=\"_caseSensitive\"");
        correlationMarkup.ShouldContain("@bind-Value=\"_timeoutMilliseconds\"");
        correlationMarkup.ShouldContain("@bind-Value=\"_maxPending\"");
        correlationMarkup.ShouldContain("@bind-Value=\"_boundedCapacity\"");
        correlationMarkup.ShouldNotContain("routing-correlation-panel-header");
        correlationMarkup.ShouldNotContain("routing-correlation-panel-kicker");
        correlationMarkup.ShouldNotContain("routing-correlation-panel-token");
        correlationMarkup.ShouldNotContain("routing-correlation-config-grid");
        correlationMarkup.ShouldNotContain("routing-correlation-side-grid");
        correlationMarkup.ShouldNotContain("routing-correlation-form-grid");
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
        correlationMarkup.ShouldNotContain("<span>Matching</span>");
        correlationMarkup.ShouldNotContain("<MudStack");
        correlationMarkup.ShouldNotContain("<MudChip");
        correlationMarkup.ShouldNotContain("<MudGrid");
        correlationMarkup.ShouldNotContain("<MudItem");
        correlationMarkup.ShouldNotContain("d-flex flex-wrap gap-1");

        correlationCss.ShouldContain(".routing-correlation-summary");
        correlationCss.ShouldContain(".routing-correlation-meta");
        correlationCss.ShouldContain("grid-template-columns: minmax(0, 1.08fr) minmax(0, 0.68fr) minmax(0, 0.58fr) minmax(0, 0.52fr);");
        correlationCss.ShouldNotContain("grid-template-columns: minmax(0, 1fr) 112px 76px 64px;");
        correlationCss.ShouldContain("display: -webkit-box;");
        correlationCss.ShouldContain("-webkit-line-clamp: 2;");
        correlationCss.ShouldContain(".routing-correlation-rules");
        correlationCss.ShouldContain("flex-wrap: wrap;");
        correlationCss.ShouldNotContain("grid-template-columns: repeat(4, minmax(0, auto));");
        correlationCss.ShouldContain("flex: 0 0 100%;");
        correlationCss.ShouldNotContain("grid-column: 1 / -1;");
        correlationCss.ShouldContain(".routing-correlation-token");
        correlationCss.ShouldContain("display: inline-flex;");
        correlationCss.ShouldContain("overflow-wrap: anywhere;");
        correlationCss.ShouldContain("white-space: normal;");
        correlationCss.ShouldContain(".routing-correlation-editor");
        correlationCss.ShouldContain(".routing-correlation-config-row");
        correlationCss.ShouldContain(".routing-correlation-match-composer");
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
        correlationCss.ShouldContain("grid-template-columns: minmax(0, 1.18fr) minmax(0, 0.58fr) minmax(0, 0.58fr) minmax(0, 0.58fr);");
        correlationCss.ShouldNotContain("grid-template-columns: minmax(0, 1fr) 150px 150px 150px;");
        correlationCss.ShouldContain(".routing-correlation-expression-row");
        correlationCss.ShouldContain("grid-template-columns: repeat(2, minmax(0, 1fr));");
        correlationCss.ShouldNotContain("background: color-mix(in srgb, var(--flux-surface-2) 7%, transparent);");
        correlationCss.ShouldNotContain("border: 1px solid color-mix(in srgb, var(--flux-border-soft) 24%, transparent);");
        correlationCss.ShouldNotContain("padding: 7px;");
        correlationCss.ShouldNotContain(".routing-correlation-expression-grid");
        correlationCss.ShouldContain(".routing-correlation-side-map-row");
        correlationCss.ShouldContain("grid-template-columns: minmax(0, 1fr) minmax(0, 1fr) minmax(0, 0.72fr);");
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
        correlationCss.ShouldContain("@media (max-width: 840px)");
        correlationCss.ShouldNotContain(".routing-correlation-case-option > span");
        correlationCss.ShouldNotContain(".routing-correlation-config-grid");
        correlationCss.ShouldNotContain(".routing-correlation-side-grid");
        correlationCss.ShouldNotContain(".routing-correlation-form-grid");
        correlationCss.ShouldNotContain(".routing-correlation-side-panel");
        correlationCss.ShouldNotContain(".routing-correlation-limit-panel");
        correlationCss.ShouldNotContain(".routing-correlation-matching-workspace");
        correlationCss.ShouldNotContain(".routing-correlation-matching-section");
        correlationCss.ShouldNotContain("padding: 14px;");
        correlationCss.ShouldNotContain(".flow-node-filters");
        correlationCss.ShouldNotContain("border-radius: 999px;");

        joinMarkup.ShouldContain("ShowHeaderIcon=\"false\"");
        joinMarkup.ShouldContain("ShowDisplayName=\"true\"");
        joinMarkup.ShouldContain("ShowCategoryChip=\"false\"");
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
        joinMarkup.ShouldContain("routing-join-editor");
        joinMarkup.ShouldContain("aria-label=\"Routing join settings\"");
        joinMarkup.ShouldContain("routing-join-config-row");
        joinMarkup.ShouldContain("aria-label=\"Routing join input contracts\"");
        joinMarkup.ShouldContain("routing-join-match-composer");
        joinMarkup.ShouldContain("aria-label=\"Routing join matching rule\"");
        joinMarkup.ShouldContain("routing-join-key-row");
        joinMarkup.ShouldContain("aria-label=\"Routing join key expressions\"");
        joinMarkup.ShouldContain("routing-join-control-row");
        joinMarkup.ShouldContain("aria-label=\"Routing join limit settings\"");
        joinMarkup.ShouldContain("@bind-Value=\"_leftInputType\"");
        joinMarkup.ShouldContain("@bind-Value=\"_rightInputType\"");
        joinMarkup.ShouldContain("@bind-Value=\"_leftKeyExpression\"");
        joinMarkup.ShouldContain("@bind-Value=\"_rightKeyExpression\"");
        joinMarkup.ShouldContain("@bind-Value=\"_caseSensitive\"");
        joinMarkup.ShouldContain("@bind-Value=\"_timeoutMilliseconds\"");
        joinMarkup.ShouldContain("@bind-Value=\"_maxPending\"");
        joinMarkup.ShouldContain("@bind-Value=\"_boundedCapacity\"");
        joinMarkup.ShouldNotContain("routing-join-panel-header");
        joinMarkup.ShouldNotContain("routing-join-panel-kicker");
        joinMarkup.ShouldNotContain("routing-join-panel-token");
        joinMarkup.ShouldNotContain("routing-join-type-grid");
        joinMarkup.ShouldNotContain("routing-join-expression-grid");
        joinMarkup.ShouldNotContain("routing-join-limit-grid");
        joinMarkup.ShouldNotContain("routing-join-form-grid");
        joinMarkup.ShouldNotContain("routing-join-limit-panel");
        joinMarkup.ShouldNotContain("routing-join-rule-panel");
        joinMarkup.ShouldNotContain("routing-join-editor-surface");
        joinMarkup.ShouldNotContain("routing-join-match-title");
        joinMarkup.ShouldNotContain("routing-join-matching-workspace");
        joinMarkup.ShouldNotContain("routing-join-input-grid");
        joinMarkup.ShouldNotContain("routing-join-key-grid");
        joinMarkup.ShouldNotContain("routing-join-matching-section");
        joinMarkup.ShouldNotContain("routing-join-limit-row");
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
        joinCss.ShouldContain("display: flex;");
        joinCss.ShouldContain("flex-wrap: wrap;");
        joinCss.ShouldNotContain("grid-template-columns: repeat(4, minmax(0, auto));");
        joinCss.ShouldContain(".routing-join-token");
        joinCss.ShouldContain("display: inline-flex;");
        joinCss.ShouldContain("overflow-wrap: anywhere;");
        joinCss.ShouldContain("white-space: normal;");
        joinCss.ShouldContain(".routing-join-editor");
        joinCss.ShouldContain(".routing-join-config-row");
        joinCss.ShouldContain(".routing-join-match-composer");
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
        joinCss.ShouldContain(".routing-join-key-row");
        joinCss.ShouldNotContain(".routing-join-key-grid");
        joinCss.ShouldContain("grid-template-columns: repeat(2, minmax(0, 1fr));");
        joinCss.ShouldNotContain("background: color-mix(in srgb, var(--flux-surface-2) 7%, transparent);");
        joinCss.ShouldNotContain("border: 1px solid color-mix(in srgb, var(--flux-border-soft) 24%, transparent);");
        joinCss.ShouldNotContain("padding: 7px;");
        joinCss.ShouldContain(".routing-join-control-row");
        joinCss.ShouldNotContain(".routing-join-limit-row");
        joinCss.ShouldContain("grid-template-columns: minmax(0, 1fr) minmax(0, 1fr) minmax(0, 0.55fr);");
        joinCss.ShouldContain("grid-template-columns: minmax(0, 1fr) minmax(0, 1fr) minmax(0, 0.72fr);");
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
        joinCss.ShouldContain("@media (max-width: 840px)");
        joinCss.ShouldNotContain(".routing-join-case-option > span");
        joinCss.ShouldNotContain(".routing-join-type-grid");
        joinCss.ShouldNotContain(".routing-join-expression-grid");
        joinCss.ShouldNotContain(".routing-join-limit-grid");
        joinCss.ShouldNotContain(".routing-join-form-grid");
        joinCss.ShouldNotContain(".routing-join-limit-panel");
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
        markup.ShouldContain("ShowHeaderIcon=\"false\"");
        markup.ShouldContain("ShowDisplayName=\"true\"");
        markup.ShouldContain("ShowCategoryChip=\"false\"");
        markup.ShouldContain("http-client-summary");
        markup.ShouldContain("http-client-meta");
        markup.ShouldContain("http-client-meta-item target");
        markup.ShouldContain("TargetCaption");
        markup.ShouldContain("Per request");
        markup.ShouldContain("TimeoutCaption");
        markup.ShouldContain("InputBufferCaption");
        markup.ShouldContain("http-client-contracts");
        markup.ShouldContain("aria-label=\"HTTP request fields\"");
        markup.ShouldContain("aria-label=\"HTTP response fields\"");
        markup.ShouldContain("http-client-token");
        markup.ShouldContain("method");
        markup.ShouldContain("url");
        markup.ShouldContain("headers");
        markup.ShouldContain("body");
        markup.ShouldContain("status");
        markup.ShouldContain("http-client-editor");
        markup.ShouldContain("aria-label=\"HTTP client settings\"");
        markup.ShouldContain("Label=\"Base URL\"");
        markup.ShouldContain("@bind-Value=\"_baseUrl\"");
        markup.ShouldContain("Label=\"Timeout ms\"");
        markup.ShouldContain("@bind-Value=\"_defaultTimeoutMilliseconds\"");
        markup.ShouldContain("Label=\"Max body bytes\"");
        markup.ShouldContain("@bind-Value=\"_maxResponseBodyBytes\"");
        markup.ShouldContain("Label=\"Input buffer\"");
        markup.ShouldContain("@bind-Value=\"_boundedCapacity\"");
        markup.ShouldContain("Label=\"Follow redirects\"");
        markup.ShouldContain("@bind-Value=\"_followRedirects\"");
        markup.ShouldContain("Label=\"Non-success status emits error\"");
        markup.ShouldContain("@bind-Value=\"_treatNonSuccessStatusAsError\"");
        markup.ShouldContain("Label=\"Default headers\"");
        markup.ShouldContain("@bind-Value=\"_defaultHeadersText\"");
        markup.ShouldContain("Class=\"http-client-base-url-field\"");
        markup.ShouldContain("Class=\"http-client-buffer-field\"");
        markup.ShouldNotContain("<Stat ");
        markup.ShouldNotContain("<MudChip");
        markup.ShouldNotContain("d-flex flex-wrap gap-1");
        markup.ShouldNotContain("HttpResponseOutput");
        markup.ShouldNotContain("HttpRequestInput");

        css.ShouldContain(".http-client-summary");
        css.ShouldContain(".http-client-meta");
        css.ShouldContain("grid-template-columns: minmax(0, 1.26fr) minmax(0, 0.62fr) minmax(0, 0.72fr);");
        css.ShouldNotContain("grid-template-columns: minmax(0, 1fr) 70px 84px;");
        css.ShouldContain(".http-client-contracts");
        css.ShouldContain(".http-client-contract");
        css.ShouldContain("display: flex;");
        css.ShouldContain("flex-wrap: wrap;");
        css.ShouldNotContain("grid-template-columns: repeat(4, minmax(0, auto));");
        css.ShouldContain(".http-client-contract-label");
        css.ShouldContain("flex: 0 0 100%;");
        css.ShouldContain("white-space: normal;");
        css.ShouldContain(".http-client-token");
        css.ShouldContain("display: inline-flex;");
        css.ShouldContain("overflow-wrap: anywhere;");
        css.ShouldContain("white-space: normal;");
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
        markup.ShouldContain("ShowCategoryChip=\"false\"");
        markup.ShouldContain("payload-inspector-node-summary");
        markup.ShouldContain("payload-inspector-node-meta");
        markup.ShouldContain("MqttEnvelope");
        markup.ShouldContain("InspectedMqttMessage");
        markup.ShouldContain("Auto detect");
        markup.ShouldContain("payload-inspector-node-contracts");
        markup.ShouldContain("aria-label=\"Payload inspector input fields\"");
        markup.ShouldContain("aria-label=\"Payload inspector result fields\"");
        markup.ShouldContain("payload-inspector-node-token");
        markup.ShouldContain("topic");
        markup.ShouldContain("payload");
        markup.ShouldContain("kind");
        markup.ShouldContain("contentType");
        markup.ShouldContain("formatted");
        markup.ShouldContain("hexDump");
        markup.ShouldContain("<span>Input</span>");
        markup.ShouldContain("<span>Output</span>");
        markup.ShouldContain("<Editor>");
        markup.ShouldContain("payload-inspector-node-editor");
        markup.ShouldContain("aria-label=\"Payload inspector details\"");
        markup.ShouldContain("payload-inspector-node-editor-table");
        markup.ShouldContain("aria-label=\"Payload inspector contract details\"");
        markup.ShouldContain("payload-inspector-node-editor-row");
        markup.ShouldContain("payload-inspector-node-editor-label");
        markup.ShouldContain("payload-inspector-node-editor-tokens");
        markup.ShouldContain("aria-label=\"Payload inspector detection modes\"");
        markup.ShouldContain("aria-label=\"Payload inspector input contract\"");
        markup.ShouldContain("aria-label=\"Payload inspector output contract\"");
        markup.ShouldContain("base64");
        markup.ShouldContain("binary");
        markup.ShouldNotContain("payload-inspector-node-editor-summary");
        markup.ShouldNotContain("payload-inspector-node-editor-cell");
        markup.ShouldNotContain("aria-label=\"Payload inspector contract summary\"");
        markup.ShouldNotContain("payload-inspector-node-editor-contract-list");
        markup.ShouldNotContain("payload-inspector-node-editor-contract-item");
        markup.ShouldNotContain("payload-inspector-node-editor-contract-label");
        markup.ShouldNotContain("aria-label=\"Payload inspector contracts\"");
        markup.ShouldNotContain("payload-inspector-node-config-summary");
        markup.ShouldNotContain("payload-inspector-node-setting-line");
        markup.ShouldNotContain("payload-inspector-node-token-group");
        markup.ShouldNotContain("aria-label=\"Payload inspector decode behavior\"");
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
        markup.ShouldNotContain("<span>Contract</span>");
        markup.ShouldNotContain("<span>Fields</span>");
        markup.ShouldNotContain("<MudText");
        markup.ShouldNotContain("<MudChip");
        markup.ShouldNotContain("<MudStack");
        markup.ShouldNotContain("<MudGrid");
        markup.ShouldNotContain("d-flex");
        markup.ShouldNotContain("gap-1");

        css.ShouldContain(".payload-inspector-node-summary");
        css.ShouldContain(".payload-inspector-node-meta");
        css.ShouldContain("grid-template-columns: minmax(0, 0.72fr) minmax(0, 1.38fr) minmax(0, 0.9fr);");
        css.ShouldNotContain("grid-template-columns: 76px minmax(0, 1fr) 98px;");
        css.ShouldContain(".payload-inspector-node-contracts");
        css.ShouldContain(".payload-inspector-node-contract");
        css.ShouldContain("grid-template-columns: repeat(2, minmax(0, 1fr));");
        css.ShouldContain(".payload-inspector-node-token");
        css.ShouldContain("overflow-wrap: anywhere;");
        css.ShouldContain("white-space: normal;");
        css.ShouldContain(".payload-inspector-node-editor");
        css.ShouldNotContain("gap: 10px;");
        css.ShouldContain("gap: 7px;");
        css.ShouldNotContain("gap: 8px;");
        css.ShouldNotContain(".payload-inspector-node-editor-surface");
        css.ShouldNotContain("background: color-mix(in srgb, var(--flux-surface-2) 4%, transparent);");
        css.ShouldNotContain("border: 1px solid color-mix(in srgb, var(--flux-border-soft) 42%, transparent);");
        css.ShouldNotContain("padding: 12px;");
        css.ShouldContain(".payload-inspector-node-editor-table");
        css.ShouldContain(".payload-inspector-node-editor-row");
        css.ShouldContain("grid-template-columns: minmax(0, 0.56fr) minmax(0, 0.84fr) minmax(0, 1.6fr);");
        css.ShouldNotContain("grid-template-columns: 84px minmax(150px, 0.42fr) minmax(0, 1fr);");
        css.ShouldContain("min-height: 42px;");
        css.ShouldContain("padding: 8px 2px;");
        css.ShouldNotContain("padding: 9px 2px;");
        css.ShouldContain(".payload-inspector-node-editor-label");
        css.ShouldContain(".payload-inspector-node-editor-row > strong");
        css.ShouldContain(".payload-inspector-node-editor-tokens");
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
        markup.ShouldContain("ShowCategoryChip=\"false\"");
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
        markup.ShouldContain("aria-label=\"Fallback component port contracts\"");
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
        markup.ShouldContain("ShowCategoryChip=\"false\"");
        markup.ShouldContain("generic-node-summary");
        markup.ShouldContain("generic-node-description");
        markup.ShouldContain("SummaryCaption");
        markup.ShouldContain("generic-node-meta");
        markup.ShouldContain("InputCount");
        markup.ShouldContain("OutputCount");
        markup.ShouldContain("generic-node-ports");
        markup.ShouldContain("aria-label=\"Generic node ports\"");
        markup.ShouldContain("OutputPortPreview");
        markup.ShouldContain("InputPortPreview");
        markup.ShouldContain("PortPreviewOverflow");
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
        markup.ShouldContain("aria-label=\"Generic component port contracts\"");
        markup.ShouldNotContain("generic-node-editor-port-header");
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
        css.ShouldNotContain(".generic-node-editor-port-header");
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
        markup.ShouldContain("ShowCategoryChip=\"false\"");
        markup.ShouldContain("EditDialogMaxWidth=\"MaxWidth.Medium\"");
        markup.ShouldContain("metrics-summary");
        markup.ShouldContain("metrics-status-line");
        markup.ShouldContain("metrics-status-item");
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
        markup.ShouldContain("@bind-Value=\"_boundedCapacity\"");
        markup.ShouldContain("Class=\"metrics-buffer-field\"");
        markup.ShouldContain("Label=\"Rate window seconds\"");
        markup.ShouldContain("@bind-Value=\"_rateWindowSeconds\"");
        markup.ShouldContain("Class=\"metrics-rate-window-field\"");
        markup.ShouldContain("Label=\"Readout columns\"");
        markup.ShouldContain("@bind-Value=\"_metricCardColumns\"");
        markup.ShouldContain("Class=\"metrics-readout-columns-field\"");
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
        css.ShouldContain(".metrics-status-line");
        css.ShouldContain(".metrics-status-item");
        css.ShouldContain("grid-template-columns: repeat(2, minmax(0, 1fr));");
        css.ShouldNotContain("grid-template-columns: auto minmax(0, 1fr) auto minmax(0, 1fr);");
        css.ShouldContain(".metrics-readout-strip");
        css.ShouldContain("grid-template-columns: repeat(var(--metric-readout-columns), minmax(0, 1fr));");
        css.ShouldContain(".metrics-readout-token");
        css.ShouldContain(".metrics-readout-value");
        css.ShouldContain("display: flex;");
        css.ShouldContain("justify-content: space-between;");
        css.ShouldContain("background: color-mix(in srgb, var(--flux-surface-2) 20%, transparent);");
        css.ShouldNotContain("border-left: 1px solid");
        css.ShouldContain(".metrics-readout-retained");
        css.ShouldContain("color-mix(in srgb, var(--mud-palette-tertiary) 76%, var(--mud-palette-text-primary));");
        css.ShouldNotContain("color-mix(in srgb, var(--mud-palette-warning) 76%, var(--flux-border-soft));");
        css.ShouldContain("text-overflow: ellipsis;");
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
        markup.ShouldContain("ShowHeaderIcon=\"false\"");
        markup.ShouldContain("ShowDisplayName=\"true\"");
        markup.ShouldContain("ShowCategoryChip=\"false\"");
        markup.ShouldContain("payload-inspect-summary");
        markup.ShouldContain("payload-inspect-meta");
        markup.ShouldContain("payload-inspect-meta-item input");
        markup.ShouldContain("Payload request");
        markup.ShouldContain("PreviewCaption");
        markup.ShouldContain("FormatCapCaption");
        markup.ShouldContain("InputBufferCaption");
        markup.ShouldContain("payload-inspect-contracts");
        markup.ShouldContain("aria-label=\"Payload inspection request fields\"");
        markup.ShouldContain("aria-label=\"Payload inspection result fields\"");
        markup.ShouldContain("payload-inspect-token");
        markup.ShouldContain("text");
        markup.ShouldContain("bytes");
        markup.ShouldContain("contentType");
        markup.ShouldContain("encodingHint");
        markup.ShouldContain("kind");
        markup.ShouldContain("byteCount");
        markup.ShouldContain("preview");
        markup.ShouldContain("payload-inspect-editor");
        markup.ShouldContain("aria-label=\"Payload inspection settings\"");
        markup.ShouldContain("payload-inspect-number-grid");
        markup.ShouldContain("Label=\"Preview bytes\"");
        markup.ShouldContain("@bind-Value=\"_maxPreviewBytes\"");
        markup.ShouldContain("Label=\"Formatted chars\"");
        markup.ShouldContain("@bind-Value=\"_maxFormattedChars\"");
        markup.ShouldContain("Label=\"Input buffer\"");
        markup.ShouldContain("@bind-Value=\"_boundedCapacity\"");
        markup.ShouldContain("Label=\"Detect Base64\"");
        markup.ShouldContain("@bind-Value=\"_detectBase64\"");
        markup.ShouldContain("Label=\"Format JSON\"");
        markup.ShouldContain("@bind-Value=\"_formatJson\"");
        markup.ShouldContain("Label=\"Format XML\"");
        markup.ShouldContain("@bind-Value=\"_formatXml\"");
        markup.ShouldContain("Class=\"payload-inspect-preview-field\"");
        markup.ShouldContain("Class=\"payload-inspect-buffer-field\"");
        markup.ShouldNotContain("<Stat ");
        markup.ShouldNotContain("<MudChip");
        markup.ShouldNotContain("d-flex flex-wrap gap-1");
        markup.ShouldNotContain("PayloadInspectionRequest");
        markup.ShouldNotContain("PayloadInspectionResult");

        css.ShouldContain(".payload-inspect-summary");
        css.ShouldContain(".payload-inspect-meta");
        css.ShouldContain("grid-template-columns: minmax(0, 1.22fr) minmax(0, 0.64fr) minmax(0, 0.7fr) minmax(0, 0.76fr);");
        css.ShouldNotContain("grid-template-columns: minmax(0, 1fr) 72px 78px 84px;");
        css.ShouldContain(".payload-inspect-contracts");
        css.ShouldContain(".payload-inspect-contract");
        css.ShouldContain("display: flex;");
        css.ShouldContain("flex-wrap: wrap;");
        css.ShouldNotContain("grid-template-columns: repeat(4, minmax(0, auto));");
        css.ShouldContain(".payload-inspect-contract-label");
        css.ShouldContain("flex: 0 0 100%;");
        css.ShouldContain("white-space: normal;");
        css.ShouldContain(".payload-inspect-token");
        css.ShouldContain("display: inline-flex;");
        css.ShouldContain("overflow-wrap: anywhere;");
        css.ShouldContain("white-space: normal;");
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
        markup.ShouldContain("ShowCategoryChip=\"false\"");
        markup.ShouldContain("metric-source-summary");
        markup.ShouldContain("metric-source-meta");
        markup.ShouldContain("MetricCaption");
        markup.ShouldContain("LatestValue");
        markup.ShouldContain("StartModeCaption");
        markup.ShouldContain("BufferCaption");
        markup.ShouldContain("metric-source-parameters");
        markup.ShouldContain("aria-label=\"Metric source parameters\"");
        markup.ShouldContain("ParameterPreview");
        markup.ShouldContain("ParameterPreviewOverflow");
        markup.ShouldContain("metric-source-contract");
        markup.ShouldContain("aria-label=\"Metric source output fields\"");
        markup.ShouldContain("metric-source-token");
        markup.ShouldContain("NumberMetricReading");
        markup.ShouldContain("metricId");
        markup.ShouldContain("timestamp");
        markup.ShouldContain("value");
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
        markup.ShouldContain("Class=\"metric-source-buffer-field\"");
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
        css.ShouldContain("grid-template-columns: minmax(0, 1.42fr) minmax(0, 0.72fr) minmax(0, 0.62fr) minmax(0, 0.72fr);");
        css.ShouldNotContain("grid-template-columns: minmax(0, 1fr) 64px 56px 64px;");
        css.ShouldContain(".metric-source-parameters");
        css.ShouldContain(".metric-source-contract");
        css.ShouldContain("display: flex;");
        css.ShouldContain("flex-wrap: wrap;");
        css.ShouldContain(".metric-source-contract-label");
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
        markup.ShouldContain("ShowCategoryChip=\"false\"");
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
        markup.ShouldContain("generated-source-contract");
        markup.ShouldContain("aria-label=\"Generated source output fields\"");
        markup.ShouldContain("generated-source-token");
        markup.ShouldContain("MqttEnvelope");
        markup.ShouldContain("topic");
        markup.ShouldContain("payload");
        markup.ShouldContain("qos");
        markup.ShouldContain("generated-source-editor");
        markup.ShouldContain("aria-label=\"Generated source settings\"");
        markup.ShouldContain("Label=\"Output buffer\"");
        markup.ShouldContain("@bind-Value=\"_boundedCapacity\"");
        markup.ShouldNotContain("generated-source-editor-surface");
        markup.ShouldNotContain("generated-source-message-panel");
        markup.ShouldNotContain("generated-source-panel-header");
        markup.ShouldNotContain("generated-source-panel-kicker");
        markup.ShouldNotContain("generated-source-panel-token");
        markup.ShouldNotContain("MessagePanelCaption");
        markup.ShouldContain("generated-source-settings-row");
        markup.ShouldNotContain("generated-source-control-strip");
        markup.ShouldNotContain("generated-source-editor-toolbar");
        markup.ShouldNotContain("generated-source-action-row");
        markup.ShouldContain("aria-label=\"Generated source configuration\"");
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
        markup.ShouldContain("aria-label=\"Add generated message\"");
        markup.ShouldContain("RemoveMessage(index)");
        markup.ShouldContain("aria-label=\"@($\"Remove generated message {index + 1}\")\"");
        markup.ShouldContain("ValidateEditor");
        markup.ShouldContain("Add at least one generated message before saving.");
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
        css.ShouldContain("grid-template-columns: minmax(0, 0.74fr) minmax(0, 1.32fr) minmax(0, 0.7fr);");
        css.ShouldNotContain("grid-template-columns: 74px minmax(0, 1fr) 70px;");
        css.ShouldContain("-webkit-line-clamp: 2;");
        css.ShouldContain(".generated-source-previews");
        css.ShouldContain(".generated-source-preview-row");
        css.ShouldContain("grid-template-columns: minmax(0, 1fr) auto auto;");
        css.ShouldContain("text-overflow: ellipsis;");
        css.ShouldContain(".generated-source-contract");
        css.ShouldContain("display: flex;");
        css.ShouldContain("flex-wrap: wrap;");
        css.ShouldNotContain("grid-template-columns: repeat(4, minmax(0, auto));");
        css.ShouldContain("flex: 0 0 100%;");
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
        css.ShouldContain(".generated-source-settings-row");
        css.ShouldNotContain(".generated-source-control-strip");
        css.ShouldNotContain(".generated-source-editor-toolbar");
        css.ShouldNotContain(".generated-source-action-row");
        css.ShouldContain("grid-template-columns: minmax(0, 1fr);");
        css.ShouldContain("max-width: 220px;");
        css.ShouldNotContain("grid-template-columns: minmax(180px, 220px) 28px;");
        css.ShouldNotContain("grid-template-columns: minmax(180px, 220px);");
        css.ShouldContain(".generated-source-message-table");
        css.ShouldContain("border-top: 1px solid color-mix(in srgb, var(--flux-border-soft) 58%, transparent);");
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
        markup.ShouldContain("ShowCategoryChip=\"false\"");
        markup.ShouldContain("replay-source-summary");
        markup.ShouldContain("replay-source-meta");
        markup.ShouldContain("SessionCaption");
        markup.ShouldContain("SpeedCaption");
        markup.ShouldContain("BufferCaption");
        markup.ShouldContain("replay-source-contract");
        markup.ShouldContain("aria-label=\"Replay source output fields\"");
        markup.ShouldContain("replay-source-token");
        markup.ShouldContain("MqttEnvelope");
        markup.ShouldContain("topic");
        markup.ShouldContain("payload");
        markup.ShouldContain("qos");
        markup.ShouldContain("receivedAt");
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
        markup.ShouldContain("@bind-Value=\"_sessionId\"");
        markup.ShouldContain("Label=\"Session ID\"");
        markup.ShouldContain("aria-label=\"Replay session ID\"");
        markup.ShouldNotContain("replay-source-main-grid");
        markup.ShouldNotContain("replay-source-playback-grid");
        markup.ShouldNotContain("replay-source-speed-cell");
        markup.ShouldNotContain("replay-source-field-note");
        markup.ShouldNotContain("1x is real time");
        markup.ShouldContain("Label=\"Playback speed\"");
        markup.ShouldContain("aria-label=\"Replay playback speed\"");
        markup.ShouldContain("@bind-Value=\"_speed\"");
        markup.ShouldContain("Label=\"Output buffer\"");
        markup.ShouldContain("aria-label=\"Replay output buffer\"");
        markup.ShouldContain("@bind-Value=\"_boundedCapacity\"");
        markup.ShouldNotContain("replay-source-config-grid");
        markup.ShouldNotContain("replay-source-number-grid");
        markup.ShouldNotContain("<Stat ");
        markup.ShouldNotContain("<MudStack");
        markup.ShouldNotContain("<MudChip");
        markup.ShouldNotContain("<MudGrid");
        markup.ShouldNotContain("d-flex flex-wrap gap-1");

        css.ShouldContain(".replay-source-summary");
        css.ShouldContain(".replay-source-meta");
        css.ShouldContain("grid-template-columns: minmax(0, 1.3fr) minmax(0, 0.56fr) minmax(0, 0.56fr);");
        css.ShouldNotContain("grid-template-columns: minmax(0, 1fr) 70px 70px;");
        css.ShouldContain("-webkit-line-clamp: 2;");
        css.ShouldContain(".replay-source-contract");
        css.ShouldContain("display: flex;");
        css.ShouldContain("flex-wrap: wrap;");
        css.ShouldNotContain("grid-template-columns: repeat(5, minmax(0, auto));");
        css.ShouldContain(".replay-source-contract-label");
        css.ShouldContain("flex: 0 0 100%;");
        css.ShouldContain("white-space: normal;");
        css.ShouldContain(".replay-source-token");
        css.ShouldContain("display: inline-flex;");
        css.ShouldContain("overflow-wrap: anywhere;");
        css.ShouldContain("white-space: normal;");
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
        css.ShouldContain("grid-template-columns: minmax(0, 1.24fr) minmax(0, 0.58fr) minmax(0, 0.58fr);");
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
        css.ShouldContain("text-overflow: ellipsis;");
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
        markup.ShouldContain("ShowCategoryChip=\"false\"");
        markup.ShouldContain("stored-session-source-summary");
        markup.ShouldContain("stored-session-source-meta");
        markup.ShouldContain("SessionCaption");
        markup.ShouldContain("TimingCaption");
        markup.ShouldContain("SpeedCaption");
        markup.ShouldContain("BufferCaption");
        markup.ShouldContain("stored-session-source-contract");
        markup.ShouldContain("aria-label=\"Stored session source output fields\"");
        markup.ShouldContain("stored-session-source-token");
        markup.ShouldContain("MqttEnvelope");
        markup.ShouldContain("topic");
        markup.ShouldContain("payload");
        markup.ShouldContain("qos");
        markup.ShouldContain("receivedAt");
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
        markup.ShouldContain("@bind-Value=\"_sessionId\"");
        markup.ShouldContain("Label=\"Preserve timing\"");
        markup.ShouldContain("aria-label=\"Preserve original session timing\"");
        markup.ShouldContain("@bind-Value=\"_preserveTiming\"");
        markup.ShouldNotContain("stored-session-source-timing-row");
        markup.ShouldContain("Label=\"Playback speed\"");
        markup.ShouldContain("aria-label=\"Stored session playback speed\"");
        markup.ShouldContain("@bind-Value=\"_speed\"");
        markup.ShouldContain("Disabled=\"@(!_preserveTiming)\"");
        markup.ShouldNotContain("stored-session-source-main-grid");
        markup.ShouldNotContain("stored-session-source-timing-grid");
        markup.ShouldNotContain("stored-session-source-speed-cell");
        markup.ShouldNotContain("stored-session-source-field-note");
        markup.ShouldNotContain("1x is real time");
        markup.ShouldNotContain("HelperText=\"1 = real-time\"");
        markup.ShouldContain("Label=\"Output buffer\"");
        markup.ShouldContain("aria-label=\"Stored session output buffer\"");
        markup.ShouldContain("@bind-Value=\"_boundedCapacity\"");
        markup.ShouldNotContain("stored-session-source-config-grid");
        markup.ShouldNotContain("<Stat ");
        markup.ShouldNotContain("<MudStack");
        markup.ShouldNotContain("<MudChip");
        markup.ShouldNotContain("<MudGrid");
        markup.ShouldNotContain("<MudAlert");
        markup.ShouldNotContain("d-flex flex-wrap gap-1");

        css.ShouldContain(".stored-session-source-summary");
        css.ShouldContain(".stored-session-source-meta");
        css.ShouldContain("grid-template-columns: minmax(0, 1.28fr) minmax(0, 0.62fr) minmax(0, 0.52fr) minmax(0, 0.52fr);");
        css.ShouldNotContain("grid-template-columns: minmax(0, 1fr) 76px 64px 64px;");
        css.ShouldContain("-webkit-line-clamp: 2;");
        css.ShouldContain(".stored-session-source-contract");
        css.ShouldContain("display: flex;");
        css.ShouldContain("flex-wrap: wrap;");
        css.ShouldNotContain("grid-template-columns: repeat(5, minmax(0, auto));");
        css.ShouldContain(".stored-session-source-contract-label");
        css.ShouldContain("flex: 0 0 100%;");
        css.ShouldContain("white-space: normal;");
        css.ShouldContain(".stored-session-source-token");
        css.ShouldContain("display: inline-flex;");
        css.ShouldContain("overflow-wrap: anywhere;");
        css.ShouldContain("white-space: normal;");
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
        css.ShouldContain("grid-template-columns: minmax(0, 1.24fr) minmax(0, 0.66fr) minmax(0, 0.58fr) minmax(0, 0.58fr);");
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
        css.ShouldContain("text-overflow: ellipsis;");
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
        markup.ShouldContain("TimerTokenClass");
        markup.ShouldNotContain("CategoryColor=\"@Color.Warning\"");
        markup.ShouldContain("ShowHeaderIcon=\"false\"");
        markup.ShouldContain("ShowDisplayName=\"true\"");
        markup.ShouldContain("ShowCategoryChip=\"false\"");
        markup.ShouldContain("timer-node-summary");
        markup.ShouldContain("timer-node-meta");
        markup.ShouldContain("ModeCaption");
        markup.ShouldContain("PrimaryCaption");
        markup.ShouldContain("SecondaryCaption");
        markup.ShouldContain("BoundedCapacityCaption");
        markup.ShouldContain("timer-node-contract");
        markup.ShouldContain("aria-label=\"@ContractAriaLabel\"");
        markup.ShouldContain("timer-node-token");
        markup.ShouldContain("class=\"@TimerTokenClass\"");
        markup.ShouldContain("TimerTick");
        markup.ShouldContain("ScheduleTick");
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
        markup.ShouldContain("@bind-Value=\"_intervalMilliseconds\"");
        markup.ShouldContain("Label=\"Initial delay ms\"");
        markup.ShouldContain("@bind-Value=\"_initialDelayMilliseconds\"");
        markup.ShouldContain("Label=\"Cron\"");
        markup.ShouldContain("@bind-Value=\"_cron\"");
        markup.ShouldContain("Label=\"Time zone\"");
        markup.ShouldContain("@bind-Value=\"_timeZoneId\"");
        markup.ShouldContain("Label=\"Input type\"");
        markup.ShouldContain("@bind-Value=\"_inputType\"");
        markup.ShouldContain("Label=\"Delay ms\"");
        markup.ShouldContain("@bind-Value=\"_delayMilliseconds\"");
        markup.ShouldContain("Label=\"Quiet period ms\"");
        markup.ShouldContain("@bind-Value=\"_quietPeriodMilliseconds\"");
        markup.ShouldContain("Label=\"Output buffer\"");
        markup.ShouldContain("Label=\"Input buffer\"");
        markup.ShouldContain("@bind-Value=\"_boundedCapacity\"");
        markup.ShouldContain("Label=\"Emit immediately\"");
        markup.ShouldContain("@bind-Value=\"_emitImmediately\"");
        markup.ShouldContain("Label=\"Emit first immediately\"");
        markup.ShouldContain("@bind-Value=\"_emitFirstImmediately\"");
        markup.ShouldContain("InputTypeSelect()");
        markup.ShouldContain("OutputBufferField()");
        markup.ShouldContain("InputBufferField()");
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
        css.ShouldContain("grid-template-columns: minmax(0, 0.72fr) minmax(0, 1.08fr) minmax(0, 0.9fr) minmax(0, 0.62fr);");
        css.ShouldNotContain("grid-template-columns: 72px minmax(0, 1fr) minmax(0, 0.82fr) 64px;");
        css.ShouldContain(".timer-node-contract");
        css.ShouldContain("flex-wrap: wrap;");
        css.ShouldNotContain("grid-template-columns: repeat(4, minmax(0, auto));");
        css.ShouldContain(".timer-node-contract-label");
        css.ShouldContain("flex: 0 0 100%;");
        css.ShouldNotContain("grid-column: 1 / -1;");
        css.ShouldContain(".timer-node-token");
        css.ShouldContain("display: inline-flex;");
        css.ShouldContain("color-mix(in srgb, var(--mud-palette-primary) 78%, var(--mud-palette-text-primary));");
        css.ShouldContain(".timer-node-token.passthrough");
        css.ShouldContain("color-mix(in srgb, var(--mud-palette-secondary) 78%, var(--mud-palette-text-primary));");
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
        shellMarkup.ShouldContain("ShowHeaderIcon");
        shellMarkup.ShouldContain("ShowDisplayName");
        shellMarkup.ShouldContain("ShowCategoryChip");
        shellMarkup.ShouldContain("HeaderBadge");
        shellMarkup.ShouldContain("EditDialogContentClass");
        shellMarkup.ShouldContain("EditorValidationError");
        shellMarkup.ShouldContain("flow-node-type-icon");
        shellMarkup.ShouldContain("flow-node-name");
        shellMarkup.ShouldContain("flow-node-display-name");
        shellMarkup.ShouldNotContain("Color=\"Color.Secondary\" Class=\"flow-node-display-name\"");
        shellMarkup.ShouldContain("flow-node-action flow-node-edit");
        shellMarkup.ShouldContain("Icons.Material.Filled.Settings");
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
        css.ShouldContain("flex: 0 0 24px;");
        css.ShouldContain(".flow-designer-root ::deep .flow-node-category-token");
        css.ShouldContain("max-width: 74px;");
        css.ShouldContain(".flow-designer-root ::deep .flow-node-display-name");
        css.ShouldContain("color: color-mix(in srgb, var(--mud-palette-text-secondary) 72%, var(--mud-palette-text-disabled));");
        css.ShouldContain("font-weight: 620;");
        css.ShouldContain("opacity: 0.9;");
        css.ShouldContain(".flow-designer-root ::deep .flow-node-divider");
        css.ShouldContain("height: 1px;");
        css.ShouldContain(".flow-designer-root ::deep .flow-node-activity");
        css.ShouldContain("align-items: center;");
        css.ShouldContain("grid-template-columns: 7px minmax(0, 1fr);");
        css.ShouldContain("min-height: 18px;");
        css.ShouldContain("border-top: 1px solid color-mix(in srgb, var(--flux-border-soft) 54%, transparent);");
        css.ShouldContain(".flow-designer-root ::deep .flow-node-activity-dot");
        css.ShouldContain("color: color-mix(in srgb, var(--mud-palette-text-secondary) 76%, var(--mud-palette-text-disabled));");
        css.ShouldContain("text-overflow: ellipsis;");
        css.ShouldNotContain("grid-template-columns: 16px minmax(0, 1fr);");
        css.ShouldNotContain(".flow-designer-root ::deep .flow-node-activity-icon");
        css.ShouldNotContain(".flow-node-category-chip");
        css.ShouldNotContain(".mud-alert-message");
        css.ShouldNotContain(".mud-alert-icon");
        css.ShouldNotContain(".node-stat");
        css.ShouldNotContain(".node-stat-icon");
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
        markup.ShouldContain("title=\"App JSON\"");
        markup.ShouldContain("Icon=\"@Icons.Material.Filled.Code\"");
        markup.ShouldContain("<span class=\"project-tab-name\">App JSON</span>");
        markup.ShouldContain("<span class=\"project-tabbar-app-name\">@active.Name</span>");
        markup.ShouldContain("aria-label=\"@($\"Close app {active.Name}\")\"");
        markup.ShouldContain("@onclick:stopPropagation=\"true\"");
        markup.ShouldContain("@onmousedown:stopPropagation=\"true\"");
        markup.ShouldContain("@onclick=\"@ToggleJsonView\"");
        markup.ShouldContain("private void ToggleJsonView()");
        markup.ShouldContain("private void OpenJsonView()");
        markup.ShouldContain("private void CloseJsonView()");
        markup.ShouldContain("SyncActiveArtifactState();");
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
        markup.ShouldContain("node-edit-dialog-status");
        markup.ShouldContain("node-edit-dialog-content");
        markup.ShouldContain("ContentClassName");
        markup.ShouldContain("[Parameter] public string? ContentClass");
        markup.ShouldContain("role=\"form\" aria-label=\"Edit node\"");
        markup.ShouldContain("node-edit-dialog-section node-edit-dialog-identity");
        markup.ShouldContain("aria-label=\"Node identity\"");
        markup.ShouldContain("node-edit-dialog-editor");
        markup.ShouldContain("Icons.Material.Filled.Settings");
        markup.ShouldContain("aria-describedby=\"@StatusElementId\"");
        markup.ShouldContain("role=\"status\"");
        markup.ShouldContain("aria-live=\"polite\"");
        markup.ShouldContain("OnNodeIdKeyDown");
        markup.ShouldContain("@if (!CanSubmit)");
        markup.ShouldContain("StatusElementId");
        markup.ShouldContain("SubmitStatusText");
        markup.ShouldContain("<span id=\"node-edit-dialog-status\"");
        markup.ShouldContain("class=\"node-edit-dialog-action-status\"");
        markup.ShouldContain("[Parameter] public Func<string?>? EditorValidationError");
        markup.ShouldContain("private string? _editorError;");
        markup.ShouldContain("private string? EditorError => _editorError;");
        markup.ShouldContain("private void RefreshValidationState()");
        markup.ShouldContain("RefreshValidationState();");
        markup.ShouldContain("string.IsNullOrWhiteSpace(EditorError)");
        markup.ShouldContain("NodeIdError ?? EditorError ?? \"Review required\"");
        markup.ShouldNotContain("Ready to save");
        markup.ShouldNotContain("SubmitStatusClass");
        markup.ShouldContain("node-edit-dialog-actions");
        markup.ShouldContain("aria-label=\"Cancel node edit\"");
        markup.ShouldContain("aria-label=\"Save node edit\"");
        markup.ShouldContain("Color=\"Color.Primary\"");
        markup.ShouldNotContain("Color=\"@CategoryColor\"");
        markup.ShouldNotContain("HelperText=");
        markup.ShouldNotContain("ErrorText=");
        markup.ShouldNotContain("node-edit-dialog-meta-strip");
        markup.ShouldNotContain("node-edit-dialog-title-meta");
        markup.ShouldNotContain("node-edit-dialog-section-title");
        markup.ShouldNotContain("node-edit-dialog-section-head");
        markup.ShouldNotContain("node-edit-dialog-section node-edit-dialog-editor");
        markup.ShouldNotContain("Icons.Material.Filled.Badge");
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
        css.ShouldContain(".node-edit-dialog-editor");
        css.ShouldContain("display: contents;");
        css.ShouldContain(".node-edit-dialog-content.dynamic-mapper-dialog");
        css.ShouldContain("grid-template-areas:");
        css.ShouldContain("\"identity config\"");
        css.ShouldContain("\"workbench workbench\"");
        css.ShouldContain("height: min(84vh, 820px);");
        css.ShouldContain("overflow-x: hidden;");
        css.ShouldContain(".node-edit-dialog-content ::deep(.mud-input-control)");
        css.ShouldContain(".node-edit-dialog-content ::deep(.mud-input-label)");
        css.ShouldContain(".node-edit-dialog-content ::deep(.mud-input-root)");
        css.ShouldContain("color: color-mix(in srgb, var(--mud-palette-text-primary) 80%, var(--mud-palette-text-secondary));");
        css.ShouldContain(".node-edit-dialog-content.dynamic-mapper-dialog ::deep(.dynamic-mapper-workspace)");
        css.ShouldContain(".node-edit-dialog-content.dynamic-mapper-dialog ::deep(.dynamic-mapper-control-row)");
        css.ShouldContain(".node-edit-dialog-editor ::deep(.dynamic-mapper-workspace .dynamic-mapper-monaco-editor)");
        css.ShouldContain(".node-edit-dialog-content.json-schema-validator-dialog");
        css.ShouldContain("\"schema schema\"");
        css.ShouldContain("height: min(80vh, 760px);");
        css.ShouldContain(".node-edit-dialog-content.json-schema-validator-dialog ::deep(.json-schema-validator-editor)");
        css.ShouldContain(".node-edit-dialog-content.json-schema-validator-dialog ::deep(.json-schema-validator-config-row)");
        css.ShouldContain(".node-edit-dialog-content.json-schema-validator-dialog ::deep(.json-schema-validator-schema-area)");
        css.ShouldNotContain(".node-edit-dialog-editor ::deep(.dynamic-mapper-workbench .dynamic-mapper-panel)");
        css.ShouldNotContain("height: 96px;");
        css.ShouldNotContain("height: 280px;");
        css.ShouldNotContain("height: 570px;");
        css.ShouldContain(".node-edit-dialog-action-status");
        css.ShouldNotContain(".node-edit-dialog-action-status.ready");
        css.ShouldContain("min-height: 28px;");
        css.ShouldContain(".node-edit-dialog-actions");
        css.ShouldNotContain("background: color-mix(in srgb, var(--flux-surface-2) 72%, var(--flux-surface));");
        css.ShouldNotContain("padding: 8px 10px;");
        css.ShouldContain("@media (max-width: 700px)");
        css.ShouldNotContain(".node-edit-dialog-status");
        css.ShouldNotContain(".node-edit-dialog-section-title");
        css.ShouldNotContain(".node-edit-dialog-section-head");
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
        css.ShouldNotContain(".workspace-artifact-region:focus-within");
        css.ShouldNotContain(".workspace-designer-region:focus-within");
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
        markup.ShouldContain("app-tree\" aria-label=\"App structure tree\"");
        markup.ShouldContain("aria-label=\"@AppRowLabel(a, isActive)\"");
        markup.ShouldContain("title=\"@AppRowLabel(a, isActive)\"");
        markup.ShouldContain("private static string AppRowLabel");
        markup.ShouldContain("aria-label=\"@($\"Close app {a.Name}\")\"");
        markup.ShouldContain("tree-empty-artifact-row tests");
        markup.ShouldContain("role=\"button\"");
        markup.ShouldContain("aria-label=\"Create test scenario\"");
        markup.ShouldContain("AddTestFromKeyboardAsync(args, a)");
        markup.ShouldContain("tree-empty-artifact-copy");
        markup.ShouldContain("tree-empty-artifact-cues");
        markup.ShouldContain("TreeSectionHeader(");
        markup.ShouldContain("private RenderFragment TreeSectionHeader");
        markup.ShouldContain("BrokerSection");
        markup.ShouldContain("PipelineSection");
        markup.ShouldContain("DashboardSection");
        markup.ShouldContain("TestSection");
        markup.ShouldContain("SelectAppFromKeyboardAsync(args, a)");
        markup.ShouldContain("ToggleSectionFromKeyboardAsync(args, app, section)");
        markup.ShouldContain("SelectPipelineFromKeyboardAsync(args, a, w)");
        markup.ShouldContain("SelectMetricsFromKeyboardAsync(args, a)");
        markup.ShouldContain("SelectDashboardFromKeyboardAsync(args, a, d)");
        markup.ShouldContain("SelectTestFromKeyboardAsync(args, a, t)");
        markup.ShouldContain("aria-expanded=\"@AriaExpanded(isCollapsed)\"");
        markup.ShouldContain("aria-label=\"@addTooltip\"");
        markup.ShouldContain("private static bool IsActivationKey");
        markup.ShouldContain("private static Task RunFromKeyboardAsync");
        markup.ShouldContain("TestArtifactItemClass(isTestActive, latestTestRun)");
        markup.ShouldContain("LatestTestRun(a, t)");
        markup.ShouldContain("test-artifact-icon-frame");
        markup.ShouldContain("test-artifact-copy");
        markup.ShouldContain("TestRunSummaryClass(latestTestRun)");
        markup.ShouldContain("TestRunDetailText(latestTestRun)");
        markup.ShouldContain("TestRunStateClass(latestTestRun)");
        markup.ShouldContain("tree-item-actions");
        markup.ShouldContain("tree-delete-button");
        markup.ShouldContain("RemoveTestAsync(a, t)");
        markup.ShouldContain("ShowMessageBoxAsync(");
        markup.ShouldContain("private static string TestArtifactTitle");
        markup.ShouldContain("private static string TestRunIssueText");
        markup.ShouldNotContain("TestRunPillClass(latestTestRun)");
        markup.ShouldNotContain("test-run-pill");

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
        css.ShouldContain(".test-run-state");
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

        markup.ShouldContain("aria-label=\"App structure navigation\"");
        markup.ShouldContain("StructureMenuLabel(\"Brokers\", conns.Count)");
        markup.ShouldContain("<span class=\"app-structure-name\">@active.Name</span>");
        (markup.Split("Modal=\"false\"", StringSplitOptions.None).Length - 1).ShouldBe(5);
        markup.ShouldContain("app-menu-artifact-row");
        markup.ShouldContain("app-menu-artifact-name");
        markup.ShouldContain("app-menu-empty");
        markup.ShouldContain("app-menu-state-row");
        markup.ShouldContain("app-menu-state-icon");
        markup.ShouldContain("app-menu-state-copy");
        markup.ShouldContain("app-menu-state-text");
        markup.ShouldContain("app-menu-command-item");
        markup.ShouldContain("app-menu-command-row");
        markup.ShouldContain("app-menu-command-icon");
        markup.ShouldContain("app-menu-command-copy");
        markup.ShouldContain("app-menu-command-cue");
        markup.ShouldContain("MenuStateRow(");
        markup.ShouldContain("MenuCommandRow(");
        markup.ShouldContain("private static RenderFragment MenuStateRow");
        markup.ShouldContain("private static RenderFragment MenuCommandRow");
        markup.ShouldContain("app-menu-broker-item");
        markup.ShouldContain("app-menu-broker-row");
        markup.ShouldContain("app-menu-broker-icon-frame");
        markup.ShouldContain("app-menu-broker-copy");
        markup.ShouldContain("app-menu-broker-state");
        markup.ShouldContain("BrokerEndpointLabel(c)");
        markup.ShouldContain("BrokerItemTitle(c)");
        markup.ShouldContain("BrokerRowClass(c)");
        markup.ShouldContain("BrokerStateClass(c)");
        markup.ShouldContain("BrokerStateText(c)");
        markup.ShouldContain("private static string BrokerEndpointLabel");
        markup.ShouldContain("private static string BrokerStateText");
        markup.ShouldContain("Class=\"@ArtifactMenuItemClass(active, WorkspaceArtifactKind.Pipeline, w)\"");
        markup.ShouldContain("Class=\"@ArtifactMenuItemClass(active, WorkspaceArtifactKind.Dashboard, d)\"");
        markup.ShouldContain("Class=\"@ArtifactMenuItemClass(active, WorkspaceArtifactKind.Metrics, \"Metrics\")\"");
        markup.ShouldContain("app-menu-artifact-item");
        markup.ShouldContain("app-menu-compact-artifact-row");
        markup.ShouldContain("app-menu-artifact-icon-frame pipeline");
        markup.ShouldContain("app-menu-artifact-icon-frame dashboard");
        markup.ShouldContain("app-menu-artifact-icon-frame metrics");
        markup.ShouldContain("app-menu-metrics-row");
        markup.ShouldContain("app-menu-artifact-state");
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
        markup.ShouldContain("TestRunMenuRowClass(latestTestRun)");
        markup.ShouldContain("TestRunMenuSummaryClass(latestTestRun)");
        markup.ShouldContain("TestRunMenuDetailText(latestTestRun)");
        markup.ShouldContain("TestRunMenuStateClass(latestTestRun)");
        markup.ShouldContain("app-menu-inline-action");
        markup.ShouldContain("app-menu-delete-button");
        markup.ShouldContain("aria-label=\"@DeleteLabel(");
        markup.ShouldContain("@onclick:stopPropagation=\"true\"");
        markup.ShouldContain("@onmousedown:stopPropagation=\"true\"");
        markup.ShouldContain("private static string DeleteLabel");
        markup.ShouldContain("private static ScenarioRunResult? LatestTestRun");
        markup.ShouldContain("private string TestArtifactItemClass");
        markup.ShouldContain("private static string TestRunMenuIssueText");
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
        markup.ShouldNotContain("app-menu-state-token");
        markup.ShouldNotContain("app-menu-broker-pill");
        markup.ShouldNotContain("app-menu-artifact-token");
        markup.ShouldNotContain("app-menu-test-pill");

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
        css.ShouldContain(".app-menu-state-row,");
        css.ShouldContain(".app-menu-command-row");
        css.ShouldContain("grid-template-columns: 22px minmax(0, 1fr) auto;");
        css.ShouldContain(".app-menu-state-icon,");
        css.ShouldContain(".app-menu-command-icon");
        css.ShouldContain(".app-menu-state-copy,");
        css.ShouldContain(".app-menu-command-copy");
        css.ShouldContain(".app-menu-state-text");
        css.ShouldContain(".app-menu-command-cue");
        css.ShouldContain(".app-menu-broker-row");
        css.ShouldContain(".app-menu-broker-icon-frame");
        css.ShouldContain(".app-menu-broker-copy");
        css.ShouldContain(".app-menu-broker-state");
        css.ShouldContain(".app-menu-broker-state.live");
        css.ShouldContain(".app-menu-broker-state.faulted");
        css.ShouldContain(".app-menu-broker-state.pending");
        css.ShouldContain(".app-menu-artifact-row");
        css.ShouldContain("grid-template-columns: minmax(0, 1fr) 24px;");
        css.ShouldContain(".app-menu-compact-artifact-row");
        css.ShouldContain("grid-template-columns: 22px minmax(0, 1fr) 24px;");
        css.ShouldContain(".app-menu-metrics-row");
        css.ShouldContain(".app-menu-artifact-icon-frame");
        css.ShouldContain(".app-menu-artifact-icon-frame.pipeline");
        css.ShouldContain(".app-menu-artifact-icon-frame.dashboard");
        css.ShouldContain(".app-menu-artifact-icon-frame.metrics");
        css.ShouldContain(".app-menu-artifact-state");
        css.ShouldContain(".app-menu-test-row");
        css.ShouldContain("grid-template-columns: 22px minmax(0, 1fr) minmax(94px, auto) 24px;");
        css.ShouldContain(".app-menu-test-icon-frame");
        css.ShouldContain(".app-menu-test-row.canceled .app-menu-test-icon-frame");
        css.ShouldContain(".app-menu-artifact-meta");
        css.ShouldContain(".app-menu-test-summary");
        css.ShouldContain(".app-menu-test-summary-meta");
        css.ShouldContain(".app-menu-test-state");
        css.ShouldContain("max-width: 92px;");
        css.ShouldContain(".app-menu-inline-action");
        css.ShouldContain("opacity: 0;");
        css.ShouldContain(".app-menu-child:hover .app-menu-inline-action");
        css.ShouldContain(".app-menu-inline-action ::deep .app-menu-delete-button");
        css.ShouldContain("height: 24px;");
        css.ShouldNotContain(".app-structure-menu ::deep .app-menu-danger");
        css.ShouldNotContain(".app-menu-state-token");
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

        markup.ShouldContain("aria-label=\"Open apps panel\"");
        markup.ShouldContain("apps-panel-title-icon");
        markup.ShouldContain("<strong>Open Apps</strong>");
        markup.ShouldContain("@ProjectCountLabel");
        markup.ShouldContain("apps-empty\" role=\"status\" aria-live=\"polite\"");
        markup.ShouldContain("apps-empty-title");
        markup.ShouldContain("role=\"list\"");
        markup.ShouldContain("role=\"button\"");
        markup.ShouldContain("tabindex=\"0\"");
        markup.ShouldContain("aria-label=\"@AppTileLabel(a, isActive)\"");
        markup.ShouldContain("title=\"@AppTileLabel(a, isActive)\"");
        markup.ShouldContain("SelectAppFromKeyboard(args, a)");
        markup.ShouldContain("private static bool IsActivationKey");
        markup.ShouldContain("\"Spacebar\"");
        markup.ShouldContain("app-tile-meta");
        markup.ShouldContain("app-state active");
        markup.ShouldContain("app-state unsaved");
        markup.ShouldContain("aria-label=\"@CloseLabel(a)\"");
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
        css.ShouldContain(".app-state.unsaved");
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

        markup.ShouldContain("aria-label=\"Connections panel\"");
        markup.ShouldContain("connections-title-icon");
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
        markup.ShouldContain("StatePillClass");
        markup.ShouldContain("StateDotClass");
        markup.ShouldContain("private static string ConnectionRowLabel");
        markup.ShouldContain("aria-label=\"@PrimaryActionLabel(conn)\"");
        markup.ShouldContain("aria-label=\"@RemoveLabel(conn)\"");
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
        css.ShouldContain(".connection-state.live");
        css.ShouldContain(".connection-state.pending");
        css.ShouldContain(".connection-state.faulted");
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

        markup.ShouldContain("aria-label=\"Recorded sessions panel\"");
        markup.ShouldContain("session-recording-strip");
        markup.ShouldContain("session-recording-strip\" role=\"status\" aria-live=\"polite\"");
        markup.ShouldContain("sessions-title-icon");
        markup.ShouldContain("<strong>Recordings</strong>");
        markup.ShouldContain("@SessionCountLabel");
        markup.ShouldContain("FilteredSessionCount");
        markup.ShouldContain("role=\"search\"");
        markup.ShouldContain("aria-label=\"Search recorded sessions\"");
        markup.ShouldContain("Search sessions");
        markup.ShouldContain("session-live-strip");
        markup.ShouldContain("aria-label=\"Switch to live traffic\"");
        markup.ShouldContain("sessions-empty\" role=\"status\" aria-live=\"polite\"");
        markup.ShouldContain("sessions-empty-title");
        markup.ShouldContain("sessions-list");
        markup.ShouldContain("role=\"list\"");
        markup.ShouldContain("session-project-group");
        markup.ShouldContain("session-project-head");
        markup.ShouldContain("SessionRowClass(session)");
        markup.ShouldContain("SessionDotClass(session)");
        markup.ShouldContain("SessionStateClass(session)");
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
        css.ShouldContain(".session-state.selected");
        css.ShouldContain(".session-state.recording");
        css.ShouldContain(".session-search ::deep .mud-input-outlined-border");
        css.ShouldContain("@media (max-width: 760px)");
        css.ShouldNotContain(".session-recording-pulse");
        css.ShouldNotContain(".session-row-side");
        css.ShouldNotContain(".session-row-time");
        css.ShouldNotContain("border-radius: 999px;");
        css.ShouldNotContain("box-shadow: 0 0 0");
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
