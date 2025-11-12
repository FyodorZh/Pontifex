using Serializer.BinarySerializer;
using Serializer.Extensions;
using Shared.CommonData.Attributes;

namespace Shared.CommonData.Plt
{
    public class DropItems : IDataStruct
    {
        [EditorField]
        private DropItem[] _items;

        [EditorField]
        private LootTable[] _lootTables;

        [EditorField, EditorLink("Items", "Drop Items Contexts")]
        private short? _contextId;

        [EditorField]
        private ActionDropItem[] _actions;

        public DropItems()
        {
        }

        public DropItems(DropItem[] items, LootTable[] lootTables, ActionDropItem[] actions)
        {
            _items = items;
            _lootTables = lootTables;
            _actions = actions;
        }

        public DropItem[] Items
        {
            get { return _items; }
        }

        public LootTable[] LootTables
        {
            get { return _lootTables; }
        }

        public short? ContextId
        {
            get { return _contextId; }
        }

        public ActionDropItem[] Actions
        {
            get { return _actions; }
        }

        public bool HasItems()
        {
            return _items != null && _items.Length > 0;
        }

        public bool HasLootTables()
        {
            return _lootTables != null && _lootTables.Length > 0;
        }
        
        public bool HasActions()
        {
            return _actions != null && _actions.Length > 0;
        }

        public bool Empty
        {
            get { return !(HasItems() || HasLootTables() || HasActions()); }
        }
        
        public bool Serialize(IBinarySerializer dst)
        {
            dst.Add(ref _items);
            dst.Add(ref _lootTables);
            dst.AddNullable(ref _contextId);
            dst.Add(ref _actions);

            return true;
        }
    }
}
