using System.Collections.Generic;
using System.Collections.ObjectModel;
using Serializer.BinarySerializer;

namespace Shared
{
    namespace CommonData
    {
        public interface IUnitRunesData : IDataStruct
        {
            IBaseUnitRuneItem GetRune(int id);

            int RunesCount { get; }
            ReadOnlyCollection<IBaseUnitRuneItem> Runes { get; }
        }

        public class UnitRunesData : IUnitRunesData
        {
            private UnitRuneItem[] mRunes;
            private readonly List<IBaseUnitRuneItem> mIRunes;

            public ReadOnlyCollection<IBaseUnitRuneItem> Runes { get; private set; }

            public UnitRunesData()
                : this(new UnitRuneItem[0])
            {
            }

            public UnitRunesData(UnitRuneItem[] runes)
            {
                mRunes = runes;
                mIRunes = new List<IBaseUnitRuneItem>(mRunes);
                Runes = new ReadOnlyCollection<IBaseUnitRuneItem>(mIRunes);
            }

            public int RunesCount { get { return mRunes == null ? 0 : mRunes.Length; } }

            public IBaseUnitRuneItem GetRune(int id)
            {
                if (id < 0 || id >= RunesCount)
                {
                    return null;
                }

                var desc = mRunes[id];
                if (desc.Id != id)
                {
                    Log.e("Inconsistent rune data: {0} - index {1}, Id {2}", desc.Tag, id, desc.Id);
                }
                return desc;
            }

            public bool Serialize(IBinarySerializer dst)
            {
                if (dst.isReader)
                {
                    dst.Add(ref mRunes);

                    mIRunes.Clear();
                    if (mRunes != null)
                    {
                        mIRunes.AddRange(mRunes);
                    }
                }
                else
                {
                    dst.Add(ref mRunes);
                }
                return true;
            }
        }
    }
}