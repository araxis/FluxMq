using FluxMq.Core.Models;
using FluxMq.Components.FileWriter;
using FluxMq.Components.Mapping;
using FluxFlow.Engine.Mapping;
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
                Expression = """
                new FileWriteRequest {
                  Path = "C:/tmp/" + topic.Replace("/", "_") + ".txt",
                  Content = Encoding.UTF8.GetBytes("topic=" + topic + ";payload=" + payloadText),
                  Mode = FileWriteMode.Append,
                  CreateDirectory = false
                }
                """
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
