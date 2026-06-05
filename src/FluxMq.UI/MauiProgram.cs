using FluxMq.Core.Mqtt;
using FluxMq.Core.TopicIndex;
using FluxMq.Components.Storage;
using FluxMq.Components.Storage.Repositories;
using FluxMq.UI.Services;
using Microsoft.Extensions.Logging;
#if WINDOWS
using Microsoft.Maui.LifecycleEvents;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using WinRT.Interop;
#endif
using MudBlazor.Services;

namespace FluxMq.UI;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>();

#if WINDOWS
        builder.ConfigureLifecycleEvents(events =>
        {
            events.AddWindows(windows =>
            {
                windows.OnWindowCreated(window =>
                {
                    var iconPath = Path.Combine(AppContext.BaseDirectory, "appicon.ico");
                    if (!File.Exists(iconPath))
                    {
                        return;
                    }

                    var handle = WindowNative.GetWindowHandle(window);
                    var windowId = Win32Interop.GetWindowIdFromWindow(handle);
                    AppWindow.GetFromWindowId(windowId).SetIcon(iconPath);
                });
            });
        });
#endif

        builder.Services.AddMauiBlazorWebView();
        builder.Services.AddMudServices();

        builder.Services.AddSingleton<ITopicIndex, TopicIndex>();
        builder.Services.AddSingleton<IMqttConnectionManager, MqttConnectionManager>();
        builder.Services.AddSingleton(_ =>
        {
            var directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FluxMQ");
            Directory.CreateDirectory(directory);
            return new FluxDbContext(Path.Combine(directory, "flux.db"));
        });
        builder.Services.AddSingleton<IMessageRepository, LiteDbMessageRepository>();
        builder.Services.AddSingleton<ISessionRepository, LiteDbSessionRepository>();
        builder.Services.AddSingleton<IScenarioRunHistoryRepository, LiteDbScenarioRunHistoryRepository>();
        builder.Services.AddSingleton<FluxMqComponentAliasRegistry>();
        builder.Services.AddSingleton(_ => FluxMqComponentPackageDesignMetadataProviders.CreateDefault());
        builder.Services.AddSingleton<IFluxMqComponentCatalog>(services =>
            new FluxMqComponentCatalogAdapter(
                services.GetRequiredService<FluxMqComponentAliasRegistry>(),
                services.GetRequiredService<IReadOnlyList<FluxFlow.Components.Designer.IComponentDesignMetadataProvider>>()));
        builder.Services.AddSingleton<FlowComponentCatalog>();
        builder.Services.AddSingleton<DashboardWidgetCatalog>();
        builder.Services.AddSingleton<DashboardWidgetRegistry>();
        builder.Services.AddSingleton<DashboardMetricRegistry>();
        builder.Services.AddSingleton<IFluxChartAdapter, FluxChartAdapter>();
        builder.Services.AddSingleton(ScenarioStepCatalog.Shared);
        builder.Services.AddSingleton(DashboardEventFilterCatalog.Shared);
        builder.Services.AddSingleton<NodeWidgetRegistry>();
        builder.Services.AddSingleton<FlowDefinitionComposer>();
        builder.Services.AddSingleton<ProjectManagerService>();
        builder.Services.AddSingleton<LiveMqttWorkspaceService>();
        builder.Services.AddSingleton<AppThemeService>();
        builder.Services.AddSingleton<DragStateService>();
        builder.Services.AddSingleton<NodeEditDialogRefreshService>();

#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
