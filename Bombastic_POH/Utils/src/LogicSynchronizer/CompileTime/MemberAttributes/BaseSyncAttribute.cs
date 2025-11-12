using Serializer.BinarySerializer;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace Shared.LogicSynchronizer
{
    public class BaseSyncAttribute : BaseSyncStrategyAttribute
    {
        /// <summary>
        /// Запрещает возможность работы метода/проперти в режиме ридонли (если false)
        /// </summary>
        public bool AllowReadonly { get; set; }

        public BaseSyncAttribute()
        {
            AllowReadonly = true;
        }

        private static string StreamFieldName(MemberInfo mi)
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
                return string.Format("m{0}_{1}_Stream", mi.Name, fix);
            }
            return string.Format("m{0}_Stream", mi.Name);
        }

        public override IEnumerable<string> AdditionalFields(MemberInfo mi)
        {
            yield return string.Format("private readonly {0} {1};", SynchronizerFactory.FullName(typeof(StreamId)), StreamFieldName(mi));
        }

        public override IEnumerable<string> AppendConstructor(MemberInfo mi, bool putLogs)
        {
            yield return StreamFieldName(mi) + " = Context.NewDataStream(reader => {";
            if (mi is PropertyInfo)
            {
                if (putLogs || Log)
                {
                    yield return string.Format("    Log.i(\"Received {{0}}.{0} {{set;}}\", Context.Name);", mi.Name);
                }
                foreach (var line in Pop(((PropertyInfo)mi).PropertyType, "mCore." + mi.Name, 0))
                {
                    yield return "    " + line;
                }
            }
            else if (mi is MethodInfo)
            {
                MethodInfo m = mi as MethodInfo;
                foreach (var param in m.GetParameters())
                {
                    yield return string.Format("    {0} {1};", SynchronizerFactory.FullName(param.ParameterType), param.Name);
                    foreach (var line in Pop(param.ParameterType, param.Name, 0))
                    {
                        yield return "    " + line;
                    }
                }
                if (putLogs || Log)
                {
                    int paramsNumber = m.GetParameters().Length;
                    string placeholders = "";
                    for (int i = 1; i <= paramsNumber; ++i)
                    {
                        placeholders += " {" + i + "}";
                    }
                    string paramsValues = SynchronizerFactory.PrintMethodParams(m, false);
                    if (paramsValues != "")
                    {
                        paramsValues = ", " + paramsValues;
                    }
                    yield return string.Format("    Log.i(\"Received {{0}}.{0}({1})\", Context.Name{2});", mi.Name, placeholders, paramsValues);
                }
                yield return string.Format("    mCore.{0}({1});", m.Name, SynchronizerFactory.PrintMethodParams(m, false));
            }
            else
            {
                yield return "    // code here";
            }
            yield return "});";
        }

        protected override IEnumerable<string> FormatGetter(PropertyInfo pi, bool putLogs)
        {
            yield return string.Format("return mCore.{0};", pi.Name);
        }

        protected override IEnumerable<string> FormatSetter(PropertyInfo pi, bool putLogs)
        {
            yield return string.Format("using (var scope = Context.Send({0}))", StreamFieldName(pi));
            yield return "{";
            foreach (var cmd in Push(pi.PropertyType, "value"))
            {
                yield return cmd;
            }
            if (putLogs || Log)
            {
                yield return string.Format("    Log.i(\"Sent {{0}}.{0} {{set;}}\", Context.Name);", pi.Name);
            }
            yield return "}";
            yield return string.Format("mCore.{0} = value;", pi.Name);
        }

        protected override IEnumerable<string> FormatMethod(MethodInfo mi, bool putLogs)
        {
            yield return string.Format("using (var scope = Context.Send({0}))", StreamFieldName(mi));
            yield return "{";
            foreach (var param in mi.GetParameters())
            {
                foreach (var cmd in Push(param.ParameterType, param.Name))
                {
                    yield return cmd;
                }
            }
            if (putLogs || Log)
            {
                int paramsNumber = mi.GetParameters().Length;
                string placeholders = "";
                for (int i = 1; i <= paramsNumber; ++i)
                {
                    placeholders += " {" + i + "}";
                }
                string paramsValues = SynchronizerFactory.PrintMethodParams(mi, false);
                if (paramsValues != "")
                {
                    paramsValues = ", " + paramsValues;
                }
                yield return string.Format("    Log.i(\"Sent {{0}}.{0}({1})\", Context.Name{2});", mi.Name, placeholders, paramsValues);
            }
            yield return "}";
            yield return string.Format("{0}mCore.{1};", (mi.ReturnType != typeof(void)) ? "return " : "", SynchronizerFactory.PrintMethodInvocation(mi));
        }

        protected virtual IEnumerable<string> Push(Type type, string value)
        {
            if (type.IsPrimitive && type != typeof(IntPtr) && type != typeof(UIntPtr))
            {
                yield return string.Format("    scope.Writer.Add{0}({1});", type.Name, value);
            }
            else if (type == typeof(string))
            {
                yield return string.Format("    scope.Writer.WriteString({0});", value);
            }
            else if (typeof(IDataStruct).IsAssignableFrom(type))
            {
                if (type.IsInterface || (type.IsClass && !type.IsSealed))
                {
                    yield return string.Format("    scope.Writer.Add(ref {0});", value);
                }
                else if (type.IsClass && type.IsSealed)
                {
                    yield return string.Format("    scope.Writer.Add(ref {0});", value);
                }
                else
                {
                    yield return string.Format("    scope.Writer.Write({0});", value);
                }
            }
            else if (type.IsArray)
            {
                var elementType = type.GetElementType();
                if (elementType.IsInterface)
                {
                    throw new InvalidOperationException("Can't synchronize array of " + SynchronizerFactory.FullName(type));
                }
                if (elementType.IsClass && !elementType.IsSealed)
                {
                    throw new InvalidOperationException("Can't synchronize array of not sealed classes " + SynchronizerFactory.FullName(type));
                }
                if (typeof(IDataStruct).IsAssignableFrom(elementType))
                {
                    yield return string.Format("    scope.Writer.WriteArray({0});", value);
                }
                else
                {
                    yield return string.Format("    scope.Writer.Add(ref {0});", value);
                }
            }
            else if (type.IsEnum)
            {
                Type enumType = Enum.GetUnderlyingType(type);
                foreach (var line in Push(enumType, string.Format("({0})({1})", enumType.FullName, value)))
                {
                    yield return line;
                }
            }
            else
            {
                throw new Exception("Can't write type: " + SynchronizerFactory.FullName(type));
                //yield return "// " + SynchronizerFactory.FullName(type);
            }
        }

        protected virtual IEnumerable<string> Pop(Type type, string value, int depth)
        {
            yield return "{";

            if (type.IsPrimitive && type != typeof(IntPtr) && type != typeof(UIntPtr))
            {
                yield return string.Format("    {0} = reader.Read{1}();", value, type.Name);
            }
            else if (type == typeof(string))
            {
                yield return string.Format("    {0} = reader.ReadString();", value);
            }
            else if (typeof(IDataStruct).IsAssignableFrom(type))
            {
                if (type.IsInterface || type.IsClass)
                {
                    yield return string.Format("    {0} = null;", value);
                    yield return string.Format("    reader.Add(ref {0});", value);
                }
                else
                {
                    yield return string.Format("    {0} = reader.Read<{1}>();", value, SynchronizerFactory.FullName(type));
                }
            }
            else if (type.IsArray)
            {
                var elementType = type.GetElementType();
                if (elementType.IsInterface)
                {
                    throw new InvalidOperationException("Can't synchronize interface " + SynchronizerFactory.FullName(type));
                }
                if (elementType.IsClass && !elementType.IsSealed)
                {
                    throw new InvalidOperationException("Can't synchronize not sealed class " + SynchronizerFactory.FullName(type));
                }

                if (typeof(IDataStruct).IsAssignableFrom(elementType))
                {
                    yield return string.Format("    {0} = reader.ReadArray<{1}>();", value, SynchronizerFactory.FullName(elementType));
                }
                else
                {
                    yield return string.Format("    reader.Add(ref {0});", value);
                }
            }
            else
            {
                if (type.IsEnum)
                {
                    Type enumType = Enum.GetUnderlyingType(type);

                    yield return string.Format("    {0} enumVal;", enumType.FullName);
                    foreach (var line in Pop(enumType, "enumVal", depth + 1))
                    {
                        yield return "    " + line;
                    }
                    yield return string.Format("    {0} = ({1})enumVal;", value, SynchronizerFactory.FullName(type));
                }
                else
                {
                    throw new Exception("Can't read type: " + SynchronizerFactory.FullName(type));
                }
            }

            yield return "}";
        }
    }
}
