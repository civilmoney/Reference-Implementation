#region License
// 
// Civil Money is free and unencumbered software released into the public domain (unlicense.org), unless otherwise 
// denoted in the source file.
//
#endregion

using Bridge.Html5;
using System.Collections.Generic;

namespace CM.Javascript {

    /// <summary>
    /// Shows basic peer status information for diagnosing network issues.
    /// </summary>
    internal class StatusPage : Page {
        private Client _Client;
        private List<Peer> _Ordered;
        private Dictionary<Peer, PeerVisual> _PeerItems;
        private HTMLDivElement _PeerList;

        public override string Title {
            get {
                return SR.TITLE_PEERS;
            }
        }

        public override string Url {
            get {
                return "/status";
            }
        }

        public override void Build() {
            Element.ClassName = "statuspage";
            _PeerItems = new Dictionary<Peer, PeerVisual>();
            _Ordered = new List<Peer>();
            _Client = App.Identity.Client;
            _PeerList = Element.Div("peers");
            for (int i = 0; i < _Client.Peers.Count; i++) {
                var p = _Client.Peers[i];
                var v = new PeerVisual(p);
                v.Refresh();
                _Ordered.Add(p);
                _PeerItems[p] = v;
                _PeerList.AppendChild(v.Element);
            }
            UpdateGraph();
            _Client.PeerStateChanged += _Client_PeerStateChanged;
            _Client.PeerRemoved += _Client_PeerRemoved;
            _Client.JoinNetwork();
        }

        public override void OnRemoved() {
            _Client.PeerStateChanged -= _Client_PeerStateChanged;
            _Client.PeerRemoved -= _Client_PeerRemoved;
        }

        private static int Compare(byte[] a, byte[] b) {
            for (int i = 0; i < a.Length && i < b.Length; i++)
                if (a[i] != b[i])
                    return a[i] - b[i];
            return a.Length - b.Length;
        }

        private void _Client_PeerRemoved(Peer p) {
            PeerVisual v;
            if (_PeerItems.TryGetValue(p, out v)) {
                v.Element.RemoveEx();
                _PeerItems.Remove(p);
                _Ordered.Remove(p);
            }
        }
        private void _Client_PeerStateChanged(Peer p) {
            PeerVisual v;
            if (!_PeerItems.TryGetValue(p, out v)) {
                v = new PeerVisual(p);
                _Ordered.Add(p);
                _PeerItems[p] = v;
                _PeerList.AppendChild(v.Element);
            }
            v.Refresh();
            UpdateGraph();
        }

        private int SortPeerByDHTPosition(Peer a, Peer b) {
            if (a.DHT_ID == null && b.DHT_ID == null) return 0;
            if (a.DHT_ID == null && b.DHT_ID != null) return -1;
            if (a.DHT_ID != null && b.DHT_ID == null) return 1;
            return Compare(a.DHT_ID, b.DHT_ID);
        }
        private void UpdateGraph() {
            _Ordered.Sort(SortPeerByDHTPosition);
            foreach (var kp in _PeerItems) {
                kp.Value.Element.Style.Order = _Ordered.IndexOf(kp.Key).ToString();
            }
        }

        private class PeerVisual {
            public HTMLDivElement Element;
            private HTMLDivElement _Content;
            private HTMLDivElement _Glyph;
            private Peer _Peer;
            private Element _Status;
            private Element _Title;

            public PeerVisual(Peer p) {
                _Peer = p;
                Element = new HTMLDivElement() { ClassName = "peer" };
                _Glyph = Element.Div("glyph");
                _Title = Element.H2(HtmlEncode(p.ToString()));
                _Status = Element.H3(null);
                _Content = Element.Div();
                Refresh();
                Element.OnClick = (e) => {
                    if (_Peer.State != PeerState.Connected)
                        _Peer.BeginConnect(null);
                    else
                        _Peer.Ping();
                };
            }

            public void Refresh() {

                // TODO: We will make all of this a bit tidier and functional if Civil Money gains traction.

                _Content.Clear();
                _Title.InnerHTML = HtmlEncode(_Peer.ToString());
                Assets.SVG glyph = null;
                string statusName = _Peer.State.ToString();
                switch (_Peer.State) {
                    case PeerState.Disconnected:
                    case PeerState.Broken: glyph = Assets.SVG.CircleError; break;
                    case PeerState.Connected: glyph = Assets.SVG.CircleTick; break;
                    case PeerState.Connecting: glyph = Assets.SVG.Wait; break;
                    default:
                    case PeerState.Unknown: glyph = Assets.SVG.CircleUnknown; statusName = "Not connected"; break;
                }
                _Glyph.InnerHTML = glyph.ToString(16, 16, "#000000");
                _Status.InnerHTML = statusName;
                var conns = _Content.Div("conns");

                if (_Peer.PredecessorEndpoint != null) {
                    conns.Span("◄ " + HtmlEncode(_Peer.PredecessorEndpoint), "pred");
                }
                if (_Peer.SuccessorEndpoint != null) {
                    conns.Span(HtmlEncode(_Peer.SuccessorEndpoint) + " ►", "succ");
                }
            }
        }
    }
}