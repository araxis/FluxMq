using FluxMq.UI.Components.Diagram;
using FluxMq.UI.Models;
using System.Text.Json.Nodes;
using DiagramPoint = Blazor.Diagrams.Core.Geometry.Point;

namespace FluxMq.UI.Components.Workspace.Nodes.JsonSchemaValidator;

public sealed class JsonSchemaValidatorNodeModel(string id, DiagramPoint position, string nodeName, FlowComponentDescriptor? descriptor, bool isResource)
    : FlowDiagramNodeModel(id, position, nodeName, "json.schema-validator", descriptor, isResource)
{
    public const string SchemaSourceInline = "inline";
    public const string SchemaSourceFile = "file";

    public string SchemaSource { get; set; } = SchemaSourceInline;
    public string SchemaId { get; set; } = "payload-object";
    public string Schema { get; set; } = DefaultSchema;
    public string SchemaPath { get; set; } = string.Empty;

    protected override void OnConfigurationLoaded(JsonObject? config)
    {
        SchemaId = ReadString(config, "schemaId", "payload-object");
        Schema = ReadString(config, "schema", DefaultSchema);
        SchemaPath = ReadString(config, "schemaPath", string.Empty);
        SchemaSource = string.IsNullOrWhiteSpace(SchemaPath) ? SchemaSourceInline : SchemaSourceFile;
    }

    public override JsonObject BuildConfiguration()
    {
        var config = new JsonObject
        {
            ["schemaId"] = string.IsNullOrWhiteSpace(SchemaId) ? "payload-object" : SchemaId.Trim()
        };

        if (string.Equals(SchemaSource, SchemaSourceFile, StringComparison.Ordinal))
        {
            config["schemaPath"] = SchemaPath.Trim();
        }
        else
        {
            config["schema"] = string.IsNullOrWhiteSpace(Schema) ? DefaultSchema : Schema.Trim();
        }

        return config;
    }

    public static string NormalizeSchemaSource(string? value)
        => string.Equals(value?.Trim(), SchemaSourceFile, StringComparison.OrdinalIgnoreCase)
            ? SchemaSourceFile
            : SchemaSourceInline;

    private static string ReadString(JsonObject? config, string key, string fallback)
        => config?[key]?.GetValue<string?>() is { Length: > 0 } value ? value : fallback;

    public const string DefaultSchema = """
    {
      "type": "object"
    }
    """;
}
