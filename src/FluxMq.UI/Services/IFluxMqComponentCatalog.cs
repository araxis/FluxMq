using FluxMq.UI.Models;
using FluxFlow.Components.Designer;
using System.Text.Json.Nodes;

namespace FluxMq.UI.Services;

public interface IFluxMqComponentCatalog
{
    IReadOnlyList<FlowComponentDescriptor> Components { get; }

    ComponentDesignMetadataCatalog DesignMetadata { get; }

    FlowComponentDescriptor? Find(string type);

    FlowComponentMetadata? FindMetadata(string type);

    JsonObject? CreateDefaultConfiguration(
        string componentType,
        FlowComponentDefaultConfigurationContext context);
}
