using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using Serializer.BinarySerializer;

namespace Shared.LogicSynchronizer
{
    public static class SynchronizerFactory
    {
        public const string NL = "\r\n";

        public static readonly string[] Tab;

        static SynchronizerFactory()
        {
            int n = 20; // максимальная вложеность табуляции
            Tab = new string[n];
            Tab[0] = NL;
            for (int i = 1; i < n; ++i)
            {
                Tab[i] = Tab[i - 1] + "    ";
            }
        }

        public class TypeKeyPair
        {
            public Type WrapperType { get; set; }
            public Type KeyType { get; set; }

            public TypeKeyPair(Type wrapperType, Type keyType)
            {
                WrapperType = wrapperType;
                KeyType = keyType;
            }

            public override int GetHashCode()
            {
                return WrapperType.GetHashCode();
            }
        }

        public class TypeKeyPair<TIWrapper, TKey> : TypeKeyPair
            where TKey : IDataStruct, IEquatable<TKey>
        {
            public TypeKeyPair()
            : base(typeof(TIWrapper), typeof(TKey))
            {
            }
        }

        public static Dictionary<Type, string> Build(string @namespace, params TypeKeyPair[] rootLogicTypes)
        {
            Dictionary<Type, TypeKeyPair> rootTypes = new Dictionary<Type, TypeKeyPair>();

            foreach (var rootLogicType in rootLogicTypes)
            {
                if (!rootLogicType.WrapperType.IsInterface)
                {
                    Log.e("Skip root type " + rootLogicType.WrapperType);
                    continue;
                }

                rootTypes.Add(rootLogicType.WrapperType, rootLogicType);
                {
                    Queue<Type> queue = new Queue<Type>();
                    queue.Enqueue(rootLogicType.WrapperType);

                    while (queue.Count > 0)
                    {
                        Type root = queue.Dequeue();

                        foreach (var type in ReferencedTypes(root))
                        {
                            if (!rootTypes.ContainsKey(type))
                            {
                                rootTypes.Add(type, new TypeKeyPair(type, rootLogicType.KeyType));
                                queue.Enqueue(type);
                            }
                        }
                    }
                }
            }

            Dictionary<Type, string> result = new Dictionary<Type, string>();

            foreach (var type in rootTypes)
            {
                Type groundWrapperType = typeof(Wrapper<>).MakeGenericType(type.Value.KeyType);
                Type baseWrapperType = groundWrapperType;
                foreach (var wrapperType in WrapperStack(type.Key))
                {
                    if (!result.ContainsKey(wrapperType))
                    {
                        string code = BuildWrapper(wrapperType, baseWrapperType, @namespace, wrapperType == type.Key, type.Value.KeyType, groundWrapperType);
                        result.Add(wrapperType, code);
                    }
                    baseWrapperType = wrapperType;
                }
            }

            return result;
        }

        private static List<Type> WrapperStack(Type finalType)
        {
            List<Type> subTypes = new List<Type>();
            subTypes.Add(finalType);

            foreach (var subType in finalType.FindInterfaces((type, cr) => type.GetCustomAttributes(typeof(WrapperAttribute), false).Length > 0, null))
            {
                subTypes.Add(subType);
            }

            subTypes.Sort((l, r) =>
            {
                if (l == r)
                    return 0;
                if (l.IsAssignableFrom(r))
                    return -1;
                return 1;
            });

            for (int i = 1; i < subTypes.Count; ++i)
            {
                if (!subTypes[i - 1].IsAssignableFrom(subTypes[i]))
                {
                    throw new Exception("Invalid type hierarchy: " + finalType);
                }
            }

            return subTypes;
        }

        private static Type[] ReferencedTypes(Type finalType)
        {
            HashSet<Type> types = new HashSet<Type>();
            foreach (var member in GetInterfaceMembers(finalType))
            {
                PropertyInfo prop = member as PropertyInfo;
                if (prop != null)
                {
                    if (prop.CanRead && prop.PropertyType.GetCustomAttributes(typeof(WrapperAttribute), false).Length > 0)
                    {
                        types.Add(prop.PropertyType);
                    }
                }
            }

            Type[] res = new Type[types.Count];
            types.CopyTo(res);
            return res;
        }

