using System.IO;
using System.Text;

namespace JsonFactory.Utils
{
    internal sealed class TextFilter : TextWriter
    {
        private TextWriter mWriter;

        public void Init(TextWriter core)
        {
            mWriter = core;
        }

        public override Encoding Encoding
        {
            get
            {
                return mWriter.Encoding;
            }
        }

        public override void Write(char value)
        {
            if (char.IsControl(value))
            {
                EscapeChar(value);
            }
            else
            {
                switch (value)
                {
                    case '"':
                        mWriter.Write('\'');
                        break;

                    case '\\':
                        mWriter.Write('/');
                        break;

                    default:
                        mWriter.Write(value);
                        break;
                }
            }
        }

        public override void Write(string value)
        {
            for (int charIndex = 0; charIndex < value.Length; charIndex++)
            {
                Write(value[charIndex]);
            }
        }

        private void EscapeChar(char charToEscape)
        {
            mWriter.Write('\\');
            mWriter.Write(charToEscape);
        }
    }
}
