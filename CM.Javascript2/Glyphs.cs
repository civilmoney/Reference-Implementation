using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CM.JS {
    internal class SVG {
        private readonly string m_Data;

        public SVG(double w, double h, string data) {
            Width = w; Height = h;
            m_Data = data.Replace("\r", " ").Replace("\n", " ").Replace("\t", " ");
        }
        public string Data => m_Data;
        public double Height { get; }
        public double Width { get; }
        public string CSS(string fill) {
            if (fill.IndexOf("rgba") == -1)
                fill = new SichboUI.Color(fill).ToRgbaString();
            return $@"background-repeat: no-repeat;background-image: " + CSSUrl(fill) + ";";
        }
        public string CSSUrl(string fill) {
            if (fill.IndexOf("rgba") == -1)
                fill = new SichboUI.Color(fill).ToRgbaString();
            return $@"url(""data:image/svg+xml,%3csvg width='{Width}' height='{Height}' viewBox='0 0 {Width} {Height}' xmlns='http://www.w3.org/2000/svg'%3e%3cpath fill='{fill}' d='{m_Data}'/%3e%3c/svg%3e"")";
        }
    }
    class Glyphs {

        public static readonly SVG CircleUnknown = new SVG(24, 24, @"M12,0C5.4,0,0,5.4,0,12s5.4,12,12,12s12-5.4,12-12S18.6,0,12,0z M14,18c0,0.6-0.4,1-1,1h-2
	c-0.6,0-1-0.4-1-1v-2c0-0.6,0.4-1,1-1h2c0.6,0,1,0.4,1,1V18z M16.8,9.1c0,1.4-0.9,2.1-2,2.8c-1,0.7-1.3,1.3-1.5,1.7
	c-0.1,0.2-0.3,0.3-0.5,0.3h-2.1c-0.3,0-0.5-0.3-0.5-0.6c0.1-0.5,0.2-1.1,0.7-1.6c0.8-0.9,2-1.7,2.3-1.9c0.3-0.2,0.5-0.5,0.5-0.8V9
	c0-0.6-0.4-1-1-1h-1.5c-0.6,0-1,0.4-1,1s-0.4,1-1,1h-1c-0.6,0-1-0.4-1-1c0-2.2,1.8-4,4-4h1.5c2.3,0,4,1.2,4,4V9.1z");

        public static readonly SVG CircleError = new SVG(24, 24, @"M23.4,6.4l-5.9-5.9C17.2,0.2,16.7,0,16.1,0H7.9c-0.5,0-1,0.2-1.4,0.6L0.6,6.4C0.2,6.8,0,7.3,0,7.9
	v8.3c0,0.5,0.2,1,0.6,1.4l5.9,5.9C6.8,23.8,7.3,24,7.9,24h8.3c0.5,0,1-0.2,1.4-0.6l5.9-5.9c0.4-0.4,0.6-0.9,0.6-1.4V7.9
	C24,7.3,23.8,6.8,23.4,6.4z M17.3,14.5c0.2,0.2,0.2,0.5,0,0.7l-2.1,2.1c-0.2,0.2-0.5,0.2-0.7,0L12,14.8l-2.5,2.5
	c-0.2,0.2-0.5,0.2-0.7,0l-2.1-2.1c-0.2-0.2-0.2-0.5,0-0.7L9.2,12L6.7,9.5C6.5,9.3,6.5,9,6.7,8.8l2.1-2.1c0.2-0.2,0.5-0.2,0.7,0
	L12,9.2l2.5-2.5c0.2-0.2,0.5-0.2,0.7,0l2.1,2.1c0.2,0.2,0.2,0.5,0,0.7L14.8,12L17.3,14.5z");

        public static readonly SVG CircleTick = new SVG(24, 24, @"M12,0C5.4,0,0,5.4,0,12s5.4,12,12,12s12-5.4,12-12S18.6,0,12,0z M18.6,8.4L11.3,17
	c-0.4,0.5-1.1,0.5-1.5,0l-3.9-4.3c-0.2-0.2-0.2-0.5,0-0.7L7,10.8c0.2-0.2,0.4-0.2,0.6-0.1l2.6,1.7l6.6-5.7c0.2-0.2,0.5-0.2,0.7,0
	l1,1C18.7,7.9,18.7,8.2,18.6,8.4z");

  public static readonly SVG Warning = new SVG(27, 24.1, @"M26.8,21.9l-12-21.1C14.5,0.3,14,0,13.5,0s-1,0.3-1.3,0.8l-12,21.1c-0.3,0.5-0.3,1,0,1.5
	c0.3,0.5,0.8,0.7,1.3,0.7h24c0.5,0,1-0.3,1.3-0.7C27.1,22.9,27.1,22.3,26.8,21.9z M15.5,20.1c0,0.6-0.4,1-1,1h-2c-0.6,0-1-0.4-1-1
	v-2c0-0.6,0.4-1,1-1h2c0.6,0,1,0.4,1,1V20.1z M15.5,11.1l-0.6,3.6c0,0.2-0.2,0.4-0.5,0.4h-1.8c-0.2,0-0.5-0.2-0.5-0.4l-0.6-3.6v-3
	c0-0.6,0.4-1,1-1h2c0.6,0,1,0.4,1,1V11.1z");
        public static readonly SVG Working = new SVG(24, 26.5, @"M20.5,6.1c2.2,2.2,3.5,5.1,3.5,8.4c0,6.8-5.6,12.3-12.5,12C5.1,26.3-0.1,20.8,0,14.4
	C0.1,8.2,4.9,3.1,11,2.6V0.5c0-0.4,0.5-0.6,0.8-0.4l5.1,3.5c0.3,0.2,0.3,0.6,0,0.8L11.8,8C11.5,8.2,11,8,11,7.6v-2
	c-4.5,0.5-8,4.3-8,8.9c0,5,4.2,9.1,9.3,9c4.9-0.1,8.9-4.4,8.7-9.3c-0.1-2.5-1.2-4.7-2.8-6.2c-0.2-0.2-0.2-0.6,0-0.8l1.7-1.2
	C20.1,5.9,20.3,5.9,20.5,6.1z");
        /*
        public static readonly SVG CircleRemove = new SVG(24, 24, @"M12,0C5.4,0,0,5.4,0,12s5.4,12,12,12s12-5.4,12-12S18.6,0,12,0z M18,13c0,0.6-0.4,1-1,1H7
	c-0.6,0-1-0.4-1-1v-2c0-0.6,0.4-1,1-1h10c0.6,0,1,0.4,1,1V13z");
        public static readonly SVG CircleAdd = new SVG(24, 24, @"M12,0C5.4,0,0,5.4,0,12s5.4,12,12,12s12-5.4,12-12S18.6,0,12,0z M18,13c0,0.6-0.4,1-1,1h-3v3
	c0,0.6-0.4,1-1,1h-2c-0.6,0-1-0.4-1-1v-3H7c-0.6,0-1-0.4-1-1v-2c0-0.6,0.4-1,1-1h3V7c0-0.6,0.4-1,1-1h2c0.6,0,1,0.4,1,1v3h3
	c0.6,0,1,0.4,1,1V13z");
     */
    }
}
