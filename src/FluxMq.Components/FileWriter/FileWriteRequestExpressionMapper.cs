using FluxMq.Core.Models;
using FluxMq.Pipeline.Mapping;
using System.Text;

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
    }

    public FileWriteRequest Map(MqttEnvelope input, FlowMapContext context)
    {
        return new FileWriteRequest
        {
            Path = _engine.Evaluate<string>(_definition.PathExpression, context),
            Content = EvaluateContent(_definition.ContentExpression, context, input.Payload),
            Mode = EvaluateMode(_definition.ModeExpression, context, FileWriteMode.Overwrite),
            CreateDirectory = EvaluateBool(_definition.CreateDirectoryExpression, context, true)
        };
    }

    private byte[] EvaluateContent(string? expression, FlowMapContext context, byte[] fallback)
    {
        if (string.IsNullOrWhiteSpace(expression))
        {
            return fallback;
        }

        var value = _engine.Evaluate(expression, context, typeof(object));
        return value switch
        {
            byte[] bytes => bytes,
            string text => Encoding.UTF8.GetBytes(text),
            null => [],
            _ => throw new InvalidOperationException(
                $"Content expression returned unsupported value type '{value.GetType().FullName}'. Expected byte[] or string.")
        };
    }

    private FileWriteMode EvaluateMode(string? expression, FlowMapContext context, FileWriteMode fallback)
    {
        if (string.IsNullOrWhiteSpace(expression))
        {
            return fallback;
        }

        var value = _engine.Evaluate(expression, context, typeof(object));
        return value switch
        {
            FileWriteMode mode => mode,
            string mode => ParseMode(mode),
            _ => throw new InvalidOperationException(
                $"Mode expression returned unsupported value type '{value?.GetType().FullName ?? "null"}'. Expected FileWriteMode or string.")
        };
    }

    private bool EvaluateBool(string? expression, FlowMapContext context, bool fallback)
    {
        if (string.IsNullOrWhiteSpace(expression))
        {
            return fallback;
        }

        return _engine.Evaluate<bool>(expression, context);
    }

    private static FileWriteMode ParseMode(string value)
    {
        if (Enum.TryParse<FileWriteMode>(value, ignoreCase: true, out var mode))
        {
            return mode;
        }

        throw new InvalidOperationException("File write mode must be Overwrite, Append, or CreateNew.");
    }
}
