using System;
using System.Collections.Generic;
using Actuarius.Memory;
using Pontifex.Factory;
using Scriba;

namespace Pontifex.Converters
{
    public interface IConvertersGraph
    {
        ITransport? TryConvert(ITransport transport, TransportType targetType, 
            IMemoryRental? memoryRental = null, ILogger? logger = null);

        bool HasPath(TransportType from, TransportType to, out int length);
    }
    
    public class ConvertersGraph : IConvertersGraph
    {
        public static readonly IConvertersGraph Default = new ConvertersGraph(
            new NoAckRawReliableToNoAckRawUnreliableConverter(),
            new NoAckRawUnreliableToNoAckRawReliableConverter(),
            new AckRawReliableToNoAckRawReliableConverter());
        
        public static readonly IConvertersGraph Empty = new ConvertersGraph();

        private readonly List<ITransportConverter>?[][] _convertersMap;
        
        public ConvertersGraph(params ITransportConverter[] converters)
        {
            const int N = 8;
            _convertersMap = new List<ITransportConverter>?[N][];
            for (int i = 0; i < N; i++)
            {
                _convertersMap[i] = new List<ITransportConverter>[N];
                _convertersMap[i][i] = new List<ITransportConverter>();
            }
            
            foreach (var converter in converters)
            {
                int fromIndex = (int)converter.From;
                int toIndex = (int)converter.To;

                for (int i = 0; i < N; i++)
                {
                    for (int j = 0; j < N; j++)
                    {
                        int curDst = _convertersMap[i][j]?.Count ?? 1000;
                        int possibleDst = (_convertersMap[i][fromIndex]?.Count ?? 1000) +
                                          (_convertersMap[toIndex][j]?.Count ?? 1000) + 1;
                        if (possibleDst < curDst)
                        {
                            var list = _convertersMap[i][j] ??= new List<ITransportConverter>();
                            list.Clear();

                            if (_convertersMap[i][fromIndex] != null)
                                list.AddRange(_convertersMap[i][fromIndex]!);
                            list.Add(converter);
                            if (_convertersMap[toIndex][j] != null)
                                list.AddRange(_convertersMap[toIndex][j]!);
                        }
                    }
                }
            }
        }

        public ITransport? TryConvert(ITransport transport, TransportType targetType, 
            IMemoryRental? memoryRental = null, ILogger? logger = null)
        {
            if (transport.Type == targetType)
            {
                return transport;
            }
            
            var list = _convertersMap[(int)transport.Type][(int)targetType];
            if (list == null)
            {
                return null;
            }
            
            memoryRental ??= transport.Memory;
            logger ??= transport.Log;

            ITransport res = transport;
            foreach (var converter in list)
            {
                res = converter.Convert(res, memoryRental, logger);
            }

            return res;
        }

        public bool HasPath(TransportType from, TransportType to, out int length)
        {
            var list = _convertersMap[(int)from][(int)to];
            if (list == null)
            {
                length = 0;
                return false;
            }

            length = list.Count;
            return true;
        }
    }
}