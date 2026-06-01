using FluxFlow.Components.Http.Contracts;
using FluxFlow.Components.Payloads.Contracts;
using FluxFlow.Engine.Mapping;
using System.Text;

namespace FluxMq.Components.Mapping;

public sealed class HttpResponseOutputFlowMapContextFactory : IFlowMapContextFactory<HttpResponseOutput>
{
    public FlowMapContext Create(HttpResponseOutput input)
        => HttpPayloadExpressionContextFactory.Create(input);
}

public sealed class HttpErrorOutputFlowMapContextFactory : IFlowMapContextFactory<HttpErrorOutput>
{
    public FlowMapContext Create(HttpErrorOutput input)
        => HttpPayloadExpressionContextFactory.Create(input);
}

public sealed class PayloadInspectionResultFlowMapContextFactory : IFlowMapContextFactory<PayloadInspectionResult>
{
    public FlowMapContext Create(PayloadInspectionResult input)
        => HttpPayloadExpressionContextFactory.Create(input);
}

public static class HttpPayloadExpressionContextFactory
{
    public static FlowMapContext Create(HttpResponseOutput response)
    {
        ArgumentNullException.ThrowIfNull(response);

        return CreateBase(response, typeof(HttpResponseOutput), new Dictionary<string, object?>
        {
            ["response"] = response,
            ["timestamp"] = response.Timestamp,
            ["method"] = response.Method,
            ["url"] = response.Url,
            ["statusCode"] = response.StatusCode,
            ["reasonPhrase"] = response.ReasonPhrase,
            ["headers"] = response.Headers,
            ["bodyBytes"] = response.BodyBytes,
            ["body"] = response.Body,
            ["bodyText"] = response.Body ?? Encoding.UTF8.GetString(response.BodyBytes),
            ["contentType"] = response.ContentType,
            ["elapsedMilliseconds"] = response.ElapsedMilliseconds,
            ["success"] = response.Success,
            ["bodyTruncated"] = response.BodyTruncated
        });
    }

    public static FlowMapContext Create(HttpErrorOutput error)
    {
        ArgumentNullException.ThrowIfNull(error);

        return CreateBase(error, typeof(HttpErrorOutput), new Dictionary<string, object?>
        {
            ["error"] = error,
            ["timestamp"] = error.Timestamp,
            ["kind"] = error.Kind,
            ["message"] = error.Message,
            ["statusCode"] = error.StatusCode,
            ["reasonPhrase"] = error.ReasonPhrase,
            ["method"] = error.Method,
            ["url"] = error.Url,
            ["elapsedMilliseconds"] = error.ElapsedMilliseconds
        });
    }

    public static FlowMapContext Create(PayloadInspectionResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        return CreateBase(result, typeof(PayloadInspectionResult), new Dictionary<string, object?>
        {
            ["inspection"] = result,
            ["timestamp"] = result.Timestamp,
            ["payloadKind"] = result.Kind,
            ["kind"] = result.Kind,
            ["contentType"] = result.ContentType,
            ["byteCount"] = result.ByteCount,
            ["detectedEncoding"] = result.DetectedEncoding,
            ["textPreview"] = result.TextPreview,
            ["textPreviewTruncated"] = result.TextPreviewTruncated,
            ["formattedPreview"] = result.FormattedPreview,
            ["formattedPreviewTruncated"] = result.FormattedPreviewTruncated,
            ["parseError"] = result.ParseError,
            ["base64DecodedByteCount"] = result.Base64DecodedByteCount
        });
    }

    internal static void AddSharedTypes(Dictionary<string, object?> variables)
    {
        variables["Encoding"] = typeof(Encoding);
        variables["PayloadInspectionRequest"] = typeof(PayloadInspectionRequest);
        variables["PayloadInspectionResult"] = typeof(PayloadInspectionResult);
        variables["PayloadKind"] = typeof(PayloadKind);
        variables["HttpRequestInput"] = typeof(HttpRequestInput);
        variables["HttpResponseOutput"] = typeof(HttpResponseOutput);
        variables["HttpErrorOutput"] = typeof(HttpErrorOutput);
        variables["HttpErrorKind"] = typeof(HttpErrorKind);
    }

    private static FlowMapContext CreateBase(
        object input,
        Type inputType,
        Dictionary<string, object?> variables)
    {
        variables["input"] = input;
        variables["value"] = input;
        variables["inputType"] = inputType.Name;
        AddSharedTypes(variables);
        return new FlowMapContext { Variables = variables };
    }
}
