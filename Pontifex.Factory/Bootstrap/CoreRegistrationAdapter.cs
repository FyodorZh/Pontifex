using System;
using System.Collections.Generic;
using Pontifex.TransportFormula.Internal;
using Core = Pontifex.Factory.Registration;
using Formula = Pontifex.TransportFormula.Registry;

namespace Pontifex.Factory.Bootstrap
{
    // Adapts the transport-facing registration DTOs to the formula registry DTOs.
    // A core registration is the single source for both the formula metadata and
    // the runtime constructor entry. The adapter normalizes names only; the
    // formula registry owns all registration validation. See parsing.md §6.9.
    internal static class CoreRegistrationAdapter
    {
        public static Formula.TransportRegistration Adapt(
            Core.ITransportRegistration registration)
        {
            if (registration is null)
            {
                throw new ArgumentNullException(nameof(registration));
            }

            var constructor = new Formula.TransportConstructorInfo(
                NameNormalizer.Normalize(registration.Name),
                registration.Type,
                AdaptParameters(registration.Parameters),
                // The host registered aliases explicitly, so there is no hint
                // list for the resolver to suggest.
                Array.Empty<string>());

            return new Formula.TransportRegistration(
                constructor,
                AdaptAliases(registration.Aliases));
        }

        private static IReadOnlyList<Formula.Param> AdaptParameters(
            IReadOnlyList<Core.TransportParameter>? parameters)
        {
            if (parameters is null || parameters.Count == 0)
            {
                return Array.Empty<Formula.Param>();
            }

            var result = new List<Formula.Param>(parameters.Count);
            for (var i = 0; i < parameters.Count; i++)
            {
                result.Add(AdaptParameter(parameters[i]));
            }

            return result.AsReadOnly();
        }

        private static Formula.Param AdaptParameter(Core.TransportParameter parameter)
        {
            if (parameter is null)
            {
                throw new ArgumentException(
                    "A transport parameter cannot be null.",
                    "registration");
            }

            var name = NameNormalizer.Normalize(parameter.Name);
            switch (parameter)
            {
                case Core.StringParameter stringParameter:
                    return new Formula.StringParam(
                        name,
                        stringParameter.Required,
                        stringParameter.Positional,
                        AdaptValidator(stringParameter.Validate));
                case Core.IntegerParameter integerParameter:
                    return new Formula.IntegerParam(
                        name,
                        integerParameter.Required,
                        integerParameter.Positional,
                        AdaptIntegerType(integerParameter.Type));
                case Core.DecimalParameter decimalParameter:
                    return new Formula.DecimalParam(
                        name,
                        decimalParameter.Required,
                        decimalParameter.Positional,
                        AdaptDecimalType(decimalParameter.Type));
                case Core.NestedTransportParameter transportParameter:
                    return new Formula.TransportParam(
                        name,
                        transportParameter.Required,
                        transportParameter.Positional,
                        AdaptTypeSet(transportParameter.Expected));
                case Core.TransportListParameter listParameter:
                    return new Formula.TransportListParam(
                        name,
                        listParameter.Required,
                        listParameter.Positional,
                        AdaptTypeSet(listParameter.Expected),
                        listParameter.Homogeneous,
                        listParameter.Min,
                        listParameter.Max);
                case Core.StructParameter structParameter:
                    return new Formula.StructParam(
                        name,
                        structParameter.Required,
                        structParameter.Positional,
                        AdaptSchema(structParameter.Schema));
                default:
                    throw new ArgumentException(
                        "Unsupported transport parameter kind: "
                        + parameter.GetType().FullName,
                        "registration");
            }
        }

        private static Formula.TransportTypeSet AdaptTypeSet(Core.TransportTypeSet set)
        {
            switch (set.Kind)
            {
                case Core.TransportTypeSetKind.Any:
                    return new Formula.TransportTypeSet(
                        Formula.TransportTypeSetKind.Any,
                        Array.Empty<TransportType>());
                case Core.TransportTypeSetKind.Exactly:
                case Core.TransportTypeSetKind.OneOf:
                    if (set.Types.Count == 0)
                    {
                        // A core OneOf set cannot be empty by construction, but a
                        // malformed implementation must not reach the formula
                        // registry as an empty set.
                        throw new ArgumentException(
                            "A transport type set must contain at least one type.",
                            "registration");
                    }

                    var types = new TransportType[set.Types.Count];
                    for (var i = 0; i < types.Length; i++)
                    {
                        types[i] = set.Types[i];
                    }

                    return new Formula.TransportTypeSet(
                        set.Kind == Core.TransportTypeSetKind.Exactly
                            ? Formula.TransportTypeSetKind.Exactly
                            : Formula.TransportTypeSetKind.OneOf,
                        types);
                default:
                    throw new ArgumentException(
                        "Unsupported transport type set kind: " + set.Kind,
                        "registration");
            }
        }

