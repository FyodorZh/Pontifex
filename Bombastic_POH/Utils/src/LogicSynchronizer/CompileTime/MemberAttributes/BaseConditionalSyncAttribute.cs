using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace Shared.LogicSynchronizer
{
    public class BaseConditionalSyncAttribute : BaseSyncStrategyAttribute
    {
        private readonly BaseSyncAttribute mSyncStrategy;

        public override bool Log
        {
            get
            {
                return base.Log;
            }
            set
            {
                base.Log = value;
                mSyncStrategy.Log = value;
            }
        }

        public BaseConditionalSyncAttribute()
            : this(new BaseSyncAttribute())
        {
        }
        
        protected BaseConditionalSyncAttribute(BaseSyncAttribute syncStrategy)
        {
            mSyncStrategy = syncStrategy;
        }

        protected override IEnumerable<string> FormatGetter(PropertyInfo pi, bool putLogs)
        {
            string structName = StructName(pi);
            yield return string.Format("return m{0}.{1};", structName, pi.Name);
        }

        protected override IEnumerable<string> FormatSetter(PropertyInfo pi, bool putLogs)
        {
            yield return "throw new System.InvalidOperationException(\"Wrong usage of conditional setter\");";
        }

        protected override IEnumerable<string> FormatMethod(MethodInfo mi, bool putLogs)
        {
            yield return "throw new System.InvalidOperationException(\"Wrong usage of conditional method\");";
        }

        private static string StructName(MemberInfo mi)
        {
            string fix = "";
            {
                MethodInfo m = mi as MethodInfo;
                if (m != null)
                {
                    var list = m.GetParameters();
                    foreach (var param in list)
                    {
                        fix = fix + param.Name[0];
                    }
                }
            }
            if (fix != "")
            {
                return string.Format("{0}_{1}_Struct", mi.Name, fix);
            }
            return string.Format("{0}_Struct", mi.Name);
        }
    }
}
