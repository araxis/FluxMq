using FluxMq.UI.Components.Diagram;
using FluxMq.UI.Models;
using System.Text.Json.Nodes;
using DiagramPoint = Blazor.Diagrams.Core.Geometry.Point;

namespace FluxMq.UI.Components.Workspace.Nodes.Timers;

public sealed class TimerIntervalNodeModel(
    string id,
    DiagramPoint position,
    string nodeName,
    FlowComponentDescriptor? descriptor,
    bool isResource)
    : FlowDiagramNodeModel(id, position, nodeName, TimerNodeTypes.Interval, descriptor, isResource)
{
    public const int DefaultIntervalMilliseconds = 1000;
    public const int DefaultInitialDelayMilliseconds = 0;
    public const int DefaultBoundedCapacity = 1000;

    public int IntervalMilliseconds { get; set; } = DefaultIntervalMilliseconds;
    public int InitialDelayMilliseconds { get; set; } = DefaultInitialDelayMilliseconds;
    public bool EmitImmediately { get; set; }
    public int MaxTicks { get; set; }
    public int BoundedCapacity { get; set; } = DefaultBoundedCapacity;

    protected override void OnConfigurationLoaded(JsonObject? config)
    {
        IntervalMilliseconds = TimerNodeConfiguration.ReadPositiveInt(config, "intervalMilliseconds", DefaultIntervalMilliseconds);
        InitialDelayMilliseconds = TimerNodeConfiguration.ReadNonNegativeInt(config, "initialDelayMilliseconds", DefaultInitialDelayMilliseconds);
        EmitImmediately = TimerNodeConfiguration.ReadBool(config, "emitImmediately", false);
        MaxTicks = TimerNodeConfiguration.ReadNonNegativeInt(config, "maxTicks", 0);
        BoundedCapacity = TimerNodeConfiguration.ReadPositiveInt(config, "boundedCapacity", DefaultBoundedCapacity);
    }

    public override JsonObject BuildConfiguration()
    {
        var config = new JsonObject
        {
            ["intervalMilliseconds"] = TimerNodeConfiguration.NormalizePositiveInt(IntervalMilliseconds, DefaultIntervalMilliseconds),
            ["initialDelayMilliseconds"] = TimerNodeConfiguration.NormalizeNonNegativeInt(InitialDelayMilliseconds, DefaultInitialDelayMilliseconds),
            ["emitImmediately"] = EmitImmediately,
            ["boundedCapacity"] = TimerNodeConfiguration.NormalizePositiveInt(BoundedCapacity, DefaultBoundedCapacity)
        };

        if (MaxTicks > 0)
        {
            config["maxTicks"] = MaxTicks;
        }

        return config;
    }
}

public sealed class TimerScheduleNodeModel(
    string id,
    DiagramPoint position,
    string nodeName,
    FlowComponentDescriptor? descriptor,
    bool isResource)
    : FlowDiagramNodeModel(id, position, nodeName, TimerNodeTypes.Schedule, descriptor, isResource)
{
    public const string DefaultCron = "* * * * *";
    public const string DefaultTimeZoneId = "UTC";
    public const int DefaultBoundedCapacity = 1000;

    public string Cron { get; set; } = DefaultCron;
    public string TimeZoneId { get; set; } = DefaultTimeZoneId;
    public int MaxTicks { get; set; }
    public int BoundedCapacity { get; set; } = DefaultBoundedCapacity;

    protected override void OnConfigurationLoaded(JsonObject? config)
    {
        Cron = TimerNodeConfiguration.ReadString(config, "cron", DefaultCron);
        TimeZoneId = TimerNodeConfiguration.ReadString(config, "timeZoneId", DefaultTimeZoneId);
        MaxTicks = TimerNodeConfiguration.ReadNonNegativeInt(config, "maxTicks", 0);
        BoundedCapacity = TimerNodeConfiguration.ReadPositiveInt(config, "boundedCapacity", DefaultBoundedCapacity);
    }

    public override JsonObject BuildConfiguration()
    {
        var config = new JsonObject
        {
            ["cron"] = string.IsNullOrWhiteSpace(Cron) ? DefaultCron : Cron.Trim(),
            ["timeZoneId"] = string.IsNullOrWhiteSpace(TimeZoneId) ? DefaultTimeZoneId : TimeZoneId.Trim(),
            ["boundedCapacity"] = TimerNodeConfiguration.NormalizePositiveInt(BoundedCapacity, DefaultBoundedCapacity)
        };

        if (MaxTicks > 0)
        {
            config["maxTicks"] = MaxTicks;
        }

        return config;
    }
}

