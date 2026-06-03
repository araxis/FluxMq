using FluxMq.UI.Models;

namespace FluxMq.UI.Services;

public sealed class FlowComponentCatalog
{
    public IReadOnlyList<FlowComponentDescriptor> Components => FlowComponentMetadataRegistry.Descriptors;

    public FlowComponentDescriptor? Find(string type)
        => FlowComponentMetadataRegistry.Find(type)?.Descriptor;
}
