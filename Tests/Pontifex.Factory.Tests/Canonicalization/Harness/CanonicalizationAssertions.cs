using System;
using Pontifex.TransportFormula;
using Pontifex.TransportFormula.Canonicalization.Internal;
using Pontifex.TransportFormula.Semantics;

namespace Pontifex.Factory.Tests.Canonicalization.Harness;

// Centralised assertions for the canonicalization suite. Printer failures always
// assert phase Printing, no span, the exact code, and the exact message.
// Structural tree equality follows parsing.md §11.1 and ignores spans, aliases,
// source case, quoting, and positional versus named form.
internal static class CanonicalizationAssertions
{
    public static string CanonicalizeSucceeds(
        FormulaCanonicalizer canonicalizer,
        ResolvedFormula formula)
    {
        var result = canonicalizer.Canonicalize(formula);

        Assert.That(result, Is.Not.Null);
        return result;
    }

    public static PrinterException CanonicalizeError(
        FormulaCanonicalizer canonicalizer,
        ResolvedFormula formula,
        string code,
        string message)
    {
        var exception = Assert.Throws<PrinterException>(
            () => canonicalizer.Canonicalize(formula));

        Assert.That(exception, Is.Not.Null);
        AssertError(exception!.Error, code, message);
        return exception;
    }

    public static void AssertError(FactoryError? error, string code, string message)
    {
        Assert.That(error, Is.Not.Null);
        Assert.That(error!.Code, Is.EqualTo(code), Describe(error));
        Assert.That(error.Message, Is.EqualTo(message));
        Assert.That(error.Phase, Is.EqualTo(FactoryPhase.Printing));
        Assert.That(error.Span, Is.Null);
    }

    public static void AssertNoRawNewlines(string text)
    {
        Assert.That(text, Does.Not.Contain("\r"), "canonical output must not contain CR");
        Assert.That(text, Does.Not.Contain("\n"), "canonical output must not contain LF");
    }

    public static void AssertTreesEqual(ResolvedFormula expected, ResolvedFormula actual)
    {
        Assert.That(expected, Is.Not.Null);
        Assert.That(actual, Is.Not.Null);
        AssertExpression(expected.Root, actual.Root, "root");
    }

    private static void AssertExpression(ResolvedExpr expected, ResolvedExpr actual, string path)
    {
        switch (expected)
        {
            case ResolvedCast expectedCast:
            {
                Assert.That(actual, Is.InstanceOf<ResolvedCast>(), path + ": expected a cast node");
                var actualCast = (ResolvedCast)actual;
                Assert.That(actualCast.Target, Is.EqualTo(expectedCast.Target), path + ".target");
                AssertTransport(expectedCast.Inner, actualCast.Inner, path + ".inner");
                return;
            }

            case ResolvedTransport expectedTransport:
            {
                Assert.That(
                    actual,
                    Is.InstanceOf<ResolvedTransport>(),
                    path + ": expected a transport node");
                AssertTransport(expectedTransport, (ResolvedTransport)actual, path);
                return;
            }

            default:
                Assert.Fail(path + ": unsupported expected expression kind");
                return;
        }
    }

    private static void AssertTransport(
        ResolvedTransport expected,
        ResolvedTransport actual,
        string path)
    {
        Assert.That(
            actual.Constructor.Name,
            Is.EqualTo(expected.Constructor.Name),
            path + ".constructor.name");
        Assert.That(actual.Constructor.Type, Is.EqualTo(expected.Constructor.Type), path + ".constructor.type");
        Assert.That(actual.Type, Is.EqualTo(expected.Type), path + ".type");
        Assert.That(actual.Provided.Count, Is.EqualTo(expected.Provided.Count), path + ".provided.count");

        for (var i = 0; i < expected.Provided.Count; i++)
        {
            var expectedBinding = expected.Provided[i];
            var actualBinding = actual.Provided[i];
            var bindingPath = path + ".provided[" + i + "]";

            Assert.That(
                actualBinding.Param.Name,
                Is.EqualTo(expectedBinding.Param.Name),
                bindingPath + ".param.name");
            Assert.That(
                actualBinding.Param.GetType(),
                Is.EqualTo(expectedBinding.Param.GetType()),
                bindingPath + ".param.kind");
            AssertValue(expectedBinding.Value, actualBinding.Value, bindingPath + ".value");
        }
    }

