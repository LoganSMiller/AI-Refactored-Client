﻿// <auto-generated>
//   AI-Refactored: WorldTickDispatcher.cs (Supreme Arbitration, Infinite-Reload, Max-Realism Edition – June 2025)
//   Bulletproof, teardown-safe, headless/client parity. All ticking is retry-safe, all errors strictly isolated. No fallback/terminal disables.
//   MIT License.
// </auto-generated>

namespace AIRefactored.Runtime
{
    using System;
    using AIRefactored.AI.Core;
    using AIRefactored.Bootstrap;
    using AIRefactored.Core;
    using BepInEx.Logging;
    using UnityEngine;

    /// <summary>
    /// Globally ticks world systems after initialization using a persistent MonoBehaviour.
    /// Bulletproof: All errors are strictly contained, infinite reload/teardown safe, no fallback disables.
    /// SPT/FIKA/headless/client parity. No leaks or race conditions, always cleans up.
    /// </summary>
    public static class WorldTickDispatcher
    {
        private static GameObject _host;
        private static TickHost _monoHost;
        private static bool _isActive;
        private static bool _isQuitting;

        private static ManualLogSource Logger => Plugin.LoggerInstance;

        /// <summary>
        /// Initializes the tick dispatcher, ensuring a persistent host MonoBehaviour is active.
        /// Infinite reload/teardown safe, idempotent, and retry-guarded.
        /// </summary>
        public static void Initialize()
        {
            if (_isActive || _host != null || _monoHost != null || _isQuitting)
                return;

            try
            {
                _host = new GameObject("AIRefactored.WorldTickDispatcher");
                UnityEngine.Object.DontDestroyOnLoad(_host);

                _monoHost = _host.AddComponent<TickHost>();
                _isActive = true;

                Logger.LogDebug("[WorldTickDispatcher] ✅ Host attached and ticking.");
            }
            catch (Exception ex)
            {
                Logger.LogError("[WorldTickDispatcher] ❌ Initialization failed: " + ex);
            }
        }

        /// <summary>
        /// Shuts down and destroys the tick host and MonoBehaviour. Cleans up for reload, teardown, or quit.
        /// </summary>
        public static void Reset()
        {
            if (!_isActive && _host == null && _monoHost == null)
                return;

            _isActive = false;

            try
            {
                if (_monoHost != null)
                {
                    try { UnityEngine.Object.Destroy(_monoHost); }
                    catch (Exception ex) { Logger.LogError("[WorldTickDispatcher] ❌ Destroy _monoHost failed: " + ex); }
                }

                if (_host != null)
                {
                    try { UnityEngine.Object.Destroy(_host); }
                    catch (Exception ex) { Logger.LogError("[WorldTickDispatcher] ❌ Destroy _host failed: " + ex); }
                }

                Logger.LogDebug("[WorldTickDispatcher] 🧹 Shutdown complete.");
            }
            catch (Exception ex)
            {
                Logger.LogError("[WorldTickDispatcher] ❌ Error during host destroy: " + ex);
            }

            _monoHost = null;
            _host = null;
        }

        /// <summary>
        /// Ticks all world systems every frame; bulletproof to errors, teardown, and host state.
        /// </summary>
        public static void Tick(float deltaTime)
        {
            if (!_isActive || _isQuitting)
                return;
            if (!GameWorldHandler.IsInitialized || !GameWorldHandler.IsHost)
                return;

            try
            {
                WorldBootstrapper.Tick(deltaTime);
            }
            catch (Exception ex)
            {
                Logger.LogError("[WorldTickDispatcher] ❌ Tick error: " + ex);
            }
        }

        /// <summary>
        /// Persistent MonoBehaviour tick host. Routes Unity lifecycle events to static dispatcher, teardown safe.
        /// </summary>
        private sealed class TickHost : MonoBehaviour
        {
            private void Update()
            {
                try
                {
                    Tick(Time.deltaTime);
                }
                catch (Exception ex)
                {
                    Logger.LogError("[WorldTickDispatcher] ❌ Update exception: " + ex);
                }
            }

            private void OnDestroy()
            {
                try
                {
                    if (!_isQuitting)
                        Reset();
                }
                catch (Exception ex)
                {
                    Logger.LogError("[WorldTickDispatcher] ❌ OnDestroy failed: " + ex);
                }
            }

            private void OnApplicationQuit()
            {
                _isQuitting = true;
                Reset();
            }
        }
    }
}
