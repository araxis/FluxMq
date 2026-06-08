using FluxFlow.Components.Http.Contracts;
using FluxFlow.Components.Mapping;
using FluxFlow.Components.Mapping.Options;
using FluxFlow.Components.Payloads;
using FluxFlow.Components.Payloads.Contracts;
using FluxFlow.Components.Routing;
using FluxFlow.Components.Routing.Contracts;
using FluxFlow.Components.Routing.Options;
using FluxFlow.Components.State;
using FluxFlow.Components.State.Contracts;
using FluxFlow.Components.State.Options;
using FluxFlow.Components.Storage;
using FluxFlow.Components.Storage.FileSystem;
using FluxFlow.Components.Storage.Options;
using FluxFlow.Components.Timers;
using FluxFlow.Components.Timers.Contracts;
using FluxFlow.Components.Timers.Options;
using FluxFlow.Engine.Components;
using FluxFlow.Engine.Mapping;
using FluxFlow.Engine.Runtime;
using FluxMq.App.Metrics;
using FluxMq.Components.Control;
using FluxMq.Components.FileWriter;
using FluxMq.Components.JsonSchema;
using FluxMq.Components.Logging;
using FluxMq.Components.Mapping;
using FluxMq.Components.MqttMetrics;
using FluxMq.Components.MqttPayloadInspector;
using FluxMq.Components.MqttPublisher;
using FluxMq.Components.Replay;
using FluxMq.Core.Metrics;
using FluxMq.Core.Models;
using FluxMq.Core.Mqtt;

namespace FluxMq.App;

internal static class FluxMqRuntimePackageComponentOptions
{
    public static void ConfigureTimerComponents(TimerComponentOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        options
            .RegisterType<MqttEnvelope>("MqttEnvelope")
            .RegisterType<MqttPublishRequest>("MqttPublishRequest")
            .RegisterType<MqttRecordingRequest>("MqttRecordingRequest")
            .RegisterType<FileWriteRequest>("FileWriteRequest")
            .RegisterType<TimerTick>("TimerTick")
            .RegisterType<ScheduleTick>("ScheduleTick")
            .RegisterType<FlowLogEntry>("FlowLogEntry")
            .RegisterType<FlowError>("FlowError")
            .RegisterType<FluxMetricReading<double>>(FlowContractTypeNames.NumberMetricReading);
    }

    public static void ConfigureMappingComponents(
        MappingComponentOptions options,
        IFlowExpressionEngine expressionEngine)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(expressionEngine);

        options
            .UseExpressionEngine(new FluxMqRequestMappingExpressionEngine(expressionEngine))
            .RegisterType<object>("Any")
            .RegisterType<MqttEnvelope>("MqttEnvelope")
            .RegisterType<MqttPublishRequest>("MqttPublishRequest")
            .RegisterType<MqttRecordingRequest>("MqttRecordingRequest")
            .RegisterType<FileWriteRequest>("FileWriteRequest")
            .RegisterType<StateReducerInput>("StateReducerInput")
            .RegisterType<PayloadInspectionRequest>("PayloadInspectionRequest")
            .RegisterType<PayloadInspectionResult>("PayloadInspectionResult")
            .RegisterType<HttpRequestInput>("HttpRequestInput")
            .RegisterType<HttpResponseOutput>("HttpResponseOutput")
            .RegisterType<HttpErrorOutput>("HttpErrorOutput")
            .RegisterType<TimerTick>("TimerTick")
            .RegisterType<ScheduleTick>("ScheduleTick")
            .RegisterType<FluxMetricReading<double>>(FlowContractTypeNames.NumberMetricReading)
            .UseContextFactory<MqttEnvelope>(new MqttEnvelopeFlowMapContextFactory())
            .UseContextFactory<FluxMetricReading<double>>(new MetricReadingFlowMapContextFactory())
            .UseContextFactory<PayloadInspectionResult>(new PayloadInspectionResultFlowMapContextFactory())
            .UseContextFactory<HttpResponseOutput>(new HttpResponseOutputFlowMapContextFactory())
            .UseContextFactory<HttpErrorOutput>(new HttpErrorOutputFlowMapContextFactory());

        options
            .UseContextFactory<TimerTick>(new TimerTickFlowMapContextFactory())
            .UseContextFactory<ScheduleTick>(new ScheduleTickFlowMapContextFactory());

