#region License

//
// Civil Money is free and unencumbered software released into the public domain (unlicense.org), unless otherwise
// denoted in the source file.
//

#endregion License

using Bridge.Html5;

namespace CM.Javascript {

    /// <summary>
    /// A tooltip style pop-up component
    /// </summary>
    internal class PopupBubble {
        private Rect _Bounds;
        private HTMLElement _Details;
        private HTMLElement _Element;
        private HTMLElement _Summary;
        private HTMLElement _Title;
        public PopupBubble(HTMLElement owner) {
            _Element = owner.Div("bubble");
            _Title = _Element.Div("title");
            _Summary = _Element.Div("summary");
            _Details = _Element.Div("details");
            _Element.OnMouseOut = (e) => {
            };
            owner.OnMouseMove = (e) => {
                var pos = _Element.Position();
                var x = e.PageX - _Bounds.X;
                var y = e.PageY - _Bounds.Y;
                if (x >= 0 && x < _Bounds.Width
                    && y >= 0 && y < _Bounds.Height) {
                } else {
                    _Element.Style.Display = Display.None;
                }
            };
        }

        public void Hide() {
            _Element.Style.Display = Display.None;
        }

        public void Show(HTMLElement control, string title, string summary, string details) {
            _Title.InnerHTML = Page.HtmlEncode(title);
            _Summary.InnerHTML = Page.HtmlEncode(summary);
            _Details.InnerHTML = Page.HtmlEncode(details);
            var pos = control.Position();
            _Element.Style.Top = pos.Y + "px";
            _Element.Style.Left = (pos.X + 50) + "px";
            _Element.Style.Display = Display.Block;
            _Bounds = new Rect() {
                X = pos.X,
                Y = pos.Y,
                Width = _Element.OffsetWidth + 50,
                Height = _Element.OffsetHeight
            };
        }

        private struct Rect {
            public int Height;
            public int Width;
            public int X; public int Y;
        }
    }
}