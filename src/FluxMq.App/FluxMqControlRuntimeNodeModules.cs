using FluxFlow.Components.Assertions.Options;
using FluxFlow.Components.Control.Contracts;
using FluxFlow.Components.Control.Options;
using FluxFlow.Components.Http;
using FluxFlow.Components.Http.Contracts;
using FluxFlow.Components.Mapping;
using FluxFlow.Components.Payloads;
using FluxFlow.Components.Payloads.Contracts;
using FluxFlow.Components.Routing.Contracts;
using FluxFlow.Components.State.Contracts;
using FluxFlow.Components.Timers.Contracts;
using FluxFlow.Engine.Components;
using FluxFlow.Engine.Definitions;
using FluxFlow.Engine.Mapping;
using FluxFlow.Engine.Runtime;
using FluxMq.App.Metrics;
using FluxMq.Components.Assertions;
using FluxMq.Components.Control;
using FluxMq.Components.FileWriter;
using FluxMq.Components.JsonSchema;
using FluxMq.Components.Logging;
using FluxMq.Components.Mapping;
using FluxMq.Components.MessageSource;
using FluxMq.Components.MqttMetrics;
using FluxMq.Components.MqttPayloadInspector;
using FluxMq.Components.MqttPublisher;
using FluxMq.Components.Replay;
using FluxMq.Core.Metrics;
using FluxMq.Core.Models;
using FluxMq.Core.Mqtt;
using System.Threading.Tasks.Dataflow;
using AssertionNodeContext = FluxFlow.Components.Assertions.Contracts.AssertionNodeContext;
using static FluxMq.App.FluxMqRuntimeNodeConfigurationReader;

namespace FluxMq.App;

internal sealed class MessageFilterRuntimeNodeModule : IFluxMqRuntimeNodeModule
{
    public NodeType Type => FluxMqNodeTypes.MessageFilter;

    public RuntimeNode Build(FluxMqRuntimeNodeBuildContext context)
        => FluxMqControlNodeConfiguration.CreateMessageFilter(
            context.Address,
            context.Definition,
            context.ExpressionEngine);
}

internal sealed class ConditionRouterRuntimeNodeModule : IFluxMqRuntimeNodeModule
{
    public NodeType Type => FluxMqNodeTypes.ConditionRouter;

    public RuntimeNode Build(FluxMqRuntimeNodeBuildContext context)
        => FluxMqControlNodeConfiguration.CreateConditionRouter(
            context.Address,
            context.Definition,
            context.ExpressionEngine);
}

internal sealed class FlowAssertionRuntimeNodeModule : IFluxMqRuntimeNodeModule
{
    public NodeType Type => FluxMqNodeTypes.FlowAssertion;

    public RuntimeNode Build(FluxMqRuntimeNodeBuildContext context)
        => FluxMqControlNodeConfiguration.CreateFlowAssertion(
            context.Address,
            context.Definition,
            context.ExpressionEngine);
}

internal static class FluxMqControlNodeConfiguration
{
    private static readonly PortName InputPort = new("Input");
    private static readonly PortName OutputPort = new("Output");
    private static readonly PortName ResultPort = new("Result");
    private static readonly PortName PassedPort = new("Passed");
    private static readonly PortName FailedPort = new("Failed");
    private static readonly PortName WhenTruePort = new("WhenTrue");
    private static readonly PortName WhenFalsePort = new("WhenFalse");
    private static readonly PortName EntriesPort = new("Entries");
    private static readonly PortName ErrorsPort = new("Errors");

    public static RuntimeNode CreateMessageFilter(
        NodeAddress address,
        NodeDefinition definition,
        IFlowExpressionEngine expressionEngine)
    {
        var options = GetFilterControlOptions(definition);
        var component = new FlowFilterComponent<MqttEnvelope>(
            options,
            GetControlExpressionEngine(options, expressionEngine),
            new TopicFilterControlContextFactory(GetFilterPatterns(definition)),
            CreateControlNodeContext<MqttEnvelope>(address, options));

        return RuntimeNode.Create(
            address,
            component,
            inputs:
            [
                new InputPort<MqttEnvelope>(address.Port(InputPort), component.Input)
            ],
            outputs:
            [
                new OutputPort<MqttEnvelope>(address.Port(OutputPort), component.Output),
                new OutputPort<FlowError>(address.Port(ErrorsPort), component.Errors)
            ]);
    }

