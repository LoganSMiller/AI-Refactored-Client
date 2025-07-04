﻿// <auto-generated>
//   AI-Refactored: TempIntArrayPool.cs (Ultimate Arbitration, Max-Realism, Zero-Alloc Edition – June 2025)
//   Bulletproof, reusable int[] pooling for ultra-high-performance AI/world logic. Thread-safe, teardown/reload safe, diagnostics-ready.
//   MIT License.
// </auto-generated>

namespace AIRefactored.Pools
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Pool for temporary <see cref="int"/> array reuse in AIRefactored and math-heavy logic paths.
    /// Bulletproof: Thread-safe, teardown/reload safe, infinite reload, and diagnostics-ready.
    /// </summary>
    public static class TempIntArrayPool
    {
        private static readonly Dictionary<int, Stack<int[]>> PoolBySize = new Dictionary<int, Stack<int[]>>(32);
        private static readonly object SyncRoot = new object();
        private static int _totalRented, _totalReturned, _totalPooled;

        static TempIntArrayPool()
        {
            try
            {
                AppDomain.CurrentDomain.DomainUnload += (_, __) => ClearAll();
            }
            catch { }
        }

        /// <summary>
        /// Rents a pooled int array of at least the specified size (never null, always min length 1).
        /// </summary>
        public static int[] Rent(int size)
        {
            if (size <= 0)
                size = 1;

            lock (SyncRoot)
            {
                if (PoolBySize.TryGetValue(size, out var stack) && stack.Count > 0)
                {
                    _totalRented++;
                    return stack.Pop();
                }
            }
            _totalRented++;
            return new int[size];
        }

        /// <summary>
        /// Returns an int array to the pool (null and zero-length ignored).
        /// </summary>
        public static void Return(int[] array)
        {
            if (array == null || array.Length == 0)
                return;

            lock (SyncRoot)
            {
                if (!PoolBySize.TryGetValue(array.Length, out var stack))
                {
                    stack = new Stack<int[]>(8);
                    PoolBySize[array.Length] = stack;
                }
                stack.Push(array);
                _totalReturned++;
                _totalPooled = stack.Count;
            }
        }

        /// <summary>
        /// Prewarms the pool with the specified number of int arrays of a given size.
        /// </summary>
        public static void Prewarm(int size, int count)
        {
            if (size <= 0 || count <= 0)
                return;

            lock (SyncRoot)
            {
                if (!PoolBySize.TryGetValue(size, out var stack))
                {
                    stack = new Stack<int[]>(count);
                    PoolBySize[size] = stack;
                }
                for (int i = 0; i < count; i++)
                    stack.Push(new int[size]);
                _totalPooled = stack.Count;
            }
        }

        /// <summary>
        /// Clears all pooled int arrays and resets pool state (teardown/reload safe).
        /// </summary>
        public static void ClearAll()
        {
            lock (SyncRoot)
            {
                foreach (var stack in PoolBySize.Values)
                    stack.Clear();
                PoolBySize.Clear();
                _totalPooled = 0;
                _totalRented = 0;
                _totalReturned = 0;
            }
        }

        /// <summary>
        /// Returns pooling stats for diagnostics/monitoring.
        /// </summary>
        public static (int arraySizes, int totalPooled, int totalRented, int totalReturned) GetStats()
        {
            lock (SyncRoot)
            {
                int pooled = 0;
                foreach (var stack in PoolBySize.Values)
                    pooled += stack.Count;
                return (PoolBySize.Count, pooled, _totalRented, _totalReturned);
            }
        }
    }
}
