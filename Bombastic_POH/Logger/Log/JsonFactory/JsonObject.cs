using System.Collections.Generic;

namespace JsonFactory
{
    public interface IJsonObject
    {
        bool AddElement(string name, string value);
        bool AddElement(string name, string format, params object[] list);
        bool AddElement(string name, double value);
        bool AddElement(string name, bool value);
        bool AddElement(string name, long value);
        bool AddElement(string name, IExternalJson value);
        IJsonObject AddObject(string name);
        IJsonArray AddArray(string name);

        void Free();

        int Count { get; }
        KeyValuePair<string, JsonElement> this[int id] { get; }
        JsonElement Get(string name);
    }

    public sealed class JsonObject : IJsonObject
    {
        public static IJsonObject Construct()
        {
            return Pool<JsonObject>.New();
        }

        private readonly List<KeyValuePair<string, JsonElement>> mElements = new List<KeyValuePair<string, JsonElement>>();

        public void Free()
        {
            for (int i = 0; i < mElements.Count; ++i)
            {
                mElements[i].Value.Free();
            }
            mElements.Clear();
            Pool<JsonObject>.Free(this);
        }

        IJsonArray IJsonObject.AddArray(string name)
        {
            if (CheckName(name) && TryFind(name) < 0)
            {
                JsonArray array = Pool<JsonArray>.New();
                mElements.Add(new KeyValuePair<string, JsonElement>(name, new JsonElement(array)));
                return array;
            }
            return null;
        }

        bool IJsonObject.AddElement(string name, string value)
        {
            if (CheckName(name) && TryFind(name) < 0)
            {
                mElements.Add(new KeyValuePair<string, JsonElement>(name, new JsonElement(value)));
                return true;
            }
            return false;
        }

        bool IJsonObject.AddElement(string name, string format, params object[] list)
        {
            if (CheckName(name) && TryFind(name) < 0)
            {
                mElements.Add(new KeyValuePair<string, JsonElement>(name, new JsonElement(format, list)));
                return true;
            }
            return false;
        }

        bool IJsonObject.AddElement(string name, double value)
        {
            if (CheckName(name) && TryFind(name) < 0)
            {
                mElements.Add(new KeyValuePair<string, JsonElement>(name, new JsonElement(value)));
                return true;
            }
            return false;
        }

        bool IJsonObject.AddElement(string name, bool value)
        {
            if (CheckName(name) && TryFind(name) < 0)
            {
                mElements.Add(new KeyValuePair<string, JsonElement>(name, new JsonElement(value)));
                return true;
            }
            return false;
        }

        bool IJsonObject.AddElement(string name, long value)
        {
            if (CheckName(name) && TryFind(name) < 0)
            {
                mElements.Add(new KeyValuePair<string, JsonElement>(name, new JsonElement(value)));
                return true;
            }
            return false;
        }

        bool IJsonObject.AddElement(string name, IExternalJson value)
        {
            if (CheckName(name) && TryFind(name) < 0)
            {
                mElements.Add(new KeyValuePair<string, JsonElement>(name, new JsonElement(value)));
                return true;
            }
            return false;
        }

        IJsonObject IJsonObject.AddObject(string name)
        {
            if (TryFind(name) < 0)
            {
                JsonObject obj = Pool<JsonObject>.New();
                mElements.Add(new KeyValuePair<string, JsonElement>(name, new JsonElement(obj)));
                return obj;
            }
            return null;
        }

        void IJsonObject.Free()
        {
            Free();
        }

        int IJsonObject.Count
        {
            get { return mElements.Count; }
        }

        KeyValuePair<string, JsonElement> IJsonObject.this[int id]
        {
            get { return mElements[id]; }
        }

        JsonElement IJsonObject.Get(string name)
        {
            int id = TryFind(name);
            if (id < 0)
            {
                return new JsonElement();
            }
            return mElements[id].Value;
        }

        private bool CheckName(string name)
        {
            return !name.Contains("\"");
        }

        private int TryFind(string name)
        {
            for (int i = 0; i < mElements.Count; ++i)
            {
                if (mElements[i].Key == name)
                {
                    return i;
                }
            }
            return -1;
        }
    }
}
