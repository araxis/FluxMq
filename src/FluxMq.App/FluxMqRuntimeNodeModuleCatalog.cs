using Microsoft.Extensions.DependencyInjection;

namespace FluxMq.App;

internal static class FluxMqRuntimeNodeModuleCatalog
{
    public static IReadOnlyList<IFluxMqRuntimeNodeModule> Resolve(IServiceProvider? runtimeServices)
    {
        if (runtimeServices is null)
        {
            return CreateDefault();
        }

        return [.. FluxMqRuntimeNodeModuleTypes.All.Select(type =>
            runtimeServices.GetRequiredKeyedService<IFluxMqRuntimeNodeModule>(type.Value))];
    }

    private static IReadOnlyList<IFluxMqRuntimeNodeModule> CreateDefault()
        =>
        [
            new MqttConnectionRuntimeNodeModule(),
            new MqttTriggerRuntimeNodeModule(),
            new ConnectionStateTriggerRuntimeNodeModule(),
            new StoredSessionSourceRuntimeNodeModule(),
            new ReplaySourceRuntimeNodeModule(),
            new GeneratedMqttSourceRuntimeNodeModule(),
            new MetricSourceRuntimeNodeModule(),
            new PayloadInspectorRuntimeNodeModule(),
            new MqttMetricsRuntimeNodeModule(),
            new FlowLoggerRuntimeNodeModule(),
            new MessageFilterRuntimeNodeModule(),
            new ConditionRouterRuntimeNodeModule(),
            new FlowAssertionRuntimeNodeModule(),
            new JsonSchemaValidatorRuntimeNodeModule(),
            new MqttPublisherRuntimeNodeModule(),
            new MqttRecorderRuntimeNodeModule(),
            new FileWriterRuntimeNodeModule()
        ];
}
