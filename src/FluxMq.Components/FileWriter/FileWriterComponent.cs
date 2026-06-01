using FluxFlow.Components.FileSystem;
using FluxFlow.Engine.Components;
using FluxFlow.Engine.Definitions;
using FluxFlow.Engine.Runtime;
using System.Globalization;
using System.Text.Json;
using System.Threading.Tasks.Dataflow;
using PackageFileWriteMode = FluxFlow.Components.FileSystem.Contracts.FileWriteMode;
using PackageFileWriteRequest = FluxFlow.Components.FileSystem.Contracts.FileWriteRequest;
using PackageFileWriteResult = FluxFlow.Components.FileSystem.Contracts.FileWriteResult;

namespace FluxMq.Components.FileWriter;

public sealed class FileWriterComponent : EventFlowNodeBase
{
    private static readonly PortName InputPort = new("Input");

    private readonly RuntimeNode _innerNode;
    private readonly TransformBlock<FileWriteRequest, PackageFileWriteRequest> _input;
    private readonly ActionBlock<PackageFileWriteResult> _result;
    private readonly ActionBlock<FlowError> _errors;
    private readonly List<IDisposable> _links = [];
    private int _started;

    public FileWriterComponent(
        FlowNodeId? id = null,
        int boundedCapacity = 1000)
        : base(id ?? FlowNodeId.New())
    {
        if (boundedCapacity <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(boundedCapacity),
                "File writer bounded capacity must be greater than zero.");
        }

        _innerNode = CreateInnerNode(boundedCapacity);
        _input = new TransformBlock<FileWriteRequest, PackageFileWriteRequest>(
            MapRequest,
            new ExecutionDataflowBlockOptions
            {
                BoundedCapacity = boundedCapacity,
                EnsureOrdered = true
            });
        _result = new ActionBlock<PackageFileWriteResult>(
            PublishFileWritten,
            new ExecutionDataflowBlockOptions
            {
                BoundedCapacity = boundedCapacity,
                EnsureOrdered = true
            });
        _errors = new ActionBlock<FlowError>(
            error => TryReportError(error.Code, error.Message, error.Exception, error.Context),
            new ExecutionDataflowBlockOptions
            {
                BoundedCapacity = boundedCapacity,
                EnsureOrdered = true
            });

        _links.Add(_input.LinkTo(
            GetInput<PackageFileWriteRequest>(_innerNode, FileSystemComponentPorts.Input),
            new DataflowLinkOptions { PropagateCompletion = true }));
        _links.Add(LinkOutput(_innerNode, FileSystemComponentPorts.Result, _result));
        _links.Add(_innerNode.Node.Errors.LinkTo(
            _errors,
            new DataflowLinkOptions { PropagateCompletion = true }));

