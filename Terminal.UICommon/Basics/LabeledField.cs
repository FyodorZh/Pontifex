using Terminal.Gui;

namespace Terminal.UI
{
    public class LabeledField : View
    {
        public Label Label { get; }
        public TextField Field { get; }
        
        public LabeledField(string label, int textWidth)
        {
            Label = new Label()
            {
                X = 0, Y = 0,
                Width = label.Length,
                Height = 1,
                Text = label
            };
            Field = new TextField()
            {
                X = Pos.Right(Label),
                Y = 0,
                Width = textWidth,
                Height = 1
            };

            Add(Label, Field);
            Width = Dim.Auto();
            Height = 1;
        }
    }
}