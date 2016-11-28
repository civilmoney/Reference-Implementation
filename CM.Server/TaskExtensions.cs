#region License
// 
// Civil Money is free and unencumbered software released into the public domain (unlicense.org), unless otherwise 
// denoted in the source file.
//
#endregion

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CM.Server
{
    public static class Extensions {
        
        // Useful async enumeration pattern from https://blogs.msdn.microsoft.com/pfxteam/2012/03/04/implementing-a-simple-foreachasync/

        public static Task ForEachAsync<TSource, TResult>(
            this IEnumerable<TSource> source, int maxConcurrency,
            Func<TSource, Task<TResult>> taskSelector, Action<TSource, TResult> resultProcessor) {
            // SemaphoreSlim.WaitHandle is never created so dispose is a no-op and OK to let 
            // GC clean up at some later time.
            var limit = new System.Threading.SemaphoreSlim(maxConcurrency, maxConcurrency); 
            return Task.WhenAll(
                    from item in source
                    select ProcessAsync(item, taskSelector, resultProcessor, limit));
        }

        private static async Task ProcessAsync<TSource, TResult>(
            TSource item,
            Func<TSource, Task<TResult>> taskSelector, Action<TSource, TResult> resultProcessor,
             System.Threading.SemaphoreSlim limit) {
            TResult result = await taskSelector(item);
            await limit.WaitAsync();
            try {
                resultProcessor(item, result);
            } finally {
                limit.Release();
            }
        }
    }
}
