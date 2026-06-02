using FluxMq.UI.Components.Diagram;
using FluxMq.UI.Models;
using System.Text.Json.Nodes;
using DiagramPoint = Blazor.Diagrams.Core.Geometry.Point;

namespace FluxMq.UI.Components.Workspace.Nodes.Routing;

public static class RoutingNodeTypes
{
    public const string Switch = "flow.switch";
    public const string Window = "flow.window";
    public const string Fork = "flow.fork";
    public const string Merge = "flow.merge";
}

public sealed class RoutingSwitchNodeModel(
    string id,
    DiagramPoint position,
    string nodeName,
    FlowComponentDescriptor? descriptor,
    bool isResource)
    : FlowDiagramNodeModel(id, position, nodeName, RoutingNodeTypes.Switch, descriptor, isResource)
{
    public const string DefaultInputType = FlowContractTypeNames.MqttEnvelope;
    public const string DefaultExpression = "qos >= 1";
    public const int DefaultBoundedCapacity = 1000;

    public string InputType { get; set; } = DefaultInputType;
    public string Expression { get; set; } = DefaultExpression;
    public IReadOnlyList<RoutingSwitchRoute> Routes { get; set; } =
    [
        new("True", "WhenTrue"),
        new("False", "WhenFalse")
    ];
    public bool EmitRouteEnvelope { get; set; }
    public int BoundedCapacity { get; set; } = DefaultBoundedCapacity;

    protected override void OnConfigurationLoaded(JsonObject? config)
    {
        InputType = RoutingNodeConfiguration.NormalizeInputType(
            RoutingNodeConfiguration.ReadString(config, "inputType", DefaultInputType));
        Expression = RoutingNodeConfiguration.ReadString(config, "expression", DefaultExpression);
        EmitRouteEnvelope = RoutingNodeConfiguration.ReadBool(config, "emitRouteEnvelope", false);
        BoundedCapacity = RoutingNodeConfiguration.ReadPositiveInt(config, "boundedCapacity", DefaultBoundedCapacity);
        Routes = ReadRoutes(config);
        SetPortDescriptors(BuildPorts());
    }

    public override JsonObject BuildConfiguration()
    {
        var routes = NormalizeRoutes(Routes);
        return new JsonObject
        {
            ["inputType"] = RoutingNodeConfiguration.NormalizeInputType(InputType),
            ["expression"] = string.IsNullOrWhiteSpace(Expression) ? DefaultExpression : Expression.Trim(),
            ["routes"] = RoutingNodeConfiguration.ToArray(routes.Select(route => route.Key)),
            ["routeOutputs"] = RoutingNodeConfiguration.ToObject(routes.Select(route => (route.Key, route.OutputPort))),
            ["emitRouteEnvelope"] = EmitRouteEnvelope,
            ["boundedCapacity"] = RoutingNodeConfiguration.NormalizePositiveInt(BoundedCapacity, DefaultBoundedCapacity)
        };
    }

    public override string ResolvePortValueType(ComponentPortDescriptor descriptor)
        => descriptor.Name is "Input" or "Matched" or "Default" || Routes.Any(route => route.OutputPort == descriptor.Name)
            ? RoutingNodeConfiguration.NormalizeInputType(InputType)
            : base.ResolvePortValueType(descriptor);

    public string FormatRoutes()
        => string.Join(Environment.NewLine, NormalizeRoutes(Routes).Select(route => $"{route.Key}={route.OutputPort}"));

    public static IReadOnlyList<RoutingSwitchRoute> ParseRoutes(string? text)
    {
        var routes = new List<RoutingSwitchRoute>();
        foreach (var rawLine in RoutingNodeConfiguration.SplitLines(text))
        {
            var parts = rawLine.Split('=', 2, StringSplitOptions.TrimEntries);
            var key = parts[0];
            var output = parts.Length > 1
                ? parts[1]
                : RoutingNodeConfiguration.ToPortName(key, "Route");
            if (string.IsNullOrWhiteSpace(key))
            {
                continue;
            }

            routes.Add(new RoutingSwitchRoute(key.Trim(), RoutingNodeConfiguration.NormalizePortName(output, "Route")));
        }

        return NormalizeRoutes(routes);
    }

    private IReadOnlyList<ComponentPortDescriptor> BuildPorts()
    {
        List<ComponentPortDescriptor> ports =
        [
            new("Input", InputType, IsInput: true),
            new("Result", "FlowSwitchResult", IsInput: false)
        ];

        if (EmitRouteEnvelope)
        {
            ports.Add(new("Routed", "FlowRoute", IsInput: false));
        }

        ports.Add(new("Matched", InputType, IsInput: false));
        foreach (var routePort in NormalizeRoutes(Routes).Select(route => route.OutputPort).Distinct(StringComparer.Ordinal))
        {
            ports.Add(new(routePort, InputType, IsInput: false));
        }

        ports.Add(new("Default", InputType, IsInput: false));
        ports.Add(new("Errors", FlowContractTypeNames.FlowError, IsInput: false));
        return ports;
    }

    private static IReadOnlyList<RoutingSwitchRoute> ReadRoutes(JsonObject? config)
    {
        var routeKeys = RoutingNodeConfiguration.ReadStringArray(config, "routes");
        if (config?["routeOutputs"] is not JsonObject outputs)
        {
            return routeKeys.Count > 0
                ? NormalizeRoutes(routeKeys.Select(key => new RoutingSwitchRoute(key, RoutingNodeConfiguration.ToPortName(key, "Route"))))
                : ParseRoutes(null);
        }

        var routes = new List<RoutingSwitchRoute>();
        foreach (var key in routeKeys)
        {
            routes.Add(new RoutingSwitchRoute(
                key,
                outputs[key]?.GetValue<string?>() ?? RoutingNodeConfiguration.ToPortName(key, "Route")));
        }

        foreach (var item in outputs)
        {
            if (routes.Any(route => string.Equals(route.Key, item.Key, StringComparison.Ordinal)))
            {
                continue;
            }

            routes.Add(new RoutingSwitchRoute(item.Key, item.Value?.GetValue<string?>() ?? item.Key));
        }

        return NormalizeRoutes(routes);
    }

    private static IReadOnlyList<RoutingSwitchRoute> NormalizeRoutes(IEnumerable<RoutingSwitchRoute> routes)
    {
        var normalized = new List<RoutingSwitchRoute>();
        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var route in routes)
        {
            var key = route.Key.Trim();
            if (key.Length == 0 || !keys.Add(key))
            {
                continue;
            }

            normalized.Add(new RoutingSwitchRoute(
                key,
                RoutingNodeConfiguration.NormalizePortName(route.OutputPort, RoutingNodeConfiguration.ToPortName(key, "Route"))));
        }

        return normalized.Count > 0
            ? normalized
            :
            [
                new("True", "WhenTrue"),
                new("False", "WhenFalse")
            ];
    }
}

