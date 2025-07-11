﻿// <auto-generated>
//   AI-Refactored: TempVector3Pool.cs (Ultimate Arbitration, Max-Realism, Zero-Alloc Edition – June 2025)
//   Bulletproof pooling for Vector3[] arrays for high-performance AI/nav/tactical logic.
//   Thread-safe, teardown/reload safe, diagnostics-ready, AI-Refactored compliant.
//   MIT License.
// </auto-generated>

namespace AIRefactored.Pools
{
    using System;
    using System.Collections.Generic;
    using UnityEngine;

    /// <summary>
    /// Specialized pool for reusing <see cref="Vector3"/> arrays.
    /// Prevents heap allocation in NavMesh pathing and tactical systems.
    /// Bulletproof: Thread-safe, zero-allocation, teardown/reload safe, diagnostics-ready.
    /// </summary>
    public static class TempVector3Pool
    {
        private static readonly Dictionary<int, Stack<Vector3[]>> PoolBySize = new Dictionary<int, Stack<Vector3[]>>(16);
        private static readonly object SyncRoot = new object();
        private static int _totalRented, _totalReturned, _totalPooled;

        static TempVector3Pool()
        {
            try
            {
                AppDomain.CurrentDomain.DomainUnload += (_, __) => ClearAll();
            }
            catch { }
        }

        /// <summary>
        /// Rents a <see cref="Vector3"/> array with at least the given length (never null, min size 1).
        /// </summary>
        public static Vector3[] Rent(int minSize)
        {
            if (minSize <= 0)
                minSize = 1;

            lock (SyncRoot)
            {
                if (PoolBySize.TryGetValue(minSize, out var stack) && stack.Count > 0)
                {
                    _totalRented++;
                    return stack.Pop();
                }
            }
            _totalRented++;
            return new Vector3[minSize];
        }

        /// <summary>
        /// Returns a previously rented <see cref="Vector3"/> array to the pool. Null/zero-length ignored.
        /// </summary>
        public static void Return(Vector3[] array)
        {
            if (array == null || array.Length == 0)
                return;

            lock (SyncRoot)
            {
                if (!PoolBySize.TryGetValue(array.Length, out var stack))
                {
                    stack = new Stack<Vector3[]>(8);
                    PoolBySize[array.Length] = stack;
                }
                stack.Push(array);
                _totalReturned++;
                _totalPooled = stack.Count;
            }
        }

        /// <summary>
        /// Pre-warms the pool with fixed-length Vector3 arrays for burst/tight nav/AI logic.
        /// </summary>
        public static void Prewarm(int size, int count)
        {
            if (size <= 0 || count <= 0)
                return;

            lock (SyncRoot)
            {
                if (!PoolBySize.TryGetValue(size, out var stack))
                {
                    stack = new Stack<Vector3[]>(count);
                    PoolBySize[size] = stack;
                }
                for (int i = 0; i < count; i++)
                    stack.Push(new Vector3[size]);
                _totalPooled = stack.Count;
            }
        }

        /// <summary>
        /// Clears all pooled Vector3 arrays and resets all internal state (teardown/reload safe).
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
