using FluxMq.UI.Models;
using FluxFlow.Components.Designer;
using FluxFlow.Components.Designer.Contracts;
using FluxFlow.Engine.Definitions;
using System.Text.Json.Nodes;

namespace FluxMq.UI.Services;

public sealed class FluxMqComponentAliasRegistry
{
    private readonly IReadOnlyList<FlowComponentMetadata> _aliases;
    private readonly IReadOnlyDictionary<string, FlowComponentBehavior> _behaviors;

    public FluxMqComponentAliasRegistry()
        : this(
            FlowComponentMetadataRegistry.Components,
            FlowComponentMetadataRegistry.PackageComponentBehaviors)
    {
    }

    public FluxMqComponentAliasRegistry(IReadOnlyList<FlowComponentMetadata> aliases)
        : this(aliases, FlowComponentMetadataRegistry.PackageComponentBehaviors)
    {
    }

    public FluxMqComponentAliasRegistry(
        IReadOnlyList<FlowComponentMetadata> aliases,
        IReadOnlyDictionary<string, FlowComponentBehavior> behaviors)
    {
        ArgumentNullException.ThrowIfNull(aliases);
        ArgumentNullException.ThrowIfNull(behaviors);
        _aliases = aliases;
        _behaviors = behaviors;
    }

    public IReadOnlyList<FlowComponentMetadata> Aliases => _aliases;

    public ComponentDesignMetadataCatalog CreateDesignMetadataCatalog()
        => CreateDesignMetadataCatalog([]);

    public ComponentDesignMetadataCatalog CreateDesignMetadataCatalog(
        IEnumerable<IComponentDesignMetadataProvider> packageProviders)
    {
        ArgumentNullException.ThrowIfNull(packageProviders);

        var metadataByType = new Dictionary<NodeType, ComponentDesignMetadata>();
        foreach (var packageMetadata in packageProviders.SelectMany(provider => provider.GetMetadata()))
        {
            metadataByType[packageMetadata.Type] = packageMetadata;
        }

        foreach (var aliasMetadata in CreateAliasDesignMetadata())
        {
            metadataByType[aliasMetadata.Type] = aliasMetadata;
        }

        return new ComponentDesignMetadataCatalog().AddRange(metadataByType.Values);
    }

    public IReadOnlyList<ComponentDesignMetadata> CreateAliasDesignMetadata()
        => _aliases.Select(ToDesignMetadata).ToArray();

    public FlowComponentMetadata? FindMetadata(string type)
        => _aliases.FirstOrDefault(alias =>
            string.Equals(alias.Descriptor.Type, type, StringComparison.Ordinal));

    public FlowComponentBehavior? FindBehavior(string type)
        => _behaviors.TryGetValue(type, out var behavior) ? behavior : null;

    private static ComponentDesignMetadata ToDesignMetadata(FlowComponentMetadata metadata)
    {
        var descriptor = metadata.Descriptor;
        return new ComponentDesignMetadata
        {
            Type = new NodeType(descriptor.Type),
            DisplayName = descriptor.DisplayName,
            Category = descriptor.Category,
            Summary = descriptor.Summary,
            PreferredNodeName = metadata.PreferredNodeName,
            IconKey = IconKeyFor(descriptor),
            Ports = descriptor.Ports.Select(ToDesignPort).ToArray(),
            Attributes = new Dictionary<string, string>
            {
                ["fluxmq.alias"] = "true",
                ["fluxmq.isResource"] = descriptor.IsResource ? "true" : "false",
                ["fluxmq.defaultInputLink"] = metadata.DefaultInputLink.ToString(),
                ["fluxmq.uniquePreferredName"] = metadata.MakePreferredNodeNameUnique ? "true" : "false"
            }
        };
    }

    private static PortDesignMetadata ToDesignPort(ComponentPortDescriptor port)
        => new()
        {
            Name = new PortName(port.Name),
            Direction = port.IsInput ? PortDirection.Input : PortDirection.Output,
            ValueType = port.ValueType,
            IsPrimary = !port.SingleLink
        };

    private static string IconKeyFor(FlowComponentDescriptor descriptor)
        => descriptor.Category.ToLowerInvariant().Replace(" ", "-", StringComparison.Ordinal);
}

public sealed record FlowComponentBehavior(
    string? PreferredNodeName = null,
    bool MakePreferredNodeNameUnique = false,
    FlowComponentDefaultInputLink DefaultInputLink = FlowComponentDefaultInputLink.PreferredSource,
    Func<FlowComponentDefaultConfigurationContext, JsonObject?>? CreateDefaultConfiguration = null);
