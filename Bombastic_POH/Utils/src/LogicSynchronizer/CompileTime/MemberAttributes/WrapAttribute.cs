using System;
using System.Collections.Generic;
using System.Reflection;

namespace Shared.LogicSynchronizer
{
    [AttributeUsage(AttributeTargets.Property)]
    public class WrapAttribute : BaseSyncStrategyAttribute
    {
        public bool UseSelf { get; set; }
        public override IEnumerable<string> AdditionalFields(MemberInfo mi)
        {
            if (!UseSelf)
            {
                PropertyInfo pi = (PropertyInfo)mi;
                yield return string.Format("private readonly {0} m{1};", SynchronizerFactory.FullName(pi.PropertyType), pi.Name);
            }
        }

        public override IEnumerable<string> AppendConstructor(MemberInfo mi, bool putLogs)
        {
            if (!UseSelf)
            {
                PropertyInfo pi = (PropertyInfo)mi;
                if (!pi.CanRead || pi.CanWrite)
                {
                    throw new InvalidOperationException(mi + " is not a getter");
                }

                if (pi.PropertyType.GetCustomAttributes(typeof(WrapperAttribute), false).Length == 0)
                {
                    throw new InvalidOperationException(string.Format("type '{0}' has no [Wrapper] attribute", pi.PropertyType));
                }

                yield return string.Format("m{0} = new Wrapper{1}(Context, mCore.{0});", pi.Name, pi.PropertyType.Name);
            }
        }

        protected override IEnumerable<string> FormatGetter(PropertyInfo pi, bool putLogs)
        {
            if (UseSelf)
            {
                yield return "return this;";
            }
            else
            {
                yield return string.Format("return m{0};", pi.Name);
            }
        }

        protected sealed override IEnumerable<string> FormatSetter(PropertyInfo pi, bool putLogs)
        {
            throw new InvalidOperationException();
        }

        protected sealed override IEnumerable<string> FormatMethod(MethodInfo mi, bool putLogs)
        {
            throw new InvalidOperationException();
        }
    }
}
