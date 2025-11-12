using System;
using Serializer.BinarySerializer;
using Serializer.Extensions.Pool;
using Serializer.Factory;

namespace Shared.Protocol
{
    public static class ProtocolReader
    {
        public static DataReaderWithPool ThreadInstance(ByteArray data)
        {
            var reader = DataReaderSingleton<ModelFactory>.Instance;
            reader.Reset(data, 0);
            return reader;
        }

        public static DataReaderWithPool ThreadInstance(ByteArray data, int offset)
        {
            var reader = DataReaderSingleton<ModelFactory>.Instance;
            reader.Reset(data, offset);            
            return reader;
        }
    }

    public sealed class ProtocolWriter : DataWriterWithPool, Pool.ICollectable
    {
        private Pool.IObjectPool mOwner;

        public ProtocolWriter()
            : base(new ModelFactory())
        {
        }

        void Pool.ICollectable.Initialize(Pool.IObjectPool owner)
        {
            mOwner = owner;
        }

        void Pool.ICollectable.Collect()
        {
            Reset();
        }

        void Pool.ICollectable.Restore()
        {
            // DO NOTHING
        }

        Pool.IObjectPool Pool.ICollectable.Owner
        {
            get { return mOwner; }
        }
    }

    public static class ProtocolSerializer
    {
        /// <summary>
        /// Вынесено в поле класса, чтобы не создавать объект при каждом обращении.
        /// Это значение не должно никем изменяться.
        /// </summary>
        public static readonly byte[] NULL_ARRAY = new byte[0];
        /// <summary>
        /// Минимальный размер массива данных который будем пытаться упаковать.
        /// </summary>
        public const int MIN_DATA_SIZE_TO_PACK = 1024;
        /// <summary>
        /// Служебное поле в первом элементе байтового массива. Показывает
        /// что дальнейшая последовательность была упакована.
        /// </summary>
        public const byte DATA_PACKED_MARK = 157;
        /// <summary>
        /// Служебное поле в первом элементе байтового массива. Показывает
        /// что дальнейшая последовательность не была упакована.
        /// </summary>
        public const byte DATA_NOT_PACKED_MARK = 0;

        [ThreadStatic]
        private static ZLibCompressor mCompressor;
        private static ZLibCompressor Compressor
        {
            get
            {
                if (mCompressor == null)
                    mCompressor = new ZLibCompressor();
                return mCompressor;
            }
        }

        [ThreadStatic]
        private static ZLibDecompressor mDecompressor;
        private static ZLibDecompressor Decompressor
        {
            get
            {
                if (mDecompressor == null)
                    mDecompressor = new ZLibDecompressor();
                return mDecompressor;
            }
        }

        public static byte[] toArray<T>(ref T structData) where T : IDataStruct
        {
            try
            {
                ProtocolWriter writer = Writers.getWriter();
                writer.Add(ref structData);

                var bytes = writer.ShowByteDataUnsafe();
                return bytes.DataCopy();
            }
            catch (Exception ex)
            {
                Log.wtf("Exeption during Serializtion: {0}", ex);
                return NULL_ARRAY;
            }
        }

        public static T fromArray<T>(byte[] data)
            where T : IDataStruct
        {
            bool checkFlag;
            return fromArray<T>(data, out checkFlag);
        }

        public static T fromArray<T>(ByteArraySegment data)
            where T : IDataStruct
        {
            bool checkFlag;
            return fromArray<T>(data, out checkFlag);
        }

        public static T fromArray<T>(byte[] data, out bool checkFlag)
            where T : IDataStruct
        {
            checkFlag = false;
            T result = default(T);
            if (0 != data.Length)
            {
                try
                {
                    int destStartIndex;
                    using (ByteArray dest = unpackData(data, out destStartIndex))
                    {
                        var reader = ProtocolReader.ThreadInstance(dest.AddRef(), destStartIndex);
                        reader.Add(ref result);
                        reader.Reader.Reset();
                    }
                    checkFlag = true;
                }
                catch (Exception e)
                {
                    string inputData = Convert.ToBase64String(data);
                    Log.wtf(string.Format("Failed to deserialize struct of type {0}:\n"
                        + "Encoder: {1}\nInputData: {2}\n", typeof(T).FullName,
                        EndianIndependentSerializer.debugLog(), inputData), e);
                    result = default(T);
                }
            }
            else
            {
                checkFlag = true;
            }
            return result;
        }

        public static T fromArray<T>(ByteArraySegment data, out bool checkFlag)
            where T : IDataStruct
        {
            checkFlag = false;
            T result = default(T);
            if (0 != data.Count)
            {
                try
                {
                    var t = data.Clone();
                    using (ByteArray dest = ByteArray.AssumeControl(t))
                    {
                        var reader = ProtocolReader.ThreadInstance(dest.AddRef());
                        reader.Add(ref result);
                        reader.Reader.Reset();
                    }
                    checkFlag = true;
                }
                catch (Exception e)
                {
                    string inputData = Convert.ToBase64String(data.Clone());
                    Log.wtf(string.Format("Failed to deserialize struct of type {0}:\n"
                                          + "Encoder: {1}\nInputData: {2}\n", typeof(T).FullName,
                        EndianIndependentSerializer.debugLog(), inputData), e);
                    result = default(T);
                }
            }
            else
            {
                checkFlag = true;
            }
            return result;
        }

        private static byte[] packData(ByteArray data)
        {
            // Если размер данных достаточно велик, то 
            if (MIN_DATA_SIZE_TO_PACK <= data.Length)
            {
                using (var packed = Compressor.Pack(data, 2))
                {
                    if (packed.Length < data.Length)
                    {
                        packed.Data[0] = DATA_PACKED_MARK;
                        packed.Data[1] = DATA_PACKED_MARK;
                        return packed.DataCopy();
                    }
                }
            }

            byte[] result;

            // Отсылаем данные без упаковки.
            // Здесь важно значение первого байта.
            // Если он не совпадает с маркером упакованных данных,
            // то оставляем все как есть.
            if (DATA_PACKED_MARK != data.Data[0])
            {
                result = data.DataCopy();
            }
            else
            {
                // Иначе настоящий маркер помещаем во вторую позицию,
                // а затем повторяем весь буфер сначала.
                result = new byte[data.Length + 2];
                result[0] = DATA_PACKED_MARK;
                result[1] = DATA_NOT_PACKED_MARK;
                Array.Copy(data.Data, 0, result, 2, data.Length);
            }
            return result;
        }

        private static ByteArray unpackData(byte[] source, out int destStartIndex)
        {
            // Если первый байт - это маркер упаковки, то нужно проверить
            // второй байт.
            if (DATA_PACKED_MARK == source[0])
            {
                // Упаковка должна быть подтверждена вторым байтом.
                if (DATA_PACKED_MARK == source[1])
                {
                    destStartIndex = 0;
                    return Decompressor.Unpack(source, 2, source.Length - 2);
                }

                // Если данные все же не упакованы, то используем исходный массив,
                // сместившись на два байта вперед.
                destStartIndex = 2;
            }
            else
            {
                // Если первый байт не является маркером упаковки, то оставляем все как есть.
                destStartIndex = 0;
            }
            return ByteArray.AssumeControl(source);
        }
    }

}
