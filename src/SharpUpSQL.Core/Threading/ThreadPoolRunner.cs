using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SharpUpSQL.Core.Threading
{
    /// <summary>
    /// Parallel execution helper matching PowerUpSQL Invoke-Parallel throttle behavior.
    /// </summary>
    public static class ThreadPoolRunner
    {
        public static void RunParallel<TInput>(
            IEnumerable<TInput> inputs,
            Action<TInput> action,
            int threads)
        {
            if (action == null)
            {
                throw new ArgumentNullException("action");
            }

            var degree = threads <= 0 ? 5 : threads;
            Parallel.ForEach(
                inputs ?? Array.Empty<TInput>(),
                new ParallelOptions { MaxDegreeOfParallelism = degree },
                action);
        }

        public static List<TResult> RunParallel<TInput, TResult>(
            IEnumerable<TInput> inputs,
            Func<TInput, TResult> func,
            int threads)
        {
            if (func == null)
            {
                throw new ArgumentNullException("func");
            }

            var degree = threads <= 0 ? 5 : threads;
            var results = new ConcurrentBag<TResult>();

            Parallel.ForEach(
                inputs ?? Array.Empty<TInput>(),
                new ParallelOptions { MaxDegreeOfParallelism = degree },
                input =>
                {
                    var result = func(input);
                    if (result != null)
                    {
                        results.Add(result);
                    }
                });

            return new List<TResult>(results);
        }

        public static List<TResult> RunParallelMany<TInput, TResult>(
            IEnumerable<TInput> inputs,
            Func<TInput, IEnumerable<TResult>> func,
            int threads)
        {
            if (func == null)
            {
                throw new ArgumentNullException("func");
            }

            var degree = threads <= 0 ? 5 : threads;
            var results = new ConcurrentBag<TResult>();

            Parallel.ForEach(
                inputs ?? Array.Empty<TInput>(),
                new ParallelOptions { MaxDegreeOfParallelism = degree },
                input =>
                {
                    foreach (var result in func(input) ?? Array.Empty<TResult>())
                    {
                        results.Add(result);
                    }
                });

            return new List<TResult>(results);
        }
    }
}
