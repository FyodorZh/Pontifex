using System;
using System.Diagnostics;

namespace Shared.CommonData.Attributes
{
    [Flags]
    public enum EditorFieldParameter
    {
        None = 0,
        Unique = 1,
        MissionGuid = 2,
        UseAsId = 4,
        UseAsTag = 8,
        UnityTexture = 16,
        UnityAsset = 32,
        Color32 = 64,
        LocalizedString = 128,
        Window = 256,
        ActGuid = 512,
    }

    [Conditional("UNITY_EDITOR")]
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class EditorFieldAttribute : Attribute
    {
        public string Name { get; private set; }
        public float Order { get; private set; }
        public EditorFieldParameter Parameters { get; private set; }

        public EditorFieldAttribute()
            : this(null, 0.0f, EditorFieldParameter.None)
        {
        }

        public EditorFieldAttribute(string name)
            : this(name, 0.0f, EditorFieldParameter.None)
        {
        }

        public EditorFieldAttribute(float order)
            : this(null, order, EditorFieldParameter.None)
        {
        }

        public EditorFieldAttribute(EditorFieldParameter parameter)
            : this(null, 0.0f, parameter)
        {
        }

        public EditorFieldAttribute(float order, EditorFieldParameter parameters)
            : this(null, order, parameters)
        {
        }

        public EditorFieldAttribute(string name, float order, EditorFieldParameter parameters)
        {
            Name = name;
            Order = order;
            Parameters = parameters;
        }
    }

    [Conditional("UNITY_EDITOR")]
    [AttributeUsage(AttributeTargets.Field)]
    public class EditorLinkAttribute : Attribute
    {
        public string Db { get; private set; }
        public string Pack { get; private set; }
        
        private EditorLinkAttribute()
        {
        }

        public EditorLinkAttribute(string db, string pack)
        {
            Db = db;
            Pack = pack;
        }
    }
}
