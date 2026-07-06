using System.IO;
using Pontifex.StopReasons;
using Scriba.JsonFactory;
using Scriba.JsonFactory.ExternalJsons;

namespace Pontifex
{
    public class StopReason
    {
        /// <summary>
        /// Placeholder. No reason specified.
        /// </summary>
        public static readonly StopReason Void = new StopReason("Void");

        public static readonly UserIntention UserIntention = new UserIntention("user");

        /// <summary>
        /// The source of the problem. The entity that detected it, e.g., a transport.
        /// </summary>
        public string Source
        {
            get; protected set;
        }

        /// <summary>
        /// The type of the problem.
        /// </summary>
        public string Type
        {
            get; protected set;
        }

        public StopReason(string type)
        {
            Source = "unspecified";
            Type = type;
        }

        public StopReason(string source, string type)
        {
            Source = source;
            Type = type;
        }

        public virtual void PrintTo(IJsonObject dst)
        {
            dst.AddElement("Source", Source);
            dst.AddElement("Type", Type);
        }

        public IExternalJson Print()
        {
            JsonObjectAsExternalJson wrap = new JsonObjectAsExternalJson();
            PrintTo(wrap.Root!);
            return wrap;
        }

        public override string ToString()
        {
            var json = Print();
            StringWriter sw = new StringWriter();
            json.WriteTo(sw);
            json.Release();
            return sw.ToString();
        }
    }
}