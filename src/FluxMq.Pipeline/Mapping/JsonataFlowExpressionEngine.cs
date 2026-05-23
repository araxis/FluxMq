using Jsonata.Net.Native;
using System.Text.Json;

namespace FluxMq.Pipeline.Mapping;

public sealed class JsonataFlowExpressionEngine : IFlowExpressionEngine
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public string Name => "jsonata";

    public object? Evaluate(string expression, FlowMapContext context, Type resultType)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(expression);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(resultType);

        var inputJson = JsonSerializer.Serialize(context.Variables, Options);
        var resultJson = new JsonataQuery(expression).Eval(inputJson);

        using var document = JsonDocument.Parse(resultJson);
        return ConvertElement(document.RootElement, resultType);
    }

    private static object? ConvertElement(JsonElement element, Type resultType)
    {
        if (element.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        if (resultType == typeof(object))
        {
            return ConvertUntyped(element);
        }

        return element.Deserialize(resultType, Options);
    }

    private static object? ConvertUntyped(JsonElement element)
        => element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number when element.TryGetInt64(out var integer) => integer,
            JsonValueKind.Number => element.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            _ => element.Clone()
        };
}
