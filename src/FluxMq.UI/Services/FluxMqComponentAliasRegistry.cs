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

        foreach (var fallbackMetadata in CreatePackageFallbackDesignMetadata())
        {
            metadataByType.TryAdd(fallbackMetadata.Type, fallbackMetadata);
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

    private static IReadOnlyList<ComponentDesignMetadata> CreatePackageFallbackDesignMetadata() =>
    [
        Transform("json.parse", "JSON Parse", "jsonParser", "Parses text or bytes into a JSON value.", "JsonParseRequest", "JsonParseResult"),
        Transform("json.stringify", "JSON Stringify", "jsonStringifier", "Serializes a value into JSON text and bytes.", "JsonStringifyRequest", "JsonStringifyResult"),
        Transform("text.encode", "Text Encode", "textEncoder", "Encodes text into bytes.", "TextEncodeRequest", "TextEncodeResult"),
        Transform("text.decode", "Text Decode", "textDecoder", "Decodes bytes into text.", "TextDecodeRequest", "TextDecodeResult"),
        Transform("base64.encode", "Base64 Encode", "base64Encoder", "Encodes bytes into Base64 text.", "Base64EncodeRequest", "Base64EncodeResult"),
        Transform("base64.decode", "Base64 Decode", "base64Decoder", "Decodes Base64 text into bytes.", "Base64DecodeRequest", "Base64DecodeResult")
    ];

    private static ComponentDesignMetadata Transform(
        string type,
        string displayName,
        string preferredNodeName,
        string summary,
        string inputType,
        string outputType)
        => new()
        {
            Type = new NodeType(type),
            DisplayName = displayName,
            Category = "Serialization",
            Summary = summary,
            PreferredNodeName = preferredNodeName,
            IconKey = "serialization",
            Options =
            [
                new()
                {
                    Name = "boundedCapacity",
                    Kind = OptionValueKind.Number,
                    DisplayName = "Capacity",
                    DefaultValue = 128,
                    Min = 1
                }
            ],
            Ports =
            [
                new()
                {
                    Name = new PortName("Input"),
                    Direction = PortDirection.Input,
                    ValueType = inputType,
                    IsPrimary = true
                },
                new()
                {
                    Name = new PortName("Output"),
                    Direction = PortDirection.Output,
                    ValueType = outputType,
                    IsPrimary = true,
                    Order = 1
                },
                new()
                {
                    Name = new PortName("Errors"),
                    Direction = PortDirection.Output,
                    ValueType = "FlowError",
                    Order = 2
                }
            ],
            Attributes = new Dictionary<string, string>
            {
                ["fluxmq.packageFallback"] = "true"
            }
        };
}

public sealed record FlowComponentBehavior(
    string? PreferredNodeName = null,
    bool MakePreferredNodeNameUnique = false,
    FlowComponentDefaultInputLink DefaultInputLink = FlowComponentDefaultInputLink.PreferredSource,
    Func<FlowComponentDefaultConfigurationContext, JsonObject?>? CreateDefaultConfiguration = null);
