using System.Runtime.InteropServices;

namespace JsonFactory
{
    /// <summary>
    /// PLT-4104, additional offset don't helped
    /// </summary>
#if !UNITY_ANDROID
    [StructLayout(LayoutKind.Explicit)]
#endif
    public struct Union
    {
#if !UNITY_ANDROID
        [FieldOffset(0)]
#endif
        public bool BoolValue;

#if !UNITY_ANDROID
        [FieldOffset(0)]
#endif
        public long LongValue;

#if !UNITY_ANDROID
        [FieldOffset(0)]
#endif
        public double DoubleValue;
    }

    /// <summary>
    /// Уберструктура для хранения элемента json
    /// </summary>
    public struct JsonElement
    {
        private Union mValues;
        public object mObjectValue;

        public ElementType Type { get; private set; }

        private object[] Substrings { get; set; }

        private bool BoolValue
        {
            get { return mValues.BoolValue; }
            set { mValues.BoolValue = value; }
        }

        private long LongValue
        {
            get { return mValues.LongValue; }
            set { mValues.LongValue = value; }
        }

        private double DoubleValue
        {
            get { return mValues.DoubleValue; }
            set { mValues.DoubleValue = value; }
        }

        private string StringValue
        {
            get { return mObjectValue as string; }
            set { mObjectValue = value; }
        }

        private IExternalJson SubJson
        {
            get { return mObjectValue as IExternalJson; }
            set { mObjectValue = value; }
        }

        private JsonObject ObjectValue
        {
            get { return mObjectValue as JsonObject; }
            set { mObjectValue = value; }
        }

        private JsonArray ArrayValue
        {
            get { return mObjectValue as JsonArray; }
            set { mObjectValue = value; }
        }
        
        public JsonElement(string value)
            : this()
        {
            Type = ElementType.String;
            StringValue = value;
        }

        public JsonElement(string format, object[] list)
            : this()
        {
            Type = ElementType.StringFormat;
            StringValue = format;
            Substrings = list;
        }

        public JsonElement(double value)
            : this()
        {
            Type = ElementType.String;
            DoubleValue = value;
        }

        public JsonElement(bool value)
            : this()
        {
            Type = ElementType.Bool;
            BoolValue = value;
        }

        public JsonElement(long value)
            : this()
        {
            Type = ElementType.Long;
            LongValue = value;
        }

        public JsonElement(IExternalJson value)
            : this()
        {
            Type = ElementType.Json;
            SubJson = value;
        }

        public JsonElement(JsonObject value)
            : this()
        {
            Type = ElementType.Object;
            ObjectValue = value;
        }

        public JsonElement(JsonArray value)
            : this()
        {
            Type = ElementType.Array;
            ArrayValue = value;
        }

        public void Free()
        {
            if (SubJson != null)
            {
                SubJson.Release();
            }
            if (ObjectValue != null)
            {
                ObjectValue.Free();
            }
            if (ArrayValue != null)
            {
                ArrayValue.Free();
            }

            Type = ElementType.Unknown;
            mValues = new Union();
            mObjectValue = null;
            Substrings = null;
        }

        public bool TryGet(out string value)
        {
            if (Type == ElementType.String)
            {
                value = StringValue;
                return true;
            }
            value = "";
            return false;
        }

        public bool TryGet(out string format, out object[] substrings)
        {
            if (Type == ElementType.StringFormat)
            {
                format = StringValue;
                substrings = Substrings;
                return true;
            }
            format = "";
            substrings = null;
            return false;
        }

        public bool TryGet(out double value)
        {
            if (Type == ElementType.Number)
            {
                value = DoubleValue;
                return true;
            }
            value = 0;
            return false;
        }

        public bool TryGet(out bool value)
        {
            if (Type == ElementType.Bool)
            {
                value = BoolValue;
                return true;
            }
            value = false;
            return false;
        }

        public bool TryGet(out long value)
        {
            if (Type == ElementType.Long)
            {
                value = LongValue;
                return true;
            }
            value = 0;
            return false;
        }

        public bool TryGet(out IExternalJson value)
        {
            if (Type == ElementType.Json)
            {
                value = SubJson;
                return true;
            }
            value = null;
            return false;
        }

        public bool TryGet(out IJsonObject value)
        {
            if (Type == ElementType.Object)
            {
                value = ObjectValue;
                return true;
            }
            value = null;
            return false;
        }

        public bool TryGet(out IJsonArray value)
        {
            if (Type == ElementType.Array)
            {
                value = ArrayValue;
                return true;
            }
            value = null;
            return false;
        }
    }
}
