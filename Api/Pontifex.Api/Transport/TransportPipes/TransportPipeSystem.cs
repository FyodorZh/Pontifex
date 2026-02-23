using System;
using System.Collections.Generic;
using Actuarius.Memory;
using Archivarius;
using Pontifex.Utils;
using Pontifex.Utils.FSM;
using Scriba;

namespace Pontifex.Api
{
    public class TransportPipeSystem : IPipeSystem
    {
        private enum StartStopState
        {
            NotStarted,
            Started,
            Stopped,
        }
        
        private readonly ProtocolSerializer _serializer;
        private readonly ProtocolDeserializer _deserializer;
        private readonly IMemoryRental _memoryRental;
        private readonly ILogger Log;

        private readonly List<UnidirectionalRawPipeOut?> _rawPipeMap = new();

        private volatile bool _stopOutgoing;
        private readonly Func<UnionDataList, SendResult> _globalSender;

        private readonly IFSM<StartStopState> _state = new ConcurrentFSM<StartStopState>(
            new RatchetFSM<StartStopState>((l, r) => l.CompareTo(r), StartStopState.NotStarted)); 

        public TransportPipeSystem(Func<UnionDataList, SendResult> sender, IMemoryRental memoryRental, ILogger logger)
        {
            _serializer = new ProtocolSerializer();
            _deserializer = new ProtocolDeserializer();
            _memoryRental = memoryRental;
            Log = logger;
            
            _globalSender = data =>
            {
                if (_state.State != StartStopState.Started || _stopOutgoing)
                {
                    data.Release();
                    return SendResult.NotConnected;
                }
                return sender.Invoke(data);
            };
        }

        public bool OnReceived(UnionDataList data)
        {
            using var disposer = data.AsDisposable();
            
            if (_state.State != StartStopState.Started)
            {
                return false;
            }
            
            if (data.TryPopFirst(out short pipeId) && pipeId < _rawPipeMap.Count)
            {
                var pipe = _rawPipeMap[pipeId];
                if (pipe != null)
                {
                    return pipe.OnReceived(data.Acquire());
                }
            }

            return false;
        }
        
        #region IPipeAllocator
        
        public IUnidirectionalRawPipeIn AllocateRawPipeIn()
        {
            if (_state.State != StartStopState.NotStarted)
                throw new InvalidOperationException();

            var pipe =  new UnidirectionalRawPipeIn((short)_rawPipeMap.Count, _globalSender);
            _rawPipeMap.Add(null);
            return pipe;
        }

        public IUnidirectionalRawPipeOut AllocateRawPipeOut()
        {
            if (_state.State != StartStopState.NotStarted)
                throw new InvalidOperationException();

            var pipe = new UnidirectionalRawPipeOut();
            _rawPipeMap.Add(pipe);
            return pipe;
        }

        public IUnidirectionalModelPipeIn<TModel> AllocateModelPipeIn<TModel>() where TModel : struct, IDataStruct
        {
            return new UnidirectionalModelPipeIn<TModel>(AllocateRawPipeIn(), _serializer, _memoryRental, Log);
        }

        public IUnidirectionalModelPipeOut<TModel> AllocateModelPipeOut<TModel>() where TModel : struct, IDataStruct
        {
            return new UnidirectionalModelPipeOut<TModel>(AllocateRawPipeOut(), _deserializer);
        }

        public void Start()
        {
            _state.SetState(StartStopState.Started);
        }

        public void StopOutgoing()
        {
            _stopOutgoing = true;
        }

        public void StopAll()
        {
            _state.SetState(StartStopState.Stopped);
        }

        #endregion
    }
}