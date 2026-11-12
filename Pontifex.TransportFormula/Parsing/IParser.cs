namespace Pontifex.TransportFormula.Parsing
{
    public interface IParser
    {
        /// <summary>
        /// Parses one formula. Parser instances may be reused sequentially, but
        /// are not thread-safe because the cursor is instance state.
        /// </summary>
        FormulaParseResult Parse(string formula);
    }
}
