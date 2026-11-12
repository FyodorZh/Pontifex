using System;
using System.Collections.Generic;
using System.Linq;
using Pontifex;
using Pontifex.TransportFormula.Registry;
using Pontifex.TransportFormula.Tests.Registry.Harness;

namespace Pontifex.TransportFormula.Tests.Registry;

[TestFixture]
[Category("TransportFormula.Registry")]
public sealed class TransportRegistryTests
{
    [Test]
    public void Build_WithNoRegistrations_ReturnsEmptyRegistry()
    {
        var registry = RegistryTestData.NewBuilder().Build();

        Assert.That(registry.Types.Types, Is.Empty);
        Assert.That(registry.Types.AllTypes, Is.Empty);
        Assert.That(registry.Constructors, Is.Empty);
        Assert.That(registry.AllSchemeNames, Is.Empty);
        Assert.That(registry.FindCanonical("alpha"), Is.Null);
        Assert.That(registry.FindCandidates("alpha"), Is.Empty);
    }

    [Test]
    public void Build_ReturnsConstructorsInRegistrationOrder()
    {
        var builder = RegistryTestData.NewStandardBuilder();
        builder.RegisterTransport(RegistryTestData.Registration("gamma", RegistryTestData.TypeC, Array.Empty<string>()));
        builder.RegisterTransport(RegistryTestData.Registration("alpha", RegistryTestData.TypeA, Array.Empty<string>()));
        builder.RegisterTransport(RegistryTestData.Registration("beta", RegistryTestData.TypeB, Array.Empty<string>()));

        Assert.That(
            builder.Build().Constructors.Select(c => c.Name),
            Is.EqualTo(new[] { "gamma", "alpha", "beta" }));
    }

    [Test]
    public void FindCanonical_WithNormalizedScheme_ReturnsTheConstructor()
    {
        var builder = RegistryTestData.NewStandardBuilder();
        builder.RegisterTransport(RegistryTestData.Registration(
            "alpha",
            RegistryTestData.TypeA,
            Array.Empty<string>()));

        var constructor = builder.Build().FindCanonical("alpha");

        Assert.That(constructor, Is.Not.Null);
        Assert.That(constructor!.Name, Is.EqualTo("alpha"));
        Assert.That(constructor.Type, Is.EqualTo(RegistryTestData.TypeA));
    }

    [Test]
    public void FindCanonical_WithUppercaseScheme_ReturnsNullBecauseLookupRequiresNormalization()
    {
        var builder = RegistryTestData.NewStandardBuilder();
        builder.RegisterTransport(RegistryTestData.Registration(
            "alpha",
            RegistryTestData.TypeA,
            Array.Empty<string>()));

        Assert.That(builder.Build().FindCanonical("ALPHA"), Is.Null);
    }

    [Test]
    public void FindCanonical_WithUnknownScheme_ReturnsNull()
    {
        var registry = RegistryTestData.NewStandardBuilder().Build();

        Assert.That(registry.FindCanonical("unknown"), Is.Null);
    }

    [Test]
    public void FindCandidates_ForSchemeWithCanonicalAndAliases_ReturnsCanonicalFirstThenAliasesSortedByName()
    {
        var builder = RegistryTestData.NewStandardBuilder();
        builder.RegisterTransport(RegistryTestData.Registration(
            "shared",
            RegistryTestData.TypeA,
            Array.Empty<string>()));
        builder.RegisterTransport(RegistryTestData.Registration(
            "gamma",
            RegistryTestData.TypeC,
            new[] { "shared" }));
        builder.RegisterTransport(RegistryTestData.Registration(
            "beta",
            RegistryTestData.TypeB,
            new[] { "shared" }));

        var registry = builder.Build();

        Assert.That(
            registry.FindCandidates("shared").Select(c => c.Name),
            Is.EqualTo(new[] { "shared", "beta", "gamma" }));
    }

    [Test]
    public void FindCandidates_ForCanonicalSchemeThatIsAlsoAnAlias_ReturnsCanonicalFirst()
    {
        var builder = RegistryTestData.NewStandardBuilder();
        builder.RegisterTransport(RegistryTestData.Registration(
            "zeta",
            RegistryTestData.TypeA,
            new[] { "alpha" }));
        builder.RegisterTransport(RegistryTestData.Registration(
            "alpha",
            RegistryTestData.TypeB,
            Array.Empty<string>()));

        Assert.That(
            builder.Build().FindCandidates("alpha").Select(c => c.Name),
            Is.EqualTo(new[] { "alpha", "zeta" }));
    }

    [Test]
    public void FindCandidates_ForAliasOnlyScheme_ReturnsAliasesSortedByName()
    {
        var builder = RegistryTestData.NewStandardBuilder();
        builder.RegisterTransport(RegistryTestData.Registration(
            "beta",
            RegistryTestData.TypeB,
            new[] { "shared" }));
        builder.RegisterTransport(RegistryTestData.Registration(
            "alpha",
            RegistryTestData.TypeA,
            new[] { "shared" }));

        Assert.That(
            builder.Build().FindCandidates("shared").Select(c => c.Name),
            Is.EqualTo(new[] { "alpha", "beta" }));
    }

