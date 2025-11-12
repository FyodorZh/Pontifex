using System;
using System.Collections.Generic;
using Ionic.Zlib;
using Serializer.BinarySerializer;
using Serializer.Extensions.Pool;

namespace Shared.Protocol
{
    public static class BattleProtocol
    {
        private interface ISetupable
        {
            void Setup(bool compress);
        }

        public class Serializer : ISetupable
        {
            private bool mCompress;

            private readonly System.IO.MemoryStream mPackedStream;
            private readonly DeflateStream mCompressor;

            public Serializer()
                : this(false)
            {
            }

            public Serializer(bool compress)
            {
                mPackedStream = new System.IO.MemoryStream();
                mCompressor = new DeflateStream(mPackedStream, CompressionMode.Compress, CompressionLevel.BestCompression, true);

                (this as ISetupable).Setup(compress);
            }

            void ISetupable.Setup(bool compress)
            {
                mCompress = compress;
                if (mCompress)
                {
                    mCompressor.FlushMode = FlushType.Full;
                    mCompressor.WriteByte(123); // any byte
                    mCompressor.Flush();
                    mCompressor.FlushMode = FlushType.Sync;
                    mPackedStream.Position = 0;
                }
            }

            public ByteArray ToArray<T>(T structData) where T : global::Serializer.BinarySerializer.IDataStruct
            {
                try
                {
                    ProtocolWriter writer = Writers.getWriter();
                    writer.Add(ref structData);

                    using (var bytes = writer.ShowByteDataUnsafe())
                    {
                        return PackData(bytes);
                    }
                }
                catch (Exception ex)
                {
                    Log.wtf("Exception during Serialization: {0}", ex);
                    return ByteArray.Allocate(0);
                }
            }

            private ByteArray PackData(ByteArray data)
            {
                if (mCompress)
                {
                    mPackedStream.Position = 0;
                    mCompressor.Write(data.Data, 0, data.Length);
                    mCompressor.Flush();

                    return ByteArray.AssumeControl(mPackedStream.GetBuffer(), (int)mPackedStream.Position, false);
                }
                return data.AddRef();
            }
        }

        public class Decompressor
        {
            private readonly System.IO.MemoryStream mUnpackedStream;
            private readonly DeflateStream mDecompressor;

            private readonly bool mUsePools;

            public Decompressor(bool usePools)
            {
                mUnpackedStream = new System.IO.MemoryStream();
                mDecompressor = new DeflateStream(mUnpackedStream, CompressionMode.Decompress);
                mUsePools = usePools;
            }

            public byte[] Decompress(byte[] source)
            {
                mDecompressor.Write(source, 0, source.Length);
                mDecompressor.Flush();

                byte[] result = mUsePools ? Pool.BytesPool.AllocateNoLess((int)mUnpackedStream.Length) : new byte[mUnpackedStream.Length];

                mUnpackedStream.Position = 0;
                mUnpackedStream.Read(result, 0, (int)mUnpackedStream.Length);

                mUnpackedStream.SetLength(0);

                return result;
            }
        }

        public class Deserializer : ISetupable
        {
            private bool mCompressed;
            private readonly Decompressor mDecompressor;

            public Deserializer()
                : this(false)
            {
            }

            public Deserializer(bool compressed)
            {
                mDecompressor = new Decompressor(true);
                (this as ISetupable).Setup(compressed);
            }

            void ISetupable.Setup(bool compressed)
            {
                mCompressed = compressed;
            }

            public bool FromArray<T>(DataReaderWithPool dataReader, out T result)
                where T : IDataStruct, new()
            {
                try
                {
                    result = default(T);
                    dataReader.Add(ref result);
                    dataReader.Reader.Reset();
                    return true;
                }
                catch (Exception e)
                {
                    string text = string.Format("Failed to deserialize struct of type {0}:\nEncoder: {1}\n", typeof(T).FullName, EndianIndependentSerializer.debugLog());
                    Log.wtf(text, e);
                    result = default(T);
                    return false;
                }
            }

            public T FromArray<T>(byte[] data)
                where T : IDataStruct, new()
            {
                T result = default(T);

                byte[] dest = UnpackData(data);
                if (dest != null)
                {
                    if (!FromArray(ProtocolReader.ThreadInstance(ByteArray.AssumeControl(dest)), out result))
                    {
                        string inputData = Convert.ToBase64String(dest);
                        Log.e("InputData: {0}", inputData);
                    }

                    if (dest != data)
                    {
                        Pool.BytesPool.Free(ref dest);
                    }
                }
                return result;
            }

            private byte[] UnpackData(byte[] source)
            {
                if (mCompressed)
                {
                    try
                    {
                        return mDecompressor.Decompress(source);
                    }
                    catch (Exception ex)
                    {
                        Log.wtf("Decompression failed", ex);
                        return null;
                    }
                }
                return source;
            }
        }

        private static readonly List<Serializer> mSerializers = new List<Serializer>();
        private static readonly List<Deserializer> mDeserializers = new List<Deserializer>();

        private static T Get<T>(List<T> list, bool compress) where T : class, ISetupable, new()
        {
            T element = null;

            try
            {
                DBG.Lock.Push(list);
                lock (list)
                {
                    if (list.Count > 0)
                    {
                        element = list[list.Count - 1];
                        list.RemoveAt(list.Count - 1);
                    }
                }
            }
            finally { DBG.Lock.Pop(); }

            if (element == null)
            {
                element = new T();
            }
            element.Setup(compress);
            return element;
        }

        private static void Return<T>(List<T> list, ref T element) where T : class, ISetupable, new()
        {
            if (element != null)
            {
                try
                {
                    DBG.Lock.Push(list);
                    lock (list)
                    {
                        list.Add(element);
                    }
                }
                finally { DBG.Lock.Pop(); }
                element = null;
            }
        }

        public static Serializer GetSerializer(bool compress)
        {
            return Get(mSerializers, compress);
        }

        public static void ReturnSerializer(ref Serializer serializer)
        {
            Return(mSerializers, ref serializer);
        }

        public static Deserializer GetDeserializer(bool compress)
        {
            return Get(mDeserializers, compress);
        }

        public static void ReturnDeserializer(ref Deserializer deserializer)
        {
            Return(mDeserializers, ref deserializer);
        }
    }
}
