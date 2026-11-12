using System;
using Pontifex.TransportFormula.Registry;
using Pontifex.TransportFormula.Semantics.Internal;
using Pontifex.TransportFormula.Tests.Semantics.Harness;

namespace Pontifex.TransportFormula.Tests.Semantics;

[TestFixture]
[Category("TransportFormula.Semantics")]
public sealed class ConstructorSelectorTests
{
    [Test]
    public void Select_WithUppercaseScheme_NormalizesBeforeLookup()
    {
        var selector = new ConstructorSelector(SemanticsTestData.StandardRegistry());

        var selected = SemanticsAssertions.Select(selector, "ALPHA", SemanticsTestData.AnySet());

        Assert.That(selected.Name, Is.EqualTo("alpha"));
        Assert.That(selected.Type, Is.EqualTo(SemanticsTestData.TypeA));
    }

    [Test]
    public void Select_ForCanonicalSchemeWithAliasUnderAny_PrefersCanonical()
    {
        var registry = SemanticsTestData.StandardRegistry();
        var selector = new ConstructorSelector(registry);

        // The alias group for `alpha` contains canonical alpha (TypeA) and beta
        // (TypeB) through the alias contract from parsing.md §14.
        Assert.That(registry.FindCandidates("alpha"), Has.Count.EqualTo(2));

        var selected = SemanticsAssertions.Select(selector, "alpha", SemanticsTestData.AnySet());

        Assert.That(selected.Name, Is.EqualTo("alpha"));
    }

    [Test]
    public void Select_ForCanonicalSchemeWithMatchingExpectedType_PrefersCanonical()
    {
        var selector = new ConstructorSelector(SemanticsTestData.StandardRegistry());

        var selected = SemanticsAssertions.Select(
            selector,
            "alpha",
            SemanticsTestData.Exactly(SemanticsTestData.TypeA));

        Assert.That(selected.Name, Is.EqualTo("alpha"));
    }

    [Test]
    public void Select_ForCanonicalSchemeWhoseExpectedSetExcludesIt_FallsBackToAlias()
    {
        var selector = new ConstructorSelector(SemanticsTestData.StandardRegistry());

        var selected = SemanticsAssertions.Select(
            selector,
            "alpha",
            SemanticsTestData.Exactly(SemanticsTestData.TypeB));

        Assert.That(selected.Name, Is.EqualTo("beta"));
        Assert.That(selected.Type, Is.EqualTo(SemanticsTestData.TypeB));
    }

    [Test]
    public void Select_ForOneOfExpectedSetWhereCanonicalSurvives_PrefersCanonicalAfterFiltering()
    {
        var selector = new ConstructorSelector(SemanticsTestData.StandardRegistry());

        var selected = SemanticsAssertions.Select(
            selector,
            "alpha",
            SemanticsTestData.OneOf(SemanticsTestData.TypeA, SemanticsTestData.TypeB));

        Assert.That(selected.Name, Is.EqualTo("alpha"));
    }

    [Test]
    public void Select_ForSingleAliasCandidateUnderAny_ReturnsAliasConstructor()
    {
        var selector = new ConstructorSelector(SemanticsTestData.StandardRegistry());

        var selected = SemanticsAssertions.Select(
            selector,
            "onlybeta",
            SemanticsTestData.AnySet());

        Assert.That(selected.Name, Is.EqualTo("beta"));
        Assert.That(selected.Type, Is.EqualTo(SemanticsTestData.TypeB));
    }

    [Test]
    public void Select_ForAliasOnlySchemeWithAny_ReturnsAmbiguousWithPostFilterCandidates()
    {
        var selector = new ConstructorSelector(SemanticsTestData.StandardRegistry());

        SemanticsAssertions.SelectError(
            selector,
            "shared",
            SemanticsTestData.AnySet(),
            "E_SEM_AMBIGUOUS_SCHEME",
            "scheme 'shared' is ambiguous; candidates: beta (TypeB), gamma (TypeC)");
    }

    [Test]
    public void Select_ForAliasOnlySchemeWithExpectedType_SelectsSurvivingCandidate()
    {
        var selector = new ConstructorSelector(SemanticsTestData.StandardRegistry());

        var selected = SemanticsAssertions.Select(
            selector,
            "shared",
            SemanticsTestData.Exactly(SemanticsTestData.TypeC));

        Assert.That(selected.Name, Is.EqualTo("gamma"));
    }

    [Test]
    public void Select_WhenExpectedFilterLeavesMultipleCandidates_ReportsPostFilterCandidates()
    {
        var selector = new ConstructorSelector(SemanticsTestData.StandardRegistry());

        SemanticsAssertions.SelectError(
            selector,
            "multi",
            SemanticsTestData.OneOf(SemanticsTestData.TypeA, SemanticsTestData.TypeB),
            "E_SEM_AMBIGUOUS_SCHEME",
            "scheme 'multi' is ambiguous; candidates: alpha (TypeA), beta (TypeB)");
    }

    [Test]
    public void Select_WhenNoCandidateSurvivesExpectedFilter_ReportsPreFilterAvailableVariants()
    {
        var selector = new ConstructorSelector(SemanticsTestData.StandardRegistry());

        SemanticsAssertions.SelectError(
            selector,
            "alpha",
            SemanticsTestData.Exactly(SemanticsTestData.TypeC),
            "E_SEM_NO_VARIANT_FOR_EXPECTED_TYPE",
            "scheme 'alpha' has no variant for expected transport type TypeC; "
            + "available: alpha (TypeA), beta (TypeB)");
    }

    [Test]
    public void Select_WhenExpectedSetHasSeveralTypes_FormatsTypeSetSorted()
    {
        var selector = new ConstructorSelector(SemanticsTestData.StandardRegistry());

        SemanticsAssertions.SelectError(
            selector,
            "outer",
            SemanticsTestData.OneOf(SemanticsTestData.TypeB, SemanticsTestData.TypeA),
            "E_SEM_NO_VARIANT_FOR_EXPECTED_TYPE",
            "scheme 'outer' has no variant for expected transport type TypeA or TypeB; "
            + "available: outer (TypeC)");
    }

    [Test]
    public void Select_ForAliasOfTheSameTypeAsTheCanonicalConstructor_PrefersCanonical()
    {
        // Registration rule 9 permits an alias to target a constructor whose
        // type equals the canonical constructor's type; canonical preference
        // resolves the overlap deterministically.
        var builder = SemanticsTestData.NewBuilder();
        SemanticsTestData.Register(
            builder,
            "alpha",
            SemanticsTestData.TypeA,
            Array.Empty<string>(),
            SemanticsTestData.CreateTransportParam(
                "input",
                SemanticsTestData.AnySet(),
                required: true,
                positional: true));
        SemanticsTestData.Register(
            builder,
            "alphaAlias",
            SemanticsTestData.TypeA,
            new[] { "alpha" },
            SemanticsTestData.CreateTransportParam(
                "input",
                SemanticsTestData.AnySet(),
                required: true,
                positional: true));

        var registry = builder.Build();
        var selector = new ConstructorSelector(registry);

        Assert.That(registry.FindCandidates("alpha"), Has.Count.EqualTo(2));

        var selected = SemanticsAssertions.Select(selector, "alpha", SemanticsTestData.AnySet());

        Assert.That(selected.Name, Is.EqualTo("alpha"));
    }
}