        if (!string.Equals(expressionEngine.Name, "jsonata", StringComparison.OrdinalIgnoreCase))
        {
            options.UseExpressionEngine(
                new FluxMqRequestMappingExpressionEngine(FluxMqExpressionEngines.CreateJsonata()),
                useAsDefault: false);
        }
    }

    public static void ConfigureStateComponents(
        StateComponentOptions options,
        IFlowExpressionEngine expressionEngine)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(expressionEngine);

        options.UseExpressionEngine(expressionEngine);

        if (!string.Equals(expressionEngine.Name, "jsonata", StringComparison.OrdinalIgnoreCase))
        {
            options.UseExpressionEngine(FluxMqExpressionEngines.CreateJsonata(), useAsDefault: false);
        }
    }

    public static void ConfigureStorageComponents(
        StorageComponentOptions options,
        string? fileSystemStorageRootDirectory)
    {
        ArgumentNullException.ThrowIfNull(options);

        options.UseFileSystemStorage(new FileSystemStorageStoreOptions
        {
            RootDirectory = string.IsNullOrWhiteSpace(fileSystemStorageRootDirectory)
                ? GetDefaultFileSystemStorageRootDirectory()
                : fileSystemStorageRootDirectory,
            CreateDirectory = true,
            DefaultCollection = "default"
        });
    }

    public static void ConfigureRoutingComponents(
        RoutingComponentOptions options,
        Func<string?, IFlowExpressionEngine> expressionEngineResolver)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(expressionEngineResolver);

        options
            .UseExpressionEngineResolver(expressionEngineResolver)
            .UseDefaultContextFactory(new FluxMqRoutingContextFactory())
            .RegisterType<object>("Any")
            .RegisterType<MqttEnvelope>(FlowContractTypeNames.MqttEnvelope)
            .RegisterType<MqttPublishRequest>(FlowContractTypeNames.MqttPublishRequest)
            .RegisterType<MqttRecordingRequest>(FlowContractTypeNames.MqttRecordingRequest)
            .RegisterType<FileWriteRequest>(FlowContractTypeNames.FileWriteRequest)
            .RegisterType<StateReducerInput>(nameof(StateReducerInput))
            .RegisterType<StateReducerResult>(FlowContractTypeNames.StateReducerResult)
            .RegisterType<PayloadInspectionRequest>(FlowContractTypeNames.PayloadInspectionRequest)
            .RegisterType<PayloadInspectionResult>(FlowContractTypeNames.PayloadInspectionResult)
            .RegisterType<HttpRequestInput>(FlowContractTypeNames.HttpRequestInput)
            .RegisterType<HttpResponseOutput>(FlowContractTypeNames.HttpResponseOutput)
            .RegisterType<HttpErrorOutput>(FlowContractTypeNames.HttpErrorOutput)
            .RegisterType<JsonSchemaValidationResult>(FlowContractTypeNames.JsonSchemaValidationResult)
            .RegisterType<InspectedMqttMessage>(FlowContractTypeNames.InspectedMqttMessage)
            .RegisterType<MqttMetricsSnapshot>(FlowContractTypeNames.MqttMetricsSnapshot)
            .RegisterType<TimerTick>(FlowContractTypeNames.TimerTick)
            .RegisterType<ScheduleTick>(FlowContractTypeNames.ScheduleTick)
            .RegisterType<FlowLogEntry>(FlowContractTypeNames.FlowLogEntry)
            .RegisterType<FlowError>(FlowContractTypeNames.FlowError)
            .RegisterType<FluxMetricReading<double>>(FlowContractTypeNames.NumberMetricReading);
    }

    private static string GetDefaultFileSystemStorageRootDirectory()
        => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "FluxMq",
            "storage");
}

internal sealed class FluxMqRoutingContextFactory : IRoutingContextFactory
{
    public FlowMapContext Create(object? input, RoutingNodeContext context)
    {
        if (input is not null)
        {
            return FluxMqControlExpressionContextFactory.Create(input);
        }

        return new FlowMapContext
        {
            Variables = new Dictionary<string, object?>
            {
                ["input"] = null,
                ["value"] = null,
                ["inputType"] = context.InputType.Name
            }
        };
    }
}
