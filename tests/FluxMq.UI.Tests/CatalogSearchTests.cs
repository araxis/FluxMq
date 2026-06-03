using FluxMq.UI.Services;
using Shouldly;

namespace FluxMq.UI.Tests;

public sealed class CatalogSearchTests
{
    [Fact]
    public void Filter_ReturnsAllItems_WhenSearchIsEmpty()
    {
        var result = CatalogSearch.Filter(Items, "  ", static item => item.Fields);

        result.HasSearch.ShouldBeFalse();
        result.TotalCount.ShouldBe(3);
        result.VisibleCount.ShouldBe(3);
        result.Items.Select(static item => item.Type).ShouldBe(["mqtt.trigger", "flow.mapper", "event.counter"]);
    }

    [Fact]
    public void Filter_MatchesNameTypeCategoryAndDescription()
    {
        CatalogSearch.Filter(Items, "broker", static item => item.Fields)
            .Items
            .Single()
            .Type
            .ShouldBe("mqtt.trigger");

        CatalogSearch.Filter(Items, "flow.mapper", static item => item.Fields)
            .Items
            .Single()
            .DisplayName
            .ShouldBe("Dynamic Mapper");

        CatalogSearch.Filter(Items, "dashboard", static item => item.Fields)
            .Items
            .Single()
            .DisplayName
            .ShouldBe("Event Counter");

        CatalogSearch.Filter(Items, "json", static item => item.Fields)
            .Items
            .Single()
            .Type
            .ShouldBe("flow.mapper");
    }

    [Fact]
    public void Filter_IsCaseInsensitiveAndTrimsSearchText()
    {
        var result = CatalogSearch.Filter(Items, "  MQTT  ", static item => item.Fields);

        result.HasSearch.ShouldBeTrue();
        result.SearchText.ShouldBe("MQTT");
        result.TotalCount.ShouldBe(3);
        result.VisibleCount.ShouldBe(1);
        result.Items[0].Type.ShouldBe("mqtt.trigger");
    }

    private static readonly CatalogItem[] Items =
    [
        new("mqtt.trigger", "MQTT Trigger", "Source", "Subscribes to broker traffic and emits messages"),
        new("flow.mapper", "Dynamic Mapper", "Mapper", "Maps input values to JSON-shaped output"),
        new("event.counter", "Event Counter", "Dashboard", "Counts matching runtime events")
    ];

    private sealed record CatalogItem(
        string Type,
        string DisplayName,
        string Category,
        string Description)
    {
        public CatalogSearchFields Fields => new(Type, DisplayName, Category, Description);
    }
}
