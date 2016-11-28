#region License

//
// Civil Money is free and unencumbered software released into the public domain (unlicense.org), unless otherwise
// denoted in the source file.
//

#endregion License

using Bridge.Html5;

namespace CM.Javascript {

    internal enum FeedbackType {
        Default,
        Success,
        Error
    }

    /// <summary>
    /// An error/status/feedback UI component
    /// </summary>
    internal class Feedback {
        private HTMLElement _Element;
        private HTMLElement _Glyph;
        private HTMLElement _Message;
        public Feedback(HTMLElement owner, bool big = false) {
            _Element = owner.Span(null, "feedback hidden" + (big ? " big" : ""));
            _Glyph = _Element.Span(null, "glyph");
            _Message = _Element.Span(null, "message");
            IsShowing = false;
        }

        public bool IsShowing { get; private set; }
        public void Hide() {
            _Element.AddClass("hidden");
            IsShowing = false;
        }

        public void Set(Assets.SVG glyph, FeedbackType type, string message) {
            _Element.RemoveClass("default");
            _Element.RemoveClass("error");
            _Element.RemoveClass("success");
            _Element.AddClass(type.ToString().ToLower());

            _Glyph.InnerHTML = glyph.ToString(16, 16, "#000000");
            _Message.Clear();
            _Message.Div(null, Page.HtmlEncode(message).Replace("\n", "<br/>"));
            _Element.RemoveClass("hidden");
            IsShowing = true;
        }

        public void Set(Assets.SVG glyph, FeedbackType type, string message, string buttonLabel, System.Action onClick) {
            _Element.RemoveClass("default");
            _Element.RemoveClass("error");
            _Element.RemoveClass("success");
            _Element.AddClass(type.ToString().ToLower());
            _Glyph.InnerHTML = glyph.ToString(16, 16, "#000000");
            _Message.Clear();
            _Message.Div(null, Page.HtmlEncode(message).Replace("\n", "<br/>"));
            _Message.Button(buttonLabel, (e) => { onClick(); });
            _Element.RemoveClass("hidden");
            IsShowing = true;
        }
    }
}