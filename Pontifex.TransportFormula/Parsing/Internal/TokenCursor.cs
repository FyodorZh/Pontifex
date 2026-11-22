using System.Collections.Generic;
using Pontifex.TransportFormula.Internal;

namespace Pontifex.TransportFormula.Parsing.Internal
{
    internal sealed class TokenCursor
    {
        private readonly IReadOnlyList<FormulaToken> _tokens;
        private int _index;

        public TokenCursor(IReadOnlyList<FormulaToken> tokens)
        {
            _tokens = tokens;
            _index = 0;
        }

        public FormulaToken Current => _tokens[_index];

        public FormulaToken Peek(int distance)
        {
            var index = _index + distance;
            return index < _tokens.Count ? _tokens[index] : _tokens[_tokens.Count - 1];
        }

        public FormulaToken Consume()
        {
            var token = Current;
            if (_index < _tokens.Count - 1)
            {
                _index++;
            }

            return token;
        }

        public bool IsCurrentAsKeyword()
        {
            return Current.Kind == FormulaTokenKind.Word
                   && NameNormalizer.IsAsKeyword(Current.Text);
        }
    }
}
