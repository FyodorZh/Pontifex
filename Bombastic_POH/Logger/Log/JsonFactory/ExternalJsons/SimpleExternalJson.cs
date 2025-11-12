using JsonFactory.Utils;
using System.IO;

namespace JsonFactory.ExternalJsons
{
    public abstract class SimpleExternalJson : IExternalJson
    {
        public abstract void Release();

        public abstract void WriteTo(TextWriter output);

        public void WriteToAsText(TextWriter output)
        {
            var writer = Pool<TextFilter>.New();
            writer.Init(output);
            try
            {
                WriteTo(writer);
            }
            finally
            {
                writer.Init(null);
                Pool<TextFilter>.Free(writer);
            }
        }
    }
}
