using Serializer.BinarySerializer;

namespace Shared
{
    namespace CommonData
    {
        public interface IBuffData
        {
            RpgParameter Parameter { get; }
            float Value { get; }
            bool IsProgressParam { get; }
            bool IsSame(IBuffData other);
        }

        public class BuffDataRaw : IBuffData
        {
            public RpgParameter Parameter;
            public float Value;
            public bool IsProgressParam;

            RpgParameter IBuffData.Parameter { get { return Parameter; } }
            bool IBuffData.IsProgressParam { get { return IsProgressParam; } }
            float IBuffData.Value { get { return Value; } }
            public bool IsSame(IBuffData other)
            {
                return Parameter == other.Parameter && IsProgressParam == other.IsProgressParam;
            }

            public void Init(IBuffData other)
            {
                Parameter = other.Parameter;
                Value = other.Value;
                IsProgressParam = other.IsProgressParam;
            }

            public BuffDataRaw() { }
            public BuffDataRaw(IBuffData other)
            {
                Init(other);
            }

            public void AddValue(float other)
            {
                Value += other;
            }
        }

        public class BuffData : IBuffData, IDataStruct
        {
            private readonly BuffDataRaw Data = new BuffDataRaw();

            RpgParameter IBuffData.Parameter { get { return Data.Parameter; } }
            bool IBuffData.IsProgressParam { get { return Data.IsProgressParam; } }
            float IBuffData.Value { get { return Data.Value; } }
            bool IBuffData.IsSame(IBuffData other) { return Data.IsSame(other); }

            public void Init(IBuffData other)
            {
                Data.Init(other);
            }

            public bool Serialize(IBinarySerializer dst)
            {
                byte par = (byte) Data.Parameter;
                dst.Add(ref par);
                Data.Parameter = (RpgParameter)par;

                dst.Add(ref Data.Value);
                dst.Add(ref Data.IsProgressParam);

                return true;
            }
        }
    }
}
