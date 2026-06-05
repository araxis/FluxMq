using FluxMq.UI.Models;

namespace FluxMq.UI.Services;

public sealed class FlowComponentCatalog(IFluxMqComponentCatalog catalog)
{
    private readonly IFluxMqComponentCatalog _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));

    public FlowComponentCatalog()
        : this(FluxMqComponentCatalogAdapter.Shared)
    {
    }

    public IReadOnlyList<FlowComponentDescriptor> Components => _catalog.Components;

    public FlowComponentDescriptor? Find(string type)
        => _catalog.Find(type);
}
