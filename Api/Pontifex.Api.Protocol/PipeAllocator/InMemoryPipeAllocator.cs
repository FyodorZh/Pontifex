using System;
using System.Collections.Generic;
using Archivarius;
using Pontifex.Utils;

namespace Pontifex.Api
{
    public class InMemoryPipeAllocator
    {
        private readonly List<ITransportPipe> _pipes = new();
        private readonly int[] _counts = new[] { 0, 0 };
        
        
        public IPipeAllocator Side1 { get; }
        public IPipeAllocator Side2 { get; }

        public InMemoryPipeAllocator()
        {
            Side1 = new PipeSide(this, 0);
            Side2 = new PipeSide(this, 1);
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

            var result = new UnidirectionalRawPipe();
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

            var result = new UnidirectionalModelPipe<TModel>();
            _pipes.Add(result);
            _counts[sideId]++;
            return result;
        }

        
        private class PipeSide : IPipeAllocator
        {
            private readonly InMemoryPipeAllocator _owner;
            private readonly int _sideId;
            
            public PipeSide(InMemoryPipeAllocator owner, int sideId)
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
        }

        private class UnidirectionalRawPipe : IUnidirectionalRawPipeIn, IUnidirectionalRawPipeOut
        {
            private Func<UnionDataList, bool>? _receiver;
            
            public SendResult Send(UnionDataList data)
            {
                var receiver = _receiver;
                if (receiver == null)
                    throw new InvalidOperationException();
                receiver.Invoke(data);
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
            private Action<TModel>? _receiver;
            
            public SendResult Send(TModel model)
            {
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