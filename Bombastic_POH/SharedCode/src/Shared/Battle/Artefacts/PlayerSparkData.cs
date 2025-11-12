using System;
using System.Collections.Generic;
using System.Linq;
using Serializer.BinarySerializer;

namespace Shared.Battle
{
    public interface ISpark
    {
        short DescId { get; }
        byte Level { get; }
    }

    public class Spark : ISpark, IDataStruct
    {
        public short DescId;
        public byte Level;

        short ISpark.DescId
        {
            get { return DescId; }
        }

        byte ISpark.Level
        {
            get { return Level; }
        }

        public Spark()
        {
        }

        public Spark(short descId, byte level)
        {
            DescId = descId;
            Level = level;
        }

        public Spark Clone()
        {
            return new Spark(DescId, Level);
        }

        #region Implementation of IDataStruct

        public bool Serialize(IBinarySerializer dst)
        {
            dst.Add(ref DescId);
            dst.Add(ref Level);
            return true;
        }

        #endregion
    }
}