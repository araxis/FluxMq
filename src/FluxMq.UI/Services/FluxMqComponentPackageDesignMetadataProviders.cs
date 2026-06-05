using FluxFlow.Components.Designer;

namespace FluxMq.UI.Services;

public static class FluxMqComponentPackageDesignMetadataProviders
{
    private static readonly string[] ProviderTypeNames =
    [
        "FluxFlow.Components.Http.HttpComponentDesignMetadataProvider, FluxFlow.Components.Http",
        "FluxFlow.Components.Mapping.MappingComponentDesignMetadataProvider, FluxFlow.Components.Mapping",
        "FluxFlow.Components.Payloads.PayloadComponentDesignMetadataProvider, FluxFlow.Components.Payloads",
        "FluxFlow.Components.Routing.RoutingComponentDesignMetadataProvider, FluxFlow.Components.Routing",
        "FluxFlow.Components.Serialization.SerializationComponentDesignMetadataProvider, FluxFlow.Components.Serialization",
        "FluxFlow.Components.State.StateComponentDesignMetadataProvider, FluxFlow.Components.State",
        "FluxFlow.Components.Storage.StorageComponentDesignMetadataProvider, FluxFlow.Components.Storage",
        "FluxFlow.Components.Timers.TimerComponentDesignMetadataProvider, FluxFlow.Components.Timers"
    ];

    public static IReadOnlyList<IComponentDesignMetadataProvider> CreateDefault()
    {
        var providers = new List<IComponentDesignMetadataProvider>();

        foreach (var providerTypeName in ProviderTypeNames)
        {
            var providerType = Type.GetType(providerTypeName, throwOnError: false);
            if (providerType is null ||
                !typeof(IComponentDesignMetadataProvider).IsAssignableFrom(providerType) ||
                Activator.CreateInstance(providerType) is not IComponentDesignMetadataProvider provider)
            {
                continue;
            }

            providers.Add(provider);
        }

        return providers;
    }
}
