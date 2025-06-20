﻿// <auto-generated>
//   AI-Refactored: BotThreatEscalationMonitor.cs (Supreme Arbitration Overlay/Event, June 2025, Beyond Diamond, Mastermoveplan, SPT/FIKA/Headless/Client Parity)
//   Monitors, escalates, and coordinates threat levels, panic, squad morale, and escalation overlays.
//   Triple-guard: Arbitration, NavMesh/Y, dedup. Pooled, squad- and voice-aware, fully bulletproof. Never disables, never teleports, never tick-moves.
//   MIT License.
// </auto-generated>

namespace AIRefactored.AI.Combat
{
    using System;
    using AIRefactored.AI.Core;
    using AIRefactored.AI.Helpers;
    using AIRefactored.AI.Navigation;
    using AIRefactored.AI.Optimization;
    using AIRefactored.Pools;
    using AIRefactored.Runtime;
    using BepInEx.Logging;
    using EFT;
    using UnityEngine;

    /// <summary>
    /// Supreme arbitration: Escalates bot and squad threat state with event-only overlays, bulletproof comms, max realism, pooled and error-shielded.
    /// All overlays go through arbitration, NavMesh/y, and deduplication. Squad escalation/voice is propagated via async processors.
    /// </summary>
    public sealed class BotThreatEscalationMonitor
    {
        #region Constants

        private const float CheckInterval = 0.73f; // More responsive, less spam.
        private const float PanicDurationThreshold = 3.6f;
        private const float SquadCasualtyThreshold = 0.36f;
        private const float SquadMoraleCollapseThreshold = 0.61f;
        private const float VoiceIntervalMin = 1.9f;
        private const float VoiceIntervalMax = 4.4f;
        private const float EscalationOverlayMoveDedupSqr = 0.07f;
        private const float EscalationOverlayMoveCooldown = 0.38f;
        private const BotOverlayType OverlayType = BotOverlayType.Attack;

        private static readonly EPhraseTrigger[] EscalationPhrases = new[]
        {
            EPhraseTrigger.OnFight, EPhraseTrigger.NeedHelp, EPhraseTrigger.UnderFire,
            EPhraseTrigger.Regroup, EPhraseTrigger.Cooperation, EPhraseTrigger.CoverMe,
            EPhraseTrigger.GoForward, EPhraseTrigger.HoldPosition, EPhraseTrigger.FollowMe
        };

        private static readonly EPhraseTrigger[] SquadCollapsePhrases = new[]
        {
            EPhraseTrigger.Regroup, EPhraseTrigger.OnBeingHurt, EPhraseTrigger.NeedHelp,
            EPhraseTrigger.CoverMe, EPhraseTrigger.HoldPosition, EPhraseTrigger.FollowMe,
            EPhraseTrigger.OnFight
        };

        private static readonly ManualLogSource Logger = Plugin.LoggerInstance;

        #endregion

        #region Fields

        private BotOwner _bot;
        private BotComponentCache _cache;

        private float _panicStartTime = -1f;
        private float _nextCheckTime = -1f;
        private float _lastVoiceTime = -1f;
        private float _nextSquadVoiceTime = -1f;
        private bool _hasEscalated;
        private bool _squadCollapseTriggered;

        private Vector3 _lastEscalationMoveIssued = Vector3.zero;
        private float _lastEscalationMoveTime = -10f;

        #endregion

        #region Initialization

        /// <summary>
        /// Initialize the escalation monitor for a bot and its cache.
        /// </summary>
        public void Initialize(BotOwner botOwner)
        {
            if (botOwner == null)
            {
                Logger.LogError("[BotThreatEscalationMonitor] BotOwner is null in Initialize.");
                return;
            }

            _bot = botOwner;
            _cache = BotCacheUtility.GetCache(botOwner);

            _panicStartTime = -1f;
            _nextCheckTime = -1f;
            _lastVoiceTime = -1f;
            _nextSquadVoiceTime = -1f;
            _hasEscalated = false;
            _squadCollapseTriggered = false;

            _lastEscalationMoveIssued = Vector3.zero;
            _lastEscalationMoveTime = -10f;
        }

        #endregion

        #region Main Tick

