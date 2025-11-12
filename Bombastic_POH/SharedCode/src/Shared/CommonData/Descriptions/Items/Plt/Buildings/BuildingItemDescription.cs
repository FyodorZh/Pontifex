using Serializer.BinarySerializer;
using Shared.CommonData.Attributes;

namespace Shared.CommonData.Plt
{
    public abstract class BuildingItemDescription : ItemBaseDescription,
        IWithGrades
    {
        [EditorField(EditorFieldParameter.LocalizedString)]
        private string _text;
        [EditorField]
        private short _position;
        [EditorField]
        private short _startGrade;
        [EditorField(EditorFieldParameter.LocalizedString)]
        private string _buttonText;
        [EditorField]
        private bool _showIdleAnimation;

        protected BuildingItemDescription()
        {
        }

        protected BuildingItemDescription(string name, string text, short position, short startGrade, string buttonText)
        {
            _text = text;
            _position = position;
            _startGrade = startGrade;
            _buttonText = buttonText;
        }

        public string Text
        {
            get { return _text; }
        }

        public abstract BuildingItemLevel[] Grades { get; }

        ItemLevel[] IWithGrades.Grades
        {
            get { return Grades; }
        }

        public string ButtonText
        {
            get { return _buttonText; }
        }

        public short StartGrade
        {
            get { return _startGrade > 0 ? _startGrade : (short)1; }
        }

        public short Position
        {
            get { return _position; }
        }

        public bool ShowIdleAnimation
        {
            get { return _showIdleAnimation; }
        }

        public override bool Serialize(IBinarySerializer dst)
        {
            dst.Add(ref _text);
            dst.Add(ref _position);
            dst.Add(ref _startGrade);
            dst.Add(ref _buttonText);
            dst.Add(ref _showIdleAnimation);

            return base.Serialize(dst);
        }
    }
}