    public static RuntimeNode CreateConditionRouter(
        NodeAddress address,
        NodeDefinition definition,
        IFlowExpressionEngine expressionEngine)
    {
        var options = GetControlOptions(definition, FluxMqNodeTypes.ConditionRouter.Value);
        var inputType = FlowContractTypeNames.Normalize(options.InputType);
        return inputType switch
        {
            FlowContractTypeNames.NumberMetricReading => CreateConditionRouter<FluxMetricReading<double>>(address, options, expressionEngine),
            FlowContractTypeNames.MqttEnvelope => CreateConditionRouter<MqttEnvelope>(address, options, expressionEngine),
            _ => throw new InvalidOperationException(
                $"Flow condition inputType '{inputType}' is not supported yet. Supported inputType values: {FlowContractTypeNames.MqttEnvelope}, {FlowContractTypeNames.NumberMetricReading}.")
        };
    }

    public static RuntimeNode CreateFlowAssertion(
        NodeAddress address,
        NodeDefinition definition,
        IFlowExpressionEngine expressionEngine)
    {
        var inputType = FlowContractTypeNames.Normalize(GetStringOrDefault(definition, "inputType", FlowContractTypeNames.MqttEnvelope));
        return inputType switch
        {
            FlowContractTypeNames.MqttEnvelope => CreateFlowAssertion<MqttEnvelope>(address, definition, expressionEngine),
            FlowContractTypeNames.MqttPublishRequest => CreateFlowAssertion<MqttPublishRequest>(address, definition, expressionEngine),
            FlowContractTypeNames.MqttRecordingRequest => CreateFlowAssertion<MqttRecordingRequest>(address, definition, expressionEngine),
            FlowContractTypeNames.FileWriteRequest => CreateFlowAssertion<FileWriteRequest>(address, definition, expressionEngine),
            FlowContractTypeNames.PayloadInspectionRequest => CreateFlowAssertion<PayloadInspectionRequest>(address, definition, expressionEngine),
            FlowContractTypeNames.PayloadInspectionResult => CreateFlowAssertion<PayloadInspectionResult>(address, definition, expressionEngine),
            FlowContractTypeNames.HttpRequestInput => CreateFlowAssertion<HttpRequestInput>(address, definition, expressionEngine),
            FlowContractTypeNames.HttpResponseOutput => CreateFlowAssertion<HttpResponseOutput>(address, definition, expressionEngine),
            FlowContractTypeNames.HttpErrorOutput => CreateFlowAssertion<HttpErrorOutput>(address, definition, expressionEngine),
            FlowContractTypeNames.JsonSchemaValidationResult => CreateFlowAssertion<JsonSchemaValidationResult>(address, definition, expressionEngine),
            FlowContractTypeNames.InspectedMqttMessage => CreateFlowAssertion<InspectedMqttMessage>(address, definition, expressionEngine),
            FlowContractTypeNames.MqttMetricsSnapshot => CreateFlowAssertion<MqttMetricsSnapshot>(address, definition, expressionEngine),
            FlowContractTypeNames.TimerTick => CreateFlowAssertion<TimerTick>(address, definition, expressionEngine),
            FlowContractTypeNames.ScheduleTick => CreateFlowAssertion<ScheduleTick>(address, definition, expressionEngine),
            FlowContractTypeNames.StateReducerResult => CreateFlowAssertion<StateReducerResult>(address, definition, expressionEngine),
            FlowContractTypeNames.FlowLogEntry => CreateFlowAssertion<FlowLogEntry>(address, definition, expressionEngine),
            FlowContractTypeNames.FlowError => CreateFlowAssertion<FlowError>(address, definition, expressionEngine),
            FlowContractTypeNames.NumberMetricReading => CreateFlowAssertion<FluxMetricReading<double>>(address, definition, expressionEngine),
            _ => throw new InvalidOperationException(
                $"Flow assertion inputType '{inputType}' is not supported yet. Supported inputType values: {FlowContractTypeNames.FormatAssertionInputTypes()}.")
        };
    }