    private static void AssertValue(ResolvedValue expected, ResolvedValue actual, string path)
    {
        switch (expected)
        {
            case StringValue expectedString:
            {
                Assert.That(actual, Is.InstanceOf<StringValue>(), path + ": expected a string");
                Assert.That(
                    ((StringValue)actual).Text,
                    Is.EqualTo(expectedString.Text),
                    path + ".text");
                return;
            }

            case ResolvedExpr expectedExpression:
            {
                Assert.That(
                    actual,
                    Is.InstanceOf<ResolvedExpr>(),
                    path + ": expected a transport expression value");
                AssertExpression(
                    expectedExpression,
                    (ResolvedExpr)actual,
                    path + ".expr");
                return;
            }

            case TransportListValue expectedList:
            {
                Assert.That(
                    actual,
                    Is.InstanceOf<TransportListValue>(),
                    path + ": expected a transport list");
                var actualList = (TransportListValue)actual;
                Assert.That(
                    actualList.Elements.Count,
                    Is.EqualTo(expectedList.Elements.Count),
                    path + ".count");

                for (var i = 0; i < expectedList.Elements.Count; i++)
                {
                    AssertExpression(
                        expectedList.Elements[i],
                        actualList.Elements[i],
                        path + "[" + i + "]");
                }

                return;
            }

            case StructValue expectedStruct:
            {
                Assert.That(actual, Is.InstanceOf<StructValue>(), path + ": expected a struct");
                AssertStructFields(expectedStruct.Fields, ((StructValue)actual).Fields, path);
                return;
            }

            default:
                Assert.Fail(path + ": unsupported expected value kind");
                return;
        }
    }

    private static void AssertStructFields(
        System.Collections.Generic.IReadOnlyList<StructFieldBinding> expected,
        System.Collections.Generic.IReadOnlyList<StructFieldBinding> actual,
        string path)
    {
        Assert.That(actual.Count, Is.EqualTo(expected.Count), path + ".field.count");

        for (var i = 0; i < expected.Count; i++)
        {
            var expectedField = expected[i];
            var actualField = actual[i];
            var fieldPath = path + ".field[" + i + "]";

            Assert.That(
                actualField.Field.Name,
                Is.EqualTo(expectedField.Field.Name),
                fieldPath + ".name");
            Assert.That(
                actualField.Field.GetType(),
                Is.EqualTo(expectedField.Field.GetType()),
                fieldPath + ".kind");

            switch (expectedField.Value)
            {
                case StringValue expectedString:
                    Assert.That(
                        actualField.Value,
                        Is.InstanceOf<StringValue>(),
                        fieldPath + ": expected a string field");
                    Assert.That(
                        ((StringValue)actualField.Value).Text,
                        Is.EqualTo(expectedString.Text),
                        fieldPath + ".text");
                    break;

                case StructValue expectedNested:
                    Assert.That(
                        actualField.Value,
                        Is.InstanceOf<StructValue>(),
                        fieldPath + ": expected a nested struct field");
                    AssertStructFields(
                        expectedNested.Fields,
                        ((StructValue)actualField.Value).Fields,
                        fieldPath);
                    break;

                default:
                    Assert.Fail(fieldPath + ": unsupported expected struct field value");
                    break;
            }
        }
    }

    private static string Describe(FactoryError? error)
    {
        return error is null ? "expected a printer error, but printing succeeded" : error.ToString();
    }
}