public sealed class RoutingForkNodeModel(
    string id,
    DiagramPoint position,
    string nodeName,
    FlowComponentDescriptor? descriptor,
    bool isResource)
    : FlowDiagramNodeModel(id, position, nodeName, RoutingNodeTypes.Fork, descriptor, isResource)
{
    public const string DefaultInputType = FlowContractTypeNames.MqttEnvelope;
    public const int DefaultBoundedCapacity = 1000;

    public string InputType { get; set; } = DefaultInputType;
    public IReadOnlyList<string> Outputs { get; set; } = ["A", "B"];
    public int BoundedCapacity { get; set; } = DefaultBoundedCapacity;

    protected override void OnConfigurationLoaded(JsonObject? config)
    {
        InputType = RoutingNodeConfiguration.NormalizeInputType(
            RoutingNodeConfiguration.ReadString(config, "inputType", DefaultInputType));
        Outputs = RoutingNodeConfiguration.NormalizePortList(
            RoutingNodeConfiguration.ReadStringArray(config, "outputs", ["A", "B"]),
            ["Input", "Errors"],
            ["A", "B"]);
        BoundedCapacity = RoutingNodeConfiguration.ReadPositiveInt(config, "boundedCapacity", DefaultBoundedCapacity);
        SetPortDescriptors(BuildPorts());
    }

    public override JsonObject BuildConfiguration()
    {
        var outputs = RoutingNodeConfiguration.NormalizePortList(Outputs, ["Input", "Errors"], ["A", "B"]);
        return new JsonObject
        {
            ["inputType"] = RoutingNodeConfiguration.NormalizeInputType(InputType),
            ["outputs"] = RoutingNodeConfiguration.ToArray(outputs),
            ["boundedCapacity"] = RoutingNodeConfiguration.NormalizePositiveInt(BoundedCapacity, DefaultBoundedCapacity)
        };
    }

    public override string ResolvePortValueType(ComponentPortDescriptor descriptor)
        => descriptor.Name == "Input" || Outputs.Contains(descriptor.Name, StringComparer.Ordinal)
            ? RoutingNodeConfiguration.NormalizeInputType(InputType)
            : base.ResolvePortValueType(descriptor);

    public string FormatOutputs() => string.Join(Environment.NewLine, Outputs);

    private IReadOnlyList<ComponentPortDescriptor> BuildPorts()
    {
        List<ComponentPortDescriptor> ports = [new("Input", InputType, IsInput: true)];
        ports.AddRange(Outputs.Select(output => new ComponentPortDescriptor(output, InputType, IsInput: false)));
        ports.Add(new("Errors", FlowContractTypeNames.FlowError, IsInput: false));
        return ports;
    }
}

