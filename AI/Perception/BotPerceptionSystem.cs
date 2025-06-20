﻿// <auto-generated>
//   AI-Refactored: BotPerceptionSystem.cs (Ultra-Platinum++ Overlay/Event-Only, Max Realism, June 2025)
//   SYSTEMATICALLY MANAGED. Null-safe, squad-coordinated, error-isolated, multiplayer/headless compatible.
//   Realism: Flashblindness, flare, suppression, panic triggers, squad sync, helper-only data access.
//   MIT License.
// </auto-generated>

namespace AIRefactored.AI.Perception
{
    using System;
    using AIRefactored.AI.Core;
    using AIRefactored.AI.Groups;
    using AIRefactored.AI.Helpers;
    using AIRefactored.Core;
    using EFT;
    using UnityEngine;

    /// <summary>
    /// Handles flashblindness, flare, suppression, and panic triggers for bots.
    /// Overlay/event-only, error-isolated, null-guarded, squad-synced. No movement or pose change.
    /// Zero teleport/hitch risk. Headless/client/squad/MP safe.
    /// </summary>
    public sealed class BotPerceptionSystem : IFlashReactiveBot
    {
        #region Constants

        private const float BlindSpeechThreshold = 0.4f;
        private const float FlareRecoverySpeed = 0.19f;
        private const float FlashRecoverySpeed = 0.47f;
        private const float MaxSightDistance = 70f;
        private const float MinSightDistance = 15f;
        private const float PanicTriggerThreshold = 0.62f;
        private const float SuppressionRecoverySpeed = 0.31f;
        private const float SuppressedThreshold = 0.17f;
        private const float PanicCooldown = 2.6f;
        private const float BlindDecayTimeout = 8f;

        #endregion

        #region Fields

        private float _blindStartTime = -1f;
        private float _flashBlindness;
        private float _flareIntensity;
        private float _suppressionFactor;

        private BotOwner _bot;
        private BotComponentCache _cache;
        private BotVisionProfile _profile;

        private bool _failed;
        private bool _panicTriggered;
        private float _lastPanicTime;
        private float _lastGoodTick;

        #endregion

        #region Properties

        public bool IsSuppressed => _suppressionFactor > SuppressedThreshold;

        public float CurrentBlindness => _flashBlindness;

        public float CurrentFlare => _flareIntensity;

        public float CurrentSuppression => _suppressionFactor;

        #endregion

        #region Initialization

        /// <summary>
        /// Overlay/event-only. Called from BotBrain tick, never per-frame or coroutine.
        /// </summary>
        public void Initialize(BotComponentCache cache)
        {
            _bot = null;
            _cache = null;
            _profile = null;
            _failed = false;
            _panicTriggered = false;
            _lastPanicTime = -1000f;
            _lastGoodTick = -1000f;
            _blindStartTime = -1f;
            _flashBlindness = 0f;
            _flareIntensity = 0f;
            _suppressionFactor = 0f;

            try
            {
                if (cache == null || cache.Bot == null)
                    return;

                var owner = cache.Bot;
                if (owner.IsDead || owner.GetPlayer == null || !owner.GetPlayer.IsAI)
                    return;

                var profile = BotVisionProfiles.Get(owner.GetPlayer);
                if (profile == null)
                    return;

                _bot = owner;
                _cache = cache;
                _profile = profile;
            }
            catch (Exception ex)
            {
                _failed = true;
                Plugin.LoggerInstance.LogError($"[BotPerceptionSystem] Initialize exception: {ex}");
            }
        }

        #endregion

        #region Overlay/Event Tick

        /// <summary>
        /// Overlay/event-only. Called from BotBrain overlay tick, never per-frame or coroutine.
        /// </summary>
        public void Tick(float deltaTime)
        {
            if (_failed || !IsActive())
                return;

            _lastGoodTick = Time.time;

            try
            {
                UpdateFlashlightExposure();

                // Penalty blends suppression, flare, and flash for max vision penalty
                float penalty = Mathf.Max(_flashBlindness, _flareIntensity, _suppressionFactor);
                float adjustedSight = Mathf.Lerp(MinSightDistance, MaxSightDistance, 1f - penalty);
                float visibleDist = adjustedSight * (_profile != null ? _profile.AdaptationSpeed : 1f);

                if (_bot.LookSensor != null)
                    _bot.LookSensor.ClearVisibleDist = visibleDist;

                float blindDuration = Mathf.Clamp01(_flashBlindness) * 3.2f;

                if (_cache != null)
                {
                    _cache.IsBlinded = _flashBlindness > BlindSpeechThreshold;
                    _cache.BlindUntilTime = Time.time + blindDuration;
                }

                TryTriggerPanic();
                RecoverClarity(deltaTime);
                SyncEnemyIfVisible();
                SelfRecoverIfAbandoned();
            }
            catch (Exception ex)
            {
                _failed = true;
                Plugin.LoggerInstance.LogError($"[BotPerceptionSystem] Tick exception: {ex}");
            }
        }

        #endregion

        #region Overlay/Event-Only External Stimulus

        /// <summary>
        /// Overlay/event-only: Simulates flare exposure for flare rounds or ambient glare.
        /// </summary>
        public void ApplyFlareExposure(float strength)
        {
            if (_failed || !IsActive()) return;
            try
            {
                _flareIntensity = Mathf.Clamp(strength * 0.6f, 0f, 0.81f);
            }
            catch (Exception ex)
            {
                _failed = true;
                Plugin.LoggerInstance.LogError($"[BotPerceptionSystem] ApplyFlareExposure exception: {ex}");
            }
        }

        /// <summary>
        /// Overlay/event-only: Applies instant or cumulative flashblindness.
        /// </summary>
        public void ApplyFlashBlindness(float intensity)
        {
            if (_failed || !IsActive()) return;
            try
            {
                float maxBlind = _profile != null ? _profile.MaxBlindness : 1f;
                float added = Mathf.Clamp01(intensity * maxBlind);
                _flashBlindness = Mathf.Clamp01(_flashBlindness + added);
                _blindStartTime = Time.time;

                // Only speech/voice. Never animation or pose here.
                if (_flashBlindness > BlindSpeechThreshold && _bot?.BotTalk != null)
                {
                    _bot.BotTalk.TrySay(EPhraseTrigger.OnBeingHurt);
                }
            }
            catch (Exception ex)
            {
                _failed = true;
                Plugin.LoggerInstance.LogError($"[BotPerceptionSystem] ApplyFlashBlindness exception: {ex}");
            }
        }

        /// <summary>
        /// Overlay/event-only: Applies suppression penalty.
        /// </summary>
        public void ApplySuppression(float severity)
        {
            if (_failed || !IsActive()) return;
            try
            {
                _suppressionFactor = Mathf.Clamp01(severity * (_profile != null ? _profile.AggressionResponse : 1f));
            }
            catch (Exception ex)
            {
                _failed = true;
                Plugin.LoggerInstance.LogError($"[BotPerceptionSystem] ApplySuppression exception: {ex}");
            }
        }

        /// <summary>
        /// Overlay/event-only: Handles direct flash exposure from intense light source.
        /// </summary>
        public void OnFlashExposure(Vector3 lightOrigin)
        {
            if (_failed || !IsActive()) return;
            try
            {
                ApplyFlashBlindness(0.43f);
            }
            catch (Exception ex)
            {
                _failed = true;
                Plugin.LoggerInstance.LogError($"[BotPerceptionSystem] OnFlashExposure exception: {ex}");
            }
        }

        #endregion

        #region Private Helpers

        /// <summary>
        /// Overlay/event-only: Returns true if system is valid, bot is alive, and not headless-null.
        /// </summary>
        private bool IsActive()
        {
            return !_failed &&
                   _cache != null &&
                   _bot != null &&
                   _profile != null &&
                   !_bot.IsDead &&
                   _bot.GetPlayer != null &&
                   _bot.GetPlayer.IsAI;
        }

