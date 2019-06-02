using SichboUI;
using System;
using System.Collections.Generic;

namespace CM.JS.Controls {

    public enum ServerProgressIndicatorStatus {
        Waiting,
        Success,
        Unknown,
        Error
    }

    internal class ServerProgressIndicator : Element {
        private Dictionary<Peer, Row> _Peers;
        private Element _Prog;
        private StackPanel _Stack;
        private Row _Title;
        public ServerProgressIndicator()
            : base(className: "progress") {
            IsHitTestVisible = true;
            _Peers = new Dictionary<Peer, Row>();
            _Stack = new StackPanel();
            _Stack.HorizontalAlignment = Alignment.Stretch;
            _Stack.VerticalAlignment = Alignment.Center;
            _Stack.Margin.Value = new Thickness(30);
            _Stack.MaxWidth.Value = 500;
            _Stack.Add(_Title = new Row(null));
            _Prog = _Stack.Div(style: $"background:{Colors.FieldActive};", margin: new Thickness(0, 0, 15, 0));
            _Prog.Width.Value = 0;
            _Prog.IsWidthPercent = true;
            _Prog.HorizontalAlignment = Alignment.Left;
            Add(_Stack);
        }

        public void AppendPeers(params Peer[] ar) {
            foreach (var p in ar) {
                if (!_Peers.ContainsKey(p)) {
                    var el = _Peers[p] = new Row(p);
                    _Stack.Add(el);
                }
            }
        }

        public void Finished(ServerProgressIndicatorStatus status, string msg, string details, Action onDone) {
            _Title.Update(status, msg, details);
            _Stack.Add(new Button(ButtonStyle.BigGreen, SR.LABEL_CONTINUE, () => {
                Remove();
                onDone();
            }, margin: new Thickness(30, 0)) {
                HorizontalAlignment = Alignment.Right
            });
        }

        public void Show() {
            Retyped.dom.document.body.appendChild(Html);
            Play();
        }

        public void Update(ServerProgressIndicatorStatus status, int percent, string msg, string details = null) {
            _Title.Update(status, msg, details, percent: percent);
            _Prog.Width.Animate(percent / 100.0, Times.Normal, Easing.CubicInOut);
        }

        public void Update(Peer p, ServerProgressIndicatorStatus status, string msg, string details = null) {
            if (!_Peers.TryGetValue(p, out var el)) {
                _Peers[p] = el = new Row(p);
                _Stack.Add(el);
            }
            el.Update(status, msg, details);
        }

        protected override void OnInputDownOverride(InputState input) {
            input.IsHandled = true;
        }

        protected override void OnInputUpOverride(InputState input) {
            input.IsHandled = true;
        }

        private class Row : Element {

            private Retyped.dom.HTMLElement _Details;
            private Element _Glyph;
            private Retyped.dom.HTMLElement _Info;
            private Peer _Peer;

            public Row(Peer p = null) {
                _Peer = p;

                Margin.Value = new Thickness(0, 0, 15, 0);
                IsHitTestVisible = true;
                _Glyph = El("div",
                    style: "background-size:80%;background-repeat:no-repeat;background-position:center;",
                    halign: Alignment.Left,
                    valign: Alignment.Top);
                _Glyph.Width.Value = 32;
                _Glyph.Height.Value = 32;
                var info = Div(margin: new Thickness(0, 0, 0, 30 + 15));
                _Info = info.Html.Div();
                _Details = info.Html.Div();

                if (p == null) {
                    _Info.style.fontSize = "2em";
                    _Info.style.fontWeight = "900";
                } else {
                    _Details.style.display = "none";
                    Html.style.cursor = "pointer";
                }
            }

            public void Update(ServerProgressIndicatorStatus status, string msg, string details = null, int percent = 0) {
                var glyph = status == ServerProgressIndicatorStatus.Success ? Glyphs.CircleTick
               : status == ServerProgressIndicatorStatus.Error ? Glyphs.CircleError
               : status == ServerProgressIndicatorStatus.Unknown ? Glyphs.CircleUnknown
               : Glyphs.Working;

                _Glyph.Html.style.backgroundImage = glyph.CSSUrl(status == ServerProgressIndicatorStatus.Success ? Colors.C1
                    : status == ServerProgressIndicatorStatus.Waiting ? Colors.DarkText
                    : "#cc0000");
                if (status == ServerProgressIndicatorStatus.Waiting) {
                    _Glyph.RelativeTransform.Animate(new SichboUI.RelativeTransform(1, 1, 0, 0, 360 - 0.1, 0, 0),
                        Times.Long, Easing.Linear, AnimRepeat.Loop);
                } else {
                    _Glyph.RelativeTransform.Animate(SichboUI.RelativeTransform.Empty,
                        Times.Long, Easing.ElasticOut1);
                }
                _Info.textContent = (_Peer != null ? _Peer.EndPoint + " " : "") + msg;
                _Details.textContent = details;
            }
            protected override void OnInputUpOverride(InputState e) {
                if (e.IsHandled)
                    return;
                e.IsHandled = true;
                _Details.style.display = _Details.style.display == "none" ? "block" : "none";
                InvalidateArrange();
            }
        }
    }
}