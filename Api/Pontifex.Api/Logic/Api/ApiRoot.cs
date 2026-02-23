using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Pontifex.Api.Client;
using Pontifex.Api.Server;
using Pontifex.StopReasons;
using Pontifex.Utils.FSM;

namespace Pontifex.Api
{
    public abstract class ApiRoot : SubApi, IApiRootInternal
    {
        private enum State
        {
            Constructed, Started, ShuttingDown, Stopped
        }
        
        private readonly C2SMessageDecl<EmptyMessage> Disconnect = new C2SMessageDecl<EmptyMessage>();
        private readonly S2CMessageDecl<EmptyMessage> Kick = new S2CMessageDecl<EmptyMessage>();
        
        private static readonly Type _subProtocolType = typeof(SubApi); // SubProtocol is class, not interface!!!
        private static readonly Type _declarationType = typeof(IDeclaration);
        
        private IDeclaration[]? _declarations;

        private bool _isServerMode;
        private IPipeSystem? _pipeSystem;

        private readonly IFSM<State> _stage;
        
        private StopReason? _stopReason;

        public event Action? Connected;
        public event Action? Disconnecting;
        public event Action<StopReason>? Disconnected;
        
        
        IDeclaration[] IApiRootInternal.Declarations
        {
            get
            {
                if (_declarations == null)
                {
                    List<IDeclaration> list = new List<IDeclaration>();
                    EnumerateDeclarations(this, "", list);
                    var declarations = list.ToArray();
                    Array.Sort(declarations, (left, right) => String.Compare(left.Name, right.Name, StringComparison.Ordinal));

                    Interlocked.CompareExchange(ref _declarations, declarations, null);
                }
                return _declarations;
            }
        }

        protected ApiRoot()
        {
            var fsm = new FSM<State, int>(State.Constructed, state => (int)state);
            fsm.AddTransition(State.Constructed, State.Started);
            fsm.AddTransition(State.Started, State.ShuttingDown);
            fsm.AddTransition(State.Started, State.Stopped);
            fsm.AddTransition(State.ShuttingDown, State.Stopped);
            _stage = new ConcurrentFSM<State>(fsm); 
        }

        void IApiRoot.Start(bool isServerMode, IPipeSystem pipeSystem)
        {
            _stage.SetState(State.Started, (_, _) =>
            {
                _isServerMode = isServerMode;
                _pipeSystem = pipeSystem ?? throw new ArgumentNullException(nameof(pipeSystem));
                
                IApiRootInternal self = this;
                foreach (var decl in self.Declarations)
                {
                    decl.Start(isServerMode, pipeSystem);
                }

                if (isServerMode)
                {
                    Disconnect.SetProcessor(_ =>
                    {
                        Interlocked.CompareExchange(ref _stopReason, new GracefulRemoteIntention(ToString()), null);
                        ((IApiRoot)this).Stop();
                    });
                }
                else
                {
                    Kick.SetProcessor(_ =>
                    {
                        Interlocked.CompareExchange(ref _stopReason, new GracefulRemoteIntention(ToString()), null);
                        ((IApiRoot)this).Stop();
                    });
                }
                
                _pipeSystem.Start();
                Connected?.Invoke();
                return true;
            });
        }

        public void GracefulShutdown(TimeSpan? timeOut, string? errorMessage = null)
        {
            _stage.SetState(State.ShuttingDown, (_, _) =>
            {
                StopReason reason = errorMessage != null ? new TextFail(ToString(), errorMessage) : new UserIntention(ToString(), "GracefulShutdown");
                Interlocked.CompareExchange(ref _stopReason, reason, null);
                if (_isServerMode)
                {
                    ((ISender<EmptyMessage>)Kick).Send(new EmptyMessage());
                }
                else
                {
                    ((ISender<EmptyMessage>)Disconnect).Send(new EmptyMessage());
                }
                
                Disconnecting?.Invoke();
                _pipeSystem!.StopOutgoing();
                
                IApiRoot self = this;
                if (timeOut != null)
                {
                    Task.Run(async () =>
                    {
                        await Task.Delay(timeOut.Value);
                        self.Stop();
                    });
                }
                else
                {
                    self.Stop();
                }

                return true;
            });
        }

        void IApiRoot.Stop()
        {
            _stage.SetState(State.Stopped, (_, _) =>
            {
                var reason = _stopReason ?? new Unknown(ToString());
                Disconnected?.Invoke(reason);
                
                IApiRootInternal self = this;
                foreach (var decl in self.Declarations)
                {
                    decl.Stop();
                }
                
                _pipeSystem!.StopAll();
                _pipeSystem = null;
                return true;
            });
        }

        private void EnumerateDeclarations(SubApi root, string namePrefix, List<IDeclaration> declarations)
        {
            Type self = root.GetType();

            FieldInfo[] publicFields = self.GetFields(BindingFlags.Instance | BindingFlags.Public);
            FieldInfo[] internalFields = root is ApiRoot ? typeof(ApiRoot).GetFields(BindingFlags.Instance | BindingFlags.NonPublic) : Array.Empty<FieldInfo>();
            
            foreach (var field in internalFields.Concat(publicFields))
            {
                Type fieldType = field.FieldType;
                if (fieldType.IsSubclassOf(_subProtocolType))
                {
                    string newPrefix = namePrefix != "" ? (namePrefix + field.Name + ".") : (field.Name + ".");
                    EnumerateDeclarations((SubApi)field.GetValue(root), newPrefix, declarations);
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
