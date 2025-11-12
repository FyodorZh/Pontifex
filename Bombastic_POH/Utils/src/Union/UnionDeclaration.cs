using System.Collections.Generic;
using System.Reflection;

namespace Shared.Union
{
    public struct SubStructInfo
    {
        public readonly string TypeName;
        public readonly string FieldName;

        public SubStructInfo(string typeName, string fieldName)
        {
            TypeName = typeName;
            FieldName = fieldName;
        }
    }

    public interface IMethodDeclaration
    {
        MethodInfo Method { get; }
        IEnumerable<string> AppendBefore();
        IEnumerable<string> AppendAfter();
    }

    public interface IUnionDecl
    {
        string TypeName { get; }
        IEnumerable<SubStructInfo> SubTypes { get; }
        IEnumerable<PropertyInfo> PropertiesToImplement { get; }
        IEnumerable<IMethodDeclaration> MethodsToImplement { get; }
        IEnumerable<System.Type> InterfacesToImplement { get; }
    }

    public class MethodDeclaration : IMethodDeclaration
    {
        private string[] mAppendBefore;
        private string[] mAppendAfter;

        public MethodInfo Method { get; private set; }

        public MethodDeclaration(MethodInfo mi)
        {
            Method = mi;
            Setup(null, null);
        }

        public void Setup(string[] appendBefore, string[] appendAfter)
        {
            mAppendBefore = appendBefore ?? new string[0];
            mAppendAfter = appendAfter ?? new string[0];
        }

        public IEnumerable<string> AppendBefore()
        {
            return mAppendBefore;
        }

        public IEnumerable<string> AppendAfter()
        {
            return mAppendAfter;
        }
    }
}