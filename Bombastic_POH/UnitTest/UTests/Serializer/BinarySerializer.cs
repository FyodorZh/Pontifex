using System;
using NUnit.Framework;
using Serializer.BinarySerializer;
using Serializer.Factory;

namespace TransportAnalyzer.UTests
{
    [TestFixture]
    class BinarySerializerTest
    {
        public T Clone<T>(IDataStructFactory factory, T data)
            where T : IDataStruct
        {
            DataReader<ManagedReader> reader = new DataReader<ManagedReader>(factory, null);
            DataWriter writer = new DataWriter(factory);

            writer.Add(ref data);

            int size;
            byte[] bytes = writer.Writer.ShowByteDataUnsafe(out size);
            reader.Reader.Reset(bytes, 0);

            T res = default(T);
            reader.Add(ref res);
            return res;
        }

        private class RootClass : IDataStruct, IEquatable<RootClass>
        {
            public NestedClass c;
            public NestedClassImpl cImpl;
            public NestedClassImpl cHiddenImpl;
            public NestedStruct s;

            public bool Serialize(IBinarySerializer dst)
            {
                dst.Add(ref c);
                dst.Add(ref cImpl);
                NestedClass hide = cHiddenImpl;
                dst.Add(ref hide);
                cHiddenImpl = hide as NestedClassImpl;
                dst.Add(ref s);
                return true;
            }

            public bool Equals(RootClass other)
            {
                return c.Equals(other.c) && cImpl.Equals(other.cImpl) && cHiddenImpl.Equals(other.cHiddenImpl) && s.Equals(other.s);
            }
        }

        private struct RootStruct : IDataStruct, IEquatable<RootStruct>
        {
            public NestedClass c;
            public NestedClassImpl cImpl;
            public NestedClassImpl cHiddenImpl;
            public NestedStruct s;

            public bool Serialize(IBinarySerializer dst)
            {
                dst.Add(ref c);
                dst.Add(ref cImpl);
                NestedClass hide = cHiddenImpl;
                dst.Add(ref hide);
                cHiddenImpl = hide as NestedClassImpl;
                dst.Add(ref s);
                return true;
            }

            public bool Equals(RootStruct other)
            {
                return c.Equals(other.c) && cImpl.Equals(other.cImpl) && cHiddenImpl.Equals(other.cHiddenImpl) && s.Equals(other.s);
            }
        }

        private class NestedClass : IDataStruct, IEquatable<NestedClass>
        {
            public int a;
            public string b;

            public virtual bool Serialize(IBinarySerializer dst)
            {
                dst.Add(ref a);
                dst.Add(ref b);
                return true;
            }

            public bool Equals(NestedClass other)
            {
                return a == other.a && b == other.b;
            }
        }

        private class NestedClassImpl : NestedClass, IEquatable<NestedClassImpl>
        {
            public float c;

            public override bool Serialize(IBinarySerializer dst)
            {
                dst.Add(ref c);
                return base.Serialize(dst);
            }

            public bool Equals(NestedClassImpl other)
            {
                return base.Equals((NestedClass)other) && c == other.c;
            }
        }

        private struct NestedStruct : IDataStruct, IEquatable<NestedStruct>
        {
            public int a;
            public string b;

            public bool Serialize(IBinarySerializer dst)
            {
                dst.Add(ref a);
                dst.Add(ref b);
                return true;
            }

            public bool Equals(NestedStruct other)
            {
                return a == other.a && b == other.b;
            }
        }

        [Test]
        public void FactorySerializerTest()
        {
            ModelTinyFactory factory = new ModelTinyFactory(new[] { typeof(NestedClassImpl), typeof(NestedClass), typeof(RootClass) });

            RootClass cRoot = new RootClass()
            {
                c = new NestedClass() { a = 123, b = "hello" },
                cImpl = new NestedClassImpl() { a = 13, b = "hi" },
                cHiddenImpl = new NestedClassImpl() { a = 125, b = "bye" },
                s = new NestedStruct() { a = 3, b = "GO" }
            };

            RootClass cRootClone = Clone(factory, cRoot);

            if (!cRootClone.Equals(cRoot))
            {
                Assert.True(cRootClone.Equals(cRoot));
            }
        }
        
        [Test]
        public void NoFactorySerializerTest()
        {
            TrivialFactory voidFactory = new TrivialFactory();

            var obj = new NestedClassImpl() { a = 13, b = "hi", c = 1.5f };
            var clone = Clone(voidFactory, obj);

            if (!clone.Equals(obj))
            {
                Assert.True(clone.Equals(obj));
            }
        }
    }
}
