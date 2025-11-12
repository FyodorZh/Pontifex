using System;
using System.Text;
using Serializer.BinarySerializer.TypeReaders.TypeReaders;
using Serializer.BinarySerializer.TypeWriters.TypeWriters;

namespace Serializer.BinarySerializer
{
    public static class EndianIndependentSerializer
    {
        public static readonly ICharWriter CharWriter;
        public static readonly ICharReader CharReader;
        public static readonly IDoubleWriter DoubleWriter;
        public static readonly IDoubleReader DoubleReader;
        public static readonly IFloatWriter FloatWriter;
        public static readonly IFloatReader FloatReader;
        public static readonly IIntWriter IntWriter;
        public static readonly IIntReader IntReader;
        public static readonly ILongWriter LongWriter;
        public static readonly ILongReader LongReader;
        public static readonly IShortWriter ShortWriter;
        public static readonly IShortReader ShortReader;

        static EndianIndependentSerializer()
        {
            // Определяем изменение порядка байтов данных относительно стороны-сериализатора
            string charBytesString = _determineBytesString(new ManagedUnions2 { charValue = 'Ā' }.Bytes);
            string doubleBytesString = _determineBytesString(new ManagedUnions8 { doubleValue = 7.9499288951273625E-275 }.Bytes);
            string floatBytesString = _determineBytesString(new ManagedUnions4 { floatValue = 3.82047143E-37f }.Bytes);
            string intBytesString = _determineBytesString(new ManagedUnions4 { intValue = 50462976 }.Bytes);
            string longBytesString = _determineBytesString(new ManagedUnions8 { longValue = 506097522914230528 }.Bytes);
            string shortBytesString = _determineBytesString(new ManagedUnions2 { shortValue = 256 }.Bytes);

            _getCharWriterReader(charBytesString, out CharWriter, out CharReader);
            _getDoubleWriterReader(doubleBytesString, out DoubleWriter, out DoubleReader);
            _getFloatWriterReader(floatBytesString, out FloatWriter, out FloatReader);
            _getIntWriterReader(intBytesString, out IntWriter, out IntReader);
            _getLongWriterReader(longBytesString, out LongWriter, out LongReader);
            _getShortWriterReader(shortBytesString, out ShortWriter, out ShortReader);
        }

        public static string debugLog()
        {
            return "Using readers/writers of type:\n"
                + _debugLogObj(CharWriter)
                + _debugLogObj(CharReader)
                + _debugLogObj(DoubleWriter)
                + _debugLogObj(DoubleReader)
                + _debugLogObj(FloatWriter)
                + _debugLogObj(FloatReader)
                + _debugLogObj(IntWriter)
                + _debugLogObj(IntReader)
                + _debugLogObj(LongWriter)
                + _debugLogObj(LongReader)
                + _debugLogObj(ShortWriter)
                + _debugLogObj(ShortReader);
        }

        private static string _debugLogObj(object obj)
        {
            return obj.GetType().Name + "\n";
        }

        private static string _determineBytesString(byte[] bytes)
        {
            StringBuilder sb = new StringBuilder(0x07);
            for (int i = 0; i < bytes.Length; ++i)
            {
                bool isFind = false;
                for (int j = 0; j < bytes.Length; ++j)
                {
                    if (bytes[i] == bytes[j])
                    {
                        sb.Append(j.ToString());
                        isFind = true;
                        break;
                    }
                }
                if (!isFind)
                {
                    throw new Exception("Can't determine bytes order");
                }
            }
            return sb.ToString();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <remarks>
        /// Помнить о том что в общем случае Reader и Writer не будут симметричными
        /// </remarks>
        private static void _getCharWriterReader(string bytesInChar, out ICharWriter writer,
            out ICharReader reader)
        {
            switch (bytesInChar)
            {
                case "01":
                    {
                        writer = new CharWriter_01();
                        reader = new CharReader_01();
                    }
                    break;
                case "10":
                    {
                        writer = new CharWriter_10();
                        reader = new CharReader_10();
                    }
                    break;
                default:
                    {
                        _errorUnexpectedBytesOrder(typeof(char), bytesInChar);
                        writer = null;
                        reader = null;
                    }
                    break;
            }
        }

        private static void _getDoubleWriterReader(string bytesInDouble, out IDoubleWriter writer,
            out IDoubleReader reader)
        {
            switch (bytesInDouble)
            {
                case "01234567":
                    {
                        writer = new DoubleWriter_01234567();
                        reader = new DoubleReader_01234567();
                    }
                    break;
                case "76543210":
                    {
                        writer = new DoubleWriter_76543210();
                        reader = new DoubleReader_76543210();
                    }
                    break;
                default:
                    {
                        _errorUnexpectedBytesOrder(typeof(double), bytesInDouble);
                        writer = null;
                        reader = null;
                    }
                    break;
            }
        }

        private static void _getFloatWriterReader(string bytesInFloat, out IFloatWriter writer,
            out IFloatReader reader)
        {
            switch (bytesInFloat)
            {
                case "0123":
                    {
                        writer = new FloatWriter_0123();
                        reader = new FloatReader_0123();
                    }
                    break;
                case "3210":
                    {
                        writer = new FloatWriter_3210();
                        reader = new FloatReader_3210();
                    }
                    break;
                default:
                    {
                        _errorUnexpectedBytesOrder(typeof(float), bytesInFloat);
                        writer = null;
                        reader = null;
                    }
                    break;
            }
        }

        private static void _getIntWriterReader(string bytesInInt, out IIntWriter writer,
            out IIntReader reader)
        {
            switch (bytesInInt)
            {
                case "0123":
                    {
                        writer = new IntWriter_0123();
                        reader = new IntReader_0123();
                    }
                    break;
                case "3210":
                    {
                        writer = new IntWriter_3210();
                        reader = new IntReader_3210();
                    }
                    break;
                default:
                    {
                        _errorUnexpectedBytesOrder(typeof(int), bytesInInt);
                        writer = null;
                        reader = null;
                    }
                    break;
            }
        }

        private static void _getLongWriterReader(string bytesInLong, out ILongWriter writer,
            out ILongReader reader)
        {
            switch (bytesInLong)
            {
                case "01234567":
                    {
                        writer = new LongWriter_01234567();
                        reader = new LongReader_01234567();
                    }
                    break;
                case "76543210":
                    {
                        writer = new LongWriter_76543210();
                        reader = new LongReader_76543210();
                    }
                    break;
                default:
                    {
                        _errorUnexpectedBytesOrder(typeof(long), bytesInLong);
                        writer = null;
                        reader = null;
                    }
                    break;
            }
        }

        private static void _getShortWriterReader(string bytesInShort, out IShortWriter writer,
            out IShortReader reader)
        {
            switch (bytesInShort)
            {
                case "01":
                    {
                        writer = new ShortWriter_01();
                        reader = new ShortReader_01();
                    }
                    break;
                case "10":
                    {
                        writer = new ShortWriter_10();
                        reader = new ShortReader_10();
                    }
                    break;
                default:
                    {
                        _errorUnexpectedBytesOrder(typeof(short), bytesInShort);
                        writer = null;
                        reader = null;
                    }
                    break;
            }
        }

        private static void _errorUnexpectedBytesOrder(Type t, string bytesOrder)
        {
            throw new Exception("Unexpected bytes order of type '" + t.FullName + "' - '"
                + bytesOrder + "'");
        }
    }
}
