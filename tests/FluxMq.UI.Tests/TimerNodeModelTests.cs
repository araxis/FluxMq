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
    [InlineData("timer.debounce", typeof(TimerDebounceNodeModel))]
    [InlineData("timer.throttle", typeof(TimerThrottleNodeModel))]
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
    public void TimerDebounceNodeModel_UsesConfiguredInputTypeForPorts()
    {
        var model = new TimerDebounceNodeModel(
            "workflow1.debounce",
            new DiagramPoint(10, 20),
            "debounce",
            descriptor: null,
            isResource: false)
        {
            InputType = "TimerTick",
            QuietPeriodMilliseconds = 800,
            BoundedCapacity = 64
        };

        var config = model.BuildConfiguration();

        config["inputType"]!.GetValue<string>().ShouldBe("TimerTick");
        config["quietPeriodMilliseconds"]!.GetValue<int>().ShouldBe(800);
        config["boundedCapacity"]!.GetValue<int>().ShouldBe(64);
        model.ResolvePortValueType(new ComponentPortDescriptor("Input", "Configured input type", IsInput: true))
            .ShouldBe("TimerTick");
        model.ResolvePortValueType(new ComponentPortDescriptor("Output", "Configured input type", IsInput: false))
            .ShouldBe("TimerTick");
    }

    [Fact]
    public void TimerThrottleNodeModel_UsesConfiguredInputTypeForPorts()
    {
        var model = new TimerThrottleNodeModel(
            "workflow1.throttle",
            new DiagramPoint(10, 20),
            "throttle",
            descriptor: null,
            isResource: false)
        {
            InputType = "ScheduleTick",
            IntervalMilliseconds = 900,
            EmitFirstImmediately = false,
            BoundedCapacity = 32
        };

        var config = model.BuildConfiguration();

        config["inputType"]!.GetValue<string>().ShouldBe("ScheduleTick");
        config["intervalMilliseconds"]!.GetValue<int>().ShouldBe(900);
        config["emitFirstImmediately"]!.GetValue<bool>().ShouldBeFalse();
        config["boundedCapacity"]!.GetValue<int>().ShouldBe(32);
        model.ResolvePortValueType(new ComponentPortDescriptor("Input", "Configured input type", IsInput: true))
            .ShouldBe("ScheduleTick");
        model.ResolvePortValueType(new ComponentPortDescriptor("Output", "Configured input type", IsInput: false))
            .ShouldBe("ScheduleTick");
    }

    [Fact]
    public void TimerNodeModels_NormalizeInvalidValuesAndPreserveConfigShapes()
    {
        var interval = new TimerIntervalNodeModel(
            "workflow1.interval",
            new DiagramPoint(10, 20),
            "interval",
            descriptor: null,
            isResource: false)
        {
            IntervalMilliseconds = 0,
            InitialDelayMilliseconds = -1,
            MaxTicks = -2,
            BoundedCapacity = 0
        };

        var intervalConfig = interval.BuildConfiguration();

        intervalConfig.Select(static pair => pair.Key).ShouldBe([
            "intervalMilliseconds",
            "initialDelayMilliseconds",
            "emitImmediately",
            "boundedCapacity"
        ]);
        intervalConfig["intervalMilliseconds"]!.GetValue<int>().ShouldBe(TimerIntervalNodeModel.DefaultIntervalMilliseconds);
        intervalConfig["initialDelayMilliseconds"]!.GetValue<int>().ShouldBe(TimerIntervalNodeModel.DefaultInitialDelayMilliseconds);
        intervalConfig["boundedCapacity"]!.GetValue<int>().ShouldBe(TimerIntervalNodeModel.DefaultBoundedCapacity);
        intervalConfig.ContainsKey("maxTicks").ShouldBeFalse();

        var schedule = new TimerScheduleNodeModel(
            "workflow1.schedule",
            new DiagramPoint(10, 20),
            "schedule",
            descriptor: null,
            isResource: false)
        {
            Cron = " ",
            TimeZoneId = " ",
            MaxTicks = -1,
            BoundedCapacity = 0
        };

        var scheduleConfig = schedule.BuildConfiguration();

        scheduleConfig.Select(static pair => pair.Key).ShouldBe([
            "cron",
            "timeZoneId",
            "boundedCapacity"
        ]);
        scheduleConfig["cron"]!.GetValue<string>().ShouldBe(TimerScheduleNodeModel.DefaultCron);
        scheduleConfig["timeZoneId"]!.GetValue<string>().ShouldBe(TimerScheduleNodeModel.DefaultTimeZoneId);
        scheduleConfig["boundedCapacity"]!.GetValue<int>().ShouldBe(TimerScheduleNodeModel.DefaultBoundedCapacity);
        scheduleConfig.ContainsKey("maxTicks").ShouldBeFalse();

        var delay = new TimerDelayNodeModel(
            "workflow1.delay",
            new DiagramPoint(10, 20),
            "delay",
            descriptor: null,
            isResource: false)
        {
            InputType = "missing",
            DelayMilliseconds = -1,
            BoundedCapacity = 0
        };

        var delayConfig = delay.BuildConfiguration();

        delayConfig.Select(static pair => pair.Key).ShouldBe([
            "inputType",
            "delayMilliseconds",
            "boundedCapacity"
        ]);
        delayConfig["inputType"]!.GetValue<string>().ShouldBe(TimerDelayNodeModel.DefaultInputType);
        delayConfig["delayMilliseconds"]!.GetValue<int>().ShouldBe(TimerDelayNodeModel.DefaultDelayMilliseconds);
        delayConfig["boundedCapacity"]!.GetValue<int>().ShouldBe(TimerDelayNodeModel.DefaultBoundedCapacity);

        var debounce = new TimerDebounceNodeModel(
            "workflow1.debounce",
            new DiagramPoint(10, 20),
            "debounce",
            descriptor: null,
            isResource: false)
        {
            InputType = "missing",
            QuietPeriodMilliseconds = 0,
            BoundedCapacity = 0
        };

        var debounceConfig = debounce.BuildConfiguration();

        debounceConfig.Select(static pair => pair.Key).ShouldBe([
            "inputType",
            "quietPeriodMilliseconds",
            "boundedCapacity"
        ]);
        debounceConfig["inputType"]!.GetValue<string>().ShouldBe(TimerDelayNodeModel.DefaultInputType);
        debounceConfig["quietPeriodMilliseconds"]!.GetValue<int>().ShouldBe(TimerDebounceNodeModel.DefaultQuietPeriodMilliseconds);
        debounceConfig["boundedCapacity"]!.GetValue<int>().ShouldBe(TimerDebounceNodeModel.DefaultBoundedCapacity);

        var throttle = new TimerThrottleNodeModel(
            "workflow1.throttle",
            new DiagramPoint(10, 20),
            "throttle",
            descriptor: null,
            isResource: false)
        {
            InputType = "missing",
            IntervalMilliseconds = 0,
            BoundedCapacity = 0
        };

        var throttleConfig = throttle.BuildConfiguration();

        throttleConfig.Select(static pair => pair.Key).ShouldBe([
            "inputType",
            "intervalMilliseconds",
            "emitFirstImmediately",
            "boundedCapacity"
        ]);
        throttleConfig["inputType"]!.GetValue<string>().ShouldBe(TimerDelayNodeModel.DefaultInputType);
        throttleConfig["intervalMilliseconds"]!.GetValue<int>().ShouldBe(TimerThrottleNodeModel.DefaultIntervalMilliseconds);
        throttleConfig["boundedCapacity"]!.GetValue<int>().ShouldBe(TimerThrottleNodeModel.DefaultBoundedCapacity);
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
