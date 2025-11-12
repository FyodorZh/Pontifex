namespace Shared.Battle
{
    public enum PVEImageAlign
    {
        Left, Right
    }

    public struct PVEDialogItem
    {
        public bool ControlledUnit;
        public string CharacterName;
        public string Text;
        public string ImagePath;
        public PVEImageAlign ImageAlign;
    }

    public struct PVEDialog
    {
        public string ID;
        public PVEDialogItem[] Items;
    }
}
