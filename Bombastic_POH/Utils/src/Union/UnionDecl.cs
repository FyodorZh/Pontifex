using System;
using System.Collections.Generic;
using System.Reflection;

namespace Shared.Union
{
    public class UnionDecl : IUnionDecl
    {
        private readonly List<SubStructInfo> mSubTypes = new List<SubStructInfo>();
        private readonly List<PropertyInfo> mProperties = new List<PropertyInfo>();
        private readonly List<MethodDeclaration> mMethods = new List<MethodDeclaration>();
        private readonly List<Type> mInterfaces = new List<Type>();

        public UnionDecl(string typeName)
        {
            TypeName = typeName;
        }

        public void Append(SubStructInfo subStruct)
        {
            mSubTypes.Add(subStruct);
        }

        public void Append(PropertyInfo prop)
        {
            mProperties.Add(prop);
        }

        public void Append(MethodInfo mi)
        {
            mMethods.Add(new MethodDeclaration(mi));
        }

        public void AppendInterface(Type iType)
        {
            if (iType != null && iType.IsInterface)
            {
                mInterfaces.Add(iType);
                foreach (var prop in iType.GetProperties())
                {
                    Append(prop);
                }
                foreach (var method in iType.GetMethods())
                {
                    if (!method.IsSpecialName)
                    {
                        Append(method);
                    }
                }
            }
        }

        public MethodDeclaration Find(MethodInfo mi)
        {
            return mMethods.Find((decl) => decl.Method == mi);
        }

        public string TypeName { get; set; }

        public IEnumerable<SubStructInfo> SubTypes
        {
            get { return mSubTypes; }
        }

        public IEnumerable<PropertyInfo> PropertiesToImplement
        {
            get
            {
                foreach (var element in mProperties)
                {
                    yield return element;
                }
            }
        }

        public IEnumerable<IMethodDeclaration> MethodsToImplement
        {
            get
            {
                foreach (var element in mMethods)
                {
                    yield return element;
                }
            }
        }

        public IEnumerable<Type> InterfacesToImplement
        {
            get { return mInterfaces; }
        }
    }
}