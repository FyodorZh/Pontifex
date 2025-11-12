using System.Collections.Generic;

namespace JsonFactory
{
    public interface IJsonArray
    {
        void AddElement(string value);
        void AddElement(double value);
        void AddElement(bool value);
        void AddElement(long value);
        IJsonObject AddObject();
        IJsonArray AddArray();

        void Free();

        int Count { get; }
        JsonElement this[int id] { get; }
    }

    public sealed class JsonArray : IJsonArray
    {
        private readonly List<JsonElement> mElements = new List<JsonElement>();

        public void Free()
        {
            for (int i = 0; i < mElements.Count; ++i)
            {
                mElements[i].Free();
            }
            mElements.Clear();
            Pool<JsonArray>.Free(this);
        }

        IJsonArray IJsonArray.AddArray()
        {
            JsonArray array = Pool<JsonArray>.New();
            mElements.Add(new JsonElement(array));
            return array;
        }

        void IJsonArray.AddElement(string value)
        {
            mElements.Add(new JsonElement(value));
        }

        void IJsonArray.AddElement(double value)
        {
            mElements.Add(new JsonElement(value));
        }

        void IJsonArray.AddElement(bool value)
        {
            mElements.Add(new JsonElement(value));
        }

        void IJsonArray.AddElement(long value)
        {
            mElements.Add(new JsonElement(value));
        }

        IJsonObject IJsonArray.AddObject()
        {
            JsonObject obj = Pool<JsonObject>.New();
            mElements.Add(new JsonElement(obj));
            return obj;
        }

        void IJsonArray.Free()
        {
            Free();
        }

        int IJsonArray.Count
        {
            get { return mElements.Count; }
        }

        JsonElement IJsonArray.this[int id]
        {
            get { return mElements[id]; }
        }
    }
}
