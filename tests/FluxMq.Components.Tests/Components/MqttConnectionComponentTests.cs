using Shouldly;
using FluxMq.Components.MessageSource;
using FluxMq.Core.Mqtt;
using FluxFlow.Engine.Components;
using System.Threading.Tasks.Dataflow;

namespace FluxMq.Components.Tests.Components;

public sealed class MqttConnectionComponentTests
{
    [Fact]
    public async Task StartAsync_ConnectsTheUnderlyingClient()
    {
        var mqttClient = new TestMqttBrokerClient();
        var component = new MqttConnectionComponent(mqttClient);

        await component.StartAsync();

        mqttClient.ConnectCalls.ShouldBe(1);
    }

    [Fact]
    public async Task StartAsync_WaitsForConnectionBeforeReturning()
    {
        var connectGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var mqttClient = new TestMqttBrokerClient(connectDelay: connectGate.Task);
        var component = new MqttConnectionComponent(mqttClient);

        var startTask = component.StartAsync();
        await Task.Delay(50);

        startTask.IsCompleted.ShouldBeFalse();
        mqttClient.State.ShouldBe(MqttClientState.Connecting);

        connectGate.SetResult();

        await startTask.WaitAsync(TimeSpan.FromSeconds(5));
        mqttClient.State.ShouldBe(MqttClientState.Connected);
    }

    [Fact]
    public async Task StartAsync_IsIdempotent()
    {
        var mqttClient = new TestMqttBrokerClient();
        var component = new MqttConnectionComponent(mqttClient);

        await component.StartAsync();
        await component.StartAsync();

        mqttClient.ConnectCalls.ShouldBe(1);
    }

    [Fact]
    public void Client_ExposesUnderlyingClient()
    {
        var mqttClient = new TestMqttBrokerClient();
        var component = new MqttConnectionComponent(mqttClient);

        component.Client.ShouldBeSameAs(mqttClient);
    }

    [Fact]
    public async Task DisposeAsync_DisposesOwnedClient()
    {
        var mqttClient = new TestMqttBrokerClient();
        var component = new MqttConnectionComponent(mqttClient, disposeClientOnDispose: true);

        await component.DisposeAsync();

        mqttClient.DisposeCalls.ShouldBe(1);
    }

    [Fact]
    public async Task DisposeAsync_LeavesClientAlone_WhenNotOwned()
    {
        var mqttClient = new TestMqttBrokerClient();
        var component = new MqttConnectionComponent(mqttClient, disposeClientOnDispose: false);

        await component.DisposeAsync();

        mqttClient.DisposeCalls.ShouldBe(0);
    }

    [Fact]
    public async Task Fault_PublishesErrorAndFaultsCompletion()
    {
        var component = new MqttConnectionComponent(new TestMqttBrokerClient());
        var errors = new List<FlowError>();
        var sink = new ActionBlock<FlowError>(errors.Add);
        component.Errors.LinkTo(sink, new DataflowLinkOptions { PropagateCompletion = true });

        var failure = new InvalidOperationException("boom");
        component.Fault(failure);

        var act = async () => await component.Completion;
        var ex = await Should.ThrowAsync<InvalidOperationException>(act);
        ex.Message.ShouldBe("boom");
        await sink.Completion;

        errors.ShouldHaveSingleItem().Code.ShouldBe(FlowErrorCodes.NodeFaulted);
    }
}
