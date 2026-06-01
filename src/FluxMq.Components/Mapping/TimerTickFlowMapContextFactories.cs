using FluxMq.Components.FileWriter;
using FluxMq.Components.MqttPublisher;
using FluxMq.Components.Replay;
using FluxMq.Core.Ids;
using FluxMq.Core.Models;
using FluxFlow.Components.Timers.Contracts;
using FluxFlow.Engine.Mapping;
using MQTTnet.Protocol;
using System.Text;

namespace FluxMq.Components.Mapping;

public sealed class TimerTickFlowMapContextFactory : IFlowMapContextFactory<TimerTick>
{
    public FlowMapContext Create(TimerTick input)
        => TimerTickExpressionContextFactory.Create(input);
}

public sealed class ScheduleTickFlowMapContextFactory : IFlowMapContextFactory<ScheduleTick>
{
    public FlowMapContext Create(ScheduleTick input)
        => ScheduleTickExpressionContextFactory.Create(input);
}

public static class TimerTickExpressionContextFactory
{
    public static FlowMapContext Create(TimerTick tick)
    {
        ArgumentNullException.ThrowIfNull(tick);

        return new FlowMapContext
        {
            Variables = new Dictionary<string, object?>
            {
                ["input"] = tick,
                ["value"] = tick,
                ["tick"] = tick,
                ["name"] = tick.Name,
                ["sequence"] = tick.Sequence,
                ["timestamp"] = tick.Timestamp,
                ["startedAt"] = tick.StartedAt,
                ["dueAt"] = tick.DueAt,
                ["elapsed"] = tick.Elapsed,
                ["elapsedMilliseconds"] = tick.Elapsed.TotalMilliseconds,
                ["interval"] = tick.Interval,
                ["intervalMilliseconds"] = tick.Interval.TotalMilliseconds,
                ["drift"] = tick.Drift,
                ["driftMilliseconds"] = tick.Drift.TotalMilliseconds,
                ["Encoding"] = typeof(Encoding),
                ["Guid"] = typeof(Guid),
                ["SessionId"] = typeof(SessionId),
                ["MqttEnvelope"] = typeof(MqttEnvelope),
                ["MqttQualityOfServiceLevel"] = typeof(MqttQualityOfServiceLevel),
                ["MqttPublishRequest"] = typeof(MqttPublishRequest),
                ["MqttRecordingRequest"] = typeof(MqttRecordingRequest),
                ["FileWriteRequest"] = typeof(FileWriteRequest),
                ["FileWriteMode"] = typeof(FileWriteMode)
            }
        };
    }
}

public static class ScheduleTickExpressionContextFactory
{
    public static FlowMapContext Create(ScheduleTick tick)
    {
        ArgumentNullException.ThrowIfNull(tick);

        return new FlowMapContext
        {
            Variables = new Dictionary<string, object?>
            {
                ["input"] = tick,
                ["value"] = tick,
                ["tick"] = tick,
                ["name"] = tick.Name,
                ["sequence"] = tick.Sequence,
                ["timestamp"] = tick.Timestamp,
                ["startedAt"] = tick.StartedAt,
                ["dueAt"] = tick.DueAt,
                ["cron"] = tick.Cron,
                ["timeZoneId"] = tick.TimeZoneId,
                ["drift"] = tick.Drift,
                ["driftMilliseconds"] = tick.Drift.TotalMilliseconds,
                ["Encoding"] = typeof(Encoding),
                ["Guid"] = typeof(Guid),
                ["SessionId"] = typeof(SessionId),
                ["MqttEnvelope"] = typeof(MqttEnvelope),
                ["MqttQualityOfServiceLevel"] = typeof(MqttQualityOfServiceLevel),
                ["MqttPublishRequest"] = typeof(MqttPublishRequest),
                ["MqttRecordingRequest"] = typeof(MqttRecordingRequest),
                ["FileWriteRequest"] = typeof(FileWriteRequest),
                ["FileWriteMode"] = typeof(FileWriteMode)
            }
        };
    }
}
