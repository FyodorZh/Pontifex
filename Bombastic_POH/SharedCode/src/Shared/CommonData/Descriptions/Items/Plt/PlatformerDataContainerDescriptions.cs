using System.Collections.Generic;
using Serializer.BinarySerializer;
using Serializer.Tools;

namespace Shared.CommonData.Plt
{
    public abstract class PlatformerDataContainerDescriptions<TDataContainer> : PlatformerDataDescriptions
        where TDataContainer : class, IDataStruct
    {
        public TDataContainer Container { get; protected set; }

        protected override void InitFromRawData(byte[] bytes)
        {
            var rawData = CommonDataConstructor<TDataContainer, CommonResoucesFactory>.CreateFromBytes(bytes, true);
            if (rawData == null)
            {
                Log.e("ContainerDescriptions<{0}>.InitFromRawData() rawData is null!", typeof(TDataContainer).Name);
                return;
            }

            InitFromContainer(rawData);
        }

        protected Dictionary<short, T> GetIdDictionary<T>(T[] src)
            where T : IDescriptionBase
        {
            int count = src.Length;
            Dictionary<short, T> dictionary = new Dictionary<short, T>(count);
            for (int i = 0, n = count; i < n; ++i)
            {
                T elem = src[i];
                dictionary.Add(elem.Id, elem);
            }

            return dictionary;
        }

        public abstract void InitFromContainer(TDataContainer container);
    }
}