        private static IEnumerable<MemberInfo> GetInterfaceMembers(Type interfaceType)
        {
            HashSet<MemberInfo> set = new HashSet<MemberInfo>();
            if (interfaceType.IsInterface)
            {
                foreach (var member in interfaceType.GetMembers())
                {
                    set.Add(member);
                }

                foreach (var sub in interfaceType.GetInterfaces())
                {
                    foreach (var member in sub.GetMembers())
                    {
                        set.Add(member);
                    }
                }
            }
            return set;
        }

        private static BaseSyncStrategyAttribute GetMemberAttribute(MemberInfo member)
        {
            var list = member.GetCustomAttributes(typeof(BaseSyncStrategyAttribute), true);
            if (list.Length > 0)
            {
                return (BaseSyncStrategyAttribute)list[0];
            }
            else
            {
                return new NoSyncAttribute();
            }
        }

        private class MembersComparer : IComparer<MemberInfo>
        {
            public int Compare(MemberInfo x, MemberInfo y)
            {
                if (x == y)
                {
                    return 0;
                }

                var tx = x.DeclaringType;
                var ty = y.DeclaringType;
                if (tx != ty)
                {
                    if (tx.IsAssignableFrom(ty))
                    {
                        return -1;
                    }
                    if (ty.IsAssignableFrom(tx))
                    {
                        return 1;
                    }
                }
                int cmp = TypePriority(x).CompareTo(TypePriority(y));
                if (cmp != 0)
                {
                    return cmp;
                }

                cmp = String.Compare(x.Name, y.Name, StringComparison.Ordinal);
                if (cmp != 0)
                {
                    return cmp;
                }

                cmp = String.Compare(x.ToString(), y.ToString(), StringComparison.Ordinal);
                if (cmp == 0)
                {
                    throw new Exception();
                }
                return cmp;
            }

            private int TypePriority(MemberInfo mi)
            {
                if (mi is EventInfo)
                    return 0;
                if (mi is PropertyInfo)
                    return 1;
                if (mi is MethodInfo)
                    return 2;
                return 3;
            }
        }

        private class Comparer : IComparer<KeyValuePair<MemberInfo, BaseSyncStrategyAttribute>>
        {
            private readonly MembersComparer cmp = new MembersComparer();

            public int Compare(KeyValuePair<MemberInfo, BaseSyncStrategyAttribute> x, KeyValuePair<MemberInfo, BaseSyncStrategyAttribute> y)
            {
                return cmp.Compare(x.Key, y.Key);
            }
        }

        private static List<KeyValuePair<MemberInfo, BaseSyncStrategyAttribute>> ListMembersToImplement(Type interfaceType, Type baseType)
        {
            var res = new List<KeyValuePair<MemberInfo, BaseSyncStrategyAttribute>>();
            foreach (var member in GetInterfaceMembers(interfaceType))
            {
                if (!member.DeclaringType.IsAssignableFrom(baseType))
                {
                    res.Add(new KeyValuePair<MemberInfo, BaseSyncStrategyAttribute>(member, GetMemberAttribute(member)));
                }
            }

            res.Sort(new Comparer());
            return res;
        }

