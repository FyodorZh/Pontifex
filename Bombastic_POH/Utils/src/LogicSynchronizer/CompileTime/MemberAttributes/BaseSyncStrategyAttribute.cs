using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace Shared.LogicSynchronizer
{
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public abstract class BaseSyncStrategyAttribute : Attribute
    {
        /// <summary>
        /// Позволяет сделать имплементацию метода/проперти виртуальной
        /// </summary>
        public bool IsVirtual { get; set; }

        /// <summary>
        /// Внедряет логи в код синхронизации
        /// </summary>
        public virtual bool Log { get; set; }

        protected abstract IEnumerable<string> FormatGetter(PropertyInfo pi, bool putLogs);
        protected abstract IEnumerable<string> FormatSetter(PropertyInfo pi, bool putLogs);
        protected abstract IEnumerable<string> FormatMethod(MethodInfo mi, bool putLogs);

        public virtual IEnumerable<string> AdditionalFields(MemberInfo mi)
        {
            yield break;
        }

        public virtual IEnumerable<string> AppendConstructor(MemberInfo mi, bool putLogs)
        {
            yield break;
        }

        public void FormatMember(MemberInfo member, bool checkForReadOnlyFlag, int baseTab, StringBuilder dst)
        {
            PropertyInfo pi = member as PropertyInfo;
            MethodInfo mi = member as MethodInfo;

            if (pi != null)
            {
                dst.Append(SynchronizerFactory.NL);

                string typeName = SynchronizerFactory.FullName(pi.PropertyType);
                string propName = pi.Name;
                dst.AppendFormat("{0}{1} {2}.{3}"
                              + "{0}{{", SynchronizerFactory.Tab[baseTab], typeName, SynchronizerFactory.FullName(pi.DeclaringType), propName);
                if (pi.CanRead)
                {
                    dst.AppendFormat("{0}get"
                                  + "{0}{{", SynchronizerFactory.Tab[baseTab + 1]);
                    foreach (var line in FormatGetter(pi, Log))
                    {
                        dst.AppendFormat("{0}{1}", SynchronizerFactory.Tab[baseTab + 2], line);
                    }
                    dst.AppendFormat("{0}}}", SynchronizerFactory.Tab[baseTab + 1]);
                }
                if (pi.CanWrite)
                {
                    dst.AppendFormat("{0}set"
                                  + "{0}{{", SynchronizerFactory.Tab[baseTab + 1]);
                    int tabId = baseTab + 2;
                    if (checkForReadOnlyFlag)
                    {
                        dst.AppendFormat("{0}if (!IsReadOnly)", SynchronizerFactory.Tab[baseTab + 2]);
                        dst.AppendFormat("{0}{{", SynchronizerFactory.Tab[baseTab + 2]);
                        tabId += 1;
                    }
                    foreach (var line in FormatSetter(pi, Log))
                    {
                        dst.AppendFormat("{0}{1}", SynchronizerFactory.Tab[tabId], line);
                    }
                    if (checkForReadOnlyFlag)
                    {
                        dst.AppendFormat("{0}}}", SynchronizerFactory.Tab[baseTab + 2]);
                    }
                    dst.AppendFormat("{0}}}", SynchronizerFactory.Tab[baseTab + 1]);
                }
                dst.AppendFormat("{0}}}", SynchronizerFactory.Tab[baseTab]);
            }
            if (mi != null && !mi.IsSpecialName)
            {
                dst.Append(SynchronizerFactory.NL);
                dst.AppendFormat("{0}{1}{0}{{", SynchronizerFactory.Tab[baseTab], SynchronizerFactory.PrintMethodDeclaration(mi, IsVirtual ? SynchronizerFactory.MethodDeclType.Virtual : SynchronizerFactory.MethodDeclType.Explicit));
                int tabId = baseTab + 1;
                if (checkForReadOnlyFlag)
                {
                    dst.AppendFormat("{0}if (!IsReadOnly)", SynchronizerFactory.Tab[baseTab + 1]);
                    dst.AppendFormat("{0}{{", SynchronizerFactory.Tab[baseTab + 1]);
                    tabId += 1;
                }
                foreach (var line in FormatMethod(mi, Log))
                {
                    dst.AppendFormat("{0}{1}", SynchronizerFactory.Tab[tabId], line);
                }
                if (checkForReadOnlyFlag)
                {
                    dst.AppendFormat("{0}}}", SynchronizerFactory.Tab[baseTab + 1]);
                }
                dst.AppendFormat("{0}}}", SynchronizerFactory.Tab[baseTab]);
            }
        }
    }
}
