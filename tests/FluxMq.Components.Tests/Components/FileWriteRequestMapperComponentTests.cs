using FluxMq.Core.Models;
using FluxMq.Components.FileWriter;
using FluxMq.Components.Mapping;
using FluxMq.Pipeline.Mapping;
using Shouldly;
using System.Threading.Tasks.Dataflow;

namespace FluxMq.Components.Tests.Components;

public sealed class FileWriteRequestMapperComponentTests
{
    [Fact]
    public async Task DynamicExpressoMapper_MapsEnvelopeToFileWriteRequest()
    {
        var mapper = new FileWriteRequestExpressionMapper(
            new DynamicExpressoFlowExpressionEngine(),
            new FileWriteRequestMapDefinition
            {
                PathExpression = "\"C:/tmp/\" + topic.Replace(\"/\", \"_\") + \".txt\"",
                ContentExpression = "\"topic=\" + topic + \";payload=\" + payloadText",
                ModeExpression = "\"Append\"",
                CreateDirectoryExpression = "false"
            });
        var component = new FileWriteRequestMapperComponent(mapper);
        var output = new BufferBlock<FileWriteRequest>();

        component.Output.LinkTo(output, new DataflowLinkOptions { PropagateCompletion = true });
        component.Input.Post(new MqttEnvelope
        {
            Topic = "factory/line-1",
            Payload = "hello"u8.ToArray()
        });
        component.Complete();

        var request = await output.ReceiveAsync();
        await component.Completion;

        request.Path.ShouldBe("C:/tmp/factory_line-1.txt");
        request.Content.ShouldBe("topic=factory/line-1;payload=hello"u8.ToArray());
        request.Mode.ShouldBe(FileWriteMode.Append);
        request.CreateDirectory.ShouldBeFalse();
    }
}