    [Test]
    public void FindCandidates_ForAliasOnlyScheme_ReturnsEveryCandidateWithoutTypeFiltering()
    {
        var builder = RegistryTestData.NewStandardBuilder();
        builder.RegisterTransport(RegistryTestData.Registration(
            "alpha",
            RegistryTestData.TypeA,
            new[] { "shared" }));
        builder.RegisterTransport(RegistryTestData.Registration(
            "beta",
            RegistryTestData.TypeB,
            new[] { "shared" }));

        var candidates = builder.Build().FindCandidates("shared");

        Assert.That(candidates, Has.Count.EqualTo(2));
        Assert.That(candidates.Select(c => c.Type), Is.EqualTo(new[]
        {
            RegistryTestData.TypeA,
            RegistryTestData.TypeB
        }));
    }

    [Test]
    public void FindCandidates_WithUppercaseScheme_ReturnsEmptyBecauseLookupRequiresNormalization()
    {
        var builder = RegistryTestData.NewStandardBuilder();
        builder.RegisterTransport(RegistryTestData.Registration(
            "alpha",
            RegistryTestData.TypeA,
            new[] { "shared" }));

        Assert.That(builder.Build().FindCandidates("SHARED"), Is.Empty);
    }

    [Test]
    public void FindCandidates_WithUnknownScheme_ReturnsEmpty()
    {
        Assert.That(
            RegistryTestData.NewStandardBuilder().Build().FindCandidates("unknown"),
            Is.Empty);
    }

    [Test]
    public void AllSchemeNames_IsSortedUnionOfCanonicalAndAliasNames()
    {
        var builder = RegistryTestData.NewStandardBuilder();
        builder.RegisterTransport(RegistryTestData.Registration(
            "gamma",
            RegistryTestData.TypeC,
            new[] { "shared" }));
        builder.RegisterTransport(RegistryTestData.Registration(
            "alpha",
            RegistryTestData.TypeA,
            new[] { "shared", "fast" }));
        builder.RegisterTransport(RegistryTestData.Registration(
            "beta",
            RegistryTestData.TypeB,
            Array.Empty<string>()));

        Assert.That(builder.Build().AllSchemeNames, Is.EqualTo(new[]
        {
            "alpha",
            "beta",
            "fast",
            "gamma",
            "shared"
        }));
    }

    [Test]
    public void AllSchemeNames_ContainsAnAliasThatDuplicatesAnotherSchemeNameOnlyOnce()
    {
        var builder = RegistryTestData.NewStandardBuilder();
        builder.RegisterTransport(RegistryTestData.Registration(
            "alpha",
            RegistryTestData.TypeA,
            new[] { "beta" }));
        builder.RegisterTransport(RegistryTestData.Registration(
            "beta",
            RegistryTestData.TypeB,
            Array.Empty<string>()));

        Assert.That(builder.Build().AllSchemeNames, Is.EqualTo(new[] { "alpha", "beta" }));
    }

    [Test]
    public void Types_ExposesTheCatalog()
    {
        var registry = RegistryTestData.NewStandardBuilder().Build();

        Assert.That(registry.Types.TryResolve("typeb", out var info), Is.True);
        Assert.That(info!.CanonicalName, Is.EqualTo("TypeB"));
        Assert.That(registry.Types.AllTypes, Is.EqualTo(new[]
        {
            RegistryTestData.TypeA,
            RegistryTestData.TypeB,
            RegistryTestData.TypeC
        }));
    }

    [Test]
    public void Constructors_ExposeOnlyNormalizedMetadata()
    {
        var builder = RegistryTestData.NewStandardBuilder();
        builder.RegisterTransport(RegistryTestData.Registration(
            "ALPHA",
            RegistryTestData.TypeA,
            new[] { "FAST" },
            RegistryTestData.CreateStringParam("INPUT")));

        var constructor = builder.Build().Constructors[0];

        Assert.That(constructor.Name, Is.EqualTo("alpha"));
        Assert.That(constructor.Params[0].Name, Is.EqualTo("input"));
    }

    [Test]
    public void Build_CopiesAliases_SoLaterSourceMutationDoesNotAffectTheRegistry()
    {
        var aliases = new[] { "shared" };
        var builder = RegistryTestData.NewStandardBuilder();
        builder.RegisterTransport(RegistryTestData.Registration(
            "alpha",
            RegistryTestData.TypeA,
            aliases));

        aliases[0] = "changed";
        var registry = builder.Build();

        Assert.That(registry.FindCandidates("shared"), Has.Count.EqualTo(1));
        Assert.That(registry.FindCandidates("changed"), Is.Empty);
    }
}
