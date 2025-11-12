using JsonFactory;

public static partial class Log
{
    public struct MessageData
    {
        internal MessageData(IJsonObject data)
            : this() // Из-за глючности райдера
        {
            Data = data;
        }

        public IJsonObject Data { get; private set; }

        public string Severity
        {
            get
            {
                string severity;
                if (!Data.Get(MessageAttributes.Severity).TryGet(out severity))
                {
                    severity = "UNKNOWN";
                }
                return severity;
            }
        }

        public string Time
        {
            get
            {
                string time;
                if (!Data.Get(MessageAttributes.Time).TryGet(out time))
                {
                    time = "UNKNOWN";
                }
                return time;
            }
        }

        public bool WriteTimeTo(System.IO.TextWriter output)
        {
            return Data.Get(MessageAttributes.Time).WriteTo(output);
        }

        public bool WriteMessageTo(System.IO.TextWriter output)
        {
            IJsonArray tags;
            if (Data.Get(MessageAttributes.Tags).TryGet(out tags))
            {
                int count = tags.Count;
                for (int i = count - 1; i >= 0; --i)
                {
                    var element = tags[i];

                    IJsonObject tagPair;
                    if (element.TryGet(out tagPair))
                    {
                        int pairs = tagPair.Count;
                        for (int j = 0; j < pairs; ++j)
                        {
                            var kv = tagPair[j];
                            string value;
                            if (kv.Value.TryGet(out value))
                            {
                                output.Write(kv.Key);
                                output.Write("=");
                                output.Write(value);
                                output.Write("; ");
                            }
                        }
                    }

                    string tag;
                    if (element.TryGet(out tag))
                    {
                        output.Write(tag);
                        output.Write("; ");
                    }
                }
            }
            return Data.Get(MessageAttributes.Message).WriteTo(output, false);
        }

        public int StackTraceDepth
        {
            get
            {
                IJsonArray stack;
                if (Data.Get(MessageAttributes.Stack).TryGet(out stack))
                {
                    return stack.Count;
                }
                return 0;
            }
        }

        public bool WriteStackTrace(string prefix, System.IO.TextWriter output)
        {
            IJsonArray stack;
            if (Data.Get(MessageAttributes.Stack).TryGet(out stack))
            {
                int cnt = stack.Count;
                for (int i = 0; i < cnt; ++i)
                {
                    IJsonObject frame;
                    if (!stack[i].TryGet(out frame) || !WriteStackFrame(frame, prefix, output))
                    {
                        return false;
                    }
                    output.WriteLine();
                }
            }
            return false;
        }

        public bool WriteStackFrame(int frameId, string prefix, System.IO.TextWriter output)
        {
            IJsonArray stack;
            if (Data.Get(MessageAttributes.Stack).TryGet(out stack))
            {
                IJsonObject frame;
                if (stack[frameId].TryGet(out frame))
                {
                    return WriteStackFrame(frame, prefix, output);
                }
            }
            return false;
        }

        private bool WriteStackFrame(IJsonObject frame, string prefix, System.IO.TextWriter output)
        {
            string className;
            string methodName;
            string fileName;
            long linePos;

            if (!frame.Get(MessageAttributes.StackFrameClass).TryGet(out className) ||
                !frame.Get(MessageAttributes.StackFrameMethod).TryGet(out methodName))
            {
                return false;
            }

            if (!frame.Get(MessageAttributes.StackFrameFile).TryGet(out fileName))
            {
                fileName = "";
            }

            if (!frame.Get(MessageAttributes.StackFrameLine).TryGet(out linePos))
            {
                linePos = 0;
            }

            output.Write(className);
            output.Write('.');
            output.Write(methodName);
            output.Write(" (");
            output.Write(fileName);
            output.Write('#');
            output.Write(linePos);
            output.Write(')');

            return true;
        }
    }
}
