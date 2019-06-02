using SichboUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CM.JS {
    internal enum Screen {
        NotFound = 0,
        Account = 1,
        NewAccount = 2,
        TransactionScreen = 3,
    }
    /// <summary>
    /// Not strictly necessary but for a array constructor Bridge.NET bug
    /// </summary>
    class ScreenArgs {
        public string[] Url;
    }
    class ScreenBase : Element {

        internal static readonly Dictionary<Screen, Type> s_AllScreens = new Dictionary<Screen, Type>() {
            {Screen.Account, typeof(Screens.AccountScreen) },
            {Screen.NewAccount, typeof(Screens.AccountEditScreen) },
            {Screen.TransactionScreen, typeof(Screens.TransactionScreen) }
        };
        public static ScreenBase Create(Screen s, string[] url) {
            if (!s_AllScreens.TryGetValue(s, out var type)) {
                s_AllScreens.TryGetValue(Screen.NotFound, out type);
            }
            return Activator.CreateInstance(type, new ScreenArgs() { Url = url }).As<ScreenBase>();
        }
        protected Element m_Inner;

        public virtual bool TryHandleUrlChange(string[] url) {
            return false;
        }

        public ScreenBase(ScreenArgs e) {
            AnimFlyIn(new SichboUI.RelativeTransform(2, 2, 0, 0, 0, 0, 0));
            AnimFlyOut(new SichboUI.RelativeTransform(0.5, 0.5, 0, 0, 0, 0, 0), Times.Normal, Easing.CubicIn);
            AnimFadeInOut(Times.Normal);
        }
    }
}
