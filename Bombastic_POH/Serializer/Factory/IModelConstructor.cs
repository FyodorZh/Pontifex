using System;

namespace Serializer.Factory
{
    public interface IModelConstructor
    {
        Type FromType { get; }

        Type ToType { get; }

        object Construct();

        object Construct(object src);
    }
}
