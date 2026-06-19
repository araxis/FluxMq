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
        inspectorCss.ShouldContain("grid-template-columns: 34px minmax(0, 1fr);");
        inspectorCss.ShouldContain("grid-column: 1 / -1;");
        inspectorCss.ShouldContain("grid-row: 1 / span 2;");
        inspectorCss.ShouldContain("overflow-wrap: anywhere;");
        inspectorCss.ShouldContain("align-content: start;");
        inspectorCss.ShouldContain(".dashboard-inspector-meta-strip span:nth-child(n + 3)");
        inspectorCss.ShouldContain(".dashboard-inspector-reset-command span,");
        visualMetricRows.ShouldContain("KeyboardArrowUp");
        visualMetricRows.ShouldContain("KeyboardArrowDown");
        visualMetricRows.ShouldContain("Icons.Material.Filled.Close");
        visualMetricRows.ShouldContain("aria-label=\"@($\"Move {VisualMetricLabel(currentMetric)} up\")\"");
        visualMetricRows.ShouldContain("aria-label=\"Add metric card\"");
    }

    [Fact]
    public void LiveInspectorPanel_UsesFlatCompactPublishRail()
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

        markup.ShouldContain("Icons.Material.Filled.Search");
        markup.ShouldContain("Icons.Material.Filled.Send");
        markup.ShouldContain("Icons.Material.Filled.AccountTree");
        markup.ShouldContain("DisplayedMessages.Count > 0");
        markup.ShouldContain("LiveTopicRows.Count > 0");
        markup.ShouldContain("LastPayloadMessage is not null");
        markup.ShouldContain("LastPayloadMessage is null");
        markup.ShouldContain("MessageSource.Count > 0");
        markup.ShouldContain("Compact=\"true\"");
        markup.ShouldContain("rail-empty-row");
        markup.ShouldContain("rail-empty-state");
        markup.ShouldContain("last-payload-empty");
        markup.ShouldContain("inspect-empty-state");
        markup.ShouldContain("topic-message-table");
        markup.ShouldContain("Show message list");
        markup.ShouldContain("No message selected");
        markup.ShouldContain("publish-form-grid");
        markup.ShouldContain("publish-field broker");
        markup.ShouldContain("publish-field topic");
        markup.ShouldContain("publish-field payload");
        markup.ShouldContain("publish-retain");
        markup.ShouldNotContain("Recording");

        css.ShouldContain("flex: 0 0 36px;");
        css.ShouldContain(".inspector-tab ::deep .mud-icon-root");
        css.ShouldContain("padding: 10px;");
        css.ShouldContain(".publish-form-grid");
        css.ShouldContain("grid-template-columns: minmax(0, 1fr) minmax(0, 1fr);");
        css.ShouldContain(".publish-field.payload,");
        css.ShouldContain("grid-column: 1 / -1;");
        css.ShouldContain("text-transform: none;");
        css.ShouldContain("height: 28px;");
        css.ShouldContain("min-height: 56px;");
        css.ShouldContain("max-height: 96px;");
        css.ShouldContain(".publish-retain.active");
        css.ShouldContain("height: 30px;");
        css.ShouldContain(".rail-empty-state,");
        css.ShouldContain(".inspect-empty-state");
        css.ShouldContain("grid-template-columns: 26px minmax(0, 1fr);");
        css.ShouldContain("min-height: 92px;");
        css.ShouldContain("grid-row: 1 / span 2;");
        css.ShouldContain(".topic-message-table ::deep .mud-table-container");
        css.ShouldContain(".topic-message-table ::deep th,");
        css.ShouldContain("min-height: 32px;");
        css.ShouldContain("max-height: 72px;");
        css.ShouldNotContain(".recording-label");
        css.ShouldNotContain(".empty-topic-row");
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

        markup.ShouldContain("aria-label=\"Topic tree\"");
        markup.ShouldContain("aria-label=\"Topic messages and payload\"");
        markup.ShouldContain("Compact=\"true\"");
        markup.ShouldContain("Class=\"topic-message-grid\"");
        markup.ShouldContain("SelectedMessage is null");
        markup.ShouldContain("topic-payload-empty");
        markup.ShouldContain("role=\"status\" aria-live=\"polite\"");
        markup.ShouldContain("No payload selected");
        markup.ShouldContain("SelectedMessage.Topic");
        markup.ShouldNotContain("Topic=\"@SelectedMessage?.Topic\"");

        css.ShouldContain("grid-template-columns: minmax(320px, 384px) minmax(0, 1fr);");
        css.ShouldContain("padding: 8px 12px;");
        css.ShouldContain("height: 26px;");
        css.ShouldContain(".topic-search ::deep .mud-input-root");
        css.ShouldContain(".topic-message-grid ::deep .mud-table-container");
        css.ShouldContain(".topic-message-grid ::deep th,");
        css.ShouldContain("min-height: 32px;");
        css.ShouldContain("flex: 0 0 clamp(132px, 30%, 252px);");
        css.ShouldContain("grid-template-columns: 28px minmax(0, 1fr);");
        css.ShouldContain(".topic-empty-state ::deep .mud-icon-root");
        css.ShouldContain("grid-row: 1 / span 2;");
        css.ShouldContain("overflow-wrap: anywhere;");
        css.ShouldContain(".topic-payload-empty");
        css.ShouldContain("min-height: 128px;");
        css.ShouldContain("min-height: 92px;");
        css.ShouldContain("flex-basis: clamp(128px, 28%, 224px);");
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
        markup.ShouldContain("RunStatusPillClass(result.Status)");
        markup.ShouldContain("ActiveRunPillClass");
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

        css.ShouldContain(".test-runner-title-icon");
        css.ShouldContain(".test-runner-empty-cues");
        css.ShouldContain(".test-runner-empty-cues span");
        css.ShouldContain(".test-runner-meta-strip span,");
        css.ShouldContain(".test-runner-status-pill");
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
        markup.ShouldContain("test-step-badges");
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
        markup.ShouldNotContain("MudChip");
        markup.ShouldNotContain("MudTextField");

        css.ShouldContain(".scenario-report-title");
        css.ShouldContain(".scenario-report-toolbar");
        css.ShouldContain(".scenario-report-meta-strip");
        css.ShouldContain(".scenario-report-export-state");
        css.ShouldContain(".scenario-report-summary-grid");
        css.ShouldContain("grid-template-columns: repeat(4, minmax(0, 1fr));");
        css.ShouldContain(".scenario-report-tabs");
        css.ShouldContain(".scenario-report-viewer pre");
        css.ShouldContain(".scenario-report-empty");
        css.ShouldContain("font-family: Consolas, \"Courier New\", monospace;");
        css.ShouldContain(".scenario-report-actions");
        css.ShouldContain(".scenario-report-action-group.compact");
        css.ShouldContain(".scenario-report-action-group ::deep(.scenario-report-action)");
        css.ShouldContain("::deep(.scenario-report-close)");
        css.ShouldContain("@media (max-width: 760px)");
        css.ShouldContain("grid-template-columns: repeat(2, minmax(0, 1fr));");
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
        markup.ShouldContain("workspace-log-status @ProblemStatusClass");
        markup.ShouldContain("Show problems");
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
        css.ShouldContain(".workspace-log-filter-block");
        css.ShouldContain("grid-template-columns: auto minmax(0, 1fr);");
        css.ShouldContain("flex-wrap: nowrap;");
        css.ShouldContain("overflow-x: auto;");
        css.ShouldContain(".workspace-log-filter-button.active");
        css.ShouldContain(".workspace-log-status");
        css.ShouldContain("grid-template-columns: 22px minmax(0, 1fr) auto;");
        css.ShouldContain(".workspace-log-search ::deep .mud-input-root");
        css.ShouldContain("min-height: 30px;");
        css.ShouldContain("grid-template-columns: 24px 70px minmax(130px, 0.34fr) minmax(0, 1fr);");
        css.ShouldContain(".workspace-log-row::before");
        css.ShouldContain("width: 3px;");
        css.ShouldContain(".workspace-log-row-meta span");
        css.ShouldContain("grid-template-columns: 30px minmax(0, 1fr);");
        css.ShouldContain("min-height: 132px;");
        css.ShouldContain(".workspace-log-empty ::deep .mud-icon-root");
        css.ShouldContain("grid-row: 1 / span 3;");
        css.ShouldContain("overflow-wrap: anywhere;");
        css.ShouldContain("grid-template-columns: 24px 60px minmax(0, 1fr);");
        css.ShouldContain("grid-template-columns: 42px minmax(0, 1fr);");
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
        markup.ShouldContain("private string FileLabel");
        markup.ShouldContain("private int JsonLineCount");
        markup.ShouldContain("private string JsonSizeLabel");
        markup.ShouldNotContain("MudChip");
        markup.ShouldNotContain("MudStack Row=\"true\"");

        css.ShouldContain("grid-template-columns: minmax(0, 1fr) auto auto auto;");
        css.ShouldContain("min-height: 38px;");
        css.ShouldContain("padding: 5px 8px;");
        css.ShouldContain("height: 26px;");
        css.ShouldContain(".app-json-meta span,");
        css.ShouldContain(".app-json-state.unsaved");
        css.ShouldContain(".app-json-state.unsaved::before");
        css.ShouldContain(".app-json-toolbar ::deep .mud-icon-button");
        css.ShouldContain(".app-json-editor-shell");
        css.ShouldContain("font-size: 11.5px;");
        css.ShouldContain("margin: 6px;");
        css.ShouldContain("tab-size: 2;");
        css.ShouldContain(".app-json-empty");
        css.ShouldContain("grid-template-columns: 28px minmax(0, 1fr);");
        css.ShouldContain("grid-row: 1 / span 2;");
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
        css.ShouldContain("grid-template-columns: 28px minmax(0, 1fr);");
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
        css.ShouldContain("grid-template-columns: 42px minmax(0, 1fr);");
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
        css.ShouldContain("grid-template-columns: 34px minmax(0, 1fr) auto;");
        css.ShouldContain(".dashboard-grid-picker > .dashboard-command-label");
        css.ShouldContain("min-height: clamp(320px, 52vh, 480px);");
        css.ShouldContain("overflow-x: auto;");
        css.ShouldContain(".dashboard-empty-actions");
        css.ShouldContain("max-width: min(100%, 560px);");
        css.ShouldContain("--dashboard-grid-row-min: 86px;");
        css.ShouldContain("@media (max-width: 420px)");
        normalizedCss.ShouldContain(".dashboard-empty-panel {\n        align-items: flex-start;\n        grid-template-columns: minmax(0, 1fr);\n        justify-items: stretch;\n        width: 100%;\n    }");
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
        markup.ShouldContain("catalog-step-badges");
        markup.ShouldContain("StepPhaseBadgeClass(item)");
        markup.ShouldContain("StepKindLabel(item)");
        markup.ShouldContain("StepParameterLabel(item)");
        markup.ShouldContain("descriptor.DefaultPhase");
        markup.ShouldContain("descriptor.Fields.Count");

        css.ShouldContain(".catalog-title-copy");
        css.ShouldContain(".catalog-title-label");
        css.ShouldContain(".catalog-meta-strip span,");
        css.ShouldContain("background: var(--flux-canvas);");
        css.ShouldContain("border-bottom: 1px solid var(--flux-border-soft);");
        css.ShouldContain(".catalog-use-state.ready");
        css.ShouldContain(".catalog-use-state.inactive");
        css.ShouldContain(".catalog-empty ::deep .mud-icon-root");
        css.ShouldContain("grid-template-columns: 26px minmax(0, 1fr);");
        css.ShouldContain("min-height: 58px;");
        css.ShouldContain("grid-row: 1 / span 2;");
        css.ShouldContain("overflow-wrap: anywhere;");
        css.ShouldContain(".component-catalog.pipeline .catalog-empty");
        css.ShouldContain("min-height: 54px;");
        css.ShouldContain(".component-catalog.pipeline");
        css.ShouldContain(".component-catalog.pipeline .catalog-meta-strip");
        css.ShouldContain(".component-catalog.pipeline .catalog-item");
        css.ShouldContain("grid-template-columns: 20px minmax(0, 1fr) 38px;");
        css.ShouldContain("min-height: 31px;");
        css.ShouldContain("height: 20px;");
        css.ShouldContain(".component-catalog.test .catalog-item");
        css.ShouldContain("grid-template-columns: 24px minmax(0, 1fr) 34px;");
        css.ShouldContain("min-height: 46px;");
        css.ShouldContain(".component-catalog.dashboard .catalog-item:not(.dragging):hover,");
        css.ShouldContain(".component-catalog.dashboard .catalog-item:not(.dragging):focus-visible");
        css.ShouldContain("inset 2px 0 0 color-mix(in srgb, var(--flux-accent) 54%, transparent),");
        css.ShouldContain(".component-catalog.test .catalog-item:not(.dragging):hover,");
        css.ShouldContain(".component-catalog.test .catalog-item:not(.dragging):focus-visible");
        css.ShouldContain("inset 2px 0 0 color-mix(in srgb, var(--mud-palette-warning) 54%, transparent),");
        css.ShouldContain(".catalog-step-badges");
        css.ShouldContain(".component-catalog.test .catalog-item-badge.setup");
        css.ShouldContain(".component-catalog.test .catalog-drag-grip");
        css.ShouldContain("display: none;");
        css.ShouldContain("@media (max-width: 760px)");
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
        markup.ShouldContain("flow-canvas-command-group");
        markup.ShouldContain("role=\"status\" aria-live=\"polite\"");
        markup.ShouldContain("@EmptyCanvasHint");
        markup.ShouldContain("flow-canvas-empty-icon");
        markup.ShouldContain("AddCircle");
        markup.ShouldContain("DiagramCanvas");
        markup.ShouldContain("NavigatorWidget");

        css.ShouldContain(".flow-canvas-header");
        css.ShouldContain("grid-template-columns: minmax(0, 1fr) auto;");
        css.ShouldContain(".flow-canvas-title-copy");
        css.ShouldContain(".flow-canvas-meta-strip span,");
        css.ShouldContain(".flow-canvas-metrics");
        css.ShouldContain(".flow-canvas-command-group");
        css.ShouldContain("min-height: 46px;");
        css.ShouldContain("flex-wrap: nowrap;");
        css.ShouldContain("height: 24px;");
        css.ShouldContain("height: 26px;");
        css.ShouldContain("min-width: 238px;");
        css.ShouldContain("min-height: 22px;");
        css.ShouldContain(".flow-canvas-empty-icon");
        css.ShouldContain("max-width: min(100%, 420px);");
        css.ShouldContain("grid-template-columns: 32px minmax(0, 1fr);");
        css.ShouldContain("grid-row: 1 / span 3;");
        css.ShouldContain("max-width: min(100%, 340px);");
        css.ShouldContain("grid-row: auto;");
        css.ShouldContain("@media (max-width: 720px)");
        css.ShouldContain("grid-template-columns: minmax(0, 1fr);");
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
        var statMarkup = File.ReadAllText(Path.Combine(
            root,
            "src",
            "FluxMq.UI",
            "Components",
            "Workspace",
            "Nodes",
            "Stat.razor"));
        var css = File.ReadAllText(Path.Combine(
            root,
            "src",
            "FluxMq.UI",
            "Components",
            "Workspace",
            "FlowDesigner.razor.css"));

        shellMarkup.ShouldContain("flow-node-action flow-node-toggle");
        shellMarkup.ShouldContain("flow-node-type-icon");
        shellMarkup.ShouldContain("flow-node-name");
        shellMarkup.ShouldContain("flow-node-display-name");
        shellMarkup.ShouldContain("flow-node-action flow-node-edit");
        shellMarkup.ShouldContain("flow-node-category-chip");
        shellMarkup.ShouldContain("flow-node-divider");
        shellMarkup.ShouldContain("flow-node-activity");
        shellMarkup.ShouldContain("flow-node-collapsed-activity");
        statMarkup.ShouldContain("node-stat-icon");

        css.ShouldContain(".flow-designer-root ::deep .flow-node-action");
        css.ShouldContain("flex: 0 0 24px;");
        css.ShouldContain(".flow-designer-root ::deep .flow-node-category-chip");
        css.ShouldContain("max-width: 72px;");
        css.ShouldContain(".flow-designer-root ::deep .flow-node-divider");
        css.ShouldContain(".flow-designer-root ::deep .flow-node-activity");
        css.ShouldContain("margin: 6px -8px -6px;");
        css.ShouldContain("grid-template-columns: 16px auto minmax(0, 1fr);");
        css.ShouldContain(".flow-designer-root ::deep .node-stat-icon");
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

        markup.ShouldContain("flow-diagnostic-panel @DiagnosticSeverityClass");
        markup.ShouldContain("aria-label=\"Pipeline diagnostics\"");
        markup.ShouldContain("@DiagnosticPanelIcon");
        markup.ShouldContain("@DiagnosticPanelTitle");
        markup.ShouldContain("@DiagnosticPanelSummary");
        markup.ShouldContain("VisibleDiagnostics");
        markup.ShouldContain("DiagnosticRowClass(diagnostic)");
        markup.ShouldContain("DiagnosticTarget(diagnostic)");
        markup.ShouldContain("AdditionalDiagnosticCount");
        markup.ShouldContain("@if (ShowDiagnosticPanel)");
        markup.ShouldContain("ActionableDiagnosticCount");
        markup.ShouldContain("DiagnosticSeverityToken(diagnostic) is \"error\" or \"warning\"");
        markup.ShouldContain("flow-link-condition-panel @(ShowDiagnosticPanel ? \"with-diagnostics\" : null)");
        markup.ShouldContain("VisibleDiagnosticLimit = 3");

        css.ShouldContain(".flow-diagnostic-panel");
        css.ShouldContain("max-height: 112px;");
        css.ShouldContain("box-shadow: none;");
        css.ShouldContain(".flow-diagnostic-row");
        css.ShouldContain("grid-template-columns: 16px minmax(48px, 0.28fr) minmax(64px, 0.36fr) minmax(0, 1fr);");
        css.ShouldContain(".flow-link-condition-panel.with-diagnostics");
        css.ShouldContain("top: 184px;");
        css.ShouldContain("background: color-mix(in srgb, var(--flux-surface) 96%, transparent);");
        css.ShouldContain("min-width: 0;");
        css.ShouldContain("left: 8px;");
        css.ShouldContain("right: 8px;");
        css.ShouldContain("top: 250px;");
        css.ShouldContain(".flow-designer-root ::deep .flow-node-diagnostic-error::before");
        css.ShouldContain("width: 3px;");
        css.ShouldContain(".flow-diagnostic-message");
        css.ShouldContain("display: none;");
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
        markup.ShouldContain("node-edit-dialog-meta-strip");
        markup.ShouldContain("node-edit-dialog-shell");
        markup.ShouldContain("node-edit-dialog-section node-edit-dialog-identity");
        markup.ShouldContain("node-edit-dialog-section node-edit-dialog-editor");
        markup.ShouldContain("Unique within workflow");
        markup.ShouldContain("SubmitStatusText");
        markup.ShouldContain("node-edit-dialog-actions");
        markup.ShouldNotContain("Must be unique within the workflow.");

        css.ShouldContain(".node-edit-dialog-shell");
        css.ShouldContain("max-height: min(70vh, 640px);");
        css.ShouldContain(".node-edit-dialog-section");
        css.ShouldContain(".node-edit-dialog-shell ::deep .mud-input-control");
        css.ShouldContain(".node-edit-dialog-editor ::deep .mapper-workbench .mapper-panel");
        css.ShouldContain("height: 360px;");
        css.ShouldContain(".node-edit-dialog-action-status");
        css.ShouldContain("@media (max-width: 700px)");
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
        markup.ShouldContain("<FlowDesigner />");

        css.ShouldContain(".workspace-artifact-shell-pipeline");
        css.ShouldContain("grid-template-columns: minmax(212px, 236px) minmax(0, 1fr);");
        css.ShouldContain(".workspace-artifact-shell-pipeline .workspace-artifact-tools");
        css.ShouldContain("padding: 6px;");
        css.ShouldContain(".workspace-artifact-tools:focus-within");
        css.ShouldContain(".workspace-artifact-region:focus-within,");
        css.ShouldContain(".workspace-designer-region:focus-within");
        css.ShouldContain("box-shadow: inset 0 0 0 1px color-mix(in srgb, var(--mud-palette-primary) 34%, var(--flux-border));");
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
        markup.ShouldContain("TestRunPillClass(latestTestRun)");
        markup.ShouldContain("tree-item-actions");
        markup.ShouldContain("tree-delete-button");
        markup.ShouldContain("RemoveTestAsync(a, t)");
        markup.ShouldContain("ShowMessageBoxAsync(");
        markup.ShouldContain("private static string TestArtifactTitle");
        markup.ShouldContain("private static string TestRunIssueText");

        css.ShouldContain(".tree-empty-artifact-row");
        css.ShouldContain(".tree-empty ::deep .mud-icon-root");
        css.ShouldContain("grid-template-columns: 26px minmax(0, 1fr);");
        css.ShouldContain("min-height: 82px;");
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
        css.ShouldContain(".test-run-pill");
        css.ShouldContain(".tree-item-actions");
        css.ShouldContain("opacity: 0.58;");
        css.ShouldContain(".tree-item-actions ::deep .tree-delete-button");
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
        markup.ShouldContain("app-menu-artifact-row");
        markup.ShouldContain("app-menu-artifact-name");
        markup.ShouldContain("app-menu-empty");
        markup.ShouldContain("app-menu-state-row");
        markup.ShouldContain("app-menu-state-icon");
        markup.ShouldContain("app-menu-state-copy");
        markup.ShouldContain("app-menu-state-token");
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
        markup.ShouldContain("app-menu-broker-pill");
        markup.ShouldContain("BrokerEndpointLabel(c)");
        markup.ShouldContain("BrokerItemTitle(c)");
        markup.ShouldContain("BrokerRowClass(c)");
        markup.ShouldContain("BrokerStatePillClass(c)");
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
        markup.ShouldContain("app-menu-artifact-token");
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
        markup.ShouldContain("TestRunMenuPillClass(latestTestRun)");
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
        markup.ShouldNotContain("app-menu-danger");
        markup.ShouldNotContain("Class=\"app-menu-muted\">No");
        markup.ShouldNotContain("<MudMenuItem Icon=\"@BrokerActionIcon(c)\"");
        markup.ShouldNotContain("<MudMenuItem Icon=\"@Icons.Material.Filled.Timeline\"");
        markup.ShouldNotContain("<MudMenuItem Icon=\"@Icons.Material.Filled.Dashboard\"");
        markup.ShouldNotContain("<MudMenuItem Icon=\"@Icons.Material.Filled.QueryStats\"");
        markup.ShouldNotContain("Delete @w");
        markup.ShouldNotContain("Delete @d");
        markup.ShouldNotContain("Delete @t");

        css.ShouldContain("height: 28px;");
        css.ShouldContain("max-width: 132px;");
        css.ShouldContain(".app-menu-state-row,");
        css.ShouldContain(".app-menu-command-row");
        css.ShouldContain("grid-template-columns: 22px minmax(0, 1fr) auto;");
        css.ShouldContain(".app-menu-state-icon,");
        css.ShouldContain(".app-menu-command-icon");
        css.ShouldContain(".app-menu-state-copy,");
        css.ShouldContain(".app-menu-command-copy");
        css.ShouldContain(".app-menu-state-token");
        css.ShouldContain(".app-menu-command-cue");
        css.ShouldContain(".app-menu-broker-row");
        css.ShouldContain(".app-menu-broker-icon-frame");
        css.ShouldContain(".app-menu-broker-copy");
        css.ShouldContain(".app-menu-broker-pill");
        css.ShouldContain(".app-menu-broker-pill.live");
        css.ShouldContain(".app-menu-broker-pill.faulted");
        css.ShouldContain(".app-menu-broker-pill.pending");
        css.ShouldContain(".app-menu-artifact-row");
        css.ShouldContain("grid-template-columns: minmax(0, 1fr) 24px;");
        css.ShouldContain(".app-menu-compact-artifact-row");
        css.ShouldContain("grid-template-columns: 22px minmax(0, 1fr) 24px;");
        css.ShouldContain(".app-menu-metrics-row");
        css.ShouldContain(".app-menu-artifact-icon-frame");
        css.ShouldContain(".app-menu-artifact-icon-frame.pipeline");
        css.ShouldContain(".app-menu-artifact-icon-frame.dashboard");
        css.ShouldContain(".app-menu-artifact-icon-frame.metrics");
        css.ShouldContain(".app-menu-artifact-token");
        css.ShouldContain(".app-menu-test-row");
        css.ShouldContain("grid-template-columns: 22px minmax(0, 1fr) minmax(94px, auto) 24px;");
        css.ShouldContain(".app-menu-test-icon-frame");
        css.ShouldContain(".app-menu-test-row.canceled .app-menu-test-icon-frame");
        css.ShouldContain(".app-menu-artifact-meta");
        css.ShouldContain(".app-menu-test-summary");
        css.ShouldContain(".app-menu-test-summary-meta");
        css.ShouldContain(".app-menu-test-pill");
        css.ShouldContain("max-width: 92px;");
        css.ShouldContain(".app-menu-inline-action");
        css.ShouldContain("opacity: 0;");
        css.ShouldContain(".app-menu-child:hover .app-menu-inline-action");
        css.ShouldContain(".app-menu-inline-action ::deep .app-menu-delete-button");
        css.ShouldContain("height: 24px;");
        css.ShouldNotContain(".app-structure-menu ::deep .app-menu-danger");

        appCss.ShouldContain(".app-structure-popover .app-menu-test-item");
        appCss.ShouldContain(".app-structure-popover .app-menu-command-item");
        appCss.ShouldContain(".app-structure-popover .app-menu-artifact-item");
        appCss.ShouldContain(".app-structure-popover .app-menu-broker-item");
        appCss.ShouldContain(".app-structure-popover .app-menu-empty");
        appCss.ShouldContain("padding-left: 6px;");
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
        css.ShouldContain("grid-template-columns: 26px minmax(0, 1fr);");
        css.ShouldContain("min-height: 82px;");
        css.ShouldContain("grid-row: 1 / span 2;");
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
        css.ShouldContain("grid-template-columns: 26px minmax(0, 1fr);");
        css.ShouldContain("grid-row: 1 / span 2;");
        css.ShouldContain("min-height: 82px;");
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
        markup.ShouldContain("DurationLabel(session)");
        markup.ShouldContain("StartedLabel(session)");
        markup.ShouldContain("SelectSessionAsync(session)");
        markup.ShouldNotContain("MudTreeView");
        markup.ShouldNotContain("MudTreeViewItem");
        markup.ShouldNotContain("px-2");
        markup.ShouldNotContain("pt-1");

        css.ShouldContain(".sessions-panel");
        css.ShouldContain(".session-recording-strip");
        css.ShouldContain(".sessions-list");
        css.ShouldContain(".session-project-group");
        css.ShouldContain(".session-row");
        css.ShouldContain("grid-template-columns: 7px minmax(0, 1fr) 68px;");
        css.ShouldContain("min-height: 48px;");
        css.ShouldContain(".session-row.selected");
        css.ShouldContain(".session-row.recording");
        css.ShouldContain(".session-row:focus-visible");
        css.ShouldContain(".session-row.selected:focus-visible");
        css.ShouldContain("inset 2px 0 0 var(--flux-accent)");
        css.ShouldContain("flex-wrap: nowrap;");
        css.ShouldContain(".session-row-meta span:first-child");
        css.ShouldContain(".session-row-meta span:last-child");
        css.ShouldContain("max-width: 100%;");
        css.ShouldContain("text-overflow: ellipsis;");
        css.ShouldContain("grid-template-columns: 28px minmax(0, 1fr);");
        css.ShouldContain("min-height: 96px;");
        css.ShouldContain("grid-row: 1 / span 2;");
        css.ShouldContain("overflow-wrap: anywhere;");
        css.ShouldContain(".session-state.selected");
        css.ShouldContain(".session-state.recording");
        css.ShouldContain(".session-search ::deep .mud-input-outlined-border");
        css.ShouldContain("@media (max-width: 760px)");
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
