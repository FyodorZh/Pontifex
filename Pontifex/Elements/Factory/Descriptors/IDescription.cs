using System.Collections.Generic;
using System.Linq;

namespace Pontifex.Factory
{
    public interface IDescription
    {
        IReadOnlyDictionary<string, IElement> Elements { get; }
        IElement Get(string name);
    }
    
    public class Description : IDescription
    {
        private readonly Dictionary<string, IElement> _elements = new();
        
        public IReadOnlyDictionary<string, IElement> Elements => _elements;
        
        public IElement Get(string name)
        {
            if (_elements.TryGetValue(name, out var element))
            {
                return element;
            }
            return VoidElement.Instance;
        }
        
        public void Add(string name, IElement element)
        {
            _elements.Add(name, element);
        }

        public override string ToString()
        {
            return "{" + string.Join(", ", _elements.Select(kv => $"{kv.Key}: {kv.Value.ToString()}"));
        }
    }
}