namespace Pontifex.TransportFormula.Parsing
{
    public interface IParser
    {
        FormulaParseResult Parse(string formula);
    }
}
