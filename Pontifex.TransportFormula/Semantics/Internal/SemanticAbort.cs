using System;

namespace Pontifex.TransportFormula.Semantics.Internal
{
    // Internal control-flow channel for the first semantic error. FormulaResolver is
    // the only public entry point; it converts the abort into a
    // FormulaResolveResult.Error, matching the parser's abort pattern. Semantic
    // components therefore fail without exposing additional public contracts.
    internal sealed class SemanticAbort : Exception
    {
        public SemanticAbort(FactoryError error)
            : base((error ?? throw new ArgumentNullException(nameof(error))).Message)
        {
            Error = error;
        }

        public FactoryError Error { get; }
    }
}