        CompleteWhen(Task.WhenAll(
            _innerNode.Node.Completion,
            _result.Completion,
            _errors.Completion));
    }

    public ITargetBlock<FileWriteRequest> Input => _input;

    public override Task StartAsync(CancellationToken cancellationToken = default)
        => Interlocked.Exchange(ref _started, 1) == 0
            ? _innerNode.Node.StartAsync(cancellationToken)
            : Task.CompletedTask;

    public override void Complete()
        => _input.Complete();

    public override void Fault(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        try
        {
            ((IDataflowBlock)_input).Fault(exception);
            _innerNode.Node.Fault(exception);
            ((IDataflowBlock)_result).Fault(exception);
            ((IDataflowBlock)_errors).Fault(exception);
        }
        finally
        {
            FaultNode(exception);
        }
    }

    protected override void OnNodeCompleted()
    {
        DisposeLinks();
        base.OnNodeCompleted();
    }

    protected override void OnNodeFaulted(Exception exception)
    {
        DisposeLinks();
        base.OnNodeFaulted(exception);
    }

    private static RuntimeNode CreateInnerNode(int boundedCapacity)
    {
        var registry = new RuntimeNodeFactoryRegistry()
            .RegisterFileSystemComponents();

        if (!registry.TryGetFactory(FileSystemComponentTypes.FileWrite, out var factory))
        {
            throw new InvalidOperationException("File write package factory was not registered.");
        }

        return factory(new RuntimeNodeFactoryContext(
            new NodeName("fileWrite"),
            CreateInnerDefinition(boundedCapacity),
            "fileWrite",
            new Dictionary<NodeName, RuntimeNode>()));
    }

    private static NodeDefinition CreateInnerDefinition(int boundedCapacity)
    {
        var definition = new NodeDefinition
        {
            Type = FileSystemComponentTypes.FileWrite
        };

        definition.Configuration["boundedCapacity"] =
            JsonSerializer.SerializeToElement(boundedCapacity);
        definition.Configuration["allowAbsolutePaths"] =
            JsonSerializer.SerializeToElement(true);

        return definition;
    }

    private static ITargetBlock<T> GetInput<T>(RuntimeNode node, string portName)
    {
        var input = node.FindInput(new PortName(portName))
            ?? throw new InvalidOperationException($"Inner file writer input '{portName}' was not found.");

        return input is InputPort<T> typedInput
            ? typedInput.Target
            : throw new InvalidOperationException(
                $"Inner file writer input '{portName}' has unsupported type '{input.ValueType.Name}'.");
    }

    private static IDisposable LinkOutput<T>(
        RuntimeNode node,
        string portName,
        ITargetBlock<T> target)
    {
        var output = node.FindOutput(new PortName(portName))
            ?? throw new InvalidOperationException($"Inner file writer output '{portName}' was not found.");

        var link = output.TryLinkTo(
            new InputPort<T>(
                new PortAddress(
                    "fileWrite",
                    new NodeName(portName),
                    InputPort),
                target),
            propagateCompletion: true,
            out var error);

        if (error is not null || link is null)
        {
            throw new InvalidOperationException(
                error?.Message ?? $"Could not link inner file writer output '{portName}'.");
        }

        return link;
    }

    private static PackageFileWriteRequest MapRequest(FileWriteRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return new PackageFileWriteRequest
        {
            Path = request.Path,
            Bytes = request.Content,
            Mode = MapMode(request.Mode),
            CreateDirectories = request.CreateDirectory
        };
    }

    private static PackageFileWriteMode MapMode(FileWriteMode mode)
        => mode switch
        {
            FileWriteMode.Overwrite => PackageFileWriteMode.Overwrite,
            FileWriteMode.Append => PackageFileWriteMode.Append,
            FileWriteMode.CreateNew => PackageFileWriteMode.CreateNew,
            _ => throw new InvalidOperationException($"Unsupported file write mode '{mode}'.")
        };

    private static FileWriteMode MapMode(PackageFileWriteMode mode)
        => mode switch
        {
            PackageFileWriteMode.Overwrite => FileWriteMode.Overwrite,
            PackageFileWriteMode.Append => FileWriteMode.Append,
            PackageFileWriteMode.CreateNew => FileWriteMode.CreateNew,
            _ => throw new InvalidOperationException($"Unsupported file write mode '{mode}'.")
        };

    private void PublishFileWritten(PackageFileWriteResult result)
        => EmitEvent(new FlowEvent
        {
            Timestamp = result.WrittenAt,
            Type = FluxMqEventTypes.FileWritten,
            Source = "FileWriter",
            SourceNodeId = Id,
            Subject = result.Path,
            Status = "written",
            PayloadBytes = result.BytesWritten > int.MaxValue ? int.MaxValue : (int)result.BytesWritten,
            Attributes = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["path"] = result.Path,
                ["mode"] = MapMode(result.Mode).ToString(),
                ["bytesWritten"] = result.BytesWritten.ToString(CultureInfo.InvariantCulture)
            }
        });

    private void DisposeLinks()
    {
        foreach (var link in _links)
        {
            link.Dispose();
        }

        _links.Clear();
    }
}
