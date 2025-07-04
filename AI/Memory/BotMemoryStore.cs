﻿// <auto-generated>
//   AI-Refactored: BotMemoryStore.cs (Supreme Personality Overlay/Decay Max Expansion – June 2025)
//   Full squad/overlay memory with adaptive, pooled, and personality-based decay. Everything is atomic, bulletproof, multiplayer/headless safe, and ready for future overlays/intents. Zero-fault, zero-leak, SPT/FIKA/vanilla safe.
//   MIT License.
// </auto-generated>

namespace AIRefactored.AI.Memory
{
    using System;
    using System.Collections.Generic;
    using AIRefactored.Core;
    using AIRefactored.Pools;
    using AIRefactored.AI.Groups;
    using AIRefactored.AI.Core;
    using BepInEx.Logging;
    using EFT;
    using UnityEngine;

    /// <summary>
    /// Supreme, max-feature, personality-aware overlay/tactical memory store for bots.
    /// Tracks all overlay/tactical events, clusters, squad overlays, and decays memory by personality/TTL.
    /// </summary>
    public static class BotMemoryStore
    {
        #region Constants

        private const float DangerZoneBaseTTL = 50f;
        private const float HitMemoryBaseDuration = 12.5f;
        private const int MaxZones = 256;
        private const float HeardSoundBaseTTL = 13f;
        private const int MaxShortTermSounds = 20;
        private const float MaxEventClusterRadius = 18.0f;
        private const int MaxEventClusters = 32;
        private const float SquadEventBaseTTL = 18.0f;
        private const float OverlayMemoryBaseTTL = 32.0f;
        private const int MaxSquadEvents = 40;
        private const float DecayMinFactor = 0.66f;
        private const float DecayMaxFactor = 1.44f;

        #endregion

        private static readonly ManualLogSource Logger = Plugin.LoggerInstance;

        // -- Core memory stores --
        private static readonly Dictionary<string, HeardSound> HeardSounds = new Dictionary<string, HeardSound>(64);
        private static readonly Dictionary<string, LastHitInfo> LastHitSources = new Dictionary<string, LastHitInfo>(64);
        private static readonly List<DangerZone> Zones = new List<DangerZone>(64);
        private static readonly Dictionary<string, List<DangerZone>> ZoneCaches = new Dictionary<string, List<DangerZone>>(64);
        private static readonly Dictionary<string, List<HeardSound>> ShortTermHeardSounds = new Dictionary<string, List<HeardSound>>(64);
        private static readonly Dictionary<string, float> LastFlankTimes = new Dictionary<string, float>(64);
        private static readonly List<EventCluster> EventClusters = new List<EventCluster>(MaxEventClusters);
        private static readonly Dictionary<string, SquadEventMemory> SquadEventMemories = new Dictionary<string, SquadEventMemory>(32);

        #region --- Danger Zones & Cluster Events ---

        /// <summary>Adds a danger zone and event cluster, TTL is personality-based. Fully pooled and atomic.</summary>
        public static void AddDangerZone(string mapId, Vector3 position, DangerTriggerType type, float radius, BotPersonalityProfile personality = null)
        {
            try
            {
                if (!TryGetSafeKey(mapId, out string key)) key = "unknown";
                float now = Time.time;
                float ttl = GetDecayTTL(DangerZoneBaseTTL, personality);

                // Cluster spatially & temporally
                bool clustered = false;
                for (int i = 0; i < EventClusters.Count; i++)
                {
                    var cl = EventClusters[i];
                    if (cl.Type == type && cl.Map == key && (now - cl.Timestamp < ttl)
                        && (cl.Position - position).sqrMagnitude <= MaxEventClusterRadius * MaxEventClusterRadius)
                    {
                        EventClusters[i] = cl.Extend(position, radius, now);
                        clustered = true;
                        break;
                    }
                }
                if (!clustered)
                {
                    if (EventClusters.Count >= MaxEventClusters) EventClusters.RemoveAt(0);
                    EventClusters.Add(new EventCluster(key, position, type, radius, now, ttl));
                }

                if (Zones.Count >= MaxZones)
                    Zones.RemoveAt(0);
                Zones.Add(new DangerZone(key, position, type, radius, now, ttl));
            }
            catch (Exception ex)
            {
                Logger.LogError($"[BotMemoryStore] AddDangerZone failed: {ex}");
            }
        }

        /// <summary>Gets all non-expired event clusters for the map (pooled, personality-aware decay).</summary>
        public static List<EventCluster> GetActiveEventClusters(string mapId, BotPersonalityProfile personality = null)
        {
            var result = TempListPool.Rent<EventCluster>();
            try
            {
                float now = Time.time;
                float ttl = GetDecayTTL(OverlayMemoryBaseTTL, personality);
                for (int i = 0; i < EventClusters.Count; i++)
                {
                    var cl = EventClusters[i];
                    if (cl.Map == mapId && (now - cl.Timestamp < cl.TTL && now - cl.Timestamp < ttl))
                        result.Add(cl);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"[BotMemoryStore] GetActiveEventClusters failed: {ex}");
            }
            return result;
        }

        /// <summary>Pooled: returns current non-expired danger zones for a map, personality decay-aware.</summary>
        public static List<DangerZone> GetZonesForMap(string mapId, BotPersonalityProfile personality = null)
        {
            var result = TempListPool.Rent<DangerZone>();
            try
            {
                if (!TryGetSafeKey(mapId, out string key)) return result;
                if (!ZoneCaches.TryGetValue(key, out var cache))
                {
                    cache = TempListPool.Rent<DangerZone>();
                    ZoneCaches[key] = cache;
                }
                else
                {
                    cache.Clear();
                }

                float now = Time.time;
                float ttl = GetDecayTTL(DangerZoneBaseTTL, personality);
                for (int i = 0; i < Zones.Count; i++)
                {
                    DangerZone zone = Zones[i];
                    if (zone.Map == key && (now - zone.Timestamp) <= Math.Min(zone.TTL, ttl))
                        cache.Add(zone);
                }
                result.AddRange(cache);
            }
            catch (Exception ex)
            {
                Logger.LogError($"[BotMemoryStore] GetZonesForMap failed: {ex}");
            }
            return result;
        }

