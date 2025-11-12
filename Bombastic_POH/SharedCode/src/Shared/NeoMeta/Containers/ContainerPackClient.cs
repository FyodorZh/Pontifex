using Serializer.BinarySerializer;
using Serializer.Extensions;

namespace Shared.NeoMeta.Items
{
    public class ContainerPackClient : IDataStruct
    {
        public PackState State;
        public short PackId;
        public int? NextCollectTime;

        public ContainerPackClient()
        {            
        }

        public ContainerPackClient(PackState state, short packId, int? nextCollectTime)
        {
            State = state;
            PackId = packId;
            NextCollectTime = nextCollectTime;
        }

        public bool Serialize(IBinarySerializer dst)
        {
            var rawState = (byte)State;
            dst.Add(ref rawState);
            State = (PackState)rawState;

            dst.Add(ref PackId);
            dst.AddNullable(ref NextCollectTime);

            return true;
        }

        public enum PackState : byte
        {
            Idle,
            Generating,
            WaitingForCollect
        }

        public override string ToString()
        {
            return string.Format("ContainerPackClient State={0} PackId={1} NextCollectTime={2}", State, PackId.ToString(), NextCollectTime.ToString());
        }
    }
}