        /// <summary>
        /// Main update entry (must be ticked via BotBrain).
        /// </summary>
        public void Tick(float time)
        {
            if (!IsValid() || time < _nextCheckTime)
                return;

            _nextCheckTime = time + CheckInterval;

            try
            {
                if (!_hasEscalated && ShouldEscalate(time))
                    EscalateBot(time);

                if (!_squadCollapseTriggered && SquadMoraleCollapsed())
                {
                    _squadCollapseTriggered = true;
                    PropagateSquadCollapse(time);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("[BotThreatEscalationMonitor] Tick exception: " + ex);
            }
        }

        #endregion

        #region Public API

        /// <summary>
        /// Notify that panic was triggered (from external panic/suppression logic).
        /// </summary>
        public void NotifyPanicTriggered()
        {
            if (_panicStartTime < 0f)
                _panicStartTime = Time.time;
        }

        #endregion

        #region Escalation Logic

        private bool ShouldEscalate(float time)
        {
            return PanicDurationExceeded(time) || MultipleEnemiesVisible() || SquadHasLostTeammates();
        }

        private bool PanicDurationExceeded(float time) =>
            _panicStartTime >= 0f && (time - _panicStartTime) > PanicDurationThreshold;

        private bool MultipleEnemiesVisible()
        {
            var controller = _bot?.EnemiesController;
            if (controller?.EnemyInfos == null)
                return false;

            int visible = 0;
            foreach (var kv in controller.EnemyInfos)
            {
                if (kv.Value?.IsVisible == true && ++visible >= 2)
                    return true;
            }
            return false;
        }

        private bool SquadHasLostTeammates()
        {
            var group = _bot?.BotsGroup;
            if (group == null || group.MembersCount <= 1)
                return false;

            int dead = 0;
            for (int i = 0; i < group.MembersCount; i++)
                if (group.Member(i)?.IsDead == true)
                    dead++;

            return dead >= Mathf.CeilToInt(group.MembersCount * SquadCasualtyThreshold);
        }

        private bool SquadMoraleCollapsed()
        {
            var group = _bot?.BotsGroup;
            if (group == null || group.MembersCount <= 1)
                return false;

            int dead = 0;
            for (int i = 0; i < group.MembersCount; i++)
                if (group.Member(i)?.IsDead == true)
                    dead++;

            return dead >= Mathf.CeilToInt(group.MembersCount * SquadMoraleCollapseThreshold);
        }

        /// <summary>
        /// Escalate bot aggression and squad overlay—arbitration/event only, triple-guarded, pooled, bulletproof.
        /// </summary>
        private void EscalateBot(float time)
        {
            _hasEscalated = true;

            AIOptimizationManager.Reset(_bot);
            AIOptimizationManager.Apply(_bot);

            ApplyEscalationTuning(_bot);
            ApplyPersonalityTuning(_bot);

            // Overlay arbitration (Attack): triple-guarded.
            if (BotNavHelper.TryGetSafeTarget(_bot, out var navTarget) && IsVectorValid(navTarget))
            {
                float now = Time.time;
                if (BotOverlayManager.CanIssueMove(_bot, OverlayType)
                    && _bot.Mover != null
                    && !BotMovementHelper.IsMovementPaused(_bot)
                    && !BotMovementHelper.IsInInteractionState(_bot)
                    && (_lastEscalationMoveIssued - navTarget).sqrMagnitude > EscalationOverlayMoveDedupSqr
                    && (now - _lastEscalationMoveTime) > EscalationOverlayMoveCooldown)
                {
                    float cohesion = BotRegistry.Get(_bot.ProfileId)?.Cohesion ?? 1f;
                    BotOverlayManager.RegisterMove(_bot, OverlayType);
                    BotMovementHelper.SmoothMoveToSafe(_bot, navTarget, false, cohesion, OverlayType);

                    _lastEscalationMoveIssued = navTarget;
                    _lastEscalationMoveTime = now;
                }
            }

            if (_bot.BotTalk != null && time - _lastVoiceTime > VoiceIntervalMin)
            {
                TrySayEscalationPhrase(time);
            }

            // Propagate escalation voice/comms overlays to all squadmates (with micro-delay via async processor).
            PropagateSquadEscalation(time);
        }

        /// <summary>
        /// Propagate escalation to all alive squadmates with random async delay (never static/global).
        /// </summary>
        private void PropagateSquadEscalation(float now)
        {
            var group = _bot?.BotsGroup;
            if (group == null || group.MembersCount <= 1)
                return;

            for (int i = 0; i < group.MembersCount; i++)
            {
                BotOwner mate = group.Member(i);
                if (mate == null || mate == _bot || mate.IsDead)
                    continue;

                BotComponentCache mateCache = BotCacheUtility.GetCache(mate);
                var asyncProcessor = mate.GetComponent<BotAsyncProcessor>();
                if (mateCache?.ThreatEscalation != null && asyncProcessor != null)
                {
                    float delay = UnityEngine.Random.Range(0.17f, 0.89f); // Humanized reaction delay
                    asyncProcessor.QueueDelayedEvent(
                        (owner, cache) => cache?.ThreatEscalation?.SquadEscalationOverlay(now + delay), delay
                    );
                }
            }
        }

        /// <summary>
        /// Called via async event on squadmates to escalate overlays, comms, and personality.
        /// </summary>
        public void SquadEscalationOverlay(float time)
        {
            if (_hasEscalated) return; // Only escalate once per bot
            _hasEscalated = true;

            ApplyEscalationTuning(_bot);
            ApplyPersonalityTuning(_bot);

            if (_bot.BotTalk != null && time - _lastVoiceTime > VoiceIntervalMin)
            {
                TrySayEscalationPhrase(time);
            }
        }

        /// <summary>
        /// Propagate squad collapse voice overlays.
        /// </summary>
        private void PropagateSquadCollapse(float now)
        {
            var group = _bot?.BotsGroup;
            if (group == null || group.MembersCount <= 1)
                return;

            for (int i = 0; i < group.MembersCount; i++)
            {
                BotOwner mate = group.Member(i);
                if (mate == null || mate == _bot || mate.IsDead)
                    continue;

                BotComponentCache mateCache = BotCacheUtility.GetCache(mate);
                var asyncProcessor = mate.GetComponent<BotAsyncProcessor>();
                if (mateCache?.ThreatEscalation != null && asyncProcessor != null)
                {
                    float delay = UnityEngine.Random.Range(0.14f, 0.66f);
                    asyncProcessor.QueueDelayedEvent(
                        (owner, cache) => cache?.ThreatEscalation?.TrySquadCollapseComms(now + delay), delay
                    );
                }
            }
            TrySquadCollapseComms(now);
        }

        private void TrySquadCollapseComms(float time)
        {
            if (_bot.BotTalk != null && time - _nextSquadVoiceTime > VoiceIntervalMax)
            {
                try
                {
                    var phrase = SquadCollapsePhrases[UnityEngine.Random.Range(0, SquadCollapsePhrases.Length)];
                    _bot.BotTalk.TrySay(phrase);
                    _nextSquadVoiceTime = time;
                }
                catch (Exception ex)
                {
                    Logger.LogError("[BotThreatEscalationMonitor] Squad collapse comms failed: " + ex);
                }
            }
        }

        private void TrySayEscalationPhrase(float time)
        {
            try
            {
                var phrase = EscalationPhrases[UnityEngine.Random.Range(0, EscalationPhrases.Length)];
                _bot.BotTalk.TrySay(phrase);
                _lastVoiceTime = time;
            }
            catch (Exception ex)
            {
                Logger.LogError("[BotThreatEscalationMonitor] Escalation voice failed: " + ex);
            }
        }

        private void ApplyEscalationTuning(BotOwner bot)
        {
            var file = bot?.Settings?.FileSettings;
            if (file == null) return;

            try
            {
                file.Shoot.RECOIL_PER_METER *= 0.81f;
                file.Mind.DIST_TO_FOUND_SQRT *= 1.27f;
                file.Mind.ENEMY_LOOK_AT_ME_ANG *= 0.66f;
                file.Mind.CHANCE_TO_RUN_CAUSE_DAMAGE_0_100 = Mathf.Clamp(file.Mind.CHANCE_TO_RUN_CAUSE_DAMAGE_0_100 + 31f, 0f, 100f);
                file.Look.MAX_VISION_GRASS_METERS += 9f;
            }
            catch (Exception ex)
            {
                Logger.LogError("[BotThreatEscalationMonitor] Tuning failed: " + ex);
            }
        }

        private void ApplyPersonalityTuning(BotOwner bot)
        {
            try
            {
                var profile = BotRegistry.Get(bot.ProfileId);
                if (profile == null) return;

                profile.AggressionLevel = Mathf.Clamp01(profile.AggressionLevel + 0.32f);
                profile.Caution = Mathf.Clamp01(profile.Caution - 0.21f);
                profile.SuppressionSensitivity = Mathf.Clamp01(profile.SuppressionSensitivity * 0.63f);
                profile.AccuracyUnderFire = Mathf.Clamp01(profile.AccuracyUnderFire + 0.27f);
                profile.CommunicationLevel = Mathf.Clamp01(profile.CommunicationLevel + 0.25f);
            }
            catch (Exception ex)
            {
                Logger.LogError("[BotThreatEscalationMonitor] Personality tuning failed: " + ex);
            }
        }

        #endregion

        #region Validation

        private bool IsValid()
        {
            try
            {
                return _bot != null &&
                       !_bot.IsDead &&
                       _bot.GetPlayer is Player player &&
                       player.IsAI;
            }
            catch { return false; }
        }

        private static bool IsVectorValid(Vector3 v)
        {
            return !float.IsNaN(v.x) && !float.IsNaN(v.y) && !float.IsNaN(v.z);
        }

        #endregion
    }
}
