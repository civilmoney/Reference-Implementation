using SichboUI;
using System;

namespace CM.JS.Controls {

    internal enum ButtonStyle {
        NotSet,
        GreenOutline,
        Green,
        BlackOutline,
        BlackFill,
        BigRed,
        BigGreen,
        BigBlack
    }

    internal class Button : Element {
        public bool HrefMode;
        private bool _IsLoading;
        private Retyped.dom.HTMLElement m_Inner;
        private Action<Button> m_OnClick;
        private ButtonStyle m_Style;

        /// <param name="text">Set to null for no inner padding.</param>
        public Button(ButtonStyle style, string text, Action onClick, Alignment scaleAt = Alignment.Center, Thickness? margin = null)
            : this(style, text, (e) => { onClick?.Invoke(); }, scaleAt, margin) {
        }

        public Button(ButtonStyle style, string text, Action<Button> onClick, Alignment scaleAt = Alignment.Center, Thickness? margin = null)
           : base(tagName: "A") {
            m_OnClick = onClick;
            m_Style = style;
            if (margin != null)
                Margin.Value = margin.Value;
            string css = "border:0;cursor:pointer;display:block;white-space:nowrap;text-align: center;text-decoration:none;";
            string innerCss = text != null ? "padding:10px 20px;" : "";
            switch (style) {
                case ButtonStyle.Green:
                case ButtonStyle.BigGreen:
                    css += $"border-radius:100px;font-weight:700;background-color:{Colors.C1};color:#fff;";
                    break;

                case ButtonStyle.NotSet:
                    break;

                case ButtonStyle.BigRed:
                    css += $"border-radius:100px;font-weight:700;background-color:{Colors.C2};color:#fff;";
                    break;

                case ButtonStyle.BigBlack:
                    css += $"border-radius:100px;font-weight:700;background-color:{Colors.DarkText};color:#fff;";
                    break;

                case ButtonStyle.GreenOutline:
                    css += $"background-color:#fff;border-radius:100px; font-weight:900; color:{Colors.C1};";
                    break;

                case ButtonStyle.BlackOutline:
                    css += $"background-color:transparent;border-radius:100px;border: 1px solid #000; color:  #000;";
                    break;

                case ButtonStyle.BlackFill:
                    css += $"border-radius:100px;color:#fff;background-color:#000;";
                    break;
            }
            if (style.ToString().StartsWith("Big")) {
                css += "font-size:1.25em;";
                if (text != null)
                    innerCss = "padding:10px 30px;";
            }
            Style += css;
            m_Inner = Html.Div(className: "noselect", text: text, style: innerCss);
            HorizontalAlignment = Alignment.Left;
            VerticalAlignment = Alignment.Top;
            IsHitTestVisible = true;
            RelativeTransform.OnState(ElementState.MouseOver | ElementState.MouseDown,
                SichboUI.RelativeTransform.Empty,
                new SichboUI.RelativeTransform(1.2f, 1.2f, 0, 0, 0, 0, 0) {
                    ScaleCenter = scaleAt == Alignment.Left ? new Vector3(0.0f, 0.5f, 0)
                     : scaleAt == Alignment.Right ? new Vector3(1.0f, 0.5f, 0)
                     : scaleAt == Alignment.Top ? new Vector3(0.5f, 0, 0)
                     : new Vector3(0.5f, 0.5f, 0)
                },
                Times.Normal, Easing.ElasticOut2);
            Html.setAttribute("tabindex", "0");
            if (text != null)
                Label = text;
        }

        public string Text {
            get => m_Inner.textContent;
            set {
                m_Inner.textContent = value;
                InvalidateArrange();
            }
        }

        public void Loading() {
            _IsLoading = true;
            Html.classList.add(m_Style == ButtonStyle.BlackOutline ? "in-progress-on-white" : "in-progress");
        }

        public void LoadingDone() {
            _IsLoading = false;
            Html.classList.remove(m_Style == ButtonStyle.BlackOutline ? "in-progress-on-white" : "in-progress");
        }

        public void RaiseClick() {
            m_OnClick?.Invoke(this);
        }

        protected override Vector2 MeasureOverride(Vector2 constraintSize) {
            return base.MeasureOverride(constraintSize);
        }

        protected override void OnInputDownOverride(InputState e) {
            if (e.IsHandled || HrefMode || _IsLoading)
                return;
            e.IsHandled = true;
            e.CaptureMovement();
        }

        protected override void OnInputEnterOverride(InputState e) {
            this.BringToFront();
        }

        protected override void OnInputMoveOverride(InputState e) {
            if (e.IsHandled)
                return;
            e.IsHandled = true;
        }

        protected override void OnInputUpOverride(InputState e) {
            if (e.IsHandled || _IsLoading)
                return;
            if (HrefMode) {
                Html.click();
                return;
            }
            e.IsHandled = true;
            m_OnClick?.Invoke(this);
        }

        protected override void OnOKPressed(KeyEventArgs e) {
            if (!e.IsDown) {
                if (!_IsLoading) {
                    m_OnClick?.Invoke(this);
                }
                e.IsHandled = true;
            }
        }
    }
}