using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Shared
{
    internal class ActionHistoryTracer
    {
        public readonly struct Record
        {
            public readonly string Action;
            public readonly StackTrace Stack;

            public Record(string action, StackTrace stack)
            {
                Action = action;
                Stack = stack;
            }

            public override string ToString()
            {
                string text = Action + ": ";
                for (int i = 0; i < Math.Min(2, Stack.FrameCount); ++i)
                {
                    text += Stack.GetFrame(i) + "  ||  ";
                }

                return text;
            }
        }

        private volatile IConcurrentQueue<Record> mHistory = new TinyConcurrentQueue<Record>();

        public void RecordEvent(string name, int skipFrames = 1)
        {
            var r = new Record(name, new StackTrace(skipFrames + 1, true));
            mHistory.Put(r);
        }

        public void Clear()
        {
            mHistory = new TinyConcurrentQueue<Record>();
        }

        public List<Record> Export()
        {
            List<Record> list = new List<Record>();

            Record r;
            while (mHistory.TryPop(out r))
            {
                list.Add(r);
            }

            return list;
        }
    }
}