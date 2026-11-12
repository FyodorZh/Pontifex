using System;
using System.Linq;
using Pontifex.TransportFormula;
using Pontifex.TransportFormula.Registry;
using Pontifex.TransportFormula.Registry.Internal;
using Pontifex.Factory.Tests.Registry.Harness;

namespace Pontifex.Factory.Tests.Registry;

[TestFixture]
[Category("Factory.Registry")]
public sealed class TransportTypeCatalogTests
{
    [Test]
    public void RegisterType_WithValidName_ResolvesCaseInsensitively()
    {
        var catalog = RegistryTestData
            .NewBuilder(RegistryTestData.TypeInfo(RegistryTestData.TypeA, "TypeA"))
            .Build()
            .Types;

        foreach (var name in new[] { "TypeA", "typea", "TYPEA", "tYpEa" })
        {
            var resolved = catalog.TryResolve(name, out var info);

            Assert.That(resolved, Is.True, name);
            Assert.That(info, Is.Not.Null);
            Assert.That(info!.Type, Is.EqualTo(RegistryTestData.TypeA));
            Assert.That(info.CanonicalName, Is.EqualTo("TypeA"));
        }
    }

    [Test]
    public void RegisterType_WithMultipleTypes_PreservesRegistrationOrderInTypesAndAllTypes()
    {
        var catalog = RegistryTestData
            .NewBuilder(
                RegistryTestData.TypeCInfo(),
                RegistryTestData.TypeAInfo(),
                RegistryTestData.TypeBInfo())
            .Build()
            .Types;

        Assert.That(catalog.Types.Select(t => t.CanonicalName), Is.EqualTo(new[]
        {
            "TypeC",
            "TypeA",
            "TypeB"
        }));
        Assert.That(catalog.AllTypes, Is.EqualTo(new[]
        {
            RegistryTestData.TypeC,
            RegistryTestData.TypeA,
            RegistryTestData.TypeB
        }));
    }

    [Test]
    public void TryResolve_WithUnknownName_ReturnsFalseAndNoEntry()
    {
        var catalog = RegistryTestData.NewStandardBuilder().Build().Types;

        var resolved = catalog.TryResolve("Unknown", out var info);

        Assert.That(resolved, Is.False);
        Assert.That(info, Is.Null);
    }

    [Test]
    public void TryResolve_WithNullName_ReturnsFalseAndNoEntry()
    {
        var catalog = RegistryTestData.NewStandardBuilder().Build().Types;

        var resolved = catalog.TryResolve(null!, out var info);

        Assert.That(resolved, Is.False);
        Assert.That(info, Is.Null);
    }

    [Test]
    public void TryResolve_WithDifferentCaseAndDifferentSpelling_ReturnsCatalogSpelling()
    {
        var catalog = RegistryTestData
            .NewBuilder(RegistryTestData.TypeInfo(RegistryTestData.TypeA, "Type-A_1"))
            .Build()
            .Types;

        Assert.That(catalog.TryResolve("TYPE-A_1", out var info), Is.True);
        Assert.That(info!.CanonicalName, Is.EqualTo("Type-A_1"));
    }

    [Test]
    public void TryGetCanonicalName_ForKnownType_ReturnsCatalogSpelling()
    {
        var catalog = RegistryTestData
            .NewBuilder(RegistryTestData.TypeInfo(RegistryTestData.TypeB, "CustomTypeB"))
            .Build()
            .Types;

        Assert.That(catalog.TryGetCanonicalName(RegistryTestData.TypeB, out var name), Is.True);
        Assert.That(name, Is.EqualTo("CustomTypeB"));
    }

    [Test]
    public void TryGetCanonicalName_ForUnknownType_ReturnsFalseAndNull()
    {
        var catalog = RegistryTestData
            .NewBuilder(RegistryTestData.TypeAInfo())
            .Build()
            .Types;

        Assert.That(catalog.TryGetCanonicalName(RegistryTestData.TypeB, out var name), Is.False);
        Assert.That(name, Is.Null);
    }

    [Test]
    public void GetCanonicalName_ForKnownType_ReturnsCatalogSpelling()
    {
        var catalog = RegistryTestData
            .NewBuilder(RegistryTestData.TypeInfo(RegistryTestData.TypeC, "CustomTypeC"))
            .Build()
            .Types;

        Assert.That(catalog.GetCanonicalName(RegistryTestData.TypeC), Is.EqualTo("CustomTypeC"));
    }

    [Test]
    public void GetCanonicalName_ForUnknownType_ThrowsInvalidOperationException()
    {
        var catalog = RegistryTestData
            .NewBuilder(RegistryTestData.TypeAInfo())
            .Build()
            .Types;

        Assert.Throws<InvalidOperationException>(
            () => catalog.GetCanonicalName(RegistryTestData.TypeB));
    }

    [Test]
    public void RegisterType_WithDuplicateNameDifferentCase_ThrowsDuplicateTypeName()
    {
        var builder = RegistryTestData.NewBuilder(RegistryTestData.TypeInfo(RegistryTestData.TypeA, "TypeA"));

        RegistryAssertions.ThrowsRegistration(
            () => builder.RegisterType(RegistryTestData.TypeInfo(RegistryTestData.TypeB, "TYPEA")),
            "E_REG_DUPLICATE_TYPE_NAME",
            "transport type name 'typea' is already registered");
    }

    [TestCase("1Type")]
    [TestCase("Type A")]
    [TestCase("Type.A")]
    [TestCase("")]
    [TestCase("_Type")]
    public void RegisterType_WithInvalidName_ThrowsInvalidTypeName(string name)
    {
        var builder = RegistryTestData.NewBuilder();

        RegistryAssertions.ThrowsRegistration(
            () => builder.RegisterType(RegistryTestData.TypeInfo(RegistryTestData.TypeA, name)),
            "E_REG_INVALID_TYPE_NAME",
            "invalid transport type name '" + name + "'");
    }

    [TestCase("Type_1")]
    [TestCase("Type-1")]
    [TestCase("t1")]
    [TestCase("T")]
    public void RegisterType_WithIdentifierVariants_AcceptsThem(string name)
    {
        var builder = RegistryTestData.NewBuilder();

        builder.RegisterType(RegistryTestData.TypeInfo(RegistryTestData.TypeA, name));
        var catalog = builder.Build().Types;

        Assert.That(catalog.TryResolve(name, out var info), Is.True);
        Assert.That(info!.CanonicalName, Is.EqualTo(name));
    }

    [Test]
    public void RegisterType_AfterBuild_WithInvalidName_ThrowsBuilderSealed()
    {
        var builder = RegistryTestData.NewBuilder(RegistryTestData.TypeAInfo());
        builder.Build();

        RegistryAssertions.ThrowsRegistration(
            () => builder.RegisterType(RegistryTestData.TypeInfo(RegistryTestData.TypeB, "1Type")),
            "E_REG_BUILDER_SEALED",
            "registry builder has already been built");
    }

    [Test]
    public void RegisterType_AfterBuild_ThrowsBuilderSealed()
    {
        var builder = RegistryTestData.NewBuilder(RegistryTestData.TypeAInfo());
        builder.Build();

        RegistryAssertions.ThrowsRegistration(
            () => builder.RegisterType(RegistryTestData.TypeBInfo()),
            "E_REG_BUILDER_SEALED",
            "registry builder has already been built");
    }
}
