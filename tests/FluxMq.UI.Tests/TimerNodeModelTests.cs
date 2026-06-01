using FluxMq.UI.Components.Diagram;
using FluxMq.UI.Components.Workspace.Nodes.DynamicMapper;
using FluxMq.UI.Components.Workspace.Nodes.Timers;
using FluxMq.UI.Models;
using FluxMq.UI.Services;
using Shouldly;
using System.Text.Json.Nodes;
using DiagramPoint = Blazor.Diagrams.Core.Geometry.Point;

namespace FluxMq.UI.Tests;

public sealed class TimerNodeModelTests
{
    [Theory]
    [InlineData("timer.interval", typeof(TimerIntervalNodeModel))]
    [InlineData("timer.schedule", typeof(TimerScheduleNodeModel))]
    [InlineData("timer.delay", typeof(TimerDelayNodeModel))]
    public void FlowNodeModelFactory_CreatesTypedTimerModels(string nodeType, Type expectedType)
    {
        var catalog = new FlowComponentCatalog();
        var descriptor = catalog.Find(nodeType).ShouldNotBeNull();

        var model = FlowNodeModelFactory.Create(
            $"workflow1.{nodeType}",
            new DiagramPoint(10, 20),
            "timer",
            nodeType,
            descriptor,
            isResource: false);

        model.GetType().ShouldBe(expectedType);
    }

    [Fact]
    public void TimerIntervalNodeModel_BuildsConfiguration()
    {
        var model = new TimerIntervalNodeModel(
            "workflow1.timer",
            new DiagramPoint(10, 20),
            "timer",
            descriptor: null,
            isResource: false)
        {
            IntervalMilliseconds = 2500,
            InitialDelayMilliseconds = 500,
            EmitImmediately = true,
            MaxTicks = 3,
            BoundedCapacity = 200
        };

        var config = model.BuildConfiguration();

        config["intervalMilliseconds"]!.GetValue<int>().ShouldBe(2500);
        config["initialDelayMilliseconds"]!.GetValue<int>().ShouldBe(500);
        config["emitImmediately"]!.GetValue<bool>().ShouldBeTrue();
        config["maxTicks"]!.GetValue<int>().ShouldBe(3);
        config["boundedCapacity"]!.GetValue<int>().ShouldBe(200);
    }

    [Fact]
    public void TimerScheduleNodeModel_BuildsConfiguration()
    {
        var model = new TimerScheduleNodeModel(
            "workflow1.schedule",
            new DiagramPoint(10, 20),
            "schedule",
            descriptor: null,
            isResource: false)
        {
            Cron = "0 12 * * MON-FRI",
            TimeZoneId = "UTC",
            MaxTicks = 2,
            BoundedCapacity = 300
        };

        var config = model.BuildConfiguration();

        config["cron"]!.GetValue<string>().ShouldBe("0 12 * * MON-FRI");
        config["timeZoneId"]!.GetValue<string>().ShouldBe("UTC");
        config["maxTicks"]!.GetValue<int>().ShouldBe(2);
        config["boundedCapacity"]!.GetValue<int>().ShouldBe(300);
    }

    [Fact]
    public void TimerDelayNodeModel_UsesConfiguredInputTypeForPorts()
    {
        var model = new TimerDelayNodeModel(
            "workflow1.delay",
            new DiagramPoint(10, 20),
            "delay",
            descriptor: null,
            isResource: false)
        {
            InputType = "TimerTick",
            DelayMilliseconds = 750,
            BoundedCapacity = 250
        };

        var config = model.BuildConfiguration();

        config["inputType"]!.GetValue<string>().ShouldBe("TimerTick");
        config["delayMilliseconds"]!.GetValue<int>().ShouldBe(750);
        config["boundedCapacity"]!.GetValue<int>().ShouldBe(250);
        model.ResolvePortValueType(new ComponentPortDescriptor("Input", "Configured input type", IsInput: true))
            .ShouldBe("TimerTick");
        model.ResolvePortValueType(new ComponentPortDescriptor("Output", "Configured input type", IsInput: false))
            .ShouldBe("TimerTick");
    }

    [Fact]
    public void DynamicMapperNodeModel_UsesConfiguredInputTypeForTimerPorts()
    {
        var descriptor = new ComponentPortDescriptor("Input", "MqttEnvelope", IsInput: true);
        var model = new DynamicMapperNodeModel(
            "workflow1.mapper",
            new DiagramPoint(10, 20),
            "mapper",
            descriptor: null,
            isResource: false)
        {
            InputType = "ScheduleTick"
        };

        model.ResolvePortValueType(descriptor).ShouldBe("ScheduleTick");
    }
}
