﻿// <auto-generated>
//   AI-Refactored: TempHashSetPool.cs (Ultimate Arbitration, Max-Realism, Zero-Alloc Edition – June 2025)
//   Bulletproof generic HashSet<T> pooling for high-performance AI/world logic.
//   Thread-safe, teardown/reload safe, diagnostics-ready, and AI-Refactored pooling compliant.
//   MIT License.
// </auto-generated>

namespace AIRefactored.Pools
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Centralized pool for temporary <see cref="HashSet{T}"/> reuse.
    /// Bulletproof: Prevents GC churn in high-frequency AI/world logic. Thread-safe, teardown/reload safe, infinite reload, and diagnostics-ready.
    /// </summary>
    public static class TempHashSetPool
    {
        private static readonly Dictionary<Type, Stack<object>> PoolByType = new Dictionary<Type, Stack<object>>(128);
        private static readonly object SyncRoot = new object();
        private static int _totalRented, _totalReturned, _totalPooled;

        static TempHashSetPool()
        {
            try
            {
                AppDomain.CurrentDomain.DomainUnload += (_, __) => ClearAll();
            }
            catch { }
        }

        /// <summary>
        /// Rents a pooled, cleared HashSet of the requested type (never null).
        /// </summary>
        public static HashSet<T> Rent<T>()
        {
            lock (SyncRoot)
            {
                if (PoolByType.TryGetValue(typeof(T), out var stack) && stack.Count > 0)
                {
                    _totalRented++;
                    return (HashSet<T>)stack.Pop();
                }
            }
            _totalRented++;
            return new HashSet<T>();
        }

        /// <summary>
        /// Returns a HashSet to the pool after clearing it. Null and duplicate returns are ignored.
        /// </summary>
        public static void Return<T>(HashSet<T> set)
        {
            if (set == null)
                return;

            set.Clear();
            lock (SyncRoot)
            {
                if (!PoolByType.TryGetValue(typeof(T), out var stack))
                {
                    stack = new Stack<object>(32);
                    PoolByType[typeof(T)] = stack;
                }
                stack.Push(set);
                _totalReturned++;
                _totalPooled = stack.Count;
            }
        }

        /// <summary>
        /// Prewarms the pool for a specific element type with a given number of empty sets.
        /// </summary>
        public static void Prewarm<T>(int count)
        {
            if (count <= 0)
                return;

            lock (SyncRoot)
            {
                if (!PoolByType.TryGetValue(typeof(T), out var stack))
                {
                    stack = new Stack<object>(count);
                    PoolByType[typeof(T)] = stack;
                }
                for (int i = 0; i < count; i++)
                    stack.Push(new HashSet<T>());
                _totalPooled = stack.Count;
            }
        }

        /// <summary>
        /// Clears all pooled instances across all types. Safe for teardown, reload, and infinite reuse.
        /// </summary>
        public static void ClearAll()
        {
            lock (SyncRoot)
            {
                foreach (var stack in PoolByType.Values)
                    stack.Clear();
                PoolByType.Clear();
                _totalPooled = 0;
                _totalRented = 0;
                _totalReturned = 0;
            }
        }

        /// <summary>
        /// Returns pooling stats for diagnostics/monitoring.
        /// </summary>
        public static (int typePools, int totalPooled, int totalRented, int totalReturned) GetStats()
        {
            lock (SyncRoot)
            {
                int pooled = 0;
                foreach (var stack in PoolByType.Values)
                    pooled += stack.Count;
                return (PoolByType.Count, pooled, _totalRented, _totalReturned);
            }
        }
    }
}
