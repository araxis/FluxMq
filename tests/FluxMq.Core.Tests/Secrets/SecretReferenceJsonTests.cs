using FluxFlow.Components.Secrets.Contracts;
using FluxMq.Core.Secrets;
using Shouldly;
using System.Text.Json;

namespace FluxMq.Core.Tests.Secrets;

public sealed class SecretReferenceJsonTests
{
    [Fact]
    public void ReadOptional_ReturnsReferenceFromStringShorthand()
    {
        using var doc = JsonDocument.Parse("""{ "passwordSecret": "broker-password" }""");

        var reference = SecretReferenceJson.ReadOptional(doc.RootElement, "passwordSecret");

        reference.ShouldNotBeNull();
        reference.Name.Value.ShouldBe("broker-password");
    }

    [Fact]
    public void ReadOptional_ReturnsReferenceFromObject()
    {
        using var doc = JsonDocument.Parse("""
        {
          "passwordSecret": {
            "name": "broker-password",
            "version": "v1",
            "kind": "mqtt-password",
            "attributes": {
              "scope": "local"
            }
          }
        }
        """);

        var reference = SecretReferenceJson.ReadOptional(doc.RootElement, "passwordSecret");

        reference.ShouldNotBeNull();
        reference.Name.Value.ShouldBe("broker-password");
        reference.Version.ShouldBe("v1");
        reference.Kind.ShouldBe("mqtt-password");
        reference.Attributes["scope"].ShouldBe("local");
    }

    [Fact]
    public void Write_EmitsOnlyReferenceMetadata()
    {
        var reference = new SecretReference
        {
            Name = new SecretName("broker-password"),
            Version = "v1",
            Kind = "mqtt-password",
            Attributes = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["scope"] = "local"
            }
        };

        var json = SecretReferenceJson.Write(reference);

        json["name"]!.GetValue<string>().ShouldBe("broker-password");
        json["version"]!.GetValue<string>().ShouldBe("v1");
        json["kind"]!.GetValue<string>().ShouldBe("mqtt-password");
        json["attributes"]!["scope"]!.GetValue<string>().ShouldBe("local");
    }
}
