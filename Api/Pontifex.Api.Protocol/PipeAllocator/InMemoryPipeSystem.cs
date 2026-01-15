using System;
using System.Collections.Generic;
using Actuarius.Memory;
using Archivarius;
using Pontifex.Utils;

namespace Pontifex.Api
{
    public class InMemoryPipeSystem
    {
        private readonly List<ITransportPipe> _pipes = new();
        private readonly int[] _counts = new[] { 0, 0 };
        
        private bool _isStopped;
        
        public IPipeSystem Side1 { get; }
        public IPipeSystem Side2 { get; }

        public InMemoryPipeSystem()
        {
            Side1 = new PipeSystemSide(this, 0);
            Side2 = new PipeSystemSide(this, 1);
        }

        private void StopAll()
        {
            _isStopped = true;
        }

        private UnidirectionalRawPipe AllocateRawPipe(int sideId)
        {
            int otherSide = 1 - sideId;
            if (_counts[otherSide] > _counts[sideId])
            {
                if (_pipes[_counts[sideId]] is not UnidirectionalRawPipe pipe)
                {
                    throw new InvalidOperationException();
                }

                _counts[sideId]++;
                return pipe;
            }

            var result = new UnidirectionalRawPipe(this);
            _pipes.Add(result);
            _counts[sideId]++;
            return result;
        }

        private UnidirectionalModelPipe<TModel> AllocateModelPipe<TModel>(int sideId)
            where TModel : struct, IDataStruct
        {
            int otherSide = 1 - sideId;
            if (_counts[otherSide] > _counts[sideId])
            {
                if (_pipes[_counts[sideId]] is not UnidirectionalModelPipe<TModel> pipe)
                {
                    throw new InvalidOperationException();
                }

                _counts[sideId]++;
                return pipe;
            }

            var result = new UnidirectionalModelPipe<TModel>(this);
            _pipes.Add(result);
            _counts[sideId]++;
            return result;
        }

        
        private class PipeSystemSide : IPipeSystem
        {
            private readonly InMemoryPipeSystem _owner;
            private readonly int _sideId;
            
            public PipeSystemSide(InMemoryPipeSystem owner, int sideId)
            {
                _owner = owner;
                _sideId = sideId;
            }

            public IUnidirectionalRawPipeIn AllocateRawPipeIn()
            {
                return _owner.AllocateRawPipe(_sideId);
            }

            public IUnidirectionalRawPipeOut AllocateRawPipeOut()
            {
                return _owner.AllocateRawPipe(_sideId);
            }

            public IUnidirectionalModelPipeIn<TModel> AllocateModelPipeIn<TModel>() where TModel : struct, IDataStruct
            {
                return _owner.AllocateModelPipe<TModel>(_sideId);
            }

            public IUnidirectionalModelPipeOut<TModel> AllocateModelPipeOut<TModel>() where TModel : struct, IDataStruct
            {
                return _owner.AllocateModelPipe<TModel>(_sideId);
            }

            public void StopAll()
            {
                _owner.StopAll();
            }
        }

        private class UnidirectionalRawPipe : IUnidirectionalRawPipeIn, IUnidirectionalRawPipeOut
        {
            private readonly InMemoryPipeSystem _owner;
            private Func<UnionDataList, bool>? _receiver;

            public UnidirectionalRawPipe(InMemoryPipeSystem owner)
            {
                _owner = owner;
            }
            
            public SendResult Send(UnionDataList data)
            {
                using var disposer = data.AsDisposable();
                if (_owner._isStopped)
                {
                    return SendResult.NotConnected;
                }
                
                var receiver = _receiver;
                if (receiver == null)
                    throw new InvalidOperationException();
                receiver.Invoke(data.Acquire());
                return SendResult.Ok;
            }

            public void SetReceiver(Func<UnionDataList, bool>? receiver)
            {
                _receiver = receiver;
            }
        }

        private class UnidirectionalModelPipe<TModel> : IUnidirectionalModelPipeIn<TModel>, IUnidirectionalModelPipeOut<TModel>
            where TModel : struct, IDataStruct
        {
            private readonly InMemoryPipeSystem _owner;
            private Action<TModel>? _receiver;

            public UnidirectionalModelPipe(InMemoryPipeSystem owner)
            {
                _owner = owner;
            }
            
            public SendResult Send(TModel model)
            {
                if (_owner._isStopped)
                {
                    return SendResult.NotConnected;
                }
                
                var receiver = _receiver;
                if (receiver == null)
                    throw new InvalidOperationException();
                receiver.Invoke(model);
                return SendResult.Ok;
            }

            public void SetReceiver(Action<TModel>? receiver)
            {
                _receiver = receiver;
            }
        }
    }
}