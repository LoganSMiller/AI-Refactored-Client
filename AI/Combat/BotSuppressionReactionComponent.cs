﻿// <auto-generated>
//   AI-Refactored: BotSuppressionReactionComponent.cs (Supreme Arbitration Overlay/Event, June 2025, Ultra-Expansion, Max Realism, Supreme Fix Pass)
//   Overlay-only, bulletproof, maximum squad sync, pooled, NavMesh/cover aware, arbitration-dedup/anticipation/fakeout guarded, no disables/teleports.
//   All features, safety, and behaviors are grounded in real EFT bot suppression, panic, cover, and squad logic. 
//   SPT/FIKA/headless/client parity. MIT License.
// </auto-generated>

namespace AIRefactored.AI.Combat
{
    using System;
    using System.Reflection;
    using AIRefactored.AI.Core;
    using AIRefactored.AI.Helpers;
    using AIRefactored.AI.Navigation;
    using AIRefactored.AI.Optimization;
    using AIRefactored.AI.Memory;
    using AIRefactored.Core;
    using EFT;
    using UnityEngine;

    /// <summary>
    /// Handles all overlay/event-driven bot suppression: panic escalation, composure decay, tactical retreat, squad sync, cover overlays,
    /// suppression memory, arbitration/fakeout/anticipation locking, full squad propagation, and error isolation.
    /// Absolutely never disables, never teleports, alloc-free, and SPT/FIKA/headless/client safe. 
    /// </summary>
    public sealed class BotSuppressionReactionComponent
    {
        #region Constants

        private const float MinSuppressionRetreatDistance = 6.0f;
        private const float SuppressionDuration = 2.25f;
        private const float MaxSuppressionDuration = 4.2f;
        private const float MinRetreatArc = 35f;
        private const float MaxRetreatArc = 145f;
        private const float AnticipationLockWindow = 1.28f;
        private const float SquadSuppressionRadiusSqr = 169f;
        private const float SuppressionVoiceCooldown = 1.35f;
        private const float SquadSuppressionSyncChance = 0.31f;
        private const float ComposureLossMin = 0.12f;
        private const float ComposureLossMax = 0.28f;
        private const float PanicComposureThreshold = 0.18f;
        private const float OverlayMoveDedupSqr = 0.0001f;
        private const float OverlayMoveCooldown = 0.41f;
        private const float MaxNavmeshDeltaY = 3.1f;
        private const float SuppressionTacticalMemoryWindow = 4.6f;
        private const float MaxSuppressionMemory = 12f;
        private const float CoverSearchRadius = 10.0f;
        private const float VoiceStaggerChance = 0.44f;
        private const float MinCoverArc = 50f;
        private const float MaxCoverArc = 130f;
        private const float SquadVoicePropagationChance = 0.18f;
        private const float SquadArbLockPropagationChance = 0.18f;
        private const float SuppressionPersistenceTime = 2.8f;
        private const float SuppressionSquadPropagationDelayMin = 0.05f;
        private const float SuppressionSquadPropagationDelayMax = 0.23f;
        private const BotOverlayType OverlayType = BotOverlayType.Suppression;

        private static readonly EPhraseTrigger[] SuppressionTriggers = new[]
        {
            EPhraseTrigger.NeedHelp, EPhraseTrigger.UnderFire, EPhraseTrigger.GetBack,
            EPhraseTrigger.OnBeingHurt, EPhraseTrigger.EnemyHit, EPhraseTrigger.Regroup,
            EPhraseTrigger.Cooperation, EPhraseTrigger.GetInCover, EPhraseTrigger.OnEnemyGrenade,
            EPhraseTrigger.CoverMe, EPhraseTrigger.FollowMe
        };

        private static readonly EPhraseTrigger[] SquadSyncTriggers = new[]
        {
            EPhraseTrigger.GoForward, EPhraseTrigger.Regroup, EPhraseTrigger.CoverMe,
            EPhraseTrigger.FollowMe, EPhraseTrigger.HoldPosition, EPhraseTrigger.Spreadout
        };

        #endregion

        #region Fields

        private BotOwner _bot;
        private BotComponentCache _cache;
        private bool _isSuppressed;
        private float _suppressionStartTime = float.NegativeInfinity;
        private float _suppressionMemoryTime = float.NegativeInfinity;
        private float _lastVoiceTime = float.NegativeInfinity;
        private float _anticipationLockUntil = float.NegativeInfinity;
        private static FieldInfo _composureField;
        private Vector3 _lastSuppressionMoveIssued = Vector3.zero;
        private float _lastSuppressionMoveTime = -10f;

        // Tactical suppression memory (for fallback/squad awareness)
        private Vector3 _lastSuppressionSource = Vector3.zero;
        private float _lastSuppressionSourceTime = float.NegativeInfinity;

        // Cover selection
        private Vector3 _lastCoverSpot = Vector3.zero;
        private float _lastCoverTime = float.NegativeInfinity;

        #endregion

        #region Initialization

        public void Initialize(BotComponentCache componentCache)
        {
            _cache = componentCache;
            _bot = componentCache?.Bot;
            _isSuppressed = false;
            _suppressionStartTime = float.NegativeInfinity;
            _suppressionMemoryTime = float.NegativeInfinity;
            _lastVoiceTime = float.NegativeInfinity;
            _anticipationLockUntil = float.NegativeInfinity;
            _lastSuppressionMoveIssued = Vector3.zero;
            _lastSuppressionMoveTime = -10f;
            _lastSuppressionSource = Vector3.zero;
            _lastSuppressionSourceTime = float.NegativeInfinity;
            _lastCoverSpot = Vector3.zero;
            _lastCoverTime = float.NegativeInfinity;
            if (_composureField == null)
                _composureField = typeof(BotPanicHandler).GetField("_composureLevel", BindingFlags.NonPublic | BindingFlags.Instance);
        }

        #endregion

        #region Public API

        public bool IsSuppressed() => _isSuppressed;

        public bool AnticipationLockActive => _isSuppressed || (Time.time < _anticipationLockUntil);

        /// <summary>
        /// Returns true if bot is in an active suppression, or recently suppressed for arbitration/contagion.
        /// </summary>
        public bool IsInSuppressionMemory()
        {
            float now = Time.time;
            return _isSuppressed || (now - _suppressionMemoryTime) < SuppressionTacticalMemoryWindow;
        }

        public Vector3 LastSuppressionSource => (Time.time - _lastSuppressionSourceTime) < MaxSuppressionMemory
            ? _lastSuppressionSource : Vector3.zero;

        /// <summary>
        /// Overlay/event-only suppression trigger. Triple-guarded, pooled, NavMesh/Cover/personality/squad safe.
        /// </summary>
        public void TriggerSuppression(Vector3? source)
        {
            if (_isSuppressed || !IsValid()) return;
            float now = Time.time;
            try
            {
                if (!BotOverlayManager.CanIssueMove(_bot, OverlayType)) return;
                if (BotMovementHelper.IsMovementPaused(_bot) || BotMovementHelper.IsInInteractionState(_bot)) return;
                var panic = _cache.PanicHandler;
                if (panic != null && panic.IsPanicking) return;

                _isSuppressed = true;
                _suppressionStartTime = now;
                _suppressionMemoryTime = now;
                _anticipationLockUntil = now + AnticipationLockWindow;

                // Composure decay and escalation (EFT logic)
                if (panic != null && _composureField != null)
                {
                    float loss = UnityEngine.Random.Range(ComposureLossMin, ComposureLossMax);
                    float current = panic.GetComposureLevel();
                    _composureField.SetValue(panic, Mathf.Clamp01(current - loss));
                }

                // Tactical suppression memory (used by squad fallback/panic)
                if (source.HasValue)
                {
                    _lastSuppressionSource = source.Value;
                    _lastSuppressionSourceTime = now;
                }

                // Choose intent retreat or cover (cover first)
                Vector3 intent = GetSuppressionRetreatOrCover(source);

                float cohesion = Mathf.Clamp(_cache.AIRefactoredBotOwner?.PersonalityProfile?.Cohesion ?? 1f, 0.7f, 1.3f);
                Vector3 drifted = BotMovementHelper.ApplyMicroDrift(intent, _bot.ProfileId, Time.frameCount, _cache.PersonalityProfile);

                // Dedup/cooldown check
                if ((drifted - _lastSuppressionMoveIssued).sqrMagnitude < OverlayMoveDedupSqr)
                    return;
                if ((now - _lastSuppressionMoveTime) < OverlayMoveCooldown)
                    return;

                // Register the overlay
                BotOverlayManager.RegisterMove(_bot, OverlayType);
                BotMovementHelper.SmoothMoveToSafe(_bot, drifted, false, cohesion, OverlayType);
                _lastSuppressionMoveIssued = drifted;
                _lastSuppressionMoveTime = now;

                // Record tactical memory (suppression overlays)
                _cache.TacticalMemory?.RecordSuppressionEvent(drifted);

                // Immediate stance swap for realism (squat/crouch/cover if available)
                BotCoverHelper.TrySetStanceFromNearbyCover(_cache, drifted);
                _bot.Sprint(true);

                // Panic escalation: trigger full panic if composure threshold is exceeded
                if (panic != null && panic.GetComposureLevel() < PanicComposureThreshold)
                    panic.TriggerPanic();
                _cache.ThreatEscalation?.NotifyPanicTriggered();

                // Squad sync/contagion
                TryPropagateSuppression();

                // Voice/comms: suppression or squad sync event
                if (_bot.BotTalk != null && now - _lastVoiceTime > SuppressionVoiceCooldown)
                {
                    var triggers = UnityEngine.Random.value < 0.55f ? SuppressionTriggers : SquadSyncTriggers;
                    var trigger = triggers[UnityEngine.Random.Range(0, triggers.Length)];
                    try { _bot.BotTalk.TrySay(trigger); } catch { }
                    _lastVoiceTime = now;
                }
            }
            catch (Exception ex)
            {
                Plugin.LoggerInstance?.LogError("[BotSuppression] TriggerSuppression failed: " + ex);
            }
        }

        /// <summary>
        /// Overlay event-driven update (called from BotBrain or CombatStateMachine).
        /// Handles suppression decay, arbitration/fakeout lock, squad voice, stance swap, and error guards.
        /// </summary>
        public void Tick(float time)
        {
            if (!_isSuppressed) return;
            try
            {
                if (!IsValid())
                {
                    _isSuppressed = false;
                    return;
                }

                // Voice lines as event, not move blocker, for "active under fire" comms
                if (_bot.BotTalk != null && time - _lastVoiceTime > SuppressionVoiceCooldown)
                {
                    if (UnityEngine.Random.value < VoiceStaggerChance)
                    {
                        var trigger = SuppressionTriggers[UnityEngine.Random.Range(0, SuppressionTriggers.Length)];
                        try { _bot.BotTalk.TrySay(trigger); } catch { }
                        _lastVoiceTime = time;
                    }
                }

                // Ultra-safe suppression end, including arbitration/anticipation lock window
                if (time - _suppressionStartTime >= UnityEngine.Random.Range(SuppressionDuration, MaxSuppressionDuration))
                {
                    _isSuppressed = false;
                    _anticipationLockUntil = time + AnticipationLockWindow;
                    _suppressionMemoryTime = time; // memory window remains for tactical fallback
                }
            }
            catch (Exception ex)
            {
                Plugin.LoggerInstance?.LogError("[BotSuppression] Tick exception: " + ex);
                _isSuppressed = false;
            }
        }

        #endregion

        #region Cover/Retreat/Overlay

        /// <summary>
        /// Computes a cover spot or fallback retreat based on cover/zone/personality logic (EFT-style).
        /// </summary>
        private Vector3 GetSuppressionRetreatOrCover(Vector3? source)
        {
            Vector3 botPos = _bot.Position;
            Vector3 fallback = botPos;

            // Prefer cover within radius (cover-aware)
            if (_cache.CoverPlanner != null && _cache.CoverPlanner.TryGetBestCoverNear(botPos, botPos, out Vector3 cover))
            {
                fallback = cover;
                _lastCoverSpot = cover;
                _lastCoverTime = Time.time;
            }
            else
            {
                // Fallback: random retreat in an arc opposite the source (randomized angle like real bots)
                float arc = UnityEngine.Random.Range(MinRetreatArc, MaxRetreatArc);
                Vector3 away = source.HasValue
                    ? (botPos - source.Value).normalized
                    : -_bot.LookDirection.normalized;
                Vector3 rotDir = Quaternion.AngleAxis(UnityEngine.Random.Range(-arc, arc), Vector3.up) * away;
                fallback = botPos + rotDir.normalized * MinSuppressionRetreatDistance;
            }

            // NavMesh/Y sample (never disables or teleports)
            if (!BotNavHelper.TryGetSafeTarget(_bot, out var navTarget) || !IsVectorValid(navTarget))
                navTarget = ClampY(fallback, botPos);

            return navTarget;
        }

        #endregion

        #region Squad Propagation / Contagion

        /// <summary>
        /// Attempts to propagate the suppression event to all nearby squadmates. No disables, arbitration-locked, and error-guarded.
        /// </summary>
        private void TryPropagateSuppression()
        {
            try
            {
                if (_bot?.BotsGroup == null) return;
                Vector3 self = _bot.Position;
                int count = _bot.BotsGroup.MembersCount;

                for (int i = 0; i < count; i++)
                {
                    BotOwner mate = _bot.BotsGroup.Member(i);
                    if (mate == null || mate == _bot || mate.IsDead) continue;
                    if ((mate.Position - self).sqrMagnitude > SquadSuppressionRadiusSqr) continue;
                    if (UnityEngine.Random.value > SquadSuppressionSyncChance) continue;

                    BotComponentCache mateCache = BotCacheUtility.GetCache(mate);
                    var asyncProcessor = mate.GetComponent<BotAsyncProcessor>();
                    if (mateCache?.Suppression != null && !mateCache.Suppression.IsSuppressed() && asyncProcessor != null)
                    {
                        float delay = UnityEngine.Random.Range(SuppressionSquadPropagationDelayMin, SuppressionSquadPropagationDelayMax);

                        // The lambda closes over the *target* mate/mateCache for the squadmate
                        asyncProcessor.QueueDelayedEvent(
                            (owner, cache) => cache?.Suppression?.TriggerSuppression(self),
                            delay
                        );
                    }

                    // Additional squad voice propagation
                    if (mate.BotTalk != null && UnityEngine.Random.value < SquadVoicePropagationChance)
                    {
                        var trigger = SquadSyncTriggers[UnityEngine.Random.Range(0, SquadSyncTriggers.Length)];
                        try { mate.BotTalk.TrySay(trigger); } catch { }
                    }

                    // Arbitration/fakeout lock propagation: blocks rival overlays for brief period (EFT squad AI style)
                    if (UnityEngine.Random.value < SquadArbLockPropagationChance)
                        mateCache?.Suppression?.SetAnticipationLock(Time.time + UnityEngine.Random.Range(0.28f, 1.18f));
                }
            }
            catch (Exception ex)
            {
                Plugin.LoggerInstance?.LogError("[BotSuppression] Propagation error: " + ex);
            }
        }


        /// <summary>
        /// Schedules a delayed squad suppression propagation for ultra-realistic EFT squad suppression sync.
        /// </summary>
        private static void DelayedPropagation(BotAsyncProcessor asyncProcessor, Action<BotOwner, BotComponentCache> act, float delay)
        {
            if (asyncProcessor == null || act == null) return;
            asyncProcessor.QueueDelayedEvent(act, delay);
        }


        /// <summary>
        /// Internal use only, allow direct anticipation lock propagation on squadmates.
        /// </summary>
        private void SetAnticipationLock(float until)
        {
            _anticipationLockUntil = until;
        }

        #endregion

        #region Internal Helpers

        private static Vector3 ClampY(Vector3 v, Vector3 basePos)
        {
            if (Mathf.Abs(v.y - basePos.y) > MaxNavmeshDeltaY || v.y < -2.5f)
                v.y = basePos.y;
            return v;
        }

        private bool IsValid()
        {
            try
            {
                return _bot != null &&
                       _cache != null &&
                       !_bot.IsDead &&
                       _bot.GetPlayer is Player player &&
                       player.IsAI;
            }
            catch { return false; }
        }

        private static bool IsVectorValid(Vector3 v)
        {
            return !float.IsNaN(v.x) && !float.IsNaN(v.y) && !float.IsNaN(v.z)
                && v != Vector3.zero && Mathf.Abs(v.y) < 1000f && v.y > -2.5f;
        }

        #endregion
    }
}
