using System;
using System.Collections.Generic;
using Actuarius.Memory;
using Pontifex.Converters;
using Scriba;

namespace Pontifex.Factory
{
    public interface ITransportBuilder
    {
        IMemoryRental MemoryRental { get; }
        ILogger Logger { get; }
        ITransport Build(IDescription description);
    }

    public class TransportBuilder
    {
        private readonly DescriptionFactory _descriptionFactory = new();
        private readonly IConvertersGraph _convertersGraph;
        
        private readonly Dictionary<(TransportType, string), Func<ITransportBuilder, IDescription, ITransport?>> _serverConstructors = new();
        private readonly Dictionary<(TransportType, string), Func<ITransportBuilder, IDescription, ITransport?>> _clientConstructors = new();

        public IDescriptionFactory DescriptionFactory => _descriptionFactory;        
        
        public TransportBuilder(IConvertersGraph convertersGraph)
        {
            _convertersGraph = convertersGraph;
            _descriptionFactory.RegisterUriParser("convert", (uriBody, factory) =>
            {
                // "(transport://convert|)AckRawReliable:udp|127.0.0.1:9000"
                int pos = uriBody.IndexOf(':');
                if (pos < 0)
                {
                    return null;
                }

                string typeStr = uriBody.Substring(0, pos);
                string subUri = uriBody.Substring(pos + 1);
                

                if (!Enum.TryParse<TransportType>(typeStr, out var _))
                {
                    return null;
                }
            
                
                var sub = factory.ParseTransport(subUri);

                Description description = new Description();
                description.Add("convert_to", new StringElement(typeStr));
                description.Add("nested", new DescriptionElement(sub));

                return description;
            });
        }
        
        public void RegisterTransport(ITransportConstructor constructor)
        {
            _serverConstructors.Add((constructor.Type, constructor.Name), constructor.ConstructServer);
            _clientConstructors.Add((constructor.Type, constructor.Name), constructor.ConstructClient);
        }
        
        public ITransport BuildServer(IDescription description, IMemoryRental memoryRental, ILogger logger)
        {
            Builder builder = new Builder(memoryRental, logger, _serverConstructors, _convertersGraph);   
            return builder.Build(description);
        }
        
        public ITransport BuildClient(IDescription description, IMemoryRental memoryRental, ILogger logger)
        {
            Builder builder = new Builder(memoryRental, logger, _clientConstructors, _convertersGraph);   
            return builder.Build(description);
        }

        private class Builder : ITransportBuilder
        {
            public IMemoryRental MemoryRental { get; }
            public ILogger Logger { get; }

            private readonly IConvertersGraph _convertersGraph;
            private readonly IReadOnlyDictionary<(TransportType, string), Func<ITransportBuilder, IDescription, ITransport?>> _constructors;
             
            
            public Builder(IMemoryRental memoryRental, ILogger logger, 
                IReadOnlyDictionary<(TransportType, string), Func<ITransportBuilder, IDescription, ITransport?>> constructors, IConvertersGraph convertersGraph)
            {
                MemoryRental = memoryRental;
                Logger = logger;
                _constructors = constructors;
                _convertersGraph = convertersGraph;
            }

            public ITransport Build(IDescription description)
            {
                if (!description.Get("name").EvaluateAsString(out var transportName))
                {
                    throw new ArgumentException("Description must have a 'name' element with a string value.");
                }

                TransportType? transportType = null;
                if (description.Get("type").EvaluateAsString(out var transportTypeString))
                {
                    transportType = Enum.Parse<TransportType>(transportTypeString, true);
                }

                if (transportType != null)
                {
                    if (_constructors.TryGetValue((transportType.Value, transportName), out var constructor))
                    {
                        return constructor(this, description) ?? 
                               throw new InvalidOperationException($"Constructor for transport '{transportName}:{transportType}' returned null.");
                    }
                    throw new InvalidOperationException($"No constructor registered for transport type '{transportName}:{transportType}'.");
                }

                foreach (var keyPair in _constructors.Keys)
                {
                    if (keyPair.Item2 == transportName)
                    {
                        if (transportType != null)
                        {
                            throw new InvalidOperationException($"Ambiguous transport type for '{transportName}'.");
                        }
                    }
                }

                if (transportType != null)
                {
                    if (_constructors.TryGetValue((transportType.Value, transportName), out var constructor))
                    {
                        return constructor(this, description) ?? 
                               throw new InvalidOperationException($"Constructor for transport type '{transportName}:{transportType}' returned null.");
                    }
                }
                throw new InvalidOperationException($"Failed to find transport '{transportName}'.");
            }
        }
    }
}