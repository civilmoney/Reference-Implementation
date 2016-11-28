#region License
// 
// Civil Money is free and unencumbered software released into the public domain (unlicense.org), unless otherwise 
// denoted in the source file.
//
#endregion

using System;

namespace CM.Server {

    /// <summary>
    /// Any important short-term scheduling/timing functionality should use this clock (not the
    /// system time) which is guaranteed to never jump around.
    /// </summary>
    internal static class Clock {
        private static readonly System.Diagnostics.Stopwatch _Clock;

        static Clock() {
            _Clock = new System.Diagnostics.Stopwatch();
            _Clock.Start();
        }

        /// <summary>
        /// Gets the current server's running time.
        /// </summary>
        public static TimeSpan Elapsed {
            get { return _Clock.Elapsed; }
        }
    }
}