using System;

namespace JsonFactory
{
    public static class JsonUtils
    {
        public static bool AddMultiElement(this IJsonObject self, string name, string format, params object[] list)
        {
            if (self.AddElement(name, format, list))
            {
                const int InText = 0;
                const int InBracket = 1;
                int state = InText;

                int openBracketPos = 0;
                int globalParamId = 0;
                bool hasAt = false;
                for (int i = 0; i < format.Length; ++i)
                {
                    char ch = format[i];
                    switch (ch)
                    {
                        case '{':
                            if (state == InText)
                            {
                                state = InBracket;
                                openBracketPos = i;
                                hasAt = false;
                            }
                            else
                            {
                                return false;
                            }
                            break;
                        case '@':
                            if (state == InBracket && openBracketPos == i - 1)
                            {
                                hasAt = true;
                            }
                            break;
                        case '}':
                            if (state == InBracket)
                            {
                                string paramName = format.Substring(openBracketPos + 1, i - (openBracketPos + 1));

                                int paramId;
                                if (!int.TryParse(paramName, out paramId))
                                {
                                    paramId = globalParamId++;
                                }

                                bool res = false;
                                if (paramId < list.Length)
                                {
                                    if (hasAt)
                                    {
                                        IExternalJson nestedJson = list[paramId] as IExternalJson;
                                        if (nestedJson != null)
                                        {
                                            res = self.AddElement(paramName, nestedJson);
                                        }
                                        else
                                        {
                                            //throw new InvalidOperationException("Wrong param#" + paramId + " type. Must be IJson");
                                            res = self.AddElement(paramName, "Null JSON");
                                        }
                                    }
                                    else
                                    {
                                        self.AddElement(paramName, ToString(list[paramId], null));
                                        res = true; // не интересеует результат добавления атрибута из-за варианта "{0} {0}"
                                    }
                                }

                                if (!res)
                                {
                                    return false;
                                }
                                state = InText;
                            }
                            else
                            {
                                return false;
                            }
                            break;
                        default:
                            break;
                    }
                }
                return true;
            }
            return false;
        }

        public static bool Serialize(this IJsonObject self, System.IO.TextWriter output)
        {
            output.Write("{");
            int count = self.Count;
            for (int i = 0; i < count; ++i)
            {
                var kv = self[i];
                if (i != 0)
                {
                    output.Write(", ");
                }
                output.Write('"');
                output.Write(kv.Key);
                output.Write("\": ");
                if (!kv.Value.WriteTo(output))
                {
                    return false;
                }
            }
            output.Write("}");
            return true;
        }

        public static bool Serialize(this IJsonObject self, out string output)
        {
            var sw = Pool<System.IO.StringWriter>.New();
            output = self.Serialize(sw) ? sw.ToString() : "";
            sw.GetStringBuilder().Length = 0;
            Pool<System.IO.StringWriter>.Free(sw);
            return output != "";
        }

        public static bool WriteTo(this JsonElement element, System.IO.TextWriter output, bool escape = true)
        {
            switch (element.Type)
            {
                case ElementType.Unknown:
                    output.Write("<ERROR>");
                    return false;
                case ElementType.String:
                    {
                        string value;
                        if (!element.TryGet(out value))
                        {
                            return false;
                        }
                        output.Write('"');
                        if (escape)
                        {
                            WriteString(value, output);
                        }
                        else
                        {
                            output.Write(value);
                        }
                        output.Write('"');
                    }
                    return true;
                case ElementType.StringFormat:
                    {
                        string format;
                        object[] list;
                        if (!element.TryGet(out format, out list))
                        {
                            return false;
                        }
                        output.Write('"');
                        if (!StringFormatTo(format, list, output, escape))
                        {
                            return false;
                        }
                        output.Write('"');
                    }
                    return true;
                case ElementType.Number:
                    {
                        double value;
                        if (!element.TryGet(out value))
                        {
                            return false;
                        }
                        output.Write(value);
                    }
                    return true;
                case ElementType.Bool:
                    {
                        bool value;
                        if (!element.TryGet(out value))
                        {
                            return false;
                        }
                        output.Write(value ? "true" : "false");
                    }
                    return true;
                case ElementType.Long:
                    {
                        long value;
                        if (!element.TryGet(out value))
                        {
                            return false;
                        }
                        output.Write(value);
                    }
                    return true;
                case ElementType.Json:
                    {
                        IExternalJson value;
                        if (!element.TryGet(out value))
                        {
                            return false;
                        }
                        value.WriteTo(output);
                    }
                    return true;
                case ElementType.Object:
                    {
                        IJsonObject value;
                        if (!element.TryGet(out value))
                        {
                            return false;
                        }
                        if (!Serialize(value, output))
                        {
                            return false;
                        }
                    }
                    return true;
                case ElementType.Array:
                    {
                        IJsonArray value;
                        element.TryGet(out value);
                        output.Write('[');
                        for (int j = 0; j < value.Count; ++j)
                        {
                            if (j != 0)
                            {
                                output.Write(", ");
                            }
                            if (!WriteTo(value[j], output))
                            {
                                return false;
                            }
                        }
                        output.Write(']');
                    }
                    return true;
            }
            return false;
        }

        private static bool StringFormatTo(string format, object[] list, System.IO.TextWriter output, bool escape)
        {
            const int InText = 0;
            const int InBracket = 1;
            const int InBracketFormat = 2;

            int state = InText;
            int defaultElementId = 0;

            int idEndPos = 0;

            for (int i = 0; i < format.Length; ++i)
            {
                char ch = format[i];
                switch (ch)
                {
                    case '{':
                        if (state == InText)
                        {
                            state = InBracket;
                        }
                        else
                        {
                            return false;
                        }
                        break;
                    case ':':
                        if (state == InBracket)
                        {
                            state = InBracketFormat;
                            idEndPos = i - 1;
                        }
                        else
                        {
                            output.Write(ch);
                        }
                        break;
                    case '}':
                        if (state == InBracket || state == InBracketFormat)
                        {
                            string elementFormat;
                            if (state == InBracket)
                            {
                                idEndPos = i - 1;
                                elementFormat = null;
                            }
                            else
                            {
                                elementFormat = format.Substring(idEndPos + 2, i - (idEndPos + 2));
                            }

                            int elementId = 0;

                            int power = 1;
                            for (int pos = idEndPos; pos >= 0; --pos)
                            {
                                char digit = format[pos];
                                if (digit == '{')
                                {
                                    if (power == 1)
                                    {
                                        elementId = -1;
                                    }
                                    break;
                                }
                                if (digit >= '0' && digit <= '9' && power < 1e6)
                                {
                                    elementId += power * (digit - '0');
                                    power *= 10;
                                }
                                else
                                {
                                    elementId = -1;
                                    break;
                                }
                            }

                            if (elementId == -1)
                            {
                                elementId = defaultElementId++;
                            }

                            if (elementId >= list.Length)
                            {
                                return false;
                            }

                            IExternalJson subJson = list[elementId] as IExternalJson;
                            if (subJson != null)
                            {
                                if (escape)
                                {
                                    subJson.WriteToAsText(output);
                                }
                                else
                                {
                                    subJson.WriteTo(output);
                                }
                            }
                            else
                            {
                                var str = ToString(list[elementId], elementFormat);
                                if (escape)
                                {
                                    WriteString(str, output);
                                }
                                else
                                {
                                    output.Write(str);
                                }
                            }

                            state = InText;
                        }
                        else
                        {
                            return false;
                        }
                        break;
                    default:
                        if (state == InText)
                        {
                            if (escape)
                            {
                                WriteChar(ch, output);
                            }
                            else
                            {
                                output.Write(ch);
                            }
                        }
                        break;
                }
            }

            return true;
        }

        private static string ToString(object obj, string format)
        {
            if (obj == null)
            {
                return "null";
            }

            if (format == null)
            {
                return obj.ToString();
            }

            if (obj is bool)
            {
                return ((bool)obj).ToString();
            }

            if (obj is char)
            {
                return ((char)obj).ToString();
            }

            if (obj is sbyte)
            {
                return ((sbyte)obj).ToString(format);
            }

            if (obj is byte)
            {
                return ((byte)obj).ToString(format);
            }

            if (obj is Int16)
            {
                return ((Int16)obj).ToString(format);
            }

            if (obj is UInt16)
            {
                return ((UInt16)obj).ToString(format);
            }

            if (obj is Int32)
            {
                return ((Int32)obj).ToString(format);
            }

            if (obj is UInt32)
            {
                return ((UInt32)obj).ToString(format);
            }

            if (obj is Int64)
            {
                return ((Int64)obj).ToString(format);
            }

            if (obj is UInt64)
            {
                return ((UInt64)obj).ToString(format);
            }

            if (obj is float)
            {
                return ((float)obj).ToString(format);
            }

            if (obj is double)
            {
                return ((double)obj).ToString(format);
            }

            if (obj is Decimal)
            {
                return ((Decimal)obj).ToString(format);
            }

            if (obj is DateTime)
            {
                return ((DateTime)obj).ToString(format);
            }

            return obj.ToString();
        }

        private static void WriteChar(char ch, System.IO.TextWriter dst)
        {
            switch (ch)
            {
                case '"':
                    dst.Write('\'');
                    break;
                case '\\':
                    dst.Write("\\\\");
                    break;
                case '\t':
                    dst.Write("\\t");
                    break;
                case '\r':
                    break;
                case '\n':
                    dst.Write("\\n");
                    break;
                default:
                    dst.Write(ch);
                    break;
            }
        }

        private static void WriteString(string str, System.IO.TextWriter dst)
        {
            int len = str.Length;
            for (int i = 0; i < len; ++i)
            {
                WriteChar(str[i], dst);
            }
        }
    }
}