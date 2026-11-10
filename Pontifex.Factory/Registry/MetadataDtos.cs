using System;
using System.Collections.Generic;
using Pontifex;

namespace Pontifex.Factory.Registry;

public enum TransportTypeSetKind
{
    Any,
    Exactly,
    OneOf
}

public sealed record TransportTypeSet(
    TransportTypeSetKind Kind,
    IReadOnlyList<TransportType> Types);

public delegate string? ScalarValidator(string value);

public abstract record Param(
    string Name,
    bool Required,
    bool Positional);

public sealed record StringParam(
    string Name,
    bool Required,
    bool Positional,
    ScalarValidator? Validate) : Param(Name, Required, Positional);

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
