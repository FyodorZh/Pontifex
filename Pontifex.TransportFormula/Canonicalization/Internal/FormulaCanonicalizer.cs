using System;
using System.Collections.Generic;
using System.Text;
using Pontifex.TransportFormula.Registry;
using Pontifex.TransportFormula.Semantics;

namespace Pontifex.TransportFormula.Canonicalization.Internal
{
    // Canonical printer following parsing.md §10. The resolved tree is validated
    // first; a correct resolver never produces a tree that fails validation. Output
    // has no prefix, no trailing whitespace, and no newline. Cast targets use the
    // catalog's canonical spelling. Names come from normalized metadata and are
    // never repaired here.
    internal sealed class FormulaCanonicalizer : ICanonicalizer
    {
        private readonly ITransportTypeCatalog _catalog;
        private readonly PrinterInvariantValidator _validator;

        internal FormulaCanonicalizer(ITransportTypeCatalog catalog)
        {
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            _validator = new PrinterInvariantValidator(_catalog);
        }

        public string Canonicalize(ResolvedFormula formula)
        {
            if (formula is null)
            {
                throw new ArgumentNullException(nameof(formula));
            }

            _validator.Validate(formula);

            var builder = new StringBuilder();
            PrintExpression(formula.Root, builder);
            return builder.ToString();
        }

        private void PrintExpression(ResolvedExpr expression, StringBuilder builder)
        {
            switch (expression)
            {
                case ResolvedCast cast:
                    if (cast.Inner.Type == cast.Target)
                    {
                        throw CanonicalizationErrorFactory.InconsistentTree(
                            CanonicalizationErrorFactory.IdentityCastNode);
                    }

                    if (!_catalog.TryGetCanonicalName(cast.Target, out var canonicalTarget))
                    {
                        throw CanonicalizationErrorFactory.InvalidCastTarget(cast.Target);
                    }

                    PrintTransport(cast.Inner, builder);
                    builder.Append(" as ");
                    builder.Append(canonicalTarget);
                    return;

                case ResolvedTransport transport:
                    PrintTransport(transport, builder);
                    return;

                default:
                    throw CanonicalizationErrorFactory.InconsistentTree(
                        CanonicalizationErrorFactory.UnspecifiedInvariantViolation);
            }
        }

        private void PrintTransport(ResolvedTransport transport, StringBuilder builder)
        {
            builder.Append(transport.Constructor.Name);
            builder.Append('(');

            var entries = PositionalFormatter.Arrange(transport.Constructor, transport.Provided);
            for (var i = 0; i < entries.Count; i++)
            {
                if (i > 0)
                {
                    builder.Append(", ");
                }

                var entry = entries[i];
                if (entry.IsNamed)
                {
                    builder.Append(entry.Binding.Param.Name);
                    builder.Append('=');
                }

                PrintValue(entry.Binding.Value, builder);
            }

            builder.Append(')');
        }

        private void PrintValue(ResolvedValue value, StringBuilder builder)
        {
            switch (value)
            {
                case ScalarValue scalar:
                    builder.Append(ScalarFormatter.Format(scalar.Text));
                    return;

                case TransportValue transportValue:
                    PrintExpression(transportValue.Expr, builder);
                    return;

                case TransportListValue listValue:
                    builder.Append('[');
                    for (var i = 0; i < listValue.Elements.Count; i++)
                    {
                        if (i > 0)
                        {
                            builder.Append(", ");
                        }

                        PrintExpression(listValue.Elements[i], builder);
                    }

                    builder.Append(']');
                    return;

                case StructValue structValue:
                    builder.Append('{');
                    PrintStructFields(structValue.Fields, builder);
                    builder.Append('}');
                    return;

                default:
                    throw CanonicalizationErrorFactory.InconsistentTree(
                        CanonicalizationErrorFactory.UnspecifiedInvariantViolation);
            }
        }

        private static void PrintStructFields(
            IReadOnlyList<StructFieldBinding> fields,
            StringBuilder builder)
        {
            for (var i = 0; i < fields.Count; i++)
            {
                if (i > 0)
                {
                    builder.Append(", ");
                }

                var binding = fields[i];
                builder.Append(binding.Field.Name);
                builder.Append('=');

                switch (binding.Value)
                {
                    case StringFieldValue stringValue:
                        builder.Append(ScalarFormatter.Format(stringValue.Text));
                        break;

                    case NestedStructFieldValue nestedValue:
                        builder.Append('{');
                        PrintStructFields(nestedValue.Fields, builder);
                        builder.Append('}');
                        break;

                    default:
                        throw CanonicalizationErrorFactory.InconsistentTree(
                            CanonicalizationErrorFactory.UnspecifiedInvariantViolation);
                }
            }
        }
    }
}
