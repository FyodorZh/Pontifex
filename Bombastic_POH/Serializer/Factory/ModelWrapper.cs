using System;
using Serializer.BinarySerializer;

namespace Serializer.Factory
{
    public class ModelWrapper<TFrom, TTo, TSerializer> : IModelConstructor
        where TFrom : class, IDataStruct, new()
        where TTo : IModelBox<TFrom, TSerializer>, new()
    {
        private static readonly Type mFromType = typeof(TFrom);
        private static readonly Type mToType = typeof(TTo);

        public Type FromType
        {
            get { return mFromType; }
        }

        public Type ToType
        {
            get { return mToType; }
        }

        public object Construct()
        {
            TTo ttt = new TTo();
            ttt.Init(new TFrom());
            return ttt;
        }

        public object Construct(object src)
        {
            TTo ttt = new TTo();
            ttt.Init((TFrom)src);
            return ttt;
        }
    }
}
