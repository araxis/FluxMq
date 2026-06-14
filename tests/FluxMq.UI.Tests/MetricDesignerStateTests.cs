using FluxMq.App.Metrics;
using FluxMq.UI.Components.Workspace;
using FluxMq.UI.Services;
using Shouldly;

namespace FluxMq.UI.Tests;

public sealed class MetricDesignerStateTests
{
    private static readonly IFluxMetricCatalog Catalog = FluxMetricCatalog.CreateDefault();

    [Fact]
    public void BuildRows_FormatsSummaryReferencesAndLatestValue()
    {
        var timestamp = DateTimeOffset.UtcNow;
        var metrics = new Dictionary<string, FluxMetricResourceDefinition>(StringComparer.Ordinal)
        {
            ["publishedMessages"] = new()
            {
                TypeId = MessageCountMetric.TypeId,
                DisplayName = "Published messages",
                Parameters = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["topic"] = "factory/#",
                    ["qos"] = "1"
                }
            }
        };

        var rows = MetricDesignerState.BuildRows(
            metrics,
            Catalog,
            _ => 2,
            (string _, FluxMetricResourceDefinition _, out MetricDesignerLatestValue latest) =>
            {
                latest = new MetricDesignerLatestValue("12", "messages", timestamp);
                return true;
            });

