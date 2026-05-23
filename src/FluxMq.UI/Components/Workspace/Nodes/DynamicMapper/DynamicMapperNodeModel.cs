using FluxMq.UI.Components.Diagram;
using FluxMq.UI.Models;
using System.Text.Json.Nodes;
using DiagramPoint = Blazor.Diagrams.Core.Geometry.Point;

namespace FluxMq.UI.Components.Workspace.Nodes.DynamicMapper;

public sealed class DynamicMapperNodeModel(string id, DiagramPoint position, string nodeName, FlowComponentDescriptor? descriptor, bool isResource)
    : FlowDiagramNodeModel(id, position, nodeName, "flow.mapper", descriptor, isResource)
{
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
            Expression = DefaultExpression(OutputType, Engine);
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
                ? DefaultExpression(OutputType, Engine)
                : Expression.Trim()
        };

    public static string NormalizeOutputContract(string? value)
        => value?.Trim().ToLowerInvariant() switch
        {
            OutputContractAny => OutputContractAny,
            OutputContractJsonSchemaFile => OutputContractJsonSchemaFile,
            _ => OutputContractTyped
        };

    public static string DefaultExpression(string outputType, string engine = "jsonata")
    {
        var isJsonata = string.Equals(engine, "jsonata", StringComparison.OrdinalIgnoreCase);

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

    private static string ReadString(JsonObject? config, string key, string fallback)
        => config?[key]?.GetValue<string?>() is { Length: > 0 } value ? value : fallback;
}
