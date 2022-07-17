using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace g2yx.Services.Utils
{
    public static class AsyncEnumerableExt
    {
        private const long StartSeqNo = 0L;

        public static async IAsyncEnumerable<(T item, long seqNo)> ProcessInParallel<T>(
            this IAsyncEnumerable<T> source, Func<T, Task> processFunc, int degreeOfParallelism, [EnumeratorCancellation] CancellationToken ct)
            where T : class
        {
            const long FakeSeqNo = -1L;

            var taskPool = Enumerable.Repeat(Task.FromResult<(T item, long seqNo)>((null, FakeSeqNo)), degreeOfParallelism).ToList();

            var seqNo = StartSeqNo;
            await foreach (var item in source.WithCancellation(ct))
            {
                var completedTask = await Task.WhenAny(taskPool);
                var ixOfCompletedTask = taskPool.IndexOf(completedTask);

                // if it's not a "fake" initial task
                if (completedTask.Result.seqNo != FakeSeqNo)
                {
                    yield return (completedTask.Result.item, completedTask.Result.seqNo);
                }

                // substitute "free" slot in tasks buffer with new task
                taskPool[ixOfCompletedTask] = ProcessItem(item, seqNo);
            }

            async Task<(T item, long seqNo)> ProcessItem(T item, long seqNo)
            {
                await processFunc(item);
                return (item, seqNo);
            }
        }

        public static async IAsyncEnumerable<T> MakeOrdered<T>(
            this IAsyncEnumerable<(T item, long seqNo)> source, [EnumeratorCancellation] CancellationToken ct)
        {
            // there's no need in threadsafe data structures as we're working with only one thread at a time
            var lastConsequtiveSeqNo = StartSeqNo;
            var lastProcessedItems = new SortedDictionary<long, T>();

            await foreach (var (item, seqNo) in source.WithCancellation(ct))
            {
                lastProcessedItems.Add(seqNo, item);
                foreach (var (orderedSeqNo, orderedItem) in EnsureOrder(lastConsequtiveSeqNo, lastProcessedItems))
                {
                    lastConsequtiveSeqNo = orderedSeqNo;
                    yield return orderedItem;
                }
            }
        }

        private static IEnumerable<(long, T)> EnsureOrder<T>(long lastProcessedSeqNo, SortedDictionary<long, T> processedItems)
        {
            if (processedItems.Count == 0)
                yield break;

            while (processedItems.First().Key == lastProcessedSeqNo + 1)
            {
                var (seqNo, item) = processedItems.First();
                processedItems.Remove(seqNo);
                yield return (seqNo, item);
            }
        }
    }
}