public sealed class TimerDelayNodeModel(
    string id,
    DiagramPoint position,
    string nodeName,
    FlowComponentDescriptor? descriptor,
    bool isResource)
    : FlowDiagramNodeModel(id, position, nodeName, TimerNodeTypes.Delay, descriptor, isResource)
{
    public const string DefaultInputType = "MqttEnvelope";
    public const int DefaultDelayMilliseconds = 250;
    public const int DefaultBoundedCapacity = 1000;

    public static readonly IReadOnlyList<string> InputTypes =
    [
        "MqttEnvelope",
        "MqttPublishRequest",
        "MqttRecordingRequest",
        "FileWriteRequest",
        "TimerTick",
        "ScheduleTick",
        "FlowLogEntry",
        "FlowError",
        "string",
        "bytes",
        "json",
        "object"
    ];

    public string InputType { get; set; } = DefaultInputType;
    public int DelayMilliseconds { get; set; } = DefaultDelayMilliseconds;
    public int BoundedCapacity { get; set; } = DefaultBoundedCapacity;

    protected override void OnConfigurationLoaded(JsonObject? config)
    {
        InputType = NormalizeInputType(TimerNodeConfiguration.ReadString(config, "inputType", DefaultInputType));
        DelayMilliseconds = TimerNodeConfiguration.ReadNonNegativeInt(config, "delayMilliseconds", DefaultDelayMilliseconds);
        BoundedCapacity = TimerNodeConfiguration.ReadPositiveInt(config, "boundedCapacity", DefaultBoundedCapacity);
    }

    public override JsonObject BuildConfiguration() => new()
    {
        ["inputType"] = NormalizeInputType(InputType),
        ["delayMilliseconds"] = TimerNodeConfiguration.NormalizeNonNegativeInt(DelayMilliseconds, DefaultDelayMilliseconds),
        ["boundedCapacity"] = TimerNodeConfiguration.NormalizePositiveInt(BoundedCapacity, DefaultBoundedCapacity)
    };

    public override string ResolvePortValueType(ComponentPortDescriptor descriptor)
        => descriptor.Name is "Input" or "Output"
            ? NormalizeInputType(InputType)
            : base.ResolvePortValueType(descriptor);

    public static string NormalizeInputType(string? value)
    {
        var trimmed = value?.Trim();
        return InputTypes.Contains(trimmed, StringComparer.Ordinal)
            ? trimmed!
            : DefaultInputType;
    }
}

public static class TimerNodeTypes
{
    public const string Interval = "timer.interval";
    public const string Schedule = "timer.schedule";
    public const string Delay = "timer.delay";
}

internal static class TimerNodeConfiguration
{
    public static string ReadString(JsonObject? config, string key, string fallback)
        => config?[key]?.GetValue<string?>() is { Length: > 0 } value ? value : fallback;

    public static bool ReadBool(JsonObject? config, string key, bool fallback)
        => config?[key] is JsonValue value && value.TryGetValue<bool>(out var result) ? result : fallback;

    public static int ReadPositiveInt(JsonObject? config, string key, int fallback)
        => NormalizePositiveInt(ReadInt(config, key, fallback), fallback);

    public static int ReadNonNegativeInt(JsonObject? config, string key, int fallback)
        => NormalizeNonNegativeInt(ReadInt(config, key, fallback), fallback);

    public static int NormalizePositiveInt(int value, int fallback)
        => value > 0 ? value : fallback;

    public static int NormalizeNonNegativeInt(int value, int fallback)
        => value >= 0 ? value : fallback;

    private static int ReadInt(JsonObject? config, string key, int fallback)
        => config?[key] is JsonValue value && value.TryGetValue<int>(out var result) ? result : fallback;
}
