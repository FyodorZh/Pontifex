using System;
using Pontifex;
using Pontifex.TransportFormula.Registry;
using Pontifex.TransportFormula.Registry.Internal;

namespace Pontifex.Factory.Tests.Registry.Harness;

// Hand-written construction helpers for registry metadata. No mocking library
// is used anywhere in this suite.
internal static class RegistryTestData
{
    public static readonly TransportType TypeA = TransportType.RawReliableAck;
    public static readonly TransportType TypeB = TransportType.RawReliableNoAck;
    public static readonly TransportType TypeC = TransportType.RawUnreliableAck;
    public static readonly TransportType UnregisteredType = TransportType.RawUnreliableNoAck;

    public static TransportTypeInfo TypeInfo(TransportType type, string canonicalName)
    {
        return new TransportTypeInfo(type, canonicalName);
    }

    public static TransportTypeInfo TypeAInfo()
    {
        return TypeInfo(TypeA, "TypeA");
    }

    public static TransportTypeInfo TypeBInfo()
    {
        return TypeInfo(TypeB, "TypeB");
    }

    public static TransportTypeInfo TypeCInfo()
    {
        return TypeInfo(TypeC, "TypeC");
    }

    public static TransportRegistryBuilder NewBuilder(params TransportTypeInfo[] types)
    {
        var builder = new TransportRegistryBuilder();
        for (var i = 0; i < types.Length; i++)
        {
            builder.RegisterType(types[i]);
        }

        return builder;
    }

    public static TransportRegistryBuilder NewStandardBuilder()
    {
        return NewBuilder(TypeAInfo(), TypeBInfo(), TypeCInfo());
    }

    public static TransportConstructorInfo Constructor(
        string name,
        TransportType type,
        params Param[] parameters)
    {
        return new TransportConstructorInfo(
            name,
            type,
            parameters,
            Array.Empty<string>());
    }

    public static TransportConstructorInfo ConstructorWithSuggestedAliases(
        string name,
        TransportType type,
        string[] suggestedAliases,
        params Param[] parameters)
    {
        return new TransportConstructorInfo(name, type, parameters, suggestedAliases);
    }

    public static TransportRegistration Registration(
        TransportConstructorInfo constructor,
        params string[] aliases)
    {
        return new TransportRegistration(constructor, aliases);
    }

    public static TransportRegistration Registration(
        string name,
        TransportType type,
        string[] aliases,
        params Param[] parameters)
    {
        return new TransportRegistration(Constructor(name, type, parameters), aliases);
    }

    public static Param CreateStringParam(
        string name,
        bool required = false,
        bool positional = false,
        ScalarValidator? validate = null)
    {
        return new StringParam(name, required, positional, validate);
    }

    public static Param CreateTransportParam(
        string name,
        TransportTypeSet expected,
        bool required = false,
        bool positional = false)
    {
        return new TransportParam(name, required, positional, expected);
    }

    public static Param CreateListParam(
        string name,
        TransportTypeSet expected,
        int min = 0,
        int? max = null,
        bool homogeneous = false,
        bool required = false,
        bool positional = false)
    {
        return new TransportListParam(
            name,
            required,
            positional,
            expected,
            homogeneous,
            min,
            max);
    }

    public static Param CreateStructParam(
        string name,
        StructSchema schema,
        bool required = false,
        bool positional = false)
    {
        return new StructParam(name, required, positional, schema);
    }

    public static StructSchema Schema(params StructField[] fields)
    {
        return new StructSchema(fields);
    }

    public static StructField CreateStringField(
        string name,
        bool required = false,
        ScalarValidator? validate = null)
    {
        return new StringField(name, required, validate);
    }

    public static StructField CreateNestedField(
        string name,
        StructSchema schema,
        bool required = false)
    {
        return new NestedStructField(name, required, schema);
    }

    public static TransportTypeSet AnySet()
    {
        return new TransportTypeSet(TransportTypeSetKind.Any, Array.Empty<TransportType>());
    }

    public static TransportTypeSet Exactly(TransportType type)
    {
        return new TransportTypeSet(TransportTypeSetKind.Exactly, new[] { type });
    }

    public static TransportTypeSet OneOf(params TransportType[] types)
    {
        return new TransportTypeSet(TransportTypeSetKind.OneOf, types);
    }
}
