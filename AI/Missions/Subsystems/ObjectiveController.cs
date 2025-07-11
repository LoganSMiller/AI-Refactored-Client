﻿// <auto-generated>
//   AI-Refactored: ObjectiveController.cs (Overlay/Event-Only, Supreme Arbitration, Beyond Diamond, June 2025)
//   Max realism: All logic overlay/event only, bulletproof, pooled, squad-broadcast safe, HotspotRegistry compliant.
//   SPT, FIKA, Unity 2022.3.6f1, .NET 4.7.1 compliant.
//   MIT License.
// </auto-generated>

using MissionType = AIRefactored.AI.Missions.BotMissionController.MissionType;

namespace AIRefactored.AI.Missions.Subsystems
{
    using System;
    using System.Collections.Generic;
    using AIRefactored.AI.Core;
    using AIRefactored.AI.Helpers;
    using AIRefactored.AI.Hotspots;
    using AIRefactored.AI.Looting;
    using AIRefactored.Core;
    using AIRefactored.Pools;
    using EFT;
    using UnityEngine;

    /// <summary>
    /// Controls *intent* and tactical routing for bot missions.
    /// Emits only overlay/event intent (never issues movement).
    /// Bulletproof, squad broadcast-safe, pooled, never disables itself or squad.
    /// </summary>
    public sealed class ObjectiveController
    {
        #region Fields

        private readonly BotOwner _bot;
        private readonly BotComponentCache _cache;
        private readonly BotLootScanner _lootScanner;
        private readonly Queue<Vector3> _questRoute;
        private readonly System.Random _rng;
        private Vector3 _lastBroadcastedObjective;
        private float _lastBroadcastTime;

        /// <summary>
        /// The current intent objective (for overlay/dispatcher pickup).
        /// </summary>
        public Vector3 CurrentObjective { get; private set; }

        #endregion

        #region Constructor

        public ObjectiveController(BotOwner bot, BotComponentCache cache)
        {
            if (!EFTPlayerUtil.IsValidBotOwner(bot))
                throw new ArgumentException("[ObjectiveController] Invalid BotOwner.");
            if (cache == null)
                throw new ArgumentNullException(nameof(cache));

            _bot = bot;
            _cache = cache;
            _lootScanner = cache.LootScanner;
            _questRoute = new Queue<Vector3>(4);
            _rng = new System.Random();
        }

        #endregion

        #region Public API

        /// <summary>
        /// Sets initial mission objective and emits overlay intent.
        /// Overlay/event only: no movement call here!
        /// </summary>
        public void SetInitialObjective(MissionType type)
        {
            try
            {
                Vector3 target = GetObjectiveTarget(type);
                CurrentObjective = target;
                MaybeNotifySquadObjective(target, true);
            }
            catch (Exception ex)
            {
                Plugin.LoggerInstance.LogError($"[ObjectiveController] SetInitialObjective failed: {ex}");
            }
        }

        /// <summary>
        /// Handles logic after an objective is reached, emits next overlay move intent.
        /// Overlay/event only: no movement call here!
        /// </summary>
        public void OnObjectiveReached(MissionType type)
        {
            try
            {
                Vector3 next = GetObjectiveTarget(type);
                CurrentObjective = next;
                MaybeNotifySquadObjective(next);
            }
            catch (Exception ex)
            {
                Plugin.LoggerInstance.LogError($"[ObjectiveController] OnObjectiveReached failed: {ex}");
            }
        }

        /// <summary>
        /// Resumes quest overlay routing after interruption.
        /// Overlay/event only: no movement call here!
        /// </summary>
        public void ResumeQuesting()
        {
            try
            {
                if (_questRoute.Count == 0)
                    PopulateQuestRoute();

                if (_questRoute.Count > 0)
                {
                    Vector3 next = GetNextQuestObjective();
                    CurrentObjective = next;
                    MaybeNotifySquadObjective(next);
                }
            }
            catch (Exception ex)
            {
                Plugin.LoggerInstance.LogError($"[ObjectiveController] ResumeQuesting failed: {ex}");
            }
        }

        #endregion

        #region Routing Logic

        /// <summary>
        /// Selects the current target for the mission overlay (quest, fight, loot, fallback: self).
        /// Never issues a move.
        /// </summary>
        private Vector3 GetObjectiveTarget(MissionType type)
        {
            switch (type)
            {
                case MissionType.Quest:
                    return GetNextQuestObjective();
                case MissionType.Fight:
                    return GetFightZone();
                case MissionType.Loot:
                    return GetLootObjective();
                case MissionType.Fallback:
                    return GetFallbackObjective();
                case MissionType.Extract:
                    return GetExtractObjective();
                default:
                    return EFTPlayerUtil.GetPosition(_bot);
            }
        }

        /// <summary>
        /// Returns a random valid fight zone from HotspotRegistry (type: "combat").
        /// </summary>
        private Vector3 GetFightZone()
        {
            try
            {
                var fightSpot = HotspotRegistry.GetRandomOfType("combat");
                if (fightSpot != null)
                    return fightSpot.Position;

                // Fallback: random any spot
                var any = HotspotRegistry.GetRandom();
                return any?.Position ?? EFTPlayerUtil.GetPosition(_bot);
            }
            catch (Exception ex)
            {
                Plugin.LoggerInstance.LogError($"[ObjectiveController] GetFightZone failed: {ex}");
                return EFTPlayerUtil.GetPosition(_bot);
            }
        }

        /// <summary>
        /// Returns the best loot point as determined by BotLootScanner, or best "loot" hotspot.
        /// </summary>
        private Vector3 GetLootObjective()
        {
            try
            {
                if (_lootScanner != null)
                {
                    Vector3 pos = _lootScanner.GetBestLootPoint();
                    if (pos != Vector3.zero)
                        return pos;
                }

                var lootSpot = HotspotRegistry.GetRandomOfType("loot");
                return lootSpot?.Position ?? EFTPlayerUtil.GetPosition(_bot);
            }
            catch (Exception ex)
            {
                Plugin.LoggerInstance.LogError($"[ObjectiveController] GetLootObjective failed: {ex}");
                return EFTPlayerUtil.GetPosition(_bot);
            }
        }

        /// <summary>
        /// Returns a valid fallback or defense hotspot.
        /// </summary>
        private Vector3 GetFallbackObjective()
        {
            try
            {
                var fallbackSpot = HotspotRegistry.GetRandomOfType("fallback");
                if (fallbackSpot != null)
                    return fallbackSpot.Position;

                var defenseSpot = HotspotRegistry.GetRandomOfType("defense");
                return defenseSpot?.Position ?? EFTPlayerUtil.GetPosition(_bot);
            }
            catch (Exception ex)
            {
                Plugin.LoggerInstance.LogError($"[ObjectiveController] GetFallbackObjective failed: {ex}");
                return EFTPlayerUtil.GetPosition(_bot);
            }
        }

        /// <summary>
        /// Returns a valid extraction hotspot, or fallback to nearest exfil.
        /// </summary>
        private Vector3 GetExtractObjective()
        {
            try
            {
                var extractSpot = HotspotRegistry.GetRandomOfType("extract");
                if (extractSpot != null)
                    return extractSpot.Position;

                // Fallback: try to find nearest exfiltration point
                var exfils = UnityEngine.Object.FindObjectsOfType<EFT.Interactive.ExfiltrationPoint>();
                if (exfils != null && exfils.Length > 0)
                {
                    Vector3 pos = EFTPlayerUtil.GetPosition(_bot);
                    float minDist = float.MaxValue;
                    Vector3 best = pos;
                    for (int i = 0; i < exfils.Length; i++)
                    {
                        if (exfils[i] != null)
                        {
                            float d = (exfils[i].transform.position - pos).sqrMagnitude;
                            if (d < minDist)
                            {
                                minDist = d;
                                best = exfils[i].transform.position;
                            }
                        }
                    }
                    return best;
                }
            }
            catch (Exception ex)
            {
                Plugin.LoggerInstance.LogError($"[ObjectiveController] GetExtractObjective failed: {ex}");
            }
            return EFTPlayerUtil.GetPosition(_bot);
        }

        /// <summary>
        /// Retrieves the next objective for quest overlay, filling route if needed.
        /// </summary>
        private Vector3 GetNextQuestObjective()
        {
            if (_questRoute.Count > 0)
                return _questRoute.Dequeue();

            PopulateQuestRoute();
            return _questRoute.Count > 0 ? _questRoute.Dequeue() : EFTPlayerUtil.GetPosition(_bot);
        }

        /// <summary>
        /// Populates a quest overlay route based on nearby hotspots, direction, personality, and pooling.
        /// </summary>
        private void PopulateQuestRoute()
        {
            try
            {
                _questRoute.Clear();
                Vector3 origin = EFTPlayerUtil.GetPosition(_bot);
                Vector3 forward = _bot.LookDirection.normalized;

                Predicate<HotspotData> directionFilter = h =>
                {
                    Vector3 dir = h.Position - origin;
                    return dir.sqrMagnitude > 1f && Vector3.Dot(dir.normalized, forward) > 0.25f && h.Priority > 0.2f;
                };

                var candidates = HotspotRegistry.QueryNearby(
                    origin, 100f,
                    type: "quest",
                    filter: directionFilter
                );

                if (candidates == null || candidates.Count == 0)
                {
                    TempListPool.Return(candidates);
                    return;
                }

                int desired = UnityEngine.Random.Range(2, 4);
                var used = TempHashSetPool.Rent<int>();
                try
                {
                    while (_questRoute.Count < desired && used.Count < candidates.Count)
                    {
                        int index = UnityEngine.Random.Range(0, candidates.Count);
                        if (used.Add(index))
                            _questRoute.Enqueue(candidates[index].Position);
                    }
                }
                finally
                {
                    TempHashSetPool.Return(used);
                    TempListPool.Return(candidates);
                }
            }
            catch (Exception ex)
            {
                Plugin.LoggerInstance.LogError($"[ObjectiveController] PopulateQuestRoute failed: {ex}");
            }
        }

        /// <summary>
        /// Optionally notifies the squad of a new objective, anti-spam and deduped.
        /// Never triggers movement. Only overlay/event-safe comms.
        /// </summary>
        private void MaybeNotifySquadObjective(Vector3 obj, bool force = false)
        {
            var group = _cache.GroupBehavior;
            if (group == null || !group.IsInSquad)
                return;

            float now = Time.time;
            if (force || (obj != _lastBroadcastedObjective && now - _lastBroadcastTime > 3f))
            {
                _lastBroadcastedObjective = obj;
                _lastBroadcastTime = now;
                // Overlay/event-only squad comms could be expanded here.
            }
        }

        #endregion
    }
}
