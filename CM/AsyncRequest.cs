#region License
// 
// Civil Money is free and unencumbered software released into the public domain (unlicense.org), unless otherwise 
// denoted in the source file.
//
#endregion

using System;

namespace CM {

    public interface IAsyncRequest {
        bool IsCancelled { get; set; }
        int ProgressPercent { get; set; }
        CMResult Result { get; set; }

        void Completed(CMResult res);
    }

    /// <summary>
    /// Async/await/Task in Bridge.NET is sketchy, so to keep
    /// everything portable, we'll use this pattern for any potentially
    /// asynchronous code.
    /// </summary>
    public partial class AsyncRequest<T> : IAsyncRequest {
        public bool IsCancelled { get; set; }

        /// <summary>
        /// Set by the caller and used by the callee to return data.
        /// </summary>
        public T Item { get; set; }

        /// <summary>
        /// Raised by the callee upon completion, potentially from a worker thread.
        /// </summary>
        public Action<AsyncRequest<T>> OnComplete { get; set; }

        /// <summary>
        /// Raised by calling UpdateProgress. Client can subscribe to this to get progress feedback.
        /// </summary>
        public Action<AsyncRequest<T>> OnProgress { get; set; }

        /// <summary>
        /// Gets or sets the progress as a percentage.
        /// </summary>
        public int ProgressPercent { get; set; }

        /// <summary>
        /// Set by the callee.
        /// </summary>
        public CMResult Result { get; set; }

        public void Completed(CMResult res) {
            Result = res;
            if (OnComplete != null)
                OnComplete(this);
        }

        public void UpdateProgress(int percent) {
            ProgressPercent = percent;
            if (OnProgress != null)
                OnProgress(this);
        }

    }
}