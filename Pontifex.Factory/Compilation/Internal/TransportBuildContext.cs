using System;
using System.Collections.Generic;
using Actuarius.Memory;
using Pontifex.Factory.Registration;
using Pontifex.TransportFormula;
using Pontifex.TransportFormula.Internal;
using Pontifex.TransportFormula.Semantics;
using Scriba;

namespace Pontifex.Factory.Compilation.Internal
{
    // Maps the resolved values of one transport expression onto the core
    // registration contract. Parameter and field names are canonical, lowercase
    // in the resolved tree; lookups normalize defensively. See parsing.md §9.1.
    internal sealed class TransportBuildContext : ITransportBuildContext
    {
        private const string ScalarKind = "scalar";
        private const string TransportKind = "transport";
        private const string TransportListKind = "transport list";
        private const string StructKind = "struct";

        private readonly ResolvedTransport _transport;
        private readonly TransportCompiler _compiler;

        public TransportBuildContext(
            ResolvedTransport transport,
            TransportCompiler compiler,
            TransportRole role,
            ILogger logger,
            IMemoryRental memory)
        {
            _transport = transport ?? throw new ArgumentNullException(nameof(transport));
            _compiler = compiler ?? throw new ArgumentNullException(nameof(compiler));
            Role = role;
            Logger = logger ?? throw new ArgumentNullException(nameof(logger));
            Memory = memory ?? throw new ArgumentNullException(nameof(memory));
        }

        public TransportRole Role { get; }

        public ILogger Logger { get; }

        public IMemoryRental Memory { get; }

        public bool TryGetString(string parameterName, out string value)
        {
            var binding = FindBinding(parameterName);
            if (binding is null)
            {
                value = string.Empty;
                return false;
            }

            if (binding.Value is ScalarValue scalar)
            {
                value = scalar.Text;
                return true;
            }

            throw ParameterKindMismatch(binding, ScalarKind);
        }

        public bool TryGetTransport(
            string parameterName,
            out NestedTransportFactory factory)
        {
            var binding = FindBinding(parameterName);
            if (binding is null)
            {
                factory = null!;
                return false;
            }

            if (binding.Value is TransportValue transportValue)
            {
                // Compiles the resolved subtree afresh on every invocation, so
                // reconnect-style transports get independent nested transports.
                factory = () => _compiler.CompileExpr(transportValue.Expr);
                return true;
            }

            throw ParameterKindMismatch(binding, TransportKind);
        }

        public bool TryGetTransportList(
            string parameterName,
            out IReadOnlyList<NestedTransportFactory> factories)
        {
            var binding = FindBinding(parameterName);
            if (binding is null)
            {
                factories = Array.Empty<NestedTransportFactory>();
                return false;
            }

            if (binding.Value is TransportListValue listValue)
            {
                var elements = listValue.Elements;
                var result = new NestedTransportFactory[elements.Count];
                for (var i = 0; i < result.Length; i++)
                {
                    var element = elements[i];
                    result[i] = () => _compiler.CompileExpr(element);
                }

                factories = result;
                return true;
            }

            throw ParameterKindMismatch(binding, TransportListKind);
        }

        public bool TryGetStruct(
            string parameterName,
            out ITransportStructValue value)
        {
            var binding = FindBinding(parameterName);
            if (binding is null)
            {
                value = null!;
                return false;
            }

            if (binding.Value is StructValue structValue)
            {
                value = new TransportStructValue(
                    structValue.Fields,
                    NameNormalizer.Normalize(binding.Param.Name));
                return true;
            }

            throw ParameterKindMismatch(binding, StructKind);
        }

        private ParamBinding? FindBinding(string parameterName)
        {
            var name = NameNormalizer.Normalize(parameterName);
            var provided = _transport.Provided;
            for (var i = 0; i < provided.Count; i++)
            {
                var binding = provided[i];
                if (string.Equals(
                        NameNormalizer.Normalize(binding.Param.Name),
                        name,
                        StringComparison.Ordinal))
                {
                    return binding;
                }
            }

            return null;
        }

        private static CompilerException ParameterKindMismatch(
            ParamBinding binding,
            string expectedKind)
        {
            var actualKind = KindName(binding.Value);
            if (actualKind is null)
            {
                return InconsistentTree();
            }

            return new CompilerException(
                "E_CMP_PARAM_KIND_MISMATCH",
                $"compiler expected value kind {expectedKind} for parameter "
                + $"'{NameNormalizer.Normalize(binding.Param.Name)}', "
                + $"got {actualKind}");
        }

        private static string? KindName(ResolvedValue value)
        {
            switch (value)
            {
                case ScalarValue _:
                    return ScalarKind;
                case TransportValue _:
                    return TransportKind;
                case TransportListValue _:
                    return TransportListKind;
                case StructValue _:
                    return StructKind;
                default:
                    return null;
            }
        }

        private static CompilerException InconsistentTree()
        {
            return new CompilerException(
                "E_CMP_INCONSISTENT_TREE",
                "compiler received an inconsistent resolved tree: "
                + "unspecified invariant violation");
        }

        // A resolved struct contains only string fields and nested structs. The
        // resolver guarantees matching kinds, so a mismatch is a tree invariant
        // violation rather than a user error.
        private sealed class TransportStructValue : ITransportStructValue
        {
            private readonly IReadOnlyList<StructFieldBinding> _fields;
            private readonly string _path;

            public TransportStructValue(
                IReadOnlyList<StructFieldBinding> fields,
                string path)
            {
                _fields = fields ?? throw new ArgumentNullException(nameof(fields));
                _path = path ?? throw new ArgumentNullException(nameof(path));
            }

            public bool TryGetString(string fieldName, out string value)
            {
                var binding = FindField(fieldName);
                if (binding is null)
                {
                    value = string.Empty;
                    return false;
                }

                if (binding.Value is StringFieldValue field)
                {
                    value = field.Text;
                    return true;
                }

                throw InconsistentTree();
            }

            public bool TryGetStruct(
                string fieldName,
                out ITransportStructValue value)
            {
                var binding = FindField(fieldName);
                if (binding is null)
                {
                    value = null!;
                    return false;
                }

                if (binding.Value is NestedStructFieldValue nested)
                {
                    value = new TransportStructValue(
                        nested.Fields,
                        FieldPath(binding.Field.Name));
                    return true;
                }

                throw InconsistentTree();
            }

            private StructFieldBinding? FindField(string fieldName)
            {
                var name = NameNormalizer.Normalize(fieldName);
                for (var i = 0; i < _fields.Count; i++)
                {
                    var binding = _fields[i];
                    if (string.Equals(
                            NameNormalizer.Normalize(binding.Field.Name),
                            name,
                            StringComparison.Ordinal))
                    {
                        return binding;
                    }
                }

                return null;
            }

            private string FieldPath(string fieldName)
            {
                return _path + "." + NameNormalizer.Normalize(fieldName);
            }
        }
    }
}
