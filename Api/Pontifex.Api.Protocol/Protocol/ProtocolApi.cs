using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;

namespace Pontifex.Api
{
    public abstract class ProtocolApi : ProtocolSubApi, IProtocol
    {
        public readonly C2SMessageDecl<DisconnectMessage> Disconnect = new C2SMessageDecl<DisconnectMessage>();

        private static readonly Type _subProtocolType = typeof(ProtocolSubApi); // SubProtocol is class, not interface!!!
        private static readonly Type _declarationType = typeof(IDeclaration);

        private readonly object _locker = new object();

        private IPipeSystem? _pipeSystem;
        private IDeclaration[]? _declarations;

        IDeclaration[] IProtocol.Declarations
        {
            get
            {
                if (_declarations == null)
                {
                    lock (_locker)
                    {
                        if (_declarations == null)
                        {
                            List<IDeclaration> list = new List<IDeclaration>();
                            EnumerateDeclarations(this, "", list);
                            _declarations = list.ToArray();
                            Array.Sort(_declarations, (left, right) => String.Compare(left.Name, right.Name, StringComparison.Ordinal));
                        }
                    }
                }
                return _declarations;
            }
        }

        public void Prepare(bool isServerMode, IPipeSystem pipeSystem)
        {
            if (Interlocked.CompareExchange(ref _pipeSystem, pipeSystem, null) == null)
            {
                IProtocol self = this;
                foreach (var decl in self.Declarations)
                {
                    decl.Prepare(isServerMode, pipeSystem);
                }
            }
        }

        public void Stop()
        {
            var pipeSystem = Interlocked.Exchange(ref _pipeSystem, null);
            if (pipeSystem != null)
            {
                pipeSystem.StopAll();
                IProtocol self = this;
                foreach (var decl in self.Declarations)
                {
                    decl.Stop();
                }
            }
        }

        private void EnumerateDeclarations(ProtocolSubApi root, string namePrefix, List<IDeclaration> declarations)
        {
            Type self = root.GetType();

            FieldInfo[] fields = self.GetFields(BindingFlags.Instance | BindingFlags.Public);
            foreach (var field in fields)
            {
                Type fieldType = field.FieldType;
                if (fieldType.IsSubclassOf(_subProtocolType))
                {
                    string newPrefix = namePrefix != "" ? (namePrefix + field.Name + ".") : (field.Name + ".");
                    EnumerateDeclarations((ProtocolSubApi)field.GetValue(root), newPrefix, declarations);
                }
                else if (_declarationType.IsAssignableFrom(fieldType))
                {
                    IDeclaration declaration = (IDeclaration)field.GetValue(root);
                    declaration.SetName(namePrefix + field.Name);
                    declarations.Add(declaration);
                }
            }
        }
    }
}
