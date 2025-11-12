using Transport.StopReasons;

namespace Transport
{
    public class StopReason
    {
        /// <summary>
        /// Заглушка. Причины нет.
        /// </summary>
        public static readonly StopReason Void = new StopReason("Void");

        public static readonly UserIntention UserIntention = new UserIntention("user");

        /// <summary>
        /// Источник проблемы. Тот кто её задетектил. Например транспорт.
        /// </summary>
        public string Source
        {
            get; protected set;
        }

        /// <summary>
        /// Тип проблемы
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

        public virtual void PrintTo(JsonFactory.IJsonObject dst)
        {
            dst.AddElement("Source", Source);
            dst.AddElement("Type", Type);
        }

        public JsonFactory.IExternalJson Print()
        {
            JsonFactory.ExternalJsons.JsonObjectAsExternalJson wrap = new JsonFactory.ExternalJsons.JsonObjectAsExternalJson();
            PrintTo(wrap.Root);
            return wrap;
        }
    }
}