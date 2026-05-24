using FluxMq.Components.FileWriter;
using FluxMq.Pipeline.Components;
using Shouldly;
using System.Text;
using System.Threading.Tasks.Dataflow;

namespace FluxMq.Components.Tests.Components;

public sealed class FileWriterComponentTests
{
    [Fact]
    public async Task Input_WritesFile()
    {
        var directory = Path.Combine(Path.GetTempPath(), "fluxmq-tests", Guid.NewGuid().ToString("N"));
        var path = Path.Combine(directory, "message.txt");
        var component = new FileWriterComponent();

        component.Input.Post(new FileWriteRequest
        {
            Path = path,
            Content = Encoding.UTF8.GetBytes("hello")
        });
        component.Complete();

        await component.Completion;

        File.ReadAllText(path).ShouldBe("hello");
        Directory.Delete(directory, recursive: true);
    }

    [Fact]
    public async Task Input_EmitsFileWrittenEvent()
    {
        var directory = Path.Combine(Path.GetTempPath(), "fluxmq-tests", Guid.NewGuid().ToString("N"));
        var path = Path.Combine(directory, "event.txt");
        var component = new FileWriterComponent();
        var events = new BufferBlock<FlowEvent>();

        component.Events.LinkTo(events, new DataflowLinkOptions { PropagateCompletion = true });
        component.Input.Post(new FileWriteRequest
        {
            Path = path,
            Content = Encoding.UTF8.GetBytes("hello")
        });
        component.Complete();

        var flowEvent = await events.ReceiveAsync();
        await component.Completion;

        flowEvent.Type.ShouldBe(FlowEventTypes.FileWritten);
        flowEvent.Source.ShouldBe("FileWriter");
        flowEvent.SourceNodeId.ShouldBe(component.Id);
        flowEvent.Subject.ShouldBe(path);
        flowEvent.PayloadBytes.ShouldBe(5);
        flowEvent.PayloadPreview.ShouldBe("hello");
        flowEvent.GetAttribute("path").ShouldBe(path);
        flowEvent.GetAttribute("mode").ShouldBe(FileWriteMode.Overwrite.ToString());

        Directory.Delete(directory, recursive: true);
    }

    [Fact]
    public async Task WriteFailure_PublishesErrorAndKeepsProcessing()
    {
        var directory = Path.Combine(Path.GetTempPath(), "fluxmq-tests", Guid.NewGuid().ToString("N"));
        var existing = Path.Combine(directory, "existing.txt");
        var next = Path.Combine(directory, "next.txt");
        Directory.CreateDirectory(directory);
        File.WriteAllText(existing, "first");

        var component = new FileWriterComponent();
        var errors = new List<FlowError>();
        var errorSink = new ActionBlock<FlowError>(errors.Add);

        component.Errors.LinkTo(errorSink, new DataflowLinkOptions { PropagateCompletion = true });
        component.Input.Post(new FileWriteRequest
        {
            Path = existing,
            Content = Encoding.UTF8.GetBytes("should fail"),
            Mode = FileWriteMode.CreateNew
        });
        component.Input.Post(new FileWriteRequest
        {
            Path = next,
            Content = Encoding.UTF8.GetBytes("next")
        });
        component.Complete();

        await Task.WhenAll(component.Completion, errorSink.Completion);

        File.ReadAllText(existing).ShouldBe("first");
        File.ReadAllText(next).ShouldBe("next");
        errors.ShouldHaveSingleItem().Code.ShouldBe(FlowErrorCodes.ProcessingFailed);
        Directory.Delete(directory, recursive: true);
    }
}