        private static string BuildWrapper(Type interfaceType, Type baseType, string @namespace, bool isRootType, Type keyType, Type groundWrapperType)
        {
            WrapperAttribute wrapperAttribute = (WrapperAttribute)interfaceType.GetCustomAttributes(typeof(WrapperAttribute), false)[0];

            StringBuilder sb = new StringBuilder();

            sb.AppendFormat("using Shared.LogicSynchronizer;{0}", NL);
            if (!string.IsNullOrEmpty(@namespace))
            {
                sb.AppendFormat("namespace {0}", @namespace);
                sb.AppendFormat("{0}{{", Tab[0]);
            }

            string baseTypeName;
            if (baseType.IsInterface)
            {
                baseTypeName = @namespace + ".Wrapper" + Name(baseType);
            }
            else
            {
                baseTypeName = NonGenericName(baseType);
            }
            if (isRootType)
            {
                baseTypeName += "<" + FullName(keyType) + ">";
            }
            else
            {
                baseTypeName += "<TKey>";
            }

            string typeName = "Wrapper" + NonGenericName(interfaceType);
            string typeFullName = typeName;
            if (!isRootType)
            {
                typeFullName += "<TKey>";
            }

            List<KeyValuePair<MemberInfo, BaseSyncStrategyAttribute>> members = ListMembersToImplement(interfaceType, baseType);

            string contextTypeName = isRootType ? FullName(typeof(ISyncContextViewForWrapper<>).MakeGenericType(keyType)) : "ISyncContextViewForWrapper<TKey>";

            sb.AppendFormat("{0}public class {1} : {2}, {3}" +
                            "{0}{{", Tab[1], typeFullName, baseTypeName, FullName(interfaceType));
            sb.AppendFormat("{0}private readonly {1} mCore;{2}", Tab[2], FullName(interfaceType), NL);

            foreach (var kv in members)
            {
                foreach (var line in kv.Value.AdditionalFields(kv.Key))
                {
                    sb.AppendFormat("{0}{1}", Tab[2], line);
                }
            }

            bool canBeReadonly = wrapperAttribute.CanBeReadOnly;

            sb.AppendFormat("{0}public {1}({2} context, {3} core{4})", Tab[2], typeName, contextTypeName, FullName(interfaceType),
                            canBeReadonly ? ", System.Func<bool> readOnly = null" : "");
            sb.AppendFormat("{0}    : base(context{1}{2})", Tab[2], groundWrapperType != baseType ? ", core" : "", canBeReadonly ? ", readOnly" : "");
            sb.AppendFormat("{0}{{" +
                            "{0}    mCore = core;", Tab[2]);

            foreach (var kv in members)
            {
                foreach (var line in kv.Value.AppendConstructor(kv.Key, wrapperAttribute.Log))
                {
                    sb.AppendFormat("{0}{1}", Tab[3], line);
                }
            }
            sb.AppendFormat("{0}}}", Tab[2]);

            foreach (var kv in members)
            {
                BaseSyncStrategyAttribute attr = kv.Value;

                bool checkForReadOnlyFlag;
                {
                    BaseSyncAttribute syncAttribute = attr as BaseSyncAttribute;
                    if (syncAttribute != null)
                    {
                        checkForReadOnlyFlag = canBeReadonly && syncAttribute.AllowReadonly;
                    }
                    else
                    {
                        checkForReadOnlyFlag = false;
                    }
                }

                attr.FormatMember(kv.Key, checkForReadOnlyFlag, 2, sb);

                EventInfo ei = kv.Key as EventInfo;
                if (ei != null)
                {
                    sb.Append(NL);
                    string _typeName = FullName(ei.EventHandlerType);
                    sb.AppendFormat("{0}event {1} {2}.{3}"
                                  + "{0}{{"
                                  + "{0}    add {{ mCore.{3} += value; }}"
                                  + "{0}    remove {{ mCore.{3} -= value; }}"
                                  + "{0}}}"
                                  , Tab[2], _typeName, FullName(ei.DeclaringType), ei.Name);
                }
            }

            sb.AppendFormat("{0}}}", Tab[1]);

            if (isRootType)
            {
                sb.Append(NL);
                sb.AppendFormat(  "{0}public static class {1}Extension"
                                + "{0}{{"
                                + "{0}    public static bool CanModify(this {2} obj, ICauserChecker<{3}> causer)"
                                + "{0}    {{"
                                + "{0}        var wrapper = obj as {4};"
                                + "{0}        if (wrapper != null)"
                                + "{0}        {{"
                                + "{0}            return causer.Check(wrapper.Key);"
                                + "{0}        }}"
                                + "{0}        return true;"
                                + "{0}    }}"
                                + "{0}}}", Tab[1], typeFullName, FullName(interfaceType), FullName(keyType), typeFullName);
            }

            if (!string.IsNullOrEmpty(@namespace))
            {
                sb.AppendFormat("{0}}}", Tab[0]);
            }

            return sb.ToString();
        }

        public static string PrintMethodParams(MethodInfo mi, bool writeTypes)
        {
            string res = "";

            var parameters = mi.GetParameters();
            for (int i = 0; i < parameters.Length; ++i)
            {
                var param = parameters[i];
                if (i != 0)
                {
                    res += ", ";
                }

                if (param.ParameterType.IsByRef)
                {
                    if (param.IsOut)
                    {
                        res += "out ";
                    }
                    else
                    {
                        res += "ref ";
                    }
                }

                if (writeTypes)
                {
                    var type = param.ParameterType;
                    if (type.IsByRef)
                    {
                        type = type.GetElementType();
                    }

                    res += FullName(type) + " ";
                }

                res += param.Name;
            }
            return res;
        }

