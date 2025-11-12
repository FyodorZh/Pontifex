using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using NUnit.Framework;
using Shared;

namespace TransportAnalyzer.UTests
{
    [TestFixture]
    class TinyDictionaryTest
    {
        private struct Int : System.IEquatable<Int>
        {
            public int Value;

            public bool Equals(Int other)
            {
                return Value == other.Value;
            }

            public override int GetHashCode()
            {
                return Value;
            }
        }

        private enum CmdType
        {
            Add,
            Set,
            Remove,
            Contains,
            TryGet,
            Replace,
            Foreach,
            Count,
            COUNT
        }

        private struct CmdInfo
        {
            public CmdType Type;
            public Int Key;
            public long Value;

            public CmdInfo(CmdType type, int key, long value)
            {
                Type = type;
                Key = new Int(){Value = key};
                Value = value;
            }

            public long Apply(Dictionary<Int, long> dict)
            {
                switch (Type)
                {
                    case CmdType.Add:
                        dict.Add(Key, Value);
                        return 0;
                    case CmdType.Set:
                        dict[Key] = Value;
                        return 0;
                    case CmdType.Remove:
                        return dict.Remove(Key) ? 1 : 0;
                    case CmdType.Contains:
                        return dict.ContainsKey(Key) ? 1 : 0;
                    case CmdType.TryGet:
                        {
                            long val;
                            if (!dict.TryGetValue(Key, out val))
                            {
                                return -1;
                            }
                            return val;
                        }
                    case CmdType.Replace:
                        dict[Key] = Value;
                        return 0;
                    case CmdType.Foreach:
                        {
                            long res = 0;
                            foreach (var el in dict)
                            {
                                res += el.Value * el.Key.Value;
                            }
                            return res;
                        }
                    case CmdType.Count:
                        return dict.Count;
                    case CmdType.COUNT:
                        return 0;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }

            public long Apply(TinyDictionary<Int, long> dict)
            {
                switch (Type)
                {
                    case CmdType.Add:
                        dict.Add(Key, Value);
                        return 0;
                    case CmdType.Set:
                        dict[Key] = Value;
                        return 0;
                    case CmdType.Remove:
                        return dict.Remove(Key) ? 1 : 0;
                    case CmdType.Contains:
                        return dict.ContainsKey(Key) ? 1 : 0;
                    case CmdType.TryGet:
                    {
                        long val;
                        if (!dict.TryGetValue(Key, out val))
                        {
                            return -1;
                        }
                        return val;
                    }
                    case CmdType.Replace:
                        dict[Key] = Value;
                        return 0;
                    case CmdType.Foreach:
                    {
                        long res = 0;
                        foreach (var el in dict)
                        {
                            res += el.Value * el.Key.Value;
                        }
                        return res;
                    }
                    case CmdType.Count:
                        return dict.Count;
                    case CmdType.COUNT:
                        return 0;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
        }

        [Test]
        public void Test()
        {
            System.Random rnd = new System.Random(123);

            List<CmdInfo> safeCommands = new List<CmdInfo>();

            {
                var dict = new Dictionary<Int, long>();
                var tinyDict = new TinyDictionary<Int, long>();
                while (safeCommands.Count < 1000000)
                {
                    var type = (CmdType)rnd.Next((int)CmdType.COUNT);
                    if (type != CmdType.TryGet && rnd.Next() % 3 == 0)
                    {
                        type = CmdType.TryGet;
                    }

                    var element = new CmdInfo(type, rnd.Next(20), rnd.Next(1000));

                    Exception ex1 = null;
                    Exception ex2 = null;

                    long r1 = 0;
                    long r2 = 0;

                    try
                    {
                        r1 = element.Apply(dict);
                    }
                    catch (Exception ex)
                    {
                        ex1 = ex;
                    }

                    try
                    {
                        r2 = element.Apply(tinyDict);
                    }
                    catch (Exception ex)
                    {
                        ex2 = ex;
                    }

                    Assert.AreEqual(ex1 == null, ex2 == null);
                    Assert.AreEqual(r1, r2);

                    if (ex1 == null)
                    {
                        safeCommands.Add(element);
                    }
                }
            }

            Stopwatch sw1 = new Stopwatch();
            sw1.Start();
            {
                var dict = new Dictionary<Int, long>();
                for (int i = 0; i < safeCommands.Count; ++i)
                {
                    safeCommands[i].Apply(dict);
                }
            }
            sw1.Stop();

            Stopwatch sw2 = new Stopwatch();
            sw2.Start();
            {
                var dict = new TinyDictionary<Int, long>();
                for (int i = 0; i < safeCommands.Count; ++i)
                {
                    safeCommands[i].Apply(dict);
                }
            }
            sw2.Stop();


            Assert.Pass(string.Format("Dictionary.Time={0}ms; TinyDictionary.Time={1}ms", sw1.ElapsedMilliseconds, sw2.ElapsedMilliseconds));
        }
    }
}