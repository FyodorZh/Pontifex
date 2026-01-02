using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace Pontifex.UserApi
{
    public abstract class SubProtocol
    {
        protected virtual void FillModelTypes(HashSet<Type> types)
        {
        }
    }

    internal interface IProtocol
    {
        IDeclaration[] Declarations { get; }
        ProtocolInfo GetInfo(IModelsHashDB modelHashes);
    }

    internal class ProtocolInfo
    {
        public readonly string Hash;
        public readonly Type[] FactoryModels;
        public readonly Type[] NonFactoryModels;

        public ProtocolInfo(Type[] factoryModels, Type[] nonFactoryModels, IModelsHashDB modelHashes)
        {
            StringBuilder sb = new StringBuilder(1024);
            for (int i = 0; i < factoryModels.Length; ++i)
            {
                AppendType(sb, factoryModels[i], modelHashes);
            }
            for (int i = 0; i < nonFactoryModels.Length; ++i)
            {
                AppendType(sb, nonFactoryModels[i], modelHashes);
            }

            Hash = Shared.MD5Helper.GetMd5Hash(sb.ToString());
            FactoryModels = factoryModels;
            NonFactoryModels = nonFactoryModels;
        }

        private void AppendType(StringBuilder sb, Type type, IModelsHashDB modelHashes)
        {
            if (type.IsGenericType)
            {
                type = type.GetGenericTypeDefinition();
            }
            string modelName = type.ToString();
            string hash;
            if (modelHashes.TryGetHash(type, out hash))
            {
                sb.Append(modelName + ":" + hash);
            }
            else
            {
                Log.e("Unknown hash for type '{0}'", modelName);
            }
        }
    }

    public abstract class Protocol : SubProtocol, IProtocol
    {
        public readonly C2SMessageDecl<DisconnectMessage> Disconnect = new C2SMessageDecl<DisconnectMessage>();

        private static readonly Type mSubProtocolType = typeof(SubProtocol); // SubProtocol is class, not interface!!!
        private static readonly Type mDeclarationType = typeof(IDeclaration);

        private static readonly Dictionary<Type, ProtocolInfo> mProtocolInfo = new Dictionary<Type, ProtocolInfo>();

        private readonly object mLocker = new object();
        private IDeclaration[] mDeclarations;

        private volatile ProtocolInfo mInfo;

        public static string GetProtocolHash<TProtocol>(IModelsHashDB hashDB)
            where TProtocol : Protocol, new()
        {
            return GetProtocolInfo<TProtocol>(hashDB).Hash;
        }

        internal static ProtocolInfo GetProtocolInfo<TProtocol>(IModelsHashDB hashDB)
            where TProtocol : Protocol, new()
        {
            IProtocol protocol = new TProtocol();
            return protocol.GetInfo(hashDB);
        }

        IDeclaration[] IProtocol.Declarations
        {
            get
            {
                if (mDeclarations == null)
                {
                    lock (mLocker)
                    {
                        if (mDeclarations == null)
                        {
                            List<IDeclaration> list = new List<IDeclaration>();
                            EnumerateDeclarations(this, "", list);
                            mDeclarations = list.ToArray();
                            Array.Sort(mDeclarations, (left, right) => String.Compare(left.Name, right.Name, StringComparison.Ordinal));
                        }
                    }
                }
                return mDeclarations;
            }
        }

        ProtocolInfo IProtocol.GetInfo(IModelsHashDB modelHashes)
        {
            if (mInfo == null)
            {
                lock (mProtocolInfo)
                {
                    Type curType = GetType();

                    ProtocolInfo info;
                    if (!mProtocolInfo.TryGetValue(curType, out info))
                    {
                        IProtocol self = this;
                        IDeclaration[] declarations = self.Declarations;

                        HashSet<Type> factoryModelSet = new HashSet<Type>();
                        HashSet<Type> nonFactoryModelSet = new HashSet<Type>();

                        for (int i = 0; i < declarations.Length; ++i)
                        {
                            declarations[i].FillFactoryModels(factoryModelSet);
                            declarations[i].FillNonFactoryModels(nonFactoryModelSet);
                        }
                        FillModelTypes(factoryModelSet);

                        Type[] factoryModels = new Type[factoryModelSet.Count];
                        factoryModelSet.CopyTo(factoryModels);
                        Array.Sort(factoryModels, (left, right) => String.Compare(left.FullName, right.FullName, StringComparison.Ordinal));

                        Type[] nonFactoryModels = new Type[nonFactoryModelSet.Count];
                        nonFactoryModelSet.CopyTo(nonFactoryModels);
                        Array.Sort(nonFactoryModels, (left, right) => String.Compare(left.FullName, right.FullName, StringComparison.Ordinal));


                        info = new ProtocolInfo(factoryModels, nonFactoryModels, modelHashes);
                        mProtocolInfo.Add(curType, info);
                    }

                    mInfo = info;
                }
            }

            return mInfo;
        }

        private void EnumerateDeclarations(SubProtocol root, string namePrefix, List<IDeclaration> declarations)
        {
            Type self = root.GetType();

            FieldInfo[] fields = self.GetFields(BindingFlags.Instance | BindingFlags.Public);
            foreach (var field in fields)
            {
                Type fieldType = field.FieldType;
                if (fieldType.IsSubclassOf(mSubProtocolType))
                {
                    string newPrefix = namePrefix != "" ? (namePrefix + field.Name + ".") : (field.Name + ".");
                    EnumerateDeclarations((SubProtocol)field.GetValue(root), newPrefix, declarations);
                }
                else if (mDeclarationType.IsAssignableFrom(fieldType))
                {
                    IDeclaration declaration = (IDeclaration)field.GetValue(root);
                    declaration.SetName(namePrefix + field.Name);
                    declarations.Add(declaration);
                }
            }
        }
    }
}