        public static string FullName(Type type)
        {
            if (type == typeof(void))
            {
                return "void";
            }

            if (type.IsNested)
            {
                return FullName(type.DeclaringType) + "." + type.Name;
            }

            string name = Name(type);
            if (type.Namespace != "")
            {
                name = type.Namespace + "." + name;
            }

            return name;
        }

        public static string NonGenericName(Type type)
        {
            if (type == typeof(void))
            {
                return "void";
            }

            string name = type.Name;

            if (type.IsByRef && name.EndsWith("&"))
            {
                name = name.Substring(0, name.Length - 1);
            }

            if (type.IsGenericType)
            {
                name = name.Substring(0, name.IndexOf('`'));
            }

            return name;
        }

        public static string Name(Type type)
        {
            if (type == typeof(void))
            {
                return "void";
            }

            string name = type.Name;

            if (type.IsByRef && name.EndsWith("&"))
            {
                name = name.Substring(0, name.Length - 1);
            }

            if (type.IsGenericType)
            {
                name = name.Substring(0, name.IndexOf('`'));

                name += "<";
                var list = type.GetGenericArguments();
                for (int i = 0; i < list.Length; ++i)
                {
                    if (i != 0)
                    {
                        name += ", ";
                    }

                    name += FullName(list[i]);
                }

                name += ">";
            }
            return name;
        }

        public static string MethodName(MethodInfo method)
        {
            string name = method.Name;
            if (method.IsGenericMethodDefinition)
            {
                name += "<";
                var list = method.GetGenericArguments();
                for (int i = 0; i < list.Length; ++i)
                {
                    if (i != 0)
                    {
                        name += ", ";
                    }
                    name += list[i].Name;
                }
                name += ">";
            }
            return name;
        }

        public static IEnumerable<string> PrintMethodConstraints(MethodInfo method)
        {
            var list = method.GetGenericArguments();
            for (int i = 0; i < list.Length; ++i)
            {
                List<string> constraints = new List<string>();

                var attrs = list[i].GenericParameterAttributes;
                if ((attrs & GenericParameterAttributes.NotNullableValueTypeConstraint) != 0)
                {
                    constraints.Add("struct");
                }
                if ((attrs & GenericParameterAttributes.ReferenceTypeConstraint) != 0)
                {
                    constraints.Add("class");
                }

                foreach (var t in list[i].GetGenericParameterConstraints())
                {
                    constraints.Add(FullName(t));
                }

                if ((attrs & GenericParameterAttributes.DefaultConstructorConstraint) != 0)
                {
                    constraints.Add("new()");
                }

                string res = "where " + list[i].Name + ": ";
                if (constraints.Count > 0)
                {
                    for (int j = 0; j < constraints.Count; ++j)
                    {
                        if (j != 0)
                        {
                            res += ", ";
                        }

                        res += constraints[j];
                    }
                }
                yield return res;
            }
        }

        public static string PrintMethodInvocation(MethodInfo method)
        {
            return string.Format("{0}({1})", MethodName(method), PrintMethodParams(method, false));
        }

        public enum MethodDeclType
        {
            Implicit,
            Explicit,
            Virtual,
        }

        public static string PrintMethodDeclaration(MethodInfo method, MethodDeclType declType)
        {
            string returnType = method.ReturnType.IsGenericParameter ? method.ReturnType.Name : FullName(method.ReturnType);
            string res;

            switch (declType)
            {
                case MethodDeclType.Virtual:
                case MethodDeclType.Implicit:
                    if (method.DeclaringType == null || !method.DeclaringType.IsInterface)
                    {
                        throw new Exception();
                    }
                    res = string.Format("{0} {1}({2})", returnType, MethodName(method), PrintMethodParams(method, true));
                    foreach (var constraint in PrintMethodConstraints(method))
                    {
                        res += " " + constraint;
                    }
                    if (declType == MethodDeclType.Virtual)
                    {
                        res = "public virtual " + res;
                    }
                    else
                    {
                        res = "public " + res;
                    }
                    break;
                case MethodDeclType.Explicit:
                    res = string.Format("{0} {1}.{2}({3})", returnType, FullName(method.DeclaringType), MethodName(method), PrintMethodParams(method, true));
                    break;
                default:
                    throw new Exception();
            }

            return res;
        }
    }
}
