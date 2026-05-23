namespace FluxMq.Components.FileWriter;

public sealed record FileWriteRequest
{
    public required string Path { get; init; }
    public byte[] Content { get; init; } = [];
    public FileWriteMode Mode { get; init; } = FileWriteMode.Overwrite;
    public bool CreateDirectory { get; init; } = true;
}
