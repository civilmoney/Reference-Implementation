using SichboUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CM.JS {
    class OSD {
        static Element _Current;
        private OSD() {
            
        }
        public static void Clear() {
            if (_Current != null) {
                _Current.RemoveAfter = Times.Long;
                _Current.Removed = () => {
                    _Current = null;
                };
            }
           
        }
        public static void Show(string msg) {

            if (_Current == null) {
                _Current = new Element(
                    style: $"text-align:center; color:#fff;background-color:{Colors.LightText};border-radius:100px;padding:5px 15px;",
                    text: msg) {
                    HorizontalAlignment = Alignment.Center,
                    VerticalAlignment = Alignment.Bottom
                };
                _Current.Margin.Value = new Thickness(30, 60);
                _Current.AnimFlyIn(new SichboUI.RelativeTransform(1, 1, 0, 0, 0, 0, 5),
                     SichboUI.RelativeTransform.Empty, Times.Long,
                     Easing.CubicOut);
                _Current.AnimFlyOut(new SichboUI.RelativeTransform(1, 1, 0, 0, 0, 0, -5),
                    Times.Long,
                   Easing.CubicOut);
                _Current.AnimFadeInOut(Times.Long);
                Retyped.dom.document.body.appendChild(_Current.Html);
                _Current.Play();
            } else {
                _Current.RemoveAfter = null;
               _Current.TextContent = msg;
            }
        }
    }
}