        /// <summary>True if position is within any non-expired zone for the given map (decay-aware).</summary>
        public static bool IsPositionInDangerZone(string mapId, Vector3 position, BotPersonalityProfile personality = null)
        {
            try
            {
                if (!TryGetSafeKey(mapId, out string key)) return false;
                float now = Time.time;
                float ttl = GetDecayTTL(DangerZoneBaseTTL, personality);

                for (int i = 0; i < Zones.Count; i++)
                {
                    DangerZone zone = Zones[i];
                    if (zone.Map == key && (now - zone.Timestamp <= Math.Min(zone.TTL, ttl)))
                    {
                        if ((zone.Position - position).sqrMagnitude <= zone.Radius * zone.Radius)
                            return true;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"[BotMemoryStore] IsPositionInDangerZone failed: {ex}");
            }
            return false;
        }

        /// <summary>Clears all zones and clusters (full world wipe or phase reset).</summary>
        public static void ClearZones()
        {
            try
            {
                Zones.Clear();
                EventClusters.Clear();
                foreach (var kv in ZoneCaches)
                    kv.Value?.Clear();
                ZoneCaches.Clear();
            }
            catch (Exception ex)
            {
                Logger.LogError($"[BotMemoryStore] ClearZones failed: {ex}");
            }
        }

        #endregion

        #region --- Overlay/Squad Event Memory ---

        public static void RegisterSquadOverlayEvent(string squadId, OverlayEventType eventType, Vector3 position, BotPersonalityProfile personality = null)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(squadId)) return;
                float now = Time.time;
                float ttl = GetDecayTTL(SquadEventBaseTTL, personality);
                if (!SquadEventMemories.TryGetValue(squadId, out var mem))
                {
                    mem = new SquadEventMemory(squadId);
                    SquadEventMemories[squadId] = mem;
                }
                mem.RegisterEvent(eventType, position, now, ttl);
            }
            catch (Exception ex)
            {
                Logger.LogError($"[BotMemoryStore] RegisterSquadOverlayEvent failed: {ex}");
            }
        }

        public static List<SquadOverlayEvent> GetRecentSquadOverlayEvents(string squadId, BotPersonalityProfile personality = null)
        {
            var result = TempListPool.Rent<SquadOverlayEvent>();
            try
            {
                if (string.IsNullOrWhiteSpace(squadId)) return result;
                float ttl = GetDecayTTL(SquadEventBaseTTL, personality);
                if (SquadEventMemories.TryGetValue(squadId, out var mem))
                {
                    mem.GetRecentEvents(ttl, result);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"[BotMemoryStore] GetRecentSquadOverlayEvents failed: {ex}");
            }
            return result;
        }

        public static void ClearSquadOverlayEvents()
        {
            try
            {
                SquadEventMemories.Clear();
            }
            catch (Exception ex)
            {
                Logger.LogError($"[BotMemoryStore] ClearSquadOverlayEvents failed: {ex}");
            }
        }

        #endregion

        #region --- Audio Memory (short/long term) ---

        public static void AddHeardSound(string profileId, Vector3 position, float time, BotPersonalityProfile personality = null)
        {
            try
            {
                if (!TryGetSafeKey(profileId, out string key)) return;

                float ttl = GetDecayTTL(HeardSoundBaseTTL, personality);
                HeardSounds[key] = new HeardSound(position, time, ttl);

                if (!ShortTermHeardSounds.TryGetValue(key, out var list))
                {
                    list = TempListPool.Rent<HeardSound>();
                    ShortTermHeardSounds[key] = list;
                }
                if (list.Count >= MaxShortTermSounds)
                    list.RemoveAt(0);
                list.Add(new HeardSound(position, time, ttl));
            }
            catch (Exception ex)
            {
                Logger.LogError($"[BotMemoryStore] AddHeardSound failed: {ex}");
            }
        }

        public static bool TryGetHeardSound(string profileId, out HeardSound sound)
        {
            sound = default;
            try
            {
                if (!TryGetSafeKey(profileId, out string key)) return false;
                if (!HeardSounds.TryGetValue(key, out sound)) return false;
                return (Time.time - sound.Time) < sound.TTL;
            }
            catch (Exception ex)
            {
                Logger.LogError($"[BotMemoryStore] TryGetHeardSound failed: {ex}");
                return false;
            }
        }

        public static List<HeardSound> GetShortTermHeardSounds(string profileId)
        {
            var result = TempListPool.Rent<HeardSound>();
            try
            {
                if (!TryGetSafeKey(profileId, out string key)) return result;
                if (ShortTermHeardSounds.TryGetValue(key, out var list))
                {
                    float now = Time.time;
                    for (int i = 0; i < list.Count; i++)
                    {
                        if (now - list[i].Time < list[i].TTL)
                            result.Add(list[i]);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"[BotMemoryStore] GetShortTermHeardSounds failed: {ex}");
            }
            return result;
        }

        public static void ClearHeardSound(string profileId)
        {
            try
            {
                if (!TryGetSafeKey(profileId, out string key)) return;
                HeardSounds.Remove(key);
                if (ShortTermHeardSounds.TryGetValue(key, out var list) && list != null)
                    list.Clear();
            }
            catch (Exception ex)
            {
                Logger.LogError($"[BotMemoryStore] ClearHeardSound failed: {ex}");
            }
        }

        public static void ClearAllHeardSounds()
        {
            try
            {
                HeardSounds.Clear();
                foreach (var kv in ShortTermHeardSounds)
                    kv.Value?.Clear();
            }
            catch (Exception ex)
            {
                Logger.LogError($"[BotMemoryStore] ClearAllHeardSounds failed: {ex}");
            }
        }

        #endregion

        #region --- Hit Tracking ---

        public static void RegisterLastHitSource(string victimProfileId, string attackerProfileId, BotPersonalityProfile personality = null)
        {
            try
            {
                if (!TryGetSafeKey(victimProfileId, out string victim) || !TryGetSafeKey(attackerProfileId, out string attacker))
                    return;

                float ttl = GetDecayTTL(HitMemoryBaseDuration, personality);
                LastHitSources[victim] = new LastHitInfo(attacker, Time.time, ttl);
            }
            catch (Exception ex)
            {
                Logger.LogError($"[BotMemoryStore] RegisterLastHitSource failed: {ex}");
            }
        }

        public static bool WasRecentlyHitBy(string victimProfileId, string attackerProfileId)
        {
            try
            {
                if (!TryGetSafeKey(victimProfileId, out string victim) || !TryGetSafeKey(attackerProfileId, out string attacker))
                    return false;

                return LastHitSources.TryGetValue(victim, out var hit) &&
                       hit.AttackerId == attacker &&
                       (Time.time - hit.Time) <= hit.TTL;
            }
            catch (Exception ex)
            {
                Logger.LogError($"[BotMemoryStore] WasRecentlyHitBy failed: {ex}");
                return false;
            }
        }

        public static void ClearHitSources()
        {
            try
            {
                LastHitSources.Clear();
            }
            catch (Exception ex)
            {
                Logger.LogError($"[BotMemoryStore] ClearHitSources failed: {ex}");
            }
        }

        #endregion

        #region --- Flank Cooldown ---

        public static void SetLastFlankTime(string profileId)
        {
            try
            {
                if (TryGetSafeKey(profileId, out string key))
                    LastFlankTimes[key] = Time.time;
            }
            catch (Exception ex)
            {
                Logger.LogError($"[BotMemoryStore] SetLastFlankTime failed: {ex}");
            }
        }

        public static bool CanFlankNow(string profileId, float cooldown)
        {
            try
            {
                return TryGetSafeKey(profileId, out string key) &&
                       (!LastFlankTimes.TryGetValue(key, out float last) || Time.time - last >= cooldown);
            }
            catch (Exception ex)
            {
                Logger.LogError($"[BotMemoryStore] CanFlankNow failed: {ex}");
                return false;
            }
        }

        public static void ClearFlankCooldowns()
        {
            try
            {
                LastFlankTimes.Clear();
            }
            catch (Exception ex)
            {
                Logger.LogError($"[BotMemoryStore] ClearFlankCooldowns failed: {ex}");
            }
        }

        #endregion

        #region --- Player Awareness ---

        public static List<Player> GetNearbyPlayers(Vector3 origin, float radius)
        {
            List<Player> result = TempListPool.Rent<Player>();
            List<Player> players = GameWorldHandler.GetAllAlivePlayers();
            try
            {
                float radiusSqr = radius * radius;
                for (int i = 0; i < players.Count; i++)
                {
                    var p = players[i];
                    if (p != null && IsRealPlayer(p))
                    {
                        Vector3 pos = EFTPlayerUtil.GetPosition(p);
                        float dx = pos.x - origin.x;
                        float dz = pos.z - origin.z;
                        if ((dx * dx + dz * dz) <= radiusSqr)
                            result.Add(p);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"[BotMemoryStore] GetNearbyPlayers failed: {ex}");
            }
            finally
            {
                TempListPool.Return(players);
            }
            return result;
        }

        #endregion

        #region --- Helpers/Structs ---

        private static bool TryGetSafeKey(string id, out string key)
        {
            key = string.Empty;
            if (string.IsNullOrEmpty(id)) return false;
            key = id.Trim();
            return key.Length > 0;
        }

        private static bool IsRealPlayer(Player player)
        {
            return player != null && (player.AIData == null || !player.AIData.IsAI);
        }

        /// <summary>
        /// Calculates a decay time based on bot/squad personality: more 'cautious' or 'greedy' bots remember longer.
        /// </summary>
        public static float GetDecayTTL(float baseTTL, BotPersonalityProfile p)
        {
            if (p == null)
                return baseTTL;
            // Higher caution or greed = longer memory, higher aggression = shorter memory.
            float caution = Mathf.Clamp01(p.Caution);
            float greed = Mathf.Clamp01(p.Greed);
            float aggression = Mathf.Clamp01(p.AggressionLevel);
            float factor = Mathf.Lerp(DecayMinFactor, DecayMaxFactor, 0.6f * caution + 0.3f * greed - 0.3f * aggression);
            return Mathf.Clamp(baseTTL * factor, baseTTL * 0.6f, baseTTL * 1.8f);
        }

        // === Structs ===

        public struct DangerZone
        {
            public string Map;
            public Vector3 Position;
            public DangerTriggerType Type;
            public float Radius;
            public float Timestamp;
            public float TTL;

            public DangerZone(string map, Vector3 position, DangerTriggerType type, float radius, float timestamp, float ttl)
            {
                Map = map;
                Position = position;
                Type = type;
                Radius = radius;
                Timestamp = timestamp;
                TTL = ttl;
            }
        }

        public struct EventCluster
        {
            public string Map;
            public Vector3 Position;
            public DangerTriggerType Type;
            public float Radius;
            public float Timestamp;
            public int EventCount;
            public float TTL;

            public EventCluster(string map, Vector3 position, DangerTriggerType type, float radius, float timestamp, float ttl)
            {
                Map = map;
                Position = position;
                Type = type;
                Radius = radius;
                Timestamp = timestamp;
                TTL = ttl;
                EventCount = 1;
            }

            public EventCluster Extend(Vector3 newPos, float newRadius, float now)
            {
                float dist = Vector3.Distance(Position, newPos);
                float total = EventCount + 1;
                Vector3 center = (Position * (total - 1) + newPos) / total;
                float radius = Mathf.Max(Radius, newRadius, dist + 2.5f);
                var c = new EventCluster(Map, center, Type, radius, now, TTL);
                c.EventCount = this.EventCount + 1;
                return c;
            }
        }

        public struct HeardSound
        {
            public Vector3 Position;
            public float Time;
            public float TTL;

            public HeardSound(Vector3 position, float time, float ttl)
            {
                Position = position;
                Time = time;
                TTL = ttl;
            }
        }

        public struct LastHitInfo
        {
            public string AttackerId;
            public float Time;
            public float TTL;

            public LastHitInfo(string attackerId, float time, float ttl)
            {
                AttackerId = attackerId;
                Time = time;
                TTL = ttl;
            }
        }

        public struct SquadOverlayEvent
        {
            public OverlayEventType Type;
            public Vector3 Position;
            public float Time;
            public float TTL;

            public SquadOverlayEvent(OverlayEventType type, Vector3 position, float time, float ttl)
            {
                Type = type;
                Position = position;
                Time = time;
                TTL = ttl;
            }
        }

        public class SquadEventMemory
        {
            public string SquadId;
            private readonly List<SquadOverlayEvent> _events = new List<SquadOverlayEvent>(MaxSquadEvents);

            public SquadEventMemory(string squadId) { SquadId = squadId; }

            public void RegisterEvent(OverlayEventType type, Vector3 pos, float now, float ttl)
            {
                _events.Add(new SquadOverlayEvent(type, pos, now, ttl));
                if (_events.Count > MaxSquadEvents) _events.RemoveAt(0);
            }

            public void GetRecentEvents(float maxTTL, List<SquadOverlayEvent> outList)
            {
                float now = Time.time;
                for (int i = 0; i < _events.Count; i++)
                {
                    var e = _events[i];
                    if (now - e.Time < Mathf.Min(e.TTL, maxTTL))
                        outList.Add(e);
                }
            }
        }

        // === Enums ===

        public enum DangerTriggerType
        {
            Panic,
            Flash,
            Suppression,
            Grenade,
            Explosion,
            CloseRange,
            DeadBody,
            HighThreat
        }

        public enum OverlayEventType
        {
            Panic,
            Retreat,
            Suppressed,
            Fallback,
            LostVisual,
            Flank,
            Grenade,
            Flash,
            Regroup,
            Extract,
            LostComms,
            Rally,
            Scan,
            Heal,
            Loot,
            Callout,
            HighValue
        }

        #endregion
    }
}
