using FluxFlow.Engine.Definitions;
using FluxFlow.Components.Designer;
using FluxFlow.Components.Designer.Contracts;
using FluxMq.UI.Services;
using Shouldly;

namespace FluxMq.UI.Tests;

public sealed class FluxMqComponentCatalogAdapterTests
{
    [Fact]
    public void Adapter_exposes_fluxflow_design_metadata_for_fluxmq_aliases()
    {
        var adapter = new FluxMqComponentCatalogAdapter(new FluxMqComponentAliasRegistry());

        adapter.DesignMetadata.TryGet(new NodeType("mqtt.trigger"), out var metadata).ShouldBeTrue();
        metadata.DisplayName.ShouldBe("Live MQTT Trigger");
        metadata.Attributes["fluxmq.alias"].ShouldBe("true");
        metadata.Ports.Select(port => port.Name.Value).ShouldBe(["Output", "Errors"]);
    }

    [Fact]
    public void Flow_component_catalog_reads_descriptors_from_adapter()
    {
        var adapter = new FluxMqComponentCatalogAdapter(new FluxMqComponentAliasRegistry());
        var catalog = new FlowComponentCatalog(adapter);

        catalog.Components.Count.ShouldBe(adapter.DesignMetadata.All.Count);
        catalog.Find("flow.mapper")!.Ports.Select(port => port.Name).ShouldBe(["Input", "Output", "Errors"]);
    }

    [Fact]
    public void Adapter_keeps_fluxmq_default_configuration_compatibility()
    {
        var adapter = new FluxMqComponentCatalogAdapter(new FluxMqComponentAliasRegistry());

        var configuration = adapter.CreateDefaultConfiguration(
            "mqtt.publisher",
            new FlowComponentDefaultConfigurationContext("MqttEnvelope", "local-broker"));

        configuration.ShouldNotBeNull();
        configuration["connection"]!.GetValue<string>().ShouldBe("local-broker");
        configuration["boundedCapacity"]!.GetValue<int>().ShouldBe(1000);
    }

    [Fact]
    public void Adapter_composes_package_metadata_and_fluxmq_alias_overrides()
    {
        var packageProvider = new ComponentDesignMetadataModule(
        [
            new()
            {
                Type = new NodeType("package.sample"),
                DisplayName = "Package Sample",
                Category = "Package",
                Summary = "Package-owned metadata.",
                Ports =
                [
                    new()
                    {
                        Name = new PortName("Output"),
                        Direction = PortDirection.Output,
                        ValueType = "SampleOutput"
                    }
                ]
            },
            new()
            {
                Type = new NodeType("mqtt.trigger"),
                DisplayName = "Package Trigger",
                Category = "Package",
                Summary = "Should be overridden by FluxMQ alias metadata.",
                Ports =
                [
                    new()
                    {
                        Name = new PortName("Output"),
                        Direction = PortDirection.Output,
                        ValueType = "PackageOutput"
                    }
                ]
            }
        ]);
        var adapter = new FluxMqComponentCatalogAdapter(
            new FluxMqComponentAliasRegistry(),
            [packageProvider]);

        adapter.Find("package.sample")!.DisplayName.ShouldBe("Package Sample");
        adapter.Find("mqtt.trigger")!.DisplayName.ShouldBe("Live MQTT Trigger");
    }

    [Fact]
    public void Adapter_uses_package_metadata_with_fluxmq_behavior_overrides()
    {
        var adapter = FluxMqComponentCatalogAdapter.Shared;

        var descriptor = adapter.Find("json.parse");
        descriptor.ShouldNotBeNull();
        descriptor.DisplayName.ShouldBe("JSON Parse");
        descriptor.Category.ShouldBe("Serialization");
        descriptor.Ports.Select(port => port.Name).ShouldBe(["Input", "Output", "Errors"]);

        var metadata = adapter.FindMetadata("json.parse");
        metadata.ShouldNotBeNull();
        metadata.PreferredNodeName.ShouldBe("jsonParser");
        metadata.DefaultInputLink.ShouldBe(FlowComponentDefaultInputLink.None);

        var configuration = adapter.CreateDefaultConfiguration(
            "json.parse",
            FlowComponentDefaultConfigurationContext.Empty);
        configuration.ShouldNotBeNull();
        configuration["boundedCapacity"]!.GetValue<int>().ShouldBe(1000);
    }

    [Fact]
    public void Adapter_without_package_providers_keeps_transform_fallbacks_and_fluxmq_aliases()
    {
        var adapter = new FluxMqComponentCatalogAdapter(
            new FluxMqComponentAliasRegistry(),
            []);

        foreach (var componentType in new[]
                 {
                     "json.parse",
                     "json.stringify",
                     "text.encode",
                     "text.decode",
                     "base64.encode",
                     "base64.decode"
                 })
        {
            adapter.Find(componentType).ShouldNotBeNull(componentType);
        }

        var transform = adapter.Find("json.parse")!;
        transform.Category.ShouldBe("Serialization");
        transform.Ports.Select(port => port.Name).ShouldBe(["Input", "Output", "Errors"]);

        adapter.DesignMetadata.TryGet(new NodeType("json.parse"), out var transformMetadata).ShouldBeTrue();
        transformMetadata.Attributes["fluxmq.packageFallback"].ShouldBe("true");
        transformMetadata.Ports.Select(port => port.ValueType).ShouldBe(["JsonParseRequest", "JsonParseResult", "FlowError"]);

        var transformCompatibility = adapter.FindMetadata("json.parse");
        transformCompatibility.ShouldNotBeNull();
        transformCompatibility.PreferredNodeName.ShouldBe("jsonParser");
        transformCompatibility.DefaultInputLink.ShouldBe(FlowComponentDefaultInputLink.None);

        var transformDefaults = adapter.CreateDefaultConfiguration(
            "json.parse",
            FlowComponentDefaultConfigurationContext.Empty);
        transformDefaults.ShouldNotBeNull();
        transformDefaults["boundedCapacity"]!.GetValue<int>().ShouldBe(1000);

        var trigger = adapter.Find("mqtt.trigger");
        trigger.ShouldNotBeNull();
        trigger.DisplayName.ShouldBe("Live MQTT Trigger");
        trigger.Ports.Select(port => port.Name).ShouldBe(["Output", "Errors"]);

        var publisherDefaults = adapter.CreateDefaultConfiguration(
            "mqtt.publisher",
            new FlowComponentDefaultConfigurationContext("MqttEnvelope", "local-broker"));
        publisherDefaults.ShouldNotBeNull();
        publisherDefaults["connection"]!.GetValue<string>().ShouldBe("local-broker");
    }
}
