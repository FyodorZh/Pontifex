using System;
using System.Collections.Generic;
using System.Text.Json;

namespace Pontifex.Factory
{
    public interface IDescriptionUriFactory
    {
        Description ParseTransport(string uri);
    }
    
    public interface IDescriptionFactory
    {
        void RegisterUriParser(string typeName, Func<string, IDescriptionUriFactory, Description?> parser);
        
        IDescription FromUri(string uri);
        IDescription FromJson(JsonElement element);
    }
    
    internal class DescriptionFactory : IDescriptionFactory, IDescriptionUriFactory
    {
        private readonly Dictionary<string, Func<string, IDescriptionUriFactory, Description?>> _uriParsers = new();
        
        public void RegisterUriParser(string typeName, Func<string, IDescriptionUriFactory, Description?> parser)
        {
            _uriParsers.Add(typeName, parser);
        }

        public Description ParseTransport(string uri)
        {
            int index = uri.IndexOf('|');
            if (index == -1)
            {
                throw new ArgumentException("Invalid transport URI format. Expected 'transport|params'.");
            }

            var transportName = uri.Substring(0, index);
            var parameters = uri.Substring(index + 1);

            if (_uriParsers.TryGetValue(transportName, out var parser))
            {
                var desc = parser(parameters, this);
                if (desc == null)
                {
                    throw new InvalidOperationException($"Parser for type '{transportName}' with params '{parameters}' returned null.");
                }
                desc.Add("name", new StringElement(transportName));
                return desc;
            }

            throw new InvalidOperationException($"No parser registered for type '{transportName}'.");
        }
        
        public IDescription FromUri(string uri)
        {
            uri = uri.Substring("transport://".Length);
            return ParseTransport(uri);
        }
        
        // public static IDescription FromJson(string json)
        // {
        //     using var doc = System.Text.Json.JsonDocument.Parse(json);
        //     return FromJson(doc.RootElement);
        // }

        public IDescription FromJson(JsonElement element)
        {
            var description = new Description();
            foreach (var property in element.EnumerateObject())
            {
                var child = ConvertJsonElement(property.Value);
                if (child != null)
                {
                    description.Add(property.Name, child);
                }
            }

            return description;
        }

        private IElement? ConvertJsonElement(JsonElement element)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.String:
                {
                    var stringValue = element.GetString() ?? "";
                    if (stringValue.StartsWith("transport://"))
                    {
                        var description = FromUri(stringValue);
                        return new DescriptionElement(description);
                    }
                    return new StringElement(stringValue);
                }
                case JsonValueKind.True:
                    return new BoolElement(true);

                case JsonValueKind.False:
                    return new BoolElement(false);

                case JsonValueKind.Number:
                    if (element.TryGetInt64(out long longValue))
                        return new LongElement(longValue);
                    return new DoubleElement(element.GetDouble());

                case JsonValueKind.Object:
                    var nested = FromJson(element);
                    return new DescriptionElement(nested);

                case JsonValueKind.Null:
                    return VoidElement.Instance;

                case JsonValueKind.Array:
                {
                    var items = new List<IElement>();
                    foreach (var item in element.EnumerateArray())
                    {
                        var converted = ConvertJsonElement(item);
                        if (converted != null)
                        {
                            items.Add(converted);
                        }
                    }
                    return new ArrayElement(items);
                }

                case JsonValueKind.Undefined:
                    return null;
            }
            return null;
        }
    }
}