using FluentAssertions;
using FluxMq.Core.Ids;
using FluxMq.Pipeline.Components;
using FluxMq.Pipeline.Definitions;
using FluxMq.Pipeline.Runtime;
using FluxMq.App;
using Microsoft.Extensions.Configuration;
using System.Text;
using System.Threading.Tasks.Dataflow;

namespace FluxMq.App.Tests;

public sealed class FlowApplicationHostTests
{
    [Fact]
    public void Start_BuildsRuntimeFromConfiguration()
    {
        using var host = FlowApplicationHost.CreateDefault(BuildConfiguration(
            """
            {
              "FluxMq": {
                "FlowApplication": {
                  "workflows": {
                    "observe": {
                      "metrics": {
                        "type": "mqtt.metrics-sink"
                      }
                    }
                  }
                }
              }
            }
            """));

        var result = host.Start();

        result.IsSuccess.Should().BeTrue();
        host.State.Should().Be(FlowApplicationHostState.Running);
        host.Runtime.Should().NotBeNull();
        host.Runtime!.Workflows.Should().Contain(wf => wf.Name.Value == "observe");
    }

    [Fact]
    public async Task StopAsync_CompletesBuiltRuntime()
    {
        await using var host = FlowApplicationHost.CreateDefault(BuildConfiguration(
            """
            {
              "FluxMq": {
                "FlowApplication": {
                  "workflows": {
                    "observe": {
                      "metrics": {
                        "type": "mqtt.metrics-sink"
                      }
                    }
                  }
                }
              }
            }
            """));

        host.Start().IsSuccess.Should().BeTrue();

        await host.StopAsync();

        host.State.Should().Be(FlowApplicationHostState.Stopped);
        host.Runtime!.Completion.IsCompletedSuccessfully.Should().BeTrue();
    }

    [Fact]
    public void Start_ReturnsHostErrorWhenConfigurationIsMissing()
    {
        using var host = FlowApplicationHost.CreateDefault(BuildConfiguration("""{ "FluxMq": {} }"""));

        var result = host.Start();

        result.IsSuccess.Should().BeFalse();
        result.Errors.Should().ContainSingle()
            .Which.Code.Should().Be(FlowApplicationHostBuildErrorCode.InvalidConfiguration);
        host.State.Should().Be(FlowApplicationHostState.Empty);
    }

    [Fact]
    public void Start_ReturnsRuntimeBuildErrorsForInvalidComponentConfiguration()
    {
        using var host = FlowApplicationHost.CreateDefault(BuildConfiguration(
            """
            {
              "FluxMq": {
                "FlowApplication": {
                  "workflows": {
                    "observe": {
                      "inspect": {
                        "type": "mqtt.payload-inspector",
                        "configuration": {
                          "boundedCapacity": 0
                        }
                      }
                    }
                  }
                }
              }
            }
            """));

        var result = host.Start();

        result.IsSuccess.Should().BeFalse();
        result.Errors.Should().BeEmpty();
        result.RuntimeBuild!.Errors.Should().ContainSingle(error => error.Message.Contains("boundedCapacity"));
        host.State.Should().Be(FlowApplicationHostState.Empty);
    }

    [Fact]
    public async Task StopAsync_ConvertsRuntimeCompletionFailureToFaultedState()
    {
        var builder = new ApplicationRuntimeBuilder(new RuntimeNodeFactoryRegistry()
            .Register(new NodeType("test.faulting"), (address, _) =>
                RuntimeNode.Create(address, new FaultingNode())));

        await using var host = new FlowApplicationHost(
            BuildConfiguration(
                """
                {
                  "FluxMq": {
                    "FlowApplication": {
                      "workflows": {
                        "observe": {
                          "faulting": {
                            "type": "test.faulting"
                          }
                        }
                      }
                    }
                  }
                }
                """),
            builder);

        host.Start().IsSuccess.Should().BeTrue();

        await host.StopAsync();

        host.State.Should().Be(FlowApplicationHostState.Faulted);
        host.LastException.Should().BeOfType<InvalidOperationException>()
            .Which.Message.Should().Be("completion failed");
    }

    [Fact]
    public async Task StartAsync_ConvertsStartFailureToHostError()
    {
        var builder = new ApplicationRuntimeBuilder(new RuntimeNodeFactoryRegistry()
            .Register(new NodeType("test.start-fails"), (address, _) =>
                RuntimeNode.Create(address, new StartFailingNode())));

        await using var host = new FlowApplicationHost(
            BuildConfiguration(
                """
                {
                  "FluxMq": {
                    "FlowApplication": {
                      "workflows": {
                        "observe": {
                          "start": {
                            "type": "test.start-fails"
                          }
                        }
                      }
                    }
                  }
                }
                """),
            builder);

        var result = await host.StartAsync();

        result.IsSuccess.Should().BeFalse();
        result.Errors.Should().ContainSingle()
            .Which.Code.Should().Be(FlowApplicationHostBuildErrorCode.StartFailed);
        host.State.Should().Be(FlowApplicationHostState.Faulted);
        host.LastException.Should().BeOfType<InvalidOperationException>()
            .Which.Message.Should().Be("start failed");
    }

    private static IConfiguration BuildConfiguration(string json)
        => new ConfigurationBuilder()
            .AddJsonStream(new MemoryStream(Encoding.UTF8.GetBytes(json)))
            .Build();

    private sealed class FaultingNode : IFlowNode
    {
        private readonly TaskCompletionSource _completion = new();
        private readonly BufferBlock<FlowError> _errors = new();

        public FlowNodeId Id { get; } = FlowNodeId.New();
        public ISourceBlock<FlowError> Errors => _errors;
        public Task Completion => _completion.Task;

        public void Complete()
        {
            _completion.SetException(new InvalidOperationException("completion failed"));
            _errors.Complete();
        }

        public void Fault(Exception exception)
        {
            _completion.SetException(exception);
            _errors.Complete();
        }
    }

    private sealed class StartFailingNode : IFlowNode
    {
        private readonly BufferBlock<FlowError> _errors = new();

        public FlowNodeId Id { get; } = FlowNodeId.New();
        public ISourceBlock<FlowError> Errors => _errors;
        public Task Completion => Task.CompletedTask;

        public Task StartAsync(CancellationToken cancellationToken = default)
            => Task.FromException(new InvalidOperationException("start failed"));

        public void Complete() => _errors.Complete();

        public void Fault(Exception exception) => _errors.Complete();
    }
}