    public static IFlowExpressionEngine GetExpressionEngine(
        string? requestedEngine,
        IFlowExpressionEngine defaultExpressionEngine)
    {
        if (string.IsNullOrWhiteSpace(requestedEngine) ||
            string.Equals(requestedEngine, defaultExpressionEngine.Name, StringComparison.OrdinalIgnoreCase))
        {
            return defaultExpressionEngine;
        }

        if (string.Equals(requestedEngine, "jsonata", StringComparison.OrdinalIgnoreCase))
        {
            return FluxMqExpressionEngines.CreateJsonata();
        }

        throw new InvalidOperationException(
            $"Control expression engine '{requestedEngine}' is not registered. Supported engines: {defaultExpressionEngine.Name}, jsonata.");
    }

    private static ControlExpressionOptions GetFilterControlOptions(NodeDefinition definition)
    {
        var patterns = GetFilterPatterns(definition);
        var expression = GetNullableString(definition, "expression");
        var effectiveExpression = patterns.Count switch
        {
            > 0 when string.IsNullOrWhiteSpace(expression) => "topicMatches",
            > 0 => $"topicMatches && ({expression})",
            _ => string.IsNullOrWhiteSpace(expression) ? "true" : expression!
        };

        return CreateControlOptions(
            definition,
            FluxMqNodeTypes.MessageFilter.Value,
            "MqttEnvelope",
            effectiveExpression);
    }

    private static ControlExpressionOptions GetControlOptions(
        NodeDefinition definition,
        string nodeType,
        string defaultInputType = "MqttEnvelope",
        string? fallbackExpression = null)
    {
        var expression = GetNullableString(definition, "expression") ?? fallbackExpression;
        if (string.IsNullOrWhiteSpace(expression))
        {
            throw new InvalidOperationException($"{nodeType} requires configuration value 'expression'.");
        }

        return CreateControlOptions(
            definition,
            nodeType,
            NormalizeMapperTypeName(GetStringOrDefault(definition, "inputType", defaultInputType)),
            expression);
    }

    private static ControlExpressionOptions CreateControlOptions(
        NodeDefinition definition,
        string nodeType,
        string inputType,
        string expression)
    {
        if (string.IsNullOrWhiteSpace(inputType))
        {
            throw new InvalidOperationException($"{nodeType} option 'inputType' cannot be empty.");
        }

        var boundedCapacity = GetBoundedCapacity(definition);
        if (boundedCapacity <= 0)
        {
            throw new InvalidOperationException($"{nodeType} option 'boundedCapacity' must be greater than zero.");
        }

        return new ControlExpressionOptions
        {
            Engine = GetNullableString(definition, "engine"),
            Expression = expression.Trim(),
            ExpressionId = GetNullableString(definition, "expressionId"),
            ExpressionName = GetNullableString(definition, "expressionName"),
            InputType = inputType,
            BoundedCapacity = boundedCapacity
        };
    }

    private static RuntimeNode CreateConditionRouter<TInput>(
        NodeAddress address,
        ControlExpressionOptions options,
        IFlowExpressionEngine expressionEngine)
    {
        var component = new FlowWhenComponent<TInput>(
            options,
            GetControlExpressionEngine(options, expressionEngine),
            new FluxMqControlContextFactory(),
            CreateControlNodeContext<TInput>(address, options));

        return RuntimeNode.Create(
            address,
            component,
            inputs:
            [
                new InputPort<TInput>(address.Port(InputPort), component.Input)
            ],
            outputs:
            [
                new OutputPort<TInput>(address.Port(WhenTruePort), component.WhenTrue),
                new OutputPort<TInput>(address.Port(WhenFalsePort), component.WhenFalse),
                new OutputPort<FlowLogEntry>(address.Port(EntriesPort), component.Entries),
                new OutputPort<FlowError>(address.Port(ErrorsPort), component.Errors)
            ]);
    }

