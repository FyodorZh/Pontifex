using System;
using System.Collections.Generic;
using Pontifex;

namespace Pontifex.TransportFormula.Registry
{
    public enum TransportTypeSetKind
    {
        Any,
        Exactly,
        OneOf
    }

    public sealed record TransportTypeSet(
        TransportTypeSetKind Kind,
        IReadOnlyList<TransportType> Types)
    {
        public bool IsAny => Kind == TransportTypeSetKind.Any;

        public bool Contains(TransportType type)
        {
            if (IsAny)
            {
                return true;
            }

            for (var i = 0; i < Types.Count; i++)
            {
                if (Types[i] == type)
                {
                    return true;
                }
            }

            return false;
        }
    }

    public delegate string? ScalarValidator(string value);

    /// <summary>
    /// The inclusive range of an integer parameter or field.
    /// </summary>
    public sealed record IntegerType(
        long Min,
        long Max);

    /// <summary>
    /// The inclusive range of a decimal parameter or field.
    /// </summary>
    public sealed record DecimalType(
        decimal Min,
        decimal Max);

    public abstract record Param(
        string Name,
        bool Required,
        bool Positional);

    public sealed record StringParam(
        string Name,
        bool Required,
        bool Positional,
        ScalarValidator? Validate) : Param(Name, Required, Positional);

    public sealed record IntegerParam(
        string Name,
        bool Required,
        bool Positional,
        IntegerType Type) : Param(Name, Required, Positional);

    public sealed record DecimalParam(
        string Name,
        bool Required,
        bool Positional,
        DecimalType Type) : Param(Name, Required, Positional);

    public sealed record TransportParam(
        string Name,
        bool Required,
        bool Positional,
        TransportTypeSet Expected) : Param(Name, Required, Positional);

    public sealed record TransportListParam(
        string Name,
        bool Required,
        bool Positional,
        TransportTypeSet Expected,
        bool Homogeneous,
        int Min,
        int? Max) : Param(Name, Required, Positional);

    public sealed record StructParam(
        string Name,
        bool Required,
        bool Positional,
        StructSchema Schema) : Param(Name, Required, Positional);

    public sealed record StructSchema(
        IReadOnlyList<StructField> Fields);

    public abstract record StructField(
        string Name,
        bool Required);

    public sealed record StringField(
        string Name,
        bool Required,
        ScalarValidator? Validate) : StructField(Name, Required);

    public sealed record IntegerField(
        string Name,
        bool Required,
        IntegerType Type) : StructField(Name, Required);

    public sealed record DecimalField(
        string Name,
        bool Required,
        DecimalType Type) : StructField(Name, Required);

    public sealed record NestedStructField(
        string Name,
        bool Required,
        StructSchema Schema) : StructField(Name, Required);

    public sealed record TransportTypeInfo(
        TransportType Type,
        string CanonicalName);

    public sealed record TransportConstructorInfo(
        string Name,
        TransportType Type,
        IReadOnlyList<Param> Params,
        IReadOnlyList<string> SuggestedAliases);

    public sealed record TransportRegistration(
        TransportConstructorInfo Constructor,
        IReadOnlyList<string> Aliases);
}
