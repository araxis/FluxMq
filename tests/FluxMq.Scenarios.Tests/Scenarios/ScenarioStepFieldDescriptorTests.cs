using FluxMq.Scenarios;
using Shouldly;

namespace FluxMq.Scenarios.Tests.Scenarios;

public sealed class ScenarioStepFieldDescriptorTests
{
    [Fact]
    public void Options_NormalizesMissingListToEmpty()
    {
        var descriptor = new ScenarioStepFieldDescriptor(
            "field",
            "Field",
            ScenarioStepFieldKind.Select,
            string.Empty,
            null!);

        descriptor.Options.ShouldBeEmpty();
    }
}