        var row = rows.ShouldHaveSingleItem();
        row.Id.ShouldBe("publishedMessages");
        row.TypeName.ShouldBe(MessageCountMetric.Descriptor.DisplayName);
        row.Summary.ShouldBe("factory/# · QoS 1");
        row.ReferenceCount.ShouldBe(2);
        row.Latest.ShouldNotBeNull();
        row.Latest.FormattedValue.ShouldBe("12");
        row.Latest.Unit.ShouldBe("messages");
    }

    [Fact]
    public void ApplyFilter_FiltersBySearchAndType()
    {
        var rows = new[]
        {
            new MetricDesignerRow("messages", "Messages", MessageCountMetric.TypeId, "Message count", "factory/#", "messages", 0, null),
            new MetricDesignerRow("rates", "Rates", EventRateMetric.TypeId, "Event rate", "Window 60s", "/s", 0, null)
        };

        MetricDesignerState.ApplyFilter(rows, "factory", null)
            .Select(static row => row.Id)
            .ShouldBe(["messages"]);

        MetricDesignerState.ApplyFilter(rows, null, EventRateMetric.TypeId)
            .Select(static row => row.Id)
            .ShouldBe(["rates"]);
    }

    [Fact]
    public void ValidateDraft_ChecksIdentityNumbersRangesAndDuration()
    {
        var resource = new FluxMetricResourceDefinition
        {
            TypeId = EventRateMetric.TypeId,
            DisplayName = "Event rate",
            Parameters = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["topic"] = "factory/#",
                ["qos"] = "4",
                ["window"] = "forever"
            }
        };
        var draft = MetricDesignerDraft.FromResource("eventRate", resource);
        draft.Id = "event rate";

        var errors = MetricDesignerState.ValidateDraft(draft, Catalog, ["eventRate", "other"]);

        errors.ShouldContain("Metric id can only use letters, numbers, dots, dashes, and underscores.");
        errors.ShouldContain("QoS must be at most 2.");
        errors.ShouldContain("Window must be between 1s and 24h, for example 30s, 1m, or 2h.");
    }

    [Fact]
    public void ValidateParameter_ReturnsSingleParameterError()
    {
        var resource = new FluxMetricResourceDefinition
        {
            TypeId = EventRateMetric.TypeId,
            DisplayName = "Event rate",
            Parameters = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["qos"] = "4",
                ["window"] = "60s"
            }
        };
        var draft = MetricDesignerDraft.FromResource("eventRate", resource);
        var topic = EventRateMetric.Descriptor.Parameters.Single(static parameter => parameter.Key == "topic");
        var qos = EventRateMetric.Descriptor.Parameters.Single(static parameter => parameter.Key == "qos");
        var window = EventRateMetric.Descriptor.Parameters.Single(static parameter => parameter.Key == "window");

        MetricDesignerState.ValidateParameter(draft, topic)
            .ShouldBe("Topic filter is required.");
        MetricDesignerState.ValidateParameter(draft, qos)
            .ShouldBe("QoS must be at most 2.");

        draft.Parameters["topic"] = "factory/#";
        draft.Parameters["qos"] = "1";

        MetricDesignerState.ValidateParameter(draft, topic).ShouldBeNull();
        MetricDesignerState.ValidateParameter(draft, qos).ShouldBeNull();
        MetricDesignerState.ValidateParameter(draft, window).ShouldBeNull();
    }

    [Fact]
    public void ValidateIdentity_ReturnsSingleIdentityErrors()
    {
        var resource = new FluxMetricResourceDefinition
        {
            TypeId = EventRateMetric.TypeId,
            DisplayName = string.Empty,
            Parameters = new Dictionary<string, string>(StringComparer.Ordinal)
        };
        var draft = MetricDesignerDraft.FromResource("eventRate", resource);

        MetricDesignerState.ValidateDisplayName(draft)
            .ShouldBe("Display name is required.");

        draft.DisplayName = "Event rate";
        draft.Id = "event rate";

        MetricDesignerState.ValidateDisplayName(draft).ShouldBeNull();
        MetricDesignerState.ValidateMetricId(draft, ["eventRate"])
            .ShouldBe("Metric id can only use letters, numbers, dots, dashes, and underscores.");

        draft.Id = "other";

        MetricDesignerState.ValidateMetricId(draft, ["eventRate", "other"])
            .ShouldBe("Metric id 'other' already exists.");

        draft.Id = "eventRate";

        MetricDesignerState.ValidateMetricId(draft, ["eventRate"]).ShouldBeNull();
    }

    [Fact]
    public void Draft_TracksDirtyStateAndNormalizesDurationOnSave()
    {
        var resource = new FluxMetricResourceDefinition
        {
            TypeId = WindowedMessageCountMetric.TypeId,
            DisplayName = "Messages",
            Parameters = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["topic"] = "factory/#",
                ["qos"] = "1",
                ["window"] = "60s"
            }
        };
        var draft = MetricDesignerDraft.FromResource("messages", resource);

        draft.IsDirty.ShouldBeFalse();

        draft.DisplayName = "Factory messages";
        draft.Parameters["window"] = "1M";

        draft.IsDirty.ShouldBeTrue();
        var saved = draft.ToResource(Catalog);
        saved.DisplayName.ShouldBe("Factory messages");
        saved.Parameters["window"].ShouldBe("1m");
    }

    [Fact]
    public void DefaultParameters_UsesDescriptorDefaultsAndUniqueMetricId()
    {
        var defaults = MetricDesignerState.DefaultParameters(EventRateMetric.Descriptor);

        defaults["qos"].ShouldBe("0");
        defaults["window"].ShouldBe("60s");
        defaults.ContainsKey("topic").ShouldBeFalse();

        MetricDesignerState.UniqueMetricId("Event Rate", ["Event-Rate", "Event-Rate2"])
            .ShouldBe("Event-Rate3");
    }

    [Fact]
    public void FlowWorkspaceService_RenameMetricUpdatesBindingsAndReferenceSummaries()
    {
        var service = new FlowWorkspaceService(new FlowDefinitionComposer());
        service.AddMetric("publishedMessages");
        service.UpdateMetric(
            "publishedMessages",
            new FluxMetricResourceDefinition
            {
                TypeId = MessageCountMetric.TypeId,
                DisplayName = "Published messages",
                Parameters = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["topic"] = "factory/#",
                    ["qos"] = "1"
                }
            });
        service.AddDashboard("ops");
        service.AddDashboardWidget(DashboardWidgetCatalog.EventCounterType, "slot:0:0");
        service.UpdateDashboardWidgetBinding("eventCounter", "publishedMessages", ["publishedMessages"]);

        var reference = service.GetMetricReferenceSummaries("publishedMessages").ShouldHaveSingleItem();
        reference.DashboardName.ShouldBe("ops");
        reference.WidgetName.ShouldBe("eventCounter");
        reference.CellName.ShouldBe("cell");
        reference.CellLabel.ShouldBe("R1 C1");
        reference.IsPrimary.ShouldBeTrue();

        service.RenameMetric("publishedMessages", "publishedTotal");
        service.SetActiveDashboard("ops");

        var binding = service.GetActiveDashboardLayout()
            .ShouldNotBeNull()
            .Bindings["eventCounter"];
        binding.PrimaryMetric.ShouldBe("publishedTotal");
        binding.Metrics.ShouldBe(["publishedTotal"]);
        service.GetMetricReferenceSummaries("publishedTotal").ShouldHaveSingleItem();
    }

    [Fact]
    public void FlowWorkspaceService_DuplicateMetricCreatesCopyResource()
    {
        var service = new FlowWorkspaceService(new FlowDefinitionComposer());
        service.AddMetric("publishedMessages");
        service.UpdateMetric(
            "publishedMessages",
            new FluxMetricResourceDefinition
            {
                TypeId = MessageCountMetric.TypeId,
                DisplayName = "Published messages",
                Parameters = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["topic"] = "factory/#",
                    ["qos"] = "1"
                }
            });

        service.DuplicateMetric("publishedMessages");

        var copy = service.GetMetricResource("publishedMessagesCopy").ShouldNotBeNull();
        copy.DisplayName.ShouldBe("Published messages Copy");
        copy.TypeId.ShouldBe(MessageCountMetric.TypeId);
        copy.Parameters["topic"].ShouldBe("factory/#");
    }
}
