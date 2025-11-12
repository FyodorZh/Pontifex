using System.Collections.Generic;
using System.Reflection;

namespace Shared.LogicSynchronizer
{
    public class NoUsageAttribute : BaseSyncStrategyAttribute
    {
        protected override IEnumerable<string> FormatGetter(PropertyInfo pi, bool putLogs)
        {
            yield return "throw new System.InvalidOperationException(\"Operation is not supported by this wrapper\");";
        }

        protected override IEnumerable<string> FormatSetter(PropertyInfo pi, bool putLogs)
        {
            yield return "throw new System.InvalidOperationException(\"Operation is not supported by this wrapper\");";
        }

        protected override IEnumerable<string> FormatMethod(MethodInfo mi, bool putLogs)
        {
            yield return "throw new System.InvalidOperationException(\"Operation is not supported by this wrapper\");";
        }
    }
}
