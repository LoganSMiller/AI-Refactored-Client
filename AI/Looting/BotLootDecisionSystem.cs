﻿// <auto-generated>
//   AI-Refactored: BotLootDecisionSystem.cs (Supreme Arbitration Overlay Edition – Ultra-Platinum+++, Max Expansion, June 2025)
//   Full loot system: squad-aware, world-loot and corpse fallback, pooled registry/containers/items, contest/cooldown, zero-alloc, full pooling, event/overlay-driven.
//   Bulletproof, event/overlay-only, teardown safe, zero disables, multiplayer/headless/client parity.
//   MIT License.
// </auto-generated>

namespace AIRefactored.AI.Looting
{
    using System;
    using System.Collections.Generic;
    using AIRefactored.AI.Core;
    using AIRefactored.AI.Groups;
    using AIRefactored.Pools;
    using BepInEx.Logging;
    using EFT;
    using EFT.Interactive;
    using EFT.InventoryLogic;
    using UnityEngine;

    /// <summary>
    /// Arbitration-driven, squad/greed/personality/role-aware looting logic.
    /// Integrates full LootRegistry world loot, corpses, pooled dead body cache. Overlay/intent arbitration, contest-aware, zero-alloc, teardown-safe.
    /// </summary>
    public sealed class BotLootDecisionSystem
    {
        #region Constants

        private const float MaxLootDistance = 22f;
        private const float HighValueThreshold = 25000f;
        private const float LootCooldown = 13.8f;
        private const float ClaimCooldown = 17.6f;
        private const float BlockRadius = 4.2f;
        private const float ContestTimeout = 6.1f;
        private const float SnatchDelta = 8000f;
        private const int MaxMemory = 36;
        private const float VoiceCooldown = 4.2f;
        private const float WorldLootFallbackDist = 16.7f;
        private const float CorpseLootFallbackDist = 19.3f;
        private const float CorpseValueBias = 0.83f;
        private const float MinCorpseFreshness = 3.5f;

        #endregion

        #region Fields

        private BotOwner _bot;
        private BotComponentCache _cache;
        private BotGroupComms _comms;
        private string _groupId;
        private readonly LinkedList<string> _recentLooted = new LinkedList<string>();
        private static readonly Dictionary<string, float> _globalSquadClaims = new Dictionary<string, float>(128); // groupId:containerId
        private static readonly Dictionary<string, float> _globalContestCooldowns = new Dictionary<string, float>(64); // groupId:containerId

        private float _nextLootTime;
        private float _lastClaimTime;
        private float _lastVoiceTime;
        private string _currentClaimId;
        private bool _isActive;

        private static ManualLogSource Log => Plugin.LoggerInstance;

        #endregion

        #region Initialization

        public void Initialize(BotComponentCache cache)
        {
            _cache = cache;
            _bot = cache?.Bot;
            _comms = cache?.GroupComms;
            _groupId = _bot?.GetPlayer?.Profile?.Info?.GroupId ?? string.Empty;
            _isActive = _bot != null;
            if (!_isActive)
                Log.LogError("[BotLootDecisionSystem] Disabled due to null bot or cache.");
        }

        #endregion

        #region Tick

        public void Tick(float deltaTime)
        {
            if (!_isActive) return;
            float now = Time.time;
            var expired = TempListPool.Rent<string>();
            foreach (var kv in _globalSquadClaims)
                if (now >= kv.Value) expired.Add(kv.Key);
            for (int i = 0; i < expired.Count; i++)
                _globalSquadClaims.Remove(expired[i]);
            expired.Clear();
            foreach (var kv in _globalContestCooldowns)
                if (now >= kv.Value) expired.Add(kv.Key);
            for (int i = 0; i < expired.Count; i++)
                _globalContestCooldowns.Remove(expired[i]);
            TempListPool.Return(expired);
            while (_recentLooted.Count > MaxMemory)
                _recentLooted.RemoveFirst();
        }

        #endregion

        #region Decision Logic

