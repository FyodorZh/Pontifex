using Serializer.BinarySerializer;
using Shared.CommonData.Attributes;
using Shared.CommonData.Plt;

namespace Shared.CommonData
{
    public interface IItemBase : IDescriptionBase
    {
        ItemType ItemDescType2 { get; }
    }

    public abstract class ItemBaseDescription : DescriptionBase, IItemBase
    {
        [EditorField(EditorFieldParameter.LocalizedString)]
        private string _name;

        [EditorField(EditorFieldParameter.LocalizedString)]
        private string _description;

        [EditorField(EditorFieldParameter.UnityTexture)]
        private string _icon;

        [EditorField(EditorFieldParameter.UnityTexture)]
        private string _smallIcon;

        [EditorField]
        private WhereToFindItem[] _whereToFind;

        [EditorField]
        private bool _hideInDrop;

        [EditorField]
        private bool _hideInRewardPreview;

        public abstract ItemType ItemDescType2 { get; }

        public byte ItemDescType
        {
            get { return ItemDescType2.Value; }
        }

        public string Name
        {
            get { return _name; }
        }

        public string Description
        {
            get { return _description; }
        }

        public string Icon
        {
            get { return _icon; }
            set { _icon = value; }
        }

        public string SmallIcon
        {
            get { return _smallIcon; }
            set { _smallIcon = value; }
        }

        public WhereToFindItem[] WhereToFind
        {
            get { return _whereToFind; }
        }

        public bool HideInDrop
        {
            get { return _hideInDrop; }
        }

        public bool HideInRewardPreview
        {
            get { return _hideInRewardPreview; }
        }

        public override bool Serialize(IBinarySerializer dst)
        {
            base.Serialize(dst);

            dst.Add(ref _name);
            dst.Add(ref _description);
            dst.Add(ref _icon);
            dst.Add(ref _smallIcon);
            dst.Add(ref _whereToFind);
            dst.Add(ref _hideInDrop);
            dst.Add(ref _hideInRewardPreview);

            return true;
        }

        public virtual void OnPostprocess(ItemsDescriptions itemsDescriptions)
        {

        }

        public override string ToString()
        {
            return string.Format("[[{0}] ItemBaseDescription: name='{1}' description='{2}' icon={3} smallIcon={4} whereToFind={5}]", base.ToString(), _name, _description, _icon, _smallIcon, _whereToFind != null ? string.Concat((object[])_whereToFind) : null);
        }
    }
}
