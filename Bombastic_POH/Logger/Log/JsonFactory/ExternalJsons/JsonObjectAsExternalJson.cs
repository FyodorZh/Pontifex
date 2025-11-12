using System.IO;

namespace JsonFactory.ExternalJsons
{
    public class JsonObjectAsExternalJson : SimpleExternalJson
    {
        private JsonObject mObject;

        public IJsonObject Root
        {
            get { return mObject; }
        }

        public JsonObjectAsExternalJson()
        {
            Reinit();
        }

        public bool Reinit()
        {
            if (mObject == null)
            {
                mObject = Pool<JsonObject>.New();
                return true;
            }
            return false;
        }

        public override void Release()
        {
            if (mObject != null)
            {
                mObject.Free();
                mObject = null;
            }
        }

        public override void WriteTo(TextWriter output)
        {
            mObject.Serialize(output);
        }
    }
}
