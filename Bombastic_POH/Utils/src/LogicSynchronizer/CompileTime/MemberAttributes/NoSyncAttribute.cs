using System.Collections.Generic;
using System.Reflection;

namespace Shared.LogicSynchronizer
{
    public class NoSyncAttribute : BaseSyncStrategyAttribute
    {
        protected override IEnumerable<string> FormatGetter(PropertyInfo pi, bool putLogs)
        {
            yield return string.Format("return mCore.{0};", pi.Name);
        }

        protected override IEnumerable<string> FormatSetter(PropertyInfo pi, bool putLogs)
        {
            yield return string.Format("mCore.{0} = value;", pi.Name);
        }

        protected override IEnumerable<string> FormatMethod(MethodInfo mi, bool putLogs)
        {
            yield return string.Format("{0}mCore.{1};", (mi.ReturnType != typeof(void)) ? " return " : "", SynchronizerFactory.PrintMethodInvocation(mi));
        }
    }
}
