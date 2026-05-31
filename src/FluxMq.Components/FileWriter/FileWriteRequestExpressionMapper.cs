using FluxMq.Core.Models;
using FluxMq.Components.Mapping;
using FluxMq.Pipeline.Mapping;

namespace FluxMq.Components.FileWriter;

public sealed class FileWriteRequestExpressionMapper : IFlowMapper<MqttEnvelope, FileWriteRequest>
{
    private readonly IFlowExpressionEngine _engine;
    private readonly FileWriteRequestMapDefinition _definition;

    public FileWriteRequestExpressionMapper(
        IFlowExpressionEngine engine,
        FileWriteRequestMapDefinition definition)
    {
        _engine = engine ?? throw new ArgumentNullException(nameof(engine));
        _definition = definition ?? throw new ArgumentNullException(nameof(definition));
        if (string.IsNullOrWhiteSpace(_definition.Expression))
        {
            throw new InvalidOperationException("File write request mapping requires an expression.");
        }
    }

    public FileWriteRequest Map(MqttEnvelope input, FlowMapContext context)
        => EvaluateRequest(_definition.Expression, input, context);

    private FileWriteRequest EvaluateRequest(string expression, MqttEnvelope input, FlowMapContext context)
    {
        var value = _engine.Evaluate(expression, context, typeof(object));
        return value switch
        {
            FileWriteRequest request => request,
            null => throw new InvalidOperationException("Mapper expression returned null. Expected FileWriteRequest object."),
            _ => CoerceRequest(value, input)
        };
    }

    private static FileWriteRequest CoerceRequest(object value, MqttEnvelope input)
        => new()
        {
            Path = ExpressionObjectReader.ReadRequiredString(value, "path"),
            Content = ExpressionObjectReader.ReadBytesOrDefault(value, "content", input.Payload),
            Mode = ExpressionObjectReader.ReadEnumOrDefault(value, "mode", FileWriteMode.Overwrite),
            CreateDirectory = ExpressionObjectReader.ReadBoolOrDefault(value, "createDirectory", true)
        };
}