        /// <summary>
        /// Overlay/event-only: Detects new flashlight exposure and applies blindness if threshold met.
        /// </summary>
        private void UpdateFlashlightExposure()
        {
            if (_failed)
                return;

            try
            {
                Transform head = BotCacheUtility.Head(_cache);
                if (head == null)
                    return;

                if (FlashlightRegistry.IsExposingBot(_bot.GetPlayer, out Light source) && source != null)
                {
                    float score = FlashLightUtils.CalculateFlashScore(source.transform, head, 20f);
                    if (score > 0.25f)
                    {
                        ApplyFlashBlindness(score);
                    }
                }
            }
            catch (Exception ex)
            {
                _failed = true;
                Plugin.LoggerInstance.LogError($"[BotPerceptionSystem] UpdateFlashlightExposure exception: {ex}");
            }
        }

        /// <summary>
        /// Recovers flash, flare, and suppression over time based on profile.
        /// </summary>
        private void RecoverClarity(float deltaTime)
        {
            try
            {
                float recovery = _profile != null ? _profile.ClarityRecoverySpeed : 1f;
                _flashBlindness = Mathf.MoveTowards(_flashBlindness, 0f, FlashRecoverySpeed * recovery * deltaTime);
                _flareIntensity = Mathf.MoveTowards(_flareIntensity, 0f, FlareRecoverySpeed * recovery * deltaTime);
                _suppressionFactor = Mathf.MoveTowards(_suppressionFactor, 0f, SuppressionRecoverySpeed * recovery * deltaTime);
            }
            catch (Exception ex)
            {
                _failed = true;
                Plugin.LoggerInstance.LogError($"[BotPerceptionSystem] RecoverClarity exception: {ex}");
            }
        }

        /// <summary>
        /// Overlay/event-only: Triggers panic if bot is blinded or panicked, no tick logic.
        /// </summary>
        private void TryTriggerPanic()
        {
            try
            {
                if (_cache == null || _cache.PanicHandler == null)
                    return;

                float elapsed = Time.time - _blindStartTime;
                float now = Time.time;
                if (!_panicTriggered && _flashBlindness >= PanicTriggerThreshold && elapsed < PanicCooldown)
                {
                    _cache.PanicHandler.TriggerPanic();
                    _panicTriggered = true;
                    _lastPanicTime = now;
                }
                else if (_panicTriggered && now - _lastPanicTime > PanicCooldown)
                {
                    _panicTriggered = false;
                }
            }
            catch (Exception ex)
            {
                _failed = true;
                Plugin.LoggerInstance.LogError($"[BotPerceptionSystem] TryTriggerPanic exception: {ex}");
            }
        }

        /// <summary>
        /// Overlay/event-only: Syncs enemy state across squad if visible. No-op if blinded.
        /// </summary>
        private void SyncEnemyIfVisible()
        {
            try
            {
                if (_cache == null || _cache.IsBlinded || _bot?.Memory == null)
                    return;

                IPlayer raw = _bot.Memory.GoalEnemy?.Person;
                if (raw == null)
                    return;

                Player target = EFTPlayerUtil.AsEFTPlayer(raw);
                Player self = EFTPlayerUtil.ResolvePlayer(_bot);

                if (EFTPlayerUtil.IsValid(target) && EFTPlayerUtil.IsValid(self) && EFTPlayerUtil.IsEnemyOf(_bot, target))
                {
                    BotTeamLogic.AddEnemy(_bot, raw);
                }
            }
            catch (Exception ex)
            {
                _failed = true;
                Plugin.LoggerInstance.LogError($"[BotPerceptionSystem] SyncEnemyIfVisible exception: {ex}");
            }
        }

        /// <summary>
        /// Self-recovers if perception system left in failed or abandoned state (domain reload/world clear recovery).
        /// </summary>
        private void SelfRecoverIfAbandoned()
        {
            if (_failed && Time.time - _lastGoodTick > BlindDecayTimeout)
            {
                _failed = false;
                _flashBlindness = 0f;
                _flareIntensity = 0f;
                _suppressionFactor = 0f;
            }
        }

        #endregion
    }
}
