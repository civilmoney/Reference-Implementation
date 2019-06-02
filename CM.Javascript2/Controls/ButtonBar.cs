using SichboUI;
using System;

namespace CM.JS.Controls {

    internal class ButtonBar : StackPanel {

        public ButtonBar(string okText, Action<Button> onOK, string cancelText = null, Action onCancel = null, string deleteText = null, Action<Button> onDelete = null) {
            Orientation = Orientation.Horizontal;
            HorizontalAlignment = Alignment.Right;
            Margin.Value = new Thickness(30, 0, 30, 0);
            SmallWidth = 500;
            SmallHorizontalAlignment = Alignment.Stretch;
            SmallOrientation = Orientation.Vertical;

            Button sub = null, delete = null, cancel = null;

            sub = new Button(ButtonStyle.BigGreen, okText, onOK);
            sub.SmallHorizontalAlignment = Alignment.Stretch;

            if (deleteText != null) {
                delete = new Button(ButtonStyle.BigRed, deleteText, onDelete);
                delete.Margin.Value = new Thickness(0, 0, 0, 15);
                delete.SmallHorizontalAlignment = Alignment.Stretch;
                delete.SmallMargin.Value = new Thickness(30, 0, 0, 0);
            }

            if (cancelText != null) {
                cancel = new Button(ButtonStyle.NotSet, cancelText, onCancel);
                cancel.Margin.Value = new Thickness(0, 0, 0, 15);
                cancel.SmallHorizontalAlignment = Alignment.Stretch;
                cancel.SmallMargin.Value = new Thickness(30, 0, 0, 0);
            }

            Add(sub);

            if (delete != null)
                Add(delete);

            if (cancel != null)
                Add(cancel);
        }
    }
}