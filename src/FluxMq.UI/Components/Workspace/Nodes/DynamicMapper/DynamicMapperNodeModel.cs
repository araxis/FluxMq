using FluxMq.UI.Components.Diagram;
using FluxMq.UI.Models;
using System.Text.Json.Nodes;
using DiagramPoint = Blazor.Diagrams.Core.Geometry.Point;

namespace FluxMq.UI.Components.Workspace.Nodes.DynamicMapper;

public sealed class DynamicMapperNodeModel(string id, DiagramPoint position, string nodeName, FlowComponentDescriptor? descriptor, bool isResource)
    : FlowDiagramNodeModel(id, position, nodeName, "flow.mapper", descriptor, isResource)
{
    public static readonly IReadOnlyList<string> InputTypes =
    [
        "MqttEnvelope",
        "TimerTick",
        "ScheduleTick"
    ];

    public const string OutputContractAny = "any";
    public const string OutputContractTyped = "typed";
    public const string OutputContractJsonSchemaFile = "json-schema-file";

    public string Engine { get; set; } = "jsonata";
    public string InputType { get; set; } = "MqttEnvelope";
    public string OutputType { get; set; } = "MqttPublishRequest";
    public string OutputContract { get; set; } = OutputContractTyped;
    public string OutputSchemaPath { get; set; } = string.Empty;
    public string Expression { get; set; } = DefaultExpression("MqttPublishRequest", "jsonata");

    protected override void OnConfigurationLoaded(JsonObject? config)
    {
        Engine = ReadString(config, "engine", "jsonata");
        InputType = ReadString(config, "inputType", "MqttEnvelope");
        OutputType = ReadString(config, "outputType", "MqttPublishRequest");
        OutputContract = NormalizeOutputContract(ReadString(config, "outputContract", OutputContractTyped));
        OutputSchemaPath = ReadString(config, "outputSchemaPath", string.Empty);
        Expression = ReadString(config, "expression", string.Empty);

        if (string.IsNullOrWhiteSpace(Expression))
        {
            Expression = DefaultExpression(OutputType, Engine, InputType);
        }
    }

    public override JsonObject BuildConfiguration()
        => new()
        {
            ["engine"] = Engine,
            ["inputType"] = InputType,
            ["outputType"] = OutputType,
            ["outputContract"] = NormalizeOutputContract(OutputContract),
            ["outputSchemaPath"] = OutputSchemaPath,
            ["expression"] = string.IsNullOrWhiteSpace(Expression)
                ? DefaultExpression(OutputType, Engine, InputType)
                : Expression.Trim()
        };

    public override string ResolvePortValueType(ComponentPortDescriptor descriptor)
    {
        if (descriptor.IsInput &&
            string.Equals(descriptor.Name, "Input", StringComparison.OrdinalIgnoreCase))
        {
            return string.IsNullOrWhiteSpace(InputType) ? descriptor.ValueType : InputType.Trim();
        }

        if (!descriptor.IsInput &&
            string.Equals(descriptor.Name, "Output", StringComparison.OrdinalIgnoreCase))
        {
            return NormalizeOutputContract(OutputContract) == OutputContractTyped && !string.IsNullOrWhiteSpace(OutputType)
                ? OutputType.Trim()
                : "Any";
        }

        return base.ResolvePortValueType(descriptor);
    }

    public static string NormalizeOutputContract(string? value)
        => value?.Trim().ToLowerInvariant() switch
        {
            OutputContractAny => OutputContractAny,
            OutputContractJsonSchemaFile => OutputContractJsonSchemaFile,
            _ => OutputContractTyped
        };

    public static string NormalizeInputType(string? value)
    {
        var trimmed = value?.Trim();
        return InputTypes.Contains(trimmed, StringComparer.Ordinal)
            ? trimmed!
            : "MqttEnvelope";
    }

    public static string DefaultExpression(string outputType, string engine = "jsonata", string inputType = "MqttEnvelope")
    {
        var isJsonata = string.Equals(engine, "jsonata", StringComparison.OrdinalIgnoreCase);
        var normalizedInputType = NormalizeInputType(inputType);

        if (normalizedInputType is "TimerTick" or "ScheduleTick")
        {
            return DefaultTimerExpression(outputType, isJsonata, normalizedInputType);
        }

        return outputType switch
        {
            "FileWriteRequest" when isJsonata => """
            {
              "path": "messages/" & topic & ".txt",
              "content": payloadText,
              "mode": "Append",
              "createDirectory": true
            }
            """,
            "FileWriteRequest" => """
            new FileWriteRequest {
              Path = "messages/" + topic.Replace("/", "_") + ".txt",
              Content = payload,
              Mode = FileWriteMode.Append,
              CreateDirectory = true
            }
            """,
            "MqttRecordingRequest" when isJsonata => """
            {
              "sessionId": "00000000-0000-0000-0000-000000000001"
            }
            """,
            "MqttRecordingRequest" => """
            new MqttRecordingRequest {
              SessionId = new SessionId(Guid.Parse("00000000-0000-0000-0000-000000000001")),
              Envelope = envelope
            }
            """,
            "PayloadInspectionRequest" when isJsonata => """
            {
              "text": payloadText,
              "contentType": "application/json",
              "encodingHint": "utf-8"
            }
            """,
            "PayloadInspectionRequest" => """
            new PayloadInspectionRequest {
              Text = payloadText,
              ContentType = "application/json",
              EncodingHint = "utf-8"
            }
            """,
            "HttpRequestInput" when isJsonata => """
            {
              "method": "POST",
              "url": "https://example.test/messages",
              "headers": {
                "Content-Type": "application/json"
              },
              "body": payloadText,
              "contentType": "application/json",
              "timeoutMilliseconds": 30000
            }
            """,
            "HttpRequestInput" => """
            new HttpRequestInput {
              Method = "POST",
              Url = "https://example.test/messages",
              Body = payloadText,
              ContentType = "application/json",
              TimeoutMilliseconds = 30000
            }
            """,
            _ when isJsonata => """
            {
              "topic": topic,
              "payload": payloadText,
              "qos": qos,
              "retain": retain
            }
            """,
            _ => """
            new MqttPublishRequest {
              Topic = topic,
              Payload = payload,
              QualityOfService = qualityOfService,
              Retain = retain
            }
            """
        };
    }

    private static string DefaultTimerExpression(string outputType, bool isJsonata, string inputType)
    {
        var topicPrefix = inputType == "ScheduleTick" ? "schedule/" : "timer/";

        return outputType switch
        {
            "FileWriteRequest" when isJsonata => $$"""
            {
              "path": "{{topicPrefix}}" & name & ".json",
              "content": {
                "name": name,
                "sequence": sequence,
                "timestamp": timestamp
              },
              "mode": "Append",
              "createDirectory": true
            }
            """,
            "FileWriteRequest" => $$"""
            new FileWriteRequest {
              Path = "{{topicPrefix}}" + name + ".json",
              Content = Encoding.UTF8.GetBytes("{\"name\":\"" + name + "\",\"sequence\":" + sequence + "}"),
              Mode = FileWriteMode.Append,
              CreateDirectory = true
            }
            """,
            _ when isJsonata => $$"""
            {
              "topic": "{{topicPrefix}}" & name,
              "payload": {
                "name": name,
                "sequence": sequence,
                "timestamp": timestamp
              },
              "qos": 0,
              "retain": false
            }
            """,
            _ => $$"""
            new MqttPublishRequest {
              Topic = "{{topicPrefix}}" + name,
              Payload = Encoding.UTF8.GetBytes("{\"name\":\"" + name + "\",\"sequence\":" + sequence + "}"),
              QualityOfService = MqttQualityOfServiceLevel.AtMostOnce,
              Retain = false
            }
            """
        };
    }

    private static string ReadString(JsonObject? config, string key, string fallback)
        => config?[key]?.GetValue<string?>() is { Length: > 0 } value ? value : fallback;
}
