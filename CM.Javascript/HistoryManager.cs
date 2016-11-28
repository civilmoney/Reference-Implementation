#region License

//
// Civil Money is free and unencumbered software released into the public domain (unlicense.org), unless otherwise
// denoted in the source file.
//

#endregion License

using Bridge.Html5;
using System;

namespace CM.Javascript {

    /// <summary>
    /// Keeps track of which accounts the browser has accessed most recently, for quick access.
    /// Everything is kept in local storage.
    /// </summary>
    internal class HistoryManager {

        public static HistoryManager Instance = new HistoryManager();

        public string[] History;

        private HistoryManager() {
            History = new string[0];
            if (Window.LocalStorage != null
               && Window.LocalStorage.GetItem("history") != null) {
                var hist = Window.LocalStorage.GetItem("history").ToString().Split('\n');
                if (hist != null)
                    History = hist;
            }
        }
        public void AddAccountToViewHistory(string id) {
            for (int i = 0; i < History.Length; i++) {
                if (String.Compare(History[i], id, true) == 0) {
                    History.Splice(i, 1);
                    break;
                }
            }
            History.Splice(0, 0, id);
            if (Window.LocalStorage != null)
                Window.LocalStorage.SetItem("history", History.Join("\n"));
        }

        /// <summary>
        /// For use by mobile hooks.
        /// </summary>
        public void OverwriteHistory(string[] data) {
            History = data;
        }
    }
}