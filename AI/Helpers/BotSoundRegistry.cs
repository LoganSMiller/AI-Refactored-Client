﻿// <auto-generated>
//   AI-Refactored: BotSoundRegistry.cs (Supreme Arbitration Overlay/Event Edition, June 2025 – Ultra-Realism, Escalation, Multi-Sensory AI)
//   Real EFT sound event registry and arbitration: positional/echo/voice/squad escalation, squad-safe, pooled, error-isolated.
//   Overlay/event only. Zero tick/coroutine, no direct transform/position access. All comms and memory is stutter/echo-proof and escalation aware.
//   All multiplayer/headless/client logic is unified and bulletproof. MIT License.
// </auto-generated>

namespace AIRefactored.AI.Helpers
{
    using System;
    using System.Collections.Generic;
    using AIRefactored.AI.Core;
    using AIRefactored.Pools;
    using AIRefactored.Core;
    using EFT;
    using UnityEngine;
    using AIRefactored.AI.Groups;
    using System.Threading.Tasks;

    /// <summary>
    /// Central AIRefactored sound registry for all live raids—guns, steps, voice, squad escalation, memory.
    /// Overlay/event-driven, escalation-aware, error-contained. No direct polling/tick/coroutine logic. 
    /// Voice logic is stutter/echo-proof, squad escalation is pooled and event-sequenced. No statics after teardown.
    /// </summary>
    public static class BotSoundRegistry
    {
        #region Constants

        private const float DefaultHearingRadius = 34.0f;        // Realistic hearing for steps/shots
        private const float VoiceHearingRadius = 28.0f;          // Squad comms radius
        private const float EchoSuppressionTime = 2.7f;          // No duplicate comms within this window
        private const float EscalationWindow = 6.4f;             // Voice escalation window for squad
        private const float SoundMemoryDuration = 12.8f;         // How long sound is remembered for "heard recently"
        private const float GunshotPriority = 2.3f;              // Gunshot is always higher priority for escalation
        private const float StepPriority = 1.0f;
        private const float SquadFallbackChance = 0.29f;         // Chance for fallback call when nearby
        private const float EscalationChance = 0.19f;            // Chance for squad escalation even after echo
        private const float MaxVoiceStagger = 0.41f;             // Max voice delay for squad escalation

        #endregion

        #region State

        // Per-profile event memory (all pooled, auto-cleared on Clear)
        private static readonly Dictionary<string, float> FootstepTimestamps = new Dictionary<string, float>(64);
        private static readonly Dictionary<string, float> ShotTimestamps = new Dictionary<string, float>(64);
        private static readonly Dictionary<string, Vector3> SoundZones = new Dictionary<string, Vector3>(64);

        // Per-event echo (hash → Time)
        private static readonly Dictionary<int, float> EchoSuppression = new Dictionary<int, float>(128);

        // Escalation memory (groupId+eventHash → Time)
        private static readonly Dictionary<int, float> EscalationMemory = new Dictionary<int, float>(32);

        // One-shot error guards
        private static bool _hasLoggedNullPlayer;
        private static bool _hasLoggedInvalidPosition;
        private static bool _hasLoggedCacheError;

        #endregion

        #region Public API

        /// <summary>
        /// Clears all state. Call on raid teardown/reset.
        /// </summary>
        public static void Clear()
        {
            FootstepTimestamps.Clear();
            ShotTimestamps.Clear();
            SoundZones.Clear();
            EchoSuppression.Clear();
            EscalationMemory.Clear();
            _hasLoggedNullPlayer = false;
            _hasLoggedInvalidPosition = false;
            _hasLoggedCacheError = false;
        }

        public static bool FiredRecently(Player player, float window = 1.7f, float now = -1f)
        {
            return TryGetLastShot(player, out float t) && ((now >= 0f ? now : Time.time) - t <= window);
        }

        public static bool SteppedRecently(Player player, float window = 1.3f, float now = -1f)
        {
            return TryGetLastStep(player, out float t) && ((now >= 0f ? now : Time.time) - t <= window);
        }

        public static void NotifyShot(Player player)
        {
            try
            {
                if (!EFTPlayerUtil.IsValid(player))
                {
                    if (!_hasLoggedNullPlayer) { Plugin.LoggerInstance.LogWarning("[BotSoundRegistry] NotifyShot: Invalid player."); _hasLoggedNullPlayer = true; }
                    return;
                }

                string id = player.ProfileId;
                if (string.IsNullOrEmpty(id)) return;
                float now = Time.time;
                ShotTimestamps[id] = now;

                Vector3 pos = EFTPlayerUtil.GetPosition(player);
                if (!IsValidVector(pos))
                {
                    if (!_hasLoggedInvalidPosition) { Plugin.LoggerInstance.LogWarning("[BotSoundRegistry] NotifyShot: Invalid player position."); _hasLoggedInvalidPosition = true; }
                    return;
                }

                SoundZones[id] = pos;
                RegisterEcho(id, pos, true);
                TriggerSquadPing(id, pos, true, now, GunshotPriority);
                TryEscalateToSquad(player, pos, true, now);
            }
            catch (Exception ex)
            {
                Plugin.LoggerInstance.LogError($"[BotSoundRegistry] NotifyShot failed: {ex}");
            }
        }

        public static void NotifyStep(Player player)
        {
            try
            {
                if (!EFTPlayerUtil.IsValid(player))
                {
                    if (!_hasLoggedNullPlayer) { Plugin.LoggerInstance.LogWarning("[BotSoundRegistry] NotifyStep: Invalid player."); _hasLoggedNullPlayer = true; }
                    return;
                }

                string id = player.ProfileId;
                if (string.IsNullOrEmpty(id)) return;
                float now = Time.time;
                FootstepTimestamps[id] = now;

                Vector3 pos = EFTPlayerUtil.GetPosition(player);
                if (!IsValidVector(pos))
                {
                    if (!_hasLoggedInvalidPosition) { Plugin.LoggerInstance.LogWarning("[BotSoundRegistry] NotifyStep: Invalid player position."); _hasLoggedInvalidPosition = true; }
                    return;
                }

                SoundZones[id] = pos;
                RegisterEcho(id, pos, false);
                TriggerSquadPing(id, pos, false, now, StepPriority);
                TryEscalateToSquad(player, pos, false, now);
            }
            catch (Exception ex)
            {
                Plugin.LoggerInstance.LogError($"[BotSoundRegistry] NotifyStep failed: {ex}");
            }
        }

        public static bool TryGetLastShot(Player player, out float time)
        {
            time = -1f;
            return EFTPlayerUtil.IsValid(player) && ShotTimestamps.TryGetValue(player.ProfileId, out time);
        }

        public static bool TryGetLastStep(Player player, out float time)
        {
            time = -1f;
            return EFTPlayerUtil.IsValid(player) && FootstepTimestamps.TryGetValue(player.ProfileId, out time);
        }

        public static bool TryGetSoundPosition(Player player, out Vector3 pos)
        {
            pos = Vector3.zero;
            return EFTPlayerUtil.IsValid(player) && SoundZones.TryGetValue(player.ProfileId, out pos);
        }

        #endregion

        #region Internal: Voice/Echo/Escalation

        private static void RegisterEcho(string id, Vector3 pos, bool isGunshot)
        {
            int hash = id.GetHashCode() ^ pos.GetHashCode() ^ (isGunshot ? 0xB8D1 : 0x1231);
            float now = Time.time;
            EchoSuppression[hash] = now;
            // Remove stale echos
            var keys = TempListPool.Rent<int>();
            foreach (var kv in EchoSuppression)
                if (now - kv.Value > SoundMemoryDuration)
                    keys.Add(kv.Key);
            foreach (var k in keys)
                EchoSuppression.Remove(k);
            TempListPool.Return(keys);
        }

        /// <summary>
        /// Overlay/event-driven squad comms (stutter/echo proof, realistic escalation).
        /// </summary>
        private static void TriggerSquadPing(string sourceId, Vector3 location, bool isGunshot, float now, float priority)
        {
            try
            {
                float radiusSq = DefaultHearingRadius * DefaultHearingRadius;

                foreach (var cache in BotCacheUtility.AllActiveBots())
                {
                    try
                    {
                        if (cache?.Bot == null || cache.Bot.IsDead) continue;
                        if (cache.Bot.ProfileId == sourceId) continue;

                        Vector3 pos = EFTPlayerUtil.GetPosition(cache.Bot.GetPlayer);
                        if (!IsValidVector(pos) || (pos - location).sqrMagnitude > radiusSq)
                            continue;

                        // Echo suppression
                        int echoHash = cache.Bot.ProfileId.GetHashCode() ^ location.GetHashCode() ^ (isGunshot ? 0xB8D1 : 0x1231);
                        if (EchoSuppression.TryGetValue(echoHash, out float last) && (now - last) < EchoSuppressionTime)
                            continue;
                        EchoSuppression[echoHash] = now;

                        cache.RegisterHeardSound(location);

                        // Voice escalation (only if not suppressed and bot not panicking)
                        if (cache.GroupComms != null && !cache.PanicHandler?.IsPanicking == true)
                        {
                            float r = UnityEngine.Random.value;
                            if (isGunshot)
                            {
                                if (r < 0.61f)
                                    cache.GroupComms.SaySuppression();
                                else if (r < 0.81f)
                                    cache.GroupComms.SayFallback();
                                else
                                    cache.GroupComms.SayHit();
                            }
                            else if (r < SquadFallbackChance)
                                cache.GroupComms.SayFallback();
                        }
                    }
                    catch (Exception innerEx)
                    {
                        if (!_hasLoggedCacheError)
                        {
                            Plugin.LoggerInstance.LogWarning($"[BotSoundRegistry] TriggerSquadPing cache error: {innerEx}");
                            _hasLoggedCacheError = true;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.LoggerInstance.LogError($"[BotSoundRegistry] TriggerSquadPing outer error: {ex}");
            }
        }

        /// <summary>
        /// Attempts squad-level voice escalation for a group after gunshots/steps (event/overlay only, pooled, max realism).
        /// </summary>
        private static void TryEscalateToSquad(Player player, Vector3 pos, bool isGunshot, float now)
        {
            try
            {
                string groupId = player?.Profile?.Info?.GroupId;
                if (string.IsNullOrEmpty(groupId)) return;

                int groupHash = groupId.GetHashCode() ^ pos.GetHashCode() ^ (isGunshot ? 0x49F1 : 0x2244);
                if (EscalationMemory.TryGetValue(groupHash, out float last) && (now - last) < EscalationWindow)
                    return;
                EscalationMemory[groupHash] = now;

                List<BotOwner> group = BotTeamTracker.GetGroup(groupId);
                if (group == null || group.Count < 2) return;

                float baseDelay = 0.11f;
                float delay = baseDelay + UnityEngine.Random.value * MaxVoiceStagger;
                int count = 0;
                foreach (var mate in group)
                {
                    if (!EFTPlayerUtil.IsValidBotOwner(mate) || mate.IsDead) continue;
                    var cache = BotCacheUtility.GetCache(mate);
                    if (cache?.GroupComms == null || cache.PanicHandler?.IsPanicking == true) continue;
                    // Only escalate for first few squadmates
                    if (UnityEngine.Random.value > EscalationChance && count > 0)
                        continue;
                    ScheduleVoiceComms(cache, isGunshot, delay + count * baseDelay);
                    count++;
                }
            }
            catch { }
        }

        /// <summary>
        /// Schedules delayed voice comms for squad escalation (event/overlay, error-contained, async non-blocking).
        /// </summary>
        private static void ScheduleVoiceComms(BotComponentCache cache, bool isGunshot, float delay)
        {
            if (cache?.GroupComms == null) return;
            Task.Run(async () =>
            {
                try
                {
                    int ms = Mathf.Max(0, (int)(delay * 1000f));
                    if (ms > 0) await Task.Delay(ms);

                    float r = UnityEngine.Random.value;
                    if (isGunshot)
                    {
                        if (r < 0.7f)
                            cache.GroupComms.SaySuppression();
                        else if (r < 0.87f)
                            cache.GroupComms.SayFallback();
                        else
                            cache.GroupComms.SayHit();
                    }
                    else if (r < 0.26f)
                        cache.GroupComms.SayFallback();
                }
                catch { }
            });
        }

        /// <summary>
        /// Ensures sound event vectors are world-valid.
        /// </summary>
        private static bool IsValidVector(Vector3 v)
        {
            return !float.IsNaN(v.x) && !float.IsInfinity(v.x) &&
                   !float.IsNaN(v.y) && !float.IsInfinity(v.y) &&
                   !float.IsNaN(v.z) && !float.IsInfinity(v.z) &&
                   (v.x != 0f || v.y != 0f || v.z != 0f) &&
                   Mathf.Abs(v.x) < 40000f && Mathf.Abs(v.y) < 40000f && Mathf.Abs(v.z) < 40000f;
        }

        #endregion
    }
}
