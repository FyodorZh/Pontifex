using System;

namespace Shared
{
    public class PriorityQueueEx<Key, Data> : PriorityQueue<Key, Data>
        where Key : System.IComparable<Key>
        //where Data : System.IEquatable<Data>
    {
        public DataRef FindData(Data data)
        {
            IPriorityQueueCtl self = this;

            int count = Count;
            for (int i = 1; i <= count; i++)
            {
                if (data.Equals(self.DataAt(i)))
                {
                    return new DataRef(this, i);
                }
            }
            return new DataRef();
        }

        public DataRef FindData(Func<Data, bool> finder)
        {
            if (finder != null)
            {
                IPriorityQueueCtl self = this;

                int count = Count;
                for (int i = 1; i <= count; i++)
                {
                    if (finder(self.DataAt(i)))
                    {
                        return new DataRef(this, i);
                    }
                }
            }
            return new DataRef();
        }
    }
}