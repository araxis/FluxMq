using FluxMq.UI.Models;
using FluxFlow.Components.Designer;
using FluxFlow.Components.Designer.Contracts;
using FluxFlow.Engine.Definitions;
using System.Text.Json.Nodes;

namespace FluxMq.UI.Services;

public sealed class FluxMqComponentCatalogAdapter : IFluxMqComponentCatalog
{
    public static FluxMqComponentCatalogAdapter Shared { get; } = new(
        new FluxMqComponentAliasRegistry(),
        FluxMqComponentPackageDesignMetadataProviders.CreateDefault());

    private readonly FluxMqComponentAliasRegistry _aliases;
    private readonly IReadOnlyList<FlowComponentDescriptor> _components;
    private readonly Dictionary<string, FlowComponentMetadata> _metadataByType;

    public FluxMqComponentCatalogAdapter(FluxMqComponentAliasRegistry aliases)
        : this(aliases, [])
    {
    }

    public FluxMqComponentCatalogAdapter(
        FluxMqComponentAliasRegistry aliases,
        IEnumerable<IComponentDesignMetadataProvider> packageProviders)
    {
        _aliases = aliases ?? throw new ArgumentNullException(nameof(aliases));
        ArgumentNullException.ThrowIfNull(packageProviders);

        DesignMetadata = aliases.CreateDesignMetadataCatalog(packageProviders);
        _metadataByType = aliases.Aliases.ToDictionary(
            item => item.Descriptor.Type,
            item => item,
            StringComparer.Ordinal);
        _components = DesignMetadata.All
            .Select(design => _metadataByType.TryGetValue(design.Type.Value, out var alias)
                ? ToDescriptor(design, alias)
                : ToDescriptor(design))
            .ToArray();
    }

    public IReadOnlyList<FlowComponentDescriptor> Components => _components;

    public ComponentDesignMetadataCatalog DesignMetadata { get; }

    public FlowComponentDescriptor? Find(string type)
        => string.IsNullOrWhiteSpace(type)
            ? null
            : _components.FirstOrDefault(component => string.Equals(component.Type, type, StringComparison.Ordinal));

    public FlowComponentMetadata? FindMetadata(string type)
    {
        if (string.IsNullOrWhiteSpace(type))
        {
            return null;
        }

        if (_aliases.FindMetadata(type) is { } alias)
        {
            return alias;
        }

        if (!DesignMetadata.TryGet(new NodeType(type), out var metadata))
        {
            return null;
        }

        var behavior = _aliases.FindBehavior(type);
        var descriptor = ToDescriptor(metadata);
        return new FlowComponentMetadata(
            descriptor,
            behavior?.PreferredNodeName ?? metadata.PreferredNodeName ?? MakeNodeName(metadata.Type.Value),
            behavior?.MakePreferredNodeNameUnique ?? false,
            behavior?.DefaultInputLink ?? FlowComponentDefaultInputLink.PreferredSource,
            behavior?.CreateDefaultConfiguration ?? (_ => CreateConfigurationFromMetadata(metadata)));
    }

    public JsonObject? CreateDefaultConfiguration(
        string componentType,
        FlowComponentDefaultConfigurationContext context)
        => FindMetadata(componentType)?.CreateDefaultConfiguration?.Invoke(context);

    private static FlowComponentDescriptor ToDescriptor(
        ComponentDesignMetadata metadata,
        FlowComponentMetadata alias)
    {
        var isResource = alias.Descriptor.IsResource;
        if (metadata.Attributes.TryGetValue("fluxmq.isResource", out var resourceValue) &&
            bool.TryParse(resourceValue, out var parsedResource))
        {
            isResource = parsedResource;
        }

        return new FlowComponentDescriptor(
            metadata.Type.Value,
            metadata.DisplayName ?? alias.Descriptor.DisplayName,
            metadata.Category ?? alias.Descriptor.Category,
            metadata.Summary ?? alias.Descriptor.Summary,
            isResource,
            metadata.Ports.Select(ToDescriptorPort).ToArray());
    }

    private static FlowComponentDescriptor ToDescriptor(ComponentDesignMetadata metadata)
        => new(
            metadata.Type.Value,
            metadata.DisplayName ?? metadata.Type.Value,
            metadata.Category ?? "Custom",
            metadata.Summary ?? "Package component.",
            IsResource(metadata),
            metadata.Ports.Select(ToDescriptorPort).ToArray());

    private static bool IsResource(ComponentDesignMetadata metadata)
        => metadata.Attributes.TryGetValue("fluxmq.isResource", out var value) &&
           bool.TryParse(value, out var parsed) &&
           parsed;

    private static ComponentPortDescriptor ToDescriptorPort(PortDesignMetadata metadata)
        => new(
            metadata.Name.Value,
            metadata.ValueType ?? metadata.DisplayName ?? metadata.Name.Value,
            metadata.Direction == PortDirection.Input);

    private static JsonObject? CreateConfigurationFromMetadata(ComponentDesignMetadata metadata)
    {
        var configuration = new JsonObject();
        foreach (var option in metadata.Options)
        {
            if (option.DefaultValue is null)
            {
                continue;
            }

            configuration[option.Name] = JsonValue.Create(option.DefaultValue);
        }

        return configuration.Count == 0 ? null : configuration;
    }

    private static string MakeNodeName(string componentType)
    {
        var tail = componentType.Split('.', StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? "node";
        var parts = tail.Split('-', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
        {
            return "node";
        }

        return parts[0] + string.Concat(parts.Skip(1).Select(part => char.ToUpperInvariant(part[0]) + part[1..]));
    }
}
