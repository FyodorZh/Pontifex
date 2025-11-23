// using System;
// using Archivarius;
// using Pontifex.Utils;
//
// namespace Pontifex
// {
//     public static class UnionDataSerializer
//     {
//         public static void Serialize(this ref UnionData data, ISerializer serializer)
//         {
//             if (serializer.IsWriter)
//             {
//                 data.WriteTo(serializer.Writer);
//             }
//             else
//             {
//                 data = ReadFrom(serializer.Reader);
//             }
//         }
//         
//         private static void WriteTo(this UnionData data, ILowLevelWriter writer)
//         {
//             writer.WriteByte((byte)data.Type);
//             var alias = data.Alias;
//             switch (data.Type)
//             {
//                 case UnionDataType.Bool:
//                     writer.WriteBool(alias.BoolValue);
//                     break;
//                 case UnionDataType.Byte:
//                     writer.WriteByte(alias.ByteValue);
//                     break;
//                 case UnionDataType.Char:
//                     writer.WriteChar(alias.CharValue);
//                     break;
//                 case UnionDataType.Short:
//                     writer.WriteShort(alias.ShortValue);
//                     break;
//                 case UnionDataType.UShort:
//                     writer.WriteUShort(alias.UShortValue);
//                     break;
//                 case UnionDataType.Int:
//                     writer.WriteInt(alias.IntValue);
//                     break;
//                 case UnionDataType.UInt:
//                     writer.WriteUInt(alias.UIntValue);
//                     break;
//                 case UnionDataType.Long:
//                     writer.WriteLong(alias.LongValue);
//                     break;
//                 case UnionDataType.ULong:
//                     writer.WriteULong(alias.ULongValue);
//                     break;
//                 case UnionDataType.Float:
//                     writer.WriteFloat(alias.FloatValue);
//                     break;
//                 case UnionDataType.Double:
//                     writer.WriteDouble(alias.DoubleValue);
//                     break;
//                 case UnionDataType.Decimal:
//                     writer.WriteDecimal(alias.DecimalValue);
//                     break;
//                 case UnionDataType.Array:
//                 {
//                     
//                     writer.WriteBytes(data.Bytes!.Array);
//                 }
//                     break;
//                 default:
//                     throw new ArgumentOutOfRangeException();
//             }
//         }
//
//         private static UnionData ReadFrom(ILowLevelReader reader)
//         {
//             UnionDataType type = (UnionDataType)reader.ReadByte();
//             switch (type)
//             {
//                 case UnionDataType.Bool: return new UnionData(reader.ReadBool());
//                 case UnionDataType.Byte: return new UnionData(reader.ReadByte());
//                 case UnionDataType.Char: return new UnionData(reader.ReadChar());
//                 case UnionDataType.Short: return new UnionData(reader.ReadShort());
//                 case UnionDataType.UShort: return new UnionData(reader.ReadUShort());
//                 case UnionDataType.Int: return new UnionData(reader.ReadInt());
//                 case UnionDataType.UInt: return new UnionData(reader.ReadUInt());
//                 case UnionDataType.Long: return new UnionData(reader.ReadLong());
//                 case UnionDataType.ULong: return new UnionData(reader.ReadULong());
//                 case UnionDataType.Float: return new UnionData(reader.ReadFloat());
//                 case UnionDataType.Double: return new UnionData(reader.ReadDouble());
//                 case UnionDataType.Decimal: return new UnionData(reader.ReadDecimal());
//                 case UnionDataType.Array: return new UnionData(reader.ReadBytes());
//                 default:
//                     throw new ArgumentOutOfRangeException();
//             }
//         }
//     }
// }