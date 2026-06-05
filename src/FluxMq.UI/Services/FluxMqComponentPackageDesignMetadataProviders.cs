using FluxFlow.Components.Designer;
using FluxFlow.Components.Http;
using FluxFlow.Components.Mapping;
using FluxFlow.Components.Payloads;
using FluxFlow.Components.Routing;
using FluxFlow.Components.Serialization;
using FluxFlow.Components.State;
using FluxFlow.Components.Storage;
using FluxFlow.Components.Timers;

namespace FluxMq.UI.Services;

public static class FluxMqComponentPackageDesignMetadataProviders
{
    public static IReadOnlyList<IComponentDesignMetadataProvider> CreateDefault() =>
    [
        new HttpComponentDesignMetadataProvider(),
        new MappingComponentDesignMetadataProvider(),
        new PayloadComponentDesignMetadataProvider(),
        new RoutingComponentDesignMetadataProvider(),
        new SerializationComponentDesignMetadataProvider(),
        new StateComponentDesignMetadataProvider(),
        new StorageComponentDesignMetadataProvider(),
        new TimerComponentDesignMetadataProvider()
    ];
}