public sealed class RoutingMergeNodeModel(
    string id,
    DiagramPoint position,
    string nodeName,
    FlowComponentDescriptor? descriptor,
    bool isResource)
    : FlowDiagramNodeModel(id, position, nodeName, RoutingNodeTypes.Merge, descriptor, isResource)
{
    public const string DefaultInputType = FlowContractTypeNames.MqttEnvelope;
    public const int DefaultBoundedCapacity = 1000;

    public string InputType { get; set; } = DefaultInputType;
    public IReadOnlyList<string> Inputs { get; set; } = ["Left", "Right"];
    public int BoundedCapacity { get; set; } = DefaultBoundedCapacity;

    protected override void OnConfigurationLoaded(JsonObject? config)
    {
        InputType = RoutingNodeConfiguration.NormalizeInputType(
            RoutingNodeConfiguration.ReadString(config, "inputType", DefaultInputType));
        Inputs = RoutingNodeConfiguration.NormalizePortList(
            RoutingNodeConfiguration.ReadStringArray(config, "inputs", ["Left", "Right"]),
            ["Output", "Errors"],
            ["Left", "Right"]);
        BoundedCapacity = RoutingNodeConfiguration.ReadPositiveInt(config, "boundedCapacity", DefaultBoundedCapacity);
        SetPortDescriptors(BuildPorts());
    }

    public override JsonObject BuildConfiguration()
    {
        var inputs = RoutingNodeConfiguration.NormalizePortList(Inputs, ["Output", "Errors"], ["Left", "Right"]);
        return new JsonObject
        {
            ["inputType"] = RoutingNodeConfiguration.NormalizeInputType(InputType),
            ["inputs"] = RoutingNodeConfiguration.ToArray(inputs),
            ["boundedCapacity"] = RoutingNodeConfiguration.NormalizePositiveInt(BoundedCapacity, DefaultBoundedCapacity)
        };
    }

    public override string ResolvePortValueType(ComponentPortDescriptor descriptor)
        => Inputs.Contains(descriptor.Name, StringComparer.Ordinal)
            ? RoutingNodeConfiguration.NormalizeInputType(InputType)
            : base.ResolvePortValueType(descriptor);

    public string FormatInputs() => string.Join(Environment.NewLine, Inputs);

    private IReadOnlyList<ComponentPortDescriptor> BuildPorts()
    {
        List<ComponentPortDescriptor> ports = [.. Inputs.Select(input => new ComponentPortDescriptor(input, InputType, IsInput: true))];
        ports.Add(new("Output", "FlowMergeItem", IsInput: false));
        ports.Add(new("Errors", FlowContractTypeNames.FlowError, IsInput: false));
        return ports;
    }
}