        public bool ShouldLootNow()
        {
            if (!_isActive || _bot == null || _bot.IsDead || Time.time < _nextLootTime)
                return false;
            if (!BotOverlayManager.CanIssueMove(_bot, BotOverlayType.Loot))
                return false;
            try
            {
                if (_cache.PanicHandler?.IsPanicking == true) return false;
                if (_bot.Memory?.GoalEnemy != null) return false;
                if (_bot.EnemiesController?.EnemyInfos?.Count > 0) return false;
                if (!string.IsNullOrEmpty(_groupId))
                {
                    var squad = BotTeamTracker.GetGroup(_groupId);
                    for (int i = 0; i < squad.Count; i++)
                    {
                        BotOwner mate = squad[i];
                        if (mate == null || mate == _bot || mate.IsDead) continue;
                        float dist = Vector3.Distance(_bot.Position, mate.Position);
                        if (mate.Memory?.GoalEnemy != null && dist < 16f)
                            return false;
                        if (IsSquadmateLootingSameContainer(mate, out var claimId) && dist < BlockRadius)
                        {
                            var p = _cache.PersonalityProfile ?? BotPersonalityProfile.Default;
                            if (p.Greed > 0.83f && UnityEngine.Random.value < 0.21f) { TryContestLoot(claimId, mate, p); continue; }
                            if (p.AggressionLevel > 0.66f && UnityEngine.Random.value < 0.15f) { TryContestLoot(claimId, mate, p); continue; }
                            if (p.Caution > 0.71f && UnityEngine.Random.value < 0.47f) { Say(EPhraseTrigger.HoldPosition); return false; }
                            Say(EPhraseTrigger.LootGeneric);
                            return false;
                        }
                    }
                }
                float threshold = Mathf.Lerp(HighValueThreshold * 0.77f, HighValueThreshold * 1.13f, _cache.PersonalityProfile?.Greed ?? 0.5f);
                bool result = _cache.LootScanner?.TotalLootValue >= threshold;
                if (result) BotOverlayManager.RegisterMove(_bot, BotOverlayType.Loot);
                return result;
            }
            catch (Exception ex)
            {
                _isActive = false;
                Log.LogError($"[BotLootDecisionSystem] ShouldLootNow() failed: {ex}");
                return false;
            }
        }

        public Vector3 GetLootDestination()
        {
            if (!_isActive || _cache?.LootScanner == null) return Vector3.zero;
            try
            {
                Vector3 best = Vector3.zero;
                float bestValue = 0f;
                string bestId = null;
                float closest = float.MaxValue;
                var containers = LootRegistry.GetAllContainers();
                var p = _cache.PersonalityProfile ?? BotPersonalityProfile.Default;
                float greedBias = Mathf.Lerp(0.78f, 1.17f, p.Greed);

                // 1. Prioritize containers by value, freshness, claim/squad safety
                for (int i = 0; i < containers.Count; i++)
                {
                    var c = containers[i];
                    if (c == null || c.transform == null || !c.enabled) continue;
                    string id = c.Id;
                    if (string.IsNullOrWhiteSpace(id) || WasRecentlyLooted(id) || IsContainerClaimedBySquad(id)) continue;
                    Vector3 pos = c.transform.position;
                    float dist = Vector3.Distance(_bot.Position, pos);
                    if (dist > MaxLootDistance) continue;
                    float val = EstimateValue(c) * greedBias;
                    if (_globalSquadClaims.TryGetValue(_groupId + ":" + id, out float until) && Time.time < until)
                    {
                        float claimMin = val - SnatchDelta;
                        if ((p.Greed > 0.86f || p.AggressionLevel > 0.76f) && val > claimMin && UnityEngine.Random.value < 0.29f)
                        { TryContestLoot(id, null, p); ReleaseClaim(id); }
                        else continue;
                    }
                    if (val > bestValue || (Mathf.Approximately(val, bestValue) && dist < closest))
                    {
                        bestValue = val;
                        best = pos;
                        bestId = id;
                        closest = dist;
                    }
                }
                TempListPool.Return(containers);

                // 2. Fallback: world loot (loose items), recency and claim-safe
                if (bestValue < 1f)
                {
                    var items = LootRegistry.GetAllItems();
                    for (int i = 0; i < items.Count; i++)
                    {
                        var w = items[i];
                        if (w == null || w.transform == null || !w.enabled) continue;
                        Vector3 pos = w.transform.position;
                        float dist = Vector3.Distance(_bot.Position, pos);
                        if (dist > WorldLootFallbackDist) continue;
                        float lastSeen;
                        if (LootRegistry.TryGetLastSeenTime(w, out lastSeen) && Time.time - lastSeen > 2f) continue;
                        if (dist < closest)
                        {
                            best = pos;
                            closest = dist;
                        }
                    }
                    TempListPool.Return(items);
                }

                // 3. Fallback: corpses (DeadBodyContainerCache, profile-aware, recency, value bias)
                if (bestValue < 1f && best == Vector3.zero)
                {
                    var corpses = DeadBodyContainerCache.GetAllContainers();
                    for (int i = 0; i < corpses.Count; i++)
                    {
                        var d = corpses[i];
                        if (d == null || d.transform == null || WasRecentlyLooted(d.Id) || IsContainerClaimedBySquad(d.Id)) continue;
                        Vector3 pos = d.transform.position;
                        float dist = Vector3.Distance(_bot.Position, pos);
                        float lastSeen;
                        if (LootRegistry.TryGetLastSeenTime(d, out lastSeen) && Time.time - lastSeen < MinCorpseFreshness) continue;
                        float val = EstimateValue(d) * CorpseValueBias;
                        if (val < 4000f) continue;
                        if (dist > CorpseLootFallbackDist) continue;
                        if (dist < closest)
                        {
                            best = pos;
                            closest = dist;
                            bestId = d.Id;
                        }
                    }
                    TempListPool.Return(corpses);
                }

                // 4. Register claim (if any) and return destination
                if ((bestValue > 1f && bestId != null) || best != Vector3.zero)
                {
                    if (bestId != null) Claim(bestId);
                    return best;
                }
                return Vector3.zero;
            }
            catch (Exception ex)
            {
                _isActive = false;
                Log.LogError($"[BotLootDecisionSystem] GetLootDestination() failed: {ex}");
                return Vector3.zero;
            }
        }

        public void MarkLooted(string lootId)
        {
            if (!_isActive || string.IsNullOrWhiteSpace(lootId)) return;
            try
            {
                if (_recentLooted.Count >= MaxMemory) _recentLooted.RemoveFirst();
                _recentLooted.AddLast(lootId.Trim());
                _nextLootTime = Time.time + LootCooldown;
                ReleaseClaim(lootId);
            }
            catch (Exception ex)
            {
                _isActive = false;
                Log.LogError($"[BotLootDecisionSystem] MarkLooted() failed: {ex}");
            }
        }

        public bool WasRecentlyLooted(string lootId)
        {
            if (!_isActive || string.IsNullOrWhiteSpace(lootId)) return false;
            try
            {
                string id = lootId.Trim();
                foreach (string entry in _recentLooted)
                    if (entry == id) return true;
                return false;
            }
            catch (Exception ex)
            {
                _isActive = false;
                Log.LogError($"[BotLootDecisionSystem] WasRecentlyLooted() failed: {ex}");
                return false;
            }
        }

        #endregion

        #region Arbitration/Contest Logic

        private void Claim(string lootId)
        {
            if (string.IsNullOrWhiteSpace(lootId)) return;
            float until = Time.time + ClaimCooldown;
            string globalKey = _groupId + ":" + lootId;
            _globalSquadClaims[globalKey] = until;
            _currentClaimId = lootId;
            _lastClaimTime = Time.time;
            Say(EPhraseTrigger.LootGeneric);
        }

        private void ReleaseClaim(string lootId)
        {
            if (string.IsNullOrWhiteSpace(lootId)) return;
            string globalKey = _groupId + ":" + lootId;
            _globalSquadClaims.Remove(globalKey);
            if (_currentClaimId == lootId) _currentClaimId = null;
            Say(EPhraseTrigger.LootGeneric);
        }

        private bool IsContainerClaimedBySquad(string lootId)
        {
            string globalKey = _groupId + ":" + lootId;
            return _globalSquadClaims.TryGetValue(globalKey, out float until) && Time.time < until;
        }

        private bool IsSquadmateLootingSameContainer(BotOwner mate, out string lootedId)
        {
            lootedId = null;
            if (mate == null || mate.IsDead) return false;
            var cache = mate.GetComponent<BotComponentCache>();
            if (cache?.LootDecisionSystem == null) return false;
            string claim = cache.LootDecisionSystem._currentClaimId;
            if (!string.IsNullOrWhiteSpace(claim) && cache.LootDecisionSystem.IsContainerClaimedBySquad(claim))
            {
                lootedId = claim;
                return true;
            }
            return false;
        }

        private void TryContestLoot(string lootId, BotOwner mate, BotPersonalityProfile p)
        {
            if (string.IsNullOrWhiteSpace(lootId)) return;
            string contestKey = _groupId + ":" + lootId;
            if (_globalContestCooldowns.TryGetValue(contestKey, out float until) && Time.time < until)
                return;
            _globalContestCooldowns[contestKey] = Time.time + ContestTimeout;
            if (mate != null && p != null)
            {
                if (p.Greed > 0.85f) Say(EPhraseTrigger.OnEnemyConversation);
                else if (p.AggressionLevel > 0.7f) Say(EPhraseTrigger.OnFight);
                else Say(EPhraseTrigger.LootGeneric);
            }
            else
            {
                Say(EPhraseTrigger.LootGeneric);
            }
        }

        private void Say(EPhraseTrigger phrase)
        {
            if (_comms == null || Time.time - _lastVoiceTime < VoiceCooldown) return;
            try { _comms.Say(phrase); } catch { }
            _lastVoiceTime = Time.time;
        }

        #endregion

        #region Value Estimation

        private static float EstimateValue(LootableContainer container)
        {
            if (container?.ItemOwner?.RootItem == null)
                return 0f;
            List<Item> items = null;
            try
            {
                float total = 0f;
                items = TempListPool.Rent<Item>();
                container.ItemOwner.RootItem.GetAllItemsNonAlloc(items);
                for (int i = 0; i < items.Count; i++)
                {
                    var item = items[i];
                    if (item?.Template?.CreditsPrice > 0f)
                        total += item.Template.CreditsPrice;
                }
                return total;
            }
            catch (Exception ex)
            {
                Log.LogError($"[BotLootDecisionSystem] EstimateValue() failed: {ex}");
                return 0f;
            }
            finally
            {
                if (items != null) TempListPool.Return(items);
            }
        }

        #endregion
    }
}
