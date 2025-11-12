using System.Collections.Generic;
using System.Text;

namespace Shared.LogicSynchronizer
{
    internal class StatisticsCollector
    {
        private readonly Dictionary<string, SizeData> mTable = new Dictionary<string, SizeData>();

        private readonly List<KeyValuePair<string, SizeData>> mList = new List<KeyValuePair<string, SizeData>>();

        public StatisticsCollector()
        {
            mTable.Add("Total", new SizeData(0,0));
        }

        public void InformAboutIncoming(ISyncContextCtl context, StreamId streamId, int size)
        {
            AddIncoming("Total", size);

            string name = context.Name;
            name = name.Substring(0, name.LastIndexOf('#'));
            name = name + "-" + context.GetStreamName(streamId) + ":" + streamId.Id.ToString();

            AddIncoming(name, size);
        }

        private void AddIncoming(string key, int size)
        {
            SizeData prev;
            mTable.TryGetValue(key, out prev);
            mTable[key] = prev.Add(size);
        }

        public string Flush()
        {
            mList.AddRange(mTable);

            //mTable.Clear();
            //mTable.Add("Total", 0);

            mList.Sort((l, r) => r.Value.Size.CompareTo(l.Value.Size));
            using (var sbh = Shared.StringBuilderInstance.Get())
            {
                StringBuilder sb = sbh.SB;

                int count = mList.Count;
                for (int i = 0; i < count; ++i)
                {
                    var kv = mList[i];
                    sb.Append(kv.Value.Size);
                    sb.Append(":");
                    sb.Append(kv.Value.Count);
                    sb.Append(" ");
                    sb.AppendLine(kv.Key);
                }

                mList.Clear();
                return sb.ToString();
            }
        }

        private struct SizeData
        {
            public readonly int Size;
            public readonly int Count;

            public SizeData(int size, int count)
            {
                Size = size;
                Count = count;
            }

            public SizeData Add(int size)
            {
                return new SizeData(Size + size, Count + 1);
            }
        }
    }
}