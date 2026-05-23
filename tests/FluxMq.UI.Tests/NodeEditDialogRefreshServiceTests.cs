using FluxMq.UI.Services;
using Shouldly;

namespace FluxMq.UI.Tests;

public sealed class NodeEditDialogRefreshServiceTests
{
    [Fact]
    public async Task RefreshAsync_InvokesRegisteredDialogRefresh()
    {
        var service = new NodeEditDialogRefreshService();
        var calls = 0;

        service.Register("mapper", () =>
        {
            calls++;
            return Task.CompletedTask;
        });

        await service.RefreshAsync("mapper");

        calls.ShouldBe(1);
    }

    [Fact]
    public async Task RefreshAsync_IgnoresUnregisteredNode()
    {
        var service = new NodeEditDialogRefreshService();
        var calls = 0;
        service.Register("mapper", () =>
        {
            calls++;
            return Task.CompletedTask;
        });

        service.Unregister("mapper");

        await service.RefreshAsync("mapper");

        calls.ShouldBe(0);
    }
}