public sealed class RoutingWindowNodeModel(
    string id,
    DiagramPoint position,
    string nodeName,
    FlowComponentDescriptor? descriptor,
    bool isResource)
    : FlowDiagramNodeModel(id, position, nodeName, RoutingNodeTypes.Window, descriptor, isResource)
{
    public const string DefaultInputType = FlowContractTypeNames.MqttEnvelope;
    public const int DefaultMaxItems = 100;
    public const int DefaultTimeMilliseconds = 5000;
    public const bool DefaultEmitPartialOnCompletion = true;
    public const int DefaultBoundedCapacity = 1000;

    public string InputType { get; set; } = DefaultInputType;
    public int MaxItems { get; set; } = DefaultMaxItems;
    public int TimeMilliseconds { get; set; } = DefaultTimeMilliseconds;
    public bool EmitPartialOnCompletion { get; set; } = DefaultEmitPartialOnCompletion;
    public int BoundedCapacity { get; set; } = DefaultBoundedCapacity;

    protected override void OnConfigurationLoaded(JsonObject? config)
    {
        InputType = RoutingNodeConfiguration.NormalizeInputType(
            RoutingNodeConfiguration.ReadString(config, "inputType", DefaultInputType));
        MaxItems = RoutingNodeConfiguration.ReadNonNegativeInt(config, "maxItems", DefaultMaxItems);
        TimeMilliseconds = RoutingNodeConfiguration.ReadNonNegativeInt(
            config,
            "timeMilliseconds",
            DefaultTimeMilliseconds);
        EnsureBoundary();
        EmitPartialOnCompletion = RoutingNodeConfiguration.ReadBool(
            config,
            "emitPartialOnCompletion",
            DefaultEmitPartialOnCompletion);
        BoundedCapacity = RoutingNodeConfiguration.ReadPositiveInt(config, "boundedCapacity", DefaultBoundedCapacity);
        SetPortDescriptors(BuildPorts());
    }

    public override JsonObject BuildConfiguration()
    {
        var maxItems = RoutingNodeConfiguration.NormalizeNonNegativeInt(MaxItems, DefaultMaxItems);
        var timeMilliseconds = RoutingNodeConfiguration.NormalizeNonNegativeInt(
            TimeMilliseconds,
            DefaultTimeMilliseconds);
        if (maxItems == 0 && timeMilliseconds == 0)
        {
            maxItems = DefaultMaxItems;
        }

        return new JsonObject
        {
            ["inputType"] = RoutingNodeConfiguration.NormalizeInputType(InputType),
            ["maxItems"] = maxItems,
            ["timeMilliseconds"] = timeMilliseconds,
            ["emitPartialOnCompletion"] = EmitPartialOnCompletion,
            ["boundedCapacity"] = RoutingNodeConfiguration.NormalizePositiveInt(
                BoundedCapacity,
                DefaultBoundedCapacity)
        };
    }

    public override string ResolvePortValueType(ComponentPortDescriptor descriptor)
        => descriptor.Name switch
        {
            "Input" => RoutingNodeConfiguration.NormalizeInputType(InputType),
            "Output" => "FlowWindow",
            _ => base.ResolvePortValueType(descriptor)
        };

    private void EnsureBoundary()
    {
        if (MaxItems == 0 && TimeMilliseconds == 0)
        {
            MaxItems = DefaultMaxItems;
        }
    }

    private IReadOnlyList<ComponentPortDescriptor> BuildPorts()
        =>
        [
            new("Input", InputType, IsInput: true),
            new("Output", "FlowWindow", IsInput: false),
            new("Errors", FlowContractTypeNames.FlowError, IsInput: false)
        ];
}

public sealed record RoutingSwitchRoute(string Key, string OutputPort);

internal static class RoutingNodeConfiguration
{
    public static readonly IReadOnlyList<string> InputTypes =
    [
        FlowContractTypeNames.MqttEnvelope,
        FlowContractTypeNames.MqttPublishRequest,
        FlowContractTypeNames.MqttRecordingRequest,
        FlowContractTypeNames.FileWriteRequest,
        FlowContractTypeNames.PayloadInspectionRequest,
        FlowContractTypeNames.PayloadInspectionResult,
        FlowContractTypeNames.HttpRequestInput,
        FlowContractTypeNames.HttpResponseOutput,
        FlowContractTypeNames.HttpErrorOutput,
        FlowContractTypeNames.JsonSchemaValidationResult,
        FlowContractTypeNames.InspectedMqttMessage,
        FlowContractTypeNames.MqttMetricsSnapshot,
        FlowContractTypeNames.TimerTick,
        FlowContractTypeNames.ScheduleTick,
        FlowContractTypeNames.StateReducerResult,
        FlowContractTypeNames.FlowLogEntry,
        FlowContractTypeNames.FlowError,
        "string",
        "bytes",
        "json",
        "object"
    ];