    private static RuntimeNode CreateFlowAssertion<TInput>(
        NodeAddress address,
        NodeDefinition definition,
        IFlowExpressionEngine expressionEngine)
    {
        var expression = GetRequiredString(definition, "expression");
        var options = GetControlOptions(
            definition,
            FluxMqNodeTypes.FlowAssertion.Value,
            typeof(TInput).Name,
            expression);
        var assertionOptions = CreateAssertionOptions(definition, options);
        var component = new FlowAssertionComponent<TInput>(
            assertionOptions,
            GetAssertionExpressionEngine(assertionOptions, expressionEngine),
            new FluxMqControlContextFactory(),
            CreateAssertionNodeContext<TInput>(address, assertionOptions));

        return RuntimeNode.Create(
            address,
            component,
            inputs:
            [
                new InputPort<TInput>(address.Port(InputPort), component.Input)
            ],
            outputs:
            [
                new OutputPort<FlowAssertionResult>(address.Port(ResultPort), component.Result),
                new OutputPort<TInput>(address.Port(PassedPort), component.Passed),
                new OutputPort<TInput>(address.Port(FailedPort), component.Failed),
                new OutputPort<FlowLogEntry>(address.Port(EntriesPort), component.Entries),
                new OutputPort<FlowError>(address.Port(ErrorsPort), component.Errors)
            ]);
    }

    private static IFlowExpressionEngine GetControlExpressionEngine(
        ControlExpressionOptions options,
        IFlowExpressionEngine defaultExpressionEngine)
        => GetExpressionEngine(options.Engine, defaultExpressionEngine);

    private static IFlowExpressionEngine GetAssertionExpressionEngine(
        AssertionOptions options,
        IFlowExpressionEngine defaultExpressionEngine)
        => GetExpressionEngine(options.Engine, defaultExpressionEngine);

    private static ControlNodeContext CreateControlNodeContext<TInput>(
        NodeAddress address,
        ControlExpressionOptions options)
        => new()
        {
            Address = address,
            Options = options,
            InputType = typeof(TInput)
        };

    private static AssertionOptions CreateAssertionOptions(
        NodeDefinition definition,
        ControlExpressionOptions options)
        => new()
        {
            Engine = options.Engine,
            Expression = options.Expression,
            ExpressionId = options.ExpressionId,
            ExpressionName = options.ExpressionName,
            InputType = options.InputType,
            BoundedCapacity = options.BoundedCapacity,
            Description = GetNullableString(definition, "name") ??
                          GetNullableString(definition, "assertionName") ??
                          "Flow assertion",
            FailureMessage = GetNullableString(definition, "failureMessage"),
            EmitPassedInput = true,
            EmitFailedInput = true
        };

    private static AssertionNodeContext CreateAssertionNodeContext<TInput>(
        NodeAddress address,
        AssertionOptions options)
        => new()
        {
            Address = address,
            Options = options,
            InputType = typeof(TInput)
        };

    private static string NormalizeMapperTypeName(string value)
    {
        var trimmed = value.Trim();
        return trimmed switch
        {
            _ when trimmed.Contains('.') => trimmed[(trimmed.LastIndexOf('.') + 1)..],
            _ => trimmed
        };
    }

    private static IReadOnlyList<string> GetFilterPatterns(NodeDefinition definition)
    {
        if (!definition.Configuration.TryGetValue("patterns", out var element))
        {
            return [];
        }

        if (element.ValueKind == System.Text.Json.JsonValueKind.String)
        {
            var s = element.GetString();
            return string.IsNullOrWhiteSpace(s) ? [] : [s];
        }

        if (element.ValueKind == System.Text.Json.JsonValueKind.Array)
        {
            var list = new List<string>();
            foreach (var item in element.EnumerateArray())
            {
                if (item.ValueKind == System.Text.Json.JsonValueKind.String && item.GetString() is { Length: > 0 } pattern)
                {
                    list.Add(pattern);
                }
            }

            return list;
        }

        return [];
    }

    private sealed class TopicFilterControlContextFactory(IReadOnlyList<string> patterns) : IControlContextFactory
    {
        private readonly FluxMqControlContextFactory _inner = new();

        public FlowMapContext Create(object? input, ControlNodeContext context)
        {
            var baseContext = _inner.Create(input, context);
            var variables = new Dictionary<string, object?>(baseContext.Variables, StringComparer.Ordinal)
            {
                ["topicMatches"] = input is MqttEnvelope envelope &&
                                   patterns.Count > 0 &&
                                   MqttTopicFilterMatcher.MatchesAny(patterns, envelope.Topic)
            };

            return baseContext with { Variables = variables };
        }
    }
}
