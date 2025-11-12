using System;
using Serializer.BinarySerializer;
using Shared.CommonData.Plt.Offers;

namespace Shared.CommonData.Plt
{
    public abstract class DropItemParams : IDataStruct
    {
        public static readonly DropItemParams[] EMPTY_ARRAY = new DropItemParams[0];

        public abstract bool Serialize(IBinarySerializer dst);
    }

    public static class DropItemParamsExtensions
    {
        public static TResult Match<TResult>(this DropItemParams itemParams,
            Func<DropContainerParams, TResult> container,
            Func<DropItemWithLevelParams, TResult> level,
            Func<DropItemWithGradeParams, TResult> grade,
            Func<DropOfferParams, TResult> offer)
        {
            TResult result;
            if (TryMatch(itemParams, container, out result))
            {
                return result;
            }
            
            if (TryMatch(itemParams, level, out result))
            {
                return result;
            }
            
            if (TryMatch(itemParams, grade, out result))
            {
                return result;
            }
            
            if (TryMatch(itemParams, offer, out result))
            {
                return result;
            }

            throw new ArgumentOutOfRangeException();
        }

        private static bool TryMatch<T, TResult>(DropItemParams itemParams, Func<T, TResult> f, out TResult result)
            where T : DropItemParams
        {
            var typed = itemParams as T;
            if (typed != null)
            {
                result = f(typed);

                return true;
            }

            result = default(TResult);

            return false;
        }
    }
}
