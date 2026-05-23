using FluxMq.Core.Ids;
using FluxMq.Pipeline.Components;
using System.Threading.Tasks.Dataflow;

namespace FluxMq.Components.FileWriter;

public sealed class FileWriterComponent : IFlowNode
{
    private readonly ActionBlock<FileWriteRequest> _block;
    private readonly BroadcastBlock<FlowError> _errors;

    public FileWriterComponent(
        FlowNodeId? id = null,
        int boundedCapacity = 1000,
        int maxDegreeOfParallelism = 1)
    {
        if (maxDegreeOfParallelism <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxDegreeOfParallelism), maxDegreeOfParallelism, "Degree of parallelism must be positive.");
        }

        Id = id ?? FlowNodeId.New();
        _errors = new BroadcastBlock<FlowError>(static error => error);
        _block = new ActionBlock<FileWriteRequest>(
            WriteAsync,
            new ExecutionDataflowBlockOptions
            {
                BoundedCapacity = boundedCapacity,
                EnsureOrdered = true,
                MaxDegreeOfParallelism = maxDegreeOfParallelism
            });

        _block.Completion.ContinueWith(
            _ => _errors.Complete(),
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    public FlowNodeId Id { get; }
    public ISourceBlock<FlowError> Errors => _errors;
    public Task Completion => _block.Completion;
    public ITargetBlock<FileWriteRequest> Input => _block;

    public void Complete() => _block.Complete();

    public void Fault(Exception exception)
    {
        PublishError(FlowErrorCodes.NodeFaulted, "File writer faulted.", exception);
        ((IDataflowBlock)_block).Fault(exception);
    }

    private async Task WriteAsync(FileWriteRequest request)
    {
        try
        {
            if (request.CreateDirectory && Path.GetDirectoryName(request.Path) is { Length: > 0 } directory)
            {
                Directory.CreateDirectory(directory);
            }

            switch (request.Mode)
            {
                case FileWriteMode.Overwrite:
                    await File.WriteAllBytesAsync(request.Path, request.Content).ConfigureAwait(false);
                    break;
                case FileWriteMode.Append:
                    await using (var stream = new FileStream(request.Path, FileMode.Append, FileAccess.Write, FileShare.Read))
                    {
                        await stream.WriteAsync(request.Content).ConfigureAwait(false);
                    }
                    break;
                case FileWriteMode.CreateNew:
                    await using (var stream = new FileStream(request.Path, FileMode.CreateNew, FileAccess.Write, FileShare.Read))
                    {
                        await stream.WriteAsync(request.Content).ConfigureAwait(false);
                    }
                    break;
                default:
                    throw new InvalidOperationException($"Unsupported file write mode '{request.Mode}'.");
            }
        }
        catch (Exception exception)
        {
            PublishError(FlowErrorCodes.ProcessingFailed, "File write failed.", exception, request.Path);
        }
    }

    private void PublishError(int code, string message, Exception exception, string? context = null)
    {
        _errors.Post(new FlowError
        {
            NodeId = Id,
            Code = code,
            Message = message,
            Exception = exception,
            Context = context
        });
    }
}
