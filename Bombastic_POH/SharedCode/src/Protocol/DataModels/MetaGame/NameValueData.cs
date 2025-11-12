using System.Collections.Generic;
using Serializer.BinarySerializer;

namespace Shared.Protocol
{
    [System.Diagnostics.DebuggerDisplay("Name = {Name}, Value = {Value}")]
    public class NameValueData : IDataStruct
    {
        public string Name;
        public string Value;

        public bool Serialize(IBinarySerializer dst)
        {
            dst.Add(ref Name);
            dst.Add(ref Value);

            return true;
        }
    }

    public class NameValueDataContainer : IDataStruct
    {
        public NameValueData[] Data;

        public bool Serialize(IBinarySerializer dst)
        {
            dst.Add(ref Data);

            return true;
        }

        public void SetData(Dictionary<string, string> data)
        {
            int count = data != null ? data.Count : 0;
            Data = new NameValueData[count];
            if (count > 0)
            {
                int i = 0;
                foreach (var pair in data)
                {
                    Data[i++] = new NameValueData() { Name = pair.Key, Value = pair.Value };
                }
            }
        }
    }
}
