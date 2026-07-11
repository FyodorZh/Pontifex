using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace Pontifex.Factory
{
    public abstract class BaseElement : IElement
    {
        public abstract ElementType Type { get; }

        public virtual bool EvaluateAsBool(out bool value)
        {
            value = false;
            return false;
        }

        public virtual bool EvaluateAsLong(out long value)
        {
            value = 0;
            return false;
        }

        public virtual bool EvaluateAsDouble(out double value)
        {
            value = 0.0;
            return false;
        }

        public virtual bool EvaluateAsString(out string value)
        {
            value = string.Empty;
            return false;
        }

        public virtual bool EvaluateAsDescription([MaybeNullWhen(false)]out IDescription value)
        {
            value = null;
            return false;
        }

        public virtual bool EvaluateAsArray([MaybeNullWhen(false)]out IReadOnlyList<IElement> value)
        {
            value = null;
            return false;
        }
    }
}