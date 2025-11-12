using Serializer.BinarySerializer;
using Shared.CommonData.Attributes;

namespace Shared
{
    namespace CommonData
    {
        public interface IDescriptionBase
        {
            short Id { get; }
            string Tag { get; }
        }

        public abstract class DescriptionBase : IDescriptionBase, IDataStruct
        {
            [EditorField(EditorFieldParameter.Unique | EditorFieldParameter.UseAsId)]
            public short Id = -1;
            [EditorField(EditorFieldParameter.Unique | EditorFieldParameter.UseAsTag)]
            public string Tag;

            short IDescriptionBase.Id { get { return Id; } }
            string IDescriptionBase.Tag { get { return Tag; } }

            public virtual bool Serialize(IBinarySerializer dst)
            {
                dst.Add(ref Id);
                dst.Add(ref Tag);

                return true;
            }

            public override string ToString()
            {
                return string.Format("[DescriptionBase: id={0}, tag={1}]", Id, Tag);
            }
        }

        public static class DescriptionBaseExtensions
        {
            public static T GetByDescId<T>(this T[] items, short descId)
                where T : DescriptionBase
            {
                T result = default(T);
                for (int i = 0, c = items.Length; i < c; ++i)
                {
                    if (items[i].Id == descId)
                    {
                        result = items[i];
                        break;
                    }
                }
                
                return result;
            }
        }
    }
}