    public static string NormalizeInputType(string? value)
    {
        var normalized = string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : FlowContractTypeNames.Normalize(value);
        return InputTypes.Contains(normalized, StringComparer.Ordinal)
            ? normalized
            : FlowContractTypeNames.MqttEnvelope;
    }

    public static string ReadString(JsonObject? config, string key, string fallback)
        => config?[key]?.GetValue<string?>() is { Length: > 0 } value ? value : fallback;

    public static bool ReadBool(JsonObject? config, string key, bool fallback)
        => config?[key] is JsonValue value && value.TryGetValue<bool>(out var result) ? result : fallback;

    public static int ReadPositiveInt(JsonObject? config, string key, int fallback)
        => NormalizePositiveInt(ReadInt(config, key, fallback), fallback);

    public static int NormalizePositiveInt(int value, int fallback)
        => value > 0 ? value : fallback;

    public static int ReadNonNegativeInt(JsonObject? config, string key, int fallback)
        => NormalizeNonNegativeInt(ReadInt(config, key, fallback), fallback);

    public static int NormalizeNonNegativeInt(int value, int fallback)
        => value >= 0 ? value : fallback;

    public static IReadOnlyList<string> ReadStringArray(JsonObject? config, string key, IReadOnlyList<string>? fallback = null)
    {
        if (config?[key] is not JsonArray array)
        {
            return fallback ?? [];
        }

        return array
            .Select(item => item?.GetValue<string?>())
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Select(static value => value!.Trim())
            .ToArray();
    }

    public static IReadOnlyList<string> NormalizePortList(
        IEnumerable<string> values,
        IReadOnlyCollection<string> reserved,
        IReadOnlyList<string> fallback)
    {
        var result = new List<string>();
        var used = new HashSet<string>(StringComparer.Ordinal);
        foreach (var value in values)
        {
            var port = NormalizePortName(value, string.Empty);
            if (port.Length == 0 ||
                reserved.Contains(port, StringComparer.Ordinal) ||
                !used.Add(port))
            {
                continue;
            }

            result.Add(port);
        }

        return result.Count > 0 ? result : fallback;
    }

    public static IReadOnlyList<string> SplitLines(string? text)
        => string.IsNullOrWhiteSpace(text)
            ? []
            : text.Split(['\r', '\n', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    public static string NormalizePortName(string? value, string fallback)
    {
        var raw = string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        var chars = raw
            .Where(static c => char.IsLetterOrDigit(c) || c == '_')
            .ToArray();
        var port = new string(chars);
        if (port.Length == 0)
        {
            return string.IsNullOrWhiteSpace(fallback) ? string.Empty : NormalizePortName(fallback, string.Empty);
        }

        return char.IsDigit(port[0]) ? $"P{port}" : port;
    }

    public static string ToPortName(string routeKey, string fallback)
    {
        var parts = routeKey.Split([' ', '-', '.', '/', ':'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0)
        {
            return fallback;
        }

        return string.Concat(parts.Select(static part => char.ToUpperInvariant(part[0]) + part[1..]));
    }

    public static JsonArray ToArray(IEnumerable<string> values)
    {
        var array = new JsonArray();
        foreach (var value in values)
        {
            array.Add(value);
        }

        return array;
    }

    public static JsonObject ToObject(IEnumerable<(string Key, string Value)> values)
    {
        var obj = new JsonObject();
        foreach (var (key, value) in values)
        {
            obj[key] = value;
        }

        return obj;
    }

    private static int ReadInt(JsonObject? config, string key, int fallback)
        => config?[key] is JsonValue value && value.TryGetValue<int>(out var result) ? result : fallback;
}