        private static Formula.StructSchema AdaptSchema(Core.StructSchema schema)
        {
            if (schema is null)
            {
                throw new ArgumentException(
                    "A struct schema cannot be null.",
                    "registration");
            }

            var fields = new List<Formula.StructField>(schema.Fields.Count);
            for (var i = 0; i < schema.Fields.Count; i++)
            {
                fields.Add(AdaptField(schema.Fields[i]));
            }

            return new Formula.StructSchema(fields.AsReadOnly());
        }

        private static Formula.StructField AdaptField(Core.StructField field)
        {
            if (field is null)
            {
                throw new ArgumentException(
                    "A struct field cannot be null.",
                    "registration");
            }

            var name = NameNormalizer.Normalize(field.Name);
            switch (field)
            {
                case Core.StringField stringField:
                    return new Formula.StringField(
                        name,
                        stringField.Required,
                        AdaptValidator(stringField.Validate));
                case Core.IntegerField integerField:
                    return new Formula.IntegerField(
                        name,
                        integerField.Required,
                        AdaptIntegerType(integerField.Type));
                case Core.DecimalField decimalField:
                    return new Formula.DecimalField(
                        name,
                        decimalField.Required,
                        AdaptDecimalType(decimalField.Type));
                case Core.ArrayField arrayField:
                    return new Formula.ArrayField(
                        name,
                        arrayField.Required,
                        AdaptSingleType(arrayField.ElementType),
                        arrayField.Min,
                        arrayField.Max);
                case Core.NestedStructField nestedField:
                    return new Formula.NestedStructField(
                        name,
                        nestedField.Required,
                        AdaptSchema(nestedField.Schema));
                default:
                    throw new ArgumentException(
                        "Unsupported struct field kind: " + field.GetType().FullName,
                        "registration");
            }
        }

        private static IReadOnlyList<string> AdaptAliases(
            IReadOnlyList<string>? aliases)
        {
            if (aliases is null || aliases.Count == 0)
            {
                return Array.Empty<string>();
            }

            var result = new string[aliases.Count];
            for (var i = 0; i < result.Length; i++)
            {
                var alias = aliases[i];
                if (alias is null)
                {
                    throw new ArgumentException(
                        "A transport alias cannot be null.",
                        "registration");
                }

                result[i] = NameNormalizer.Normalize(alias);
            }

            return result;
        }

        private static Formula.ScalarValidator? AdaptValidator(
            Core.ScalarValidator? validate)
        {
            return validate is null
                ? null
                : new Formula.ScalarValidator(value => validate(value));
        }

        private static Formula.IntegerType AdaptIntegerType(Core.IntegerType type)
        {
            if (type is null)
            {
                throw new ArgumentException(
                    "An integer type cannot be null.",
                    "registration");
            }

            return new Formula.IntegerType(type.Min, type.Max);
        }

        private static Formula.DecimalType AdaptDecimalType(Core.DecimalType type)
        {
            if (type is null)
            {
                throw new ArgumentException(
                    "A decimal type cannot be null.",
                    "registration");
            }

            return new Formula.DecimalType(type.Min, type.Max);
        }

        private static Formula.SingleType AdaptSingleType(Core.SingleType type)
        {
            if (type is null)
            {
                throw new ArgumentException(
                    "An array element type cannot be null.",
                    "registration");
            }

            switch (type)
            {
                case Core.StringType stringType:
                    return new Formula.StringType(AdaptValidator(stringType.Validate));
                case Core.IntegerType integerType:
                    return AdaptIntegerType(integerType);
                case Core.DecimalType decimalType:
                    return AdaptDecimalType(decimalType);
                default:
                    throw new ArgumentException(
                        "Unsupported array element type: " + type.GetType().FullName,
                        "registration");
            }
        }
    }
}
