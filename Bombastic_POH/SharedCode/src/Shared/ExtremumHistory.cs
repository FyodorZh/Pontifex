using System;
using System.Collections.Generic;
namespace Shared
{
    public class ExtremumUT : ExtremumHistory<int>
    {
        private DateTime mNow;

        public ExtremumUT(System.TimeSpan period)
            : base(period, UtcNowDateTimeProvider.Instance)
        {
        }

        protected override void SetToMinValue(out int value)
        {
            value = int.MinValue;
        }

        protected override int Compare(int d1, int d2)
        {
            return d1.CompareTo(d2);
        }

        protected override DateTime Now
        {
            get { return mNow; }
        }

        [UT.UT("Extremum List")]
        private static void UT(UT.IUTest test)
        {
            ExtremumUT list = new ExtremumUT(new System.TimeSpan(0, 0, 1));

            int time = 0;
            list.mNow = new DateTime();
            Queue<KeyValuePair<int, int>> queue = new Queue<KeyValuePair<int, int>>();

            queue.Enqueue(new KeyValuePair<int, int>(time, 123));
            list.Push(123);

            Random rnd = new Random();
            for (int i = 0; i < 100000; ++i)
            {
                int dt = rnd.Next(10);

                time += dt;
                list.mNow = list.mNow.AddMilliseconds(dt);

                int value = rnd.Next(1000);

                list.Push(value);

                queue.Enqueue(new KeyValuePair<int, int>(time, value));
                while (queue.Count > 0 && time - queue.Peek().Key > 1000)
                {
                    queue.Dequeue();
                }

                int val = int.MinValue;
                foreach (var kv in queue)
                {
                    val = Math.Max(val, kv.Value);
                }

                test.Equal(val, list.Extremum);
            }
        }
    }
}