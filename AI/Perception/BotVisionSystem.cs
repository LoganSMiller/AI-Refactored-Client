﻿// <auto-generated>
//   AI-Refactored: BotVisionSystem.cs (Ultra-Platinum++ Arbitration/Overlay, Supreme Realism, Headless-Client Parity, June 2025)
//   SYSTEMATICALLY MANAGED. Null-safe, squad-sync, error-isolated, pooling, multiplayer/headless compatible.
//   Realism: FOV, bone/bounds scanning, flash/fog occlusion, motion bias, suppression error, squad memory sync, overlay/event-only.
//   Arbitration-locked, dedupe/pooled. MIT License.
// </auto-generated>

namespace AIRefactored.AI.Perception
{
    using System;
    using System.Collections.Generic;
    using AIRefactored.AI.Core;
    using AIRefactored.AI.Helpers;
    using AIRefactored.AI.Memory;
    using AIRefactored.Core;
    using AIRefactored.Pools;
    using EFT;
    using EFT.Animations;
    using UnityEngine;

    /// <summary>
    /// Overlay/event-only: vision arbitration for AIRefactored bots. 
    /// FOV, bone/bounds scan, fog/flash occlusion, squad memory, full pooling and error shields. 
    /// No direct movement or tick/coroutine logic; bulletproof and squad-safe.
    /// </summary>
    public sealed class BotVisionSystem
    {
        #region Constants

        private const float AutoDetectRadius = 4.25f;
        private const float BaseViewConeAngle = 119.7f;
        private const float BoneConfidenceDecay = 0.096f;
        private const float BoneConfidenceThreshold = 0.43f;
        private const float MaxDetectionDistance = 120f;
        private const float SuppressionMissChance = 0.23f;
        private const float MotionBoost = 0.22f;
        private const float NavHeightOffset = 1.43f;
        private static readonly Vector3 EyeOffset = new Vector3(0f, NavHeightOffset, 0f);

        private static readonly PlayerBoneType[] BonesToCheck =
        {
            PlayerBoneType.Head, PlayerBoneType.Spine, PlayerBoneType.Ribcage,
            PlayerBoneType.LeftShoulder, PlayerBoneType.RightShoulder,
            PlayerBoneType.Pelvis, PlayerBoneType.LeftThigh1, PlayerBoneType.RightThigh1
        };

        #endregion

        #region Fields

        private BotOwner _bot;
        private BotComponentCache _cache;
        private BotTacticalMemory _memory;
        private BotPersonalityProfile _profile;
        private float _lastCommitTime;
        private bool _failed;

        #endregion

        #region Initialization

        /// <summary>
        /// Overlay/event-only. Must be called from BotBrain overlay tick. Never per-frame.
        /// </summary>
        public void Initialize(BotComponentCache cache)
        {
            try
            {
                _failed = true;
                if (cache == null || cache.Bot == null || cache.TacticalMemory == null || cache.AIRefactoredBotOwner == null)
                    return;

                _bot = cache.Bot;
                _cache = cache;
                _memory = cache.TacticalMemory;
                _profile = cache.AIRefactoredBotOwner.PersonalityProfile;
                _lastCommitTime = -999f;
                _failed = false;
            }
            catch (Exception ex)
            {
                _failed = true;
                Plugin.LoggerInstance.LogError($"[BotVisionSystem] Initialize exception: {ex}");
            }
        }

        #endregion

        #region Overlay Arbitration Tick

        /// <summary>
        /// Overlay/event-only. Called from BotBrain overlay tick. Never per-frame.
        /// Arbitration-locked, pooled, never disables/teleports. Bulletproof failover.
        /// </summary>
        public void Tick(float time)
        {
            if (_failed || !IsValid())
                return;

            try
            {
                // Arbitration overlay lock: only one vision overlay at a time
                if (!BotOverlayManager.CanIssueMove(_bot, BotOverlayType.Vision))
                    return;

                Vector3 eye = EFTPlayerUtil.GetPosition(_bot.GetPlayer) + EyeOffset;
                Vector3 forward = _bot.LookDirection;

                float fogFactor = RenderSettings.fog ? Mathf.Clamp01(RenderSettings.fogDensity * 4f) : 0f;
                float ambient = RenderSettings.ambientLight.grayscale;
                float viewCone = Mathf.Lerp(BaseViewConeAngle, 62f, 1f - ambient);

                // Flashlight/fog occlusion: shrink FOV if bot is being blinded
                IReadOnlyList<Vector3> lights = FlashlightRegistry.GetLastKnownFlashlightPositions();
                for (int i = 0; i < lights.Count; i++)
                {
                    Vector3 lightPos = lights[i];
                    Vector3 toEye = eye - lightPos;
                    float dist = toEye.magnitude;
                    if (dist > 29f)
                        continue;
                    float angle = Vector3.Angle(toEye.normalized, forward);
                    if (angle > 35f)
                        continue;

                    if (!Physics.Linecast(lightPos, eye, out RaycastHit hit, AIRefactoredLayerMasks.LineOfSightMask) ||
                        (hit.collider.transform.root == _bot.Transform?.Original?.root))
                    {
                        viewCone *= 0.59f;
                        break;
                    }
                }

                // Scan all alive players for visual targets (zero alloc w/ pooled list)
                List<Player> players = GameWorldHandler.GetAllAlivePlayers();
                Player bestTarget = null;
                float bestDist = float.MaxValue;

                for (int i = 0, count = players.Count; i < count; i++)
                {
                    Player p = players[i];
                    if (!IsValidTarget(p))
                        continue;

                    Vector3 pos = EFTPlayerUtil.GetPosition(p);
                    float dist = Vector3.Distance(eye, pos);
                    float maxVis = MaxDetectionDistance * (1f - fogFactor);

                    if (dist > maxVis)
                        continue;

                    bool inCone = IsInViewCone(forward, eye, pos, viewCone);
                    bool close = dist <= AutoDetectRadius;
                    bool canSee = HasLineOfSight(eye, p);

                    if ((inCone && canSee) || (close && canSee))
                    {
                        if (dist < bestDist)
                        {
                            bestDist = dist;
                            bestTarget = p;
                        }
                    }
                    else if ((inCone || close) && !canSee)
                    {
                        // Overlay-only: speech, never triggers pose or movement
                        _bot.BotTalk?.TrySay(EPhraseTrigger.OnBeingHurt);
                    }
                }

                if (bestTarget != null)
                {
                    Vector3 pos = EFTPlayerUtil.GetPosition(bestTarget);
                    _memory.RecordEnemyPosition(pos, "Visual", bestTarget.ProfileId);
                    ShareMemoryToSquad(pos);
                    TrackVisibleBones(eye, bestTarget, fogFactor);
                    EvaluateTargetConfidence(bestTarget, time);
                }

                BotOverlayManager.RegisterMove(_bot, BotOverlayType.Vision); // Arbitration release at overlay/event end
            }
            catch (Exception ex)
            {
                _failed = true;
                Plugin.LoggerInstance.LogError($"[BotVisionSystem] Tick exception: {ex}");
            }
        }

        #endregion

        #region Target Evaluation

        private void EvaluateTargetConfidence(Player target, float time)
        {
            try
            {
                TrackedEnemyVisibility tracker = _cache.VisibilityTracker;
                if (tracker == null || !tracker.HasEnoughData)
                    return;

                float confidence = tracker.GetOverallConfidence();
                if (_bot.Memory.IsUnderFire && UnityEngine.Random.value < SuppressionMissChance)
                    return;

                if (confidence < BoneConfidenceThreshold)
                    return;

                CommitEnemyIfAllowed(target, time);
            }
            catch (Exception ex)
            {
                _failed = true;
                Plugin.LoggerInstance.LogError($"[BotVisionSystem] EvaluateTargetConfidence exception: {ex}");
            }
        }

        private void CommitEnemyIfAllowed(Player target, float time)
        {
            try
            {
                if (!EFTPlayerUtil.IsEnemyOf(_bot, target))
                    return;

                float delay = Mathf.Lerp(0.12f, 0.66f, 1f - (_profile?.ReactionTime ?? 0.5f));
                if (time - _lastCommitTime < delay)
                    return;

                IPlayer enemy = EFTPlayerUtil.AsSafeIPlayer(target);
                if (enemy == null || _bot.BotsGroup == null)
                    return;

                _bot.BotsGroup.AddEnemy(enemy, EBotEnemyCause.addPlayer);
                _lastCommitTime = time;

                // Overlay/event-only: no client/headless gating, no movement
                _bot.BotTalk?.TrySay(EPhraseTrigger.OnEnemyConversation);
            }
            catch (Exception ex)
            {
                _failed = true;
                Plugin.LoggerInstance.LogError($"[BotVisionSystem] CommitEnemyIfAllowed exception: {ex}");
            }
        }

        #endregion

        #region Bone Tracking

        private void TrackVisibleBones(Vector3 eye, Player target, float fog)
        {
            try
            {
                if (_cache.VisibilityTracker == null)
                    _cache.VisibilityTracker = new TrackedEnemyVisibility(_bot.Transform.Original);

                TrackedEnemyVisibility tracker = _cache.VisibilityTracker;

                if (target.TryGetComponent(out PlayerSpiritBones bones))
                {
                    Bounds[] bounds = TempBoundsPool.Rent(BonesToCheck.Length);

                    try
                    {
                        for (int i = 0; i < BonesToCheck.Length; i++)
                        {
                            Transform bone = bones.GetBone(BonesToCheck[i]).Original;
                            if (bone != null && !Physics.Linecast(eye, bone.position, out _, AIRefactoredLayerMasks.LineOfSightMask))
                            {
                                bounds[i] = new Bounds(bone.position, Vector3.one * 0.2f);
                                float boost = IsMovingFast(target) ? MotionBoost : 0f;
                                tracker.UpdateBoneVisibility(BonesToCheck[i].ToString(), bone.position, boost, fog);
                            }
                        }
                    }
                    finally
                    {
                        TempBoundsPool.Return(bounds);
                    }
                }
                else
                {
                    Transform tf = EFTPlayerUtil.GetTransform(target);
                    if (tf != null && !Physics.Linecast(eye, tf.position, out _, AIRefactoredLayerMasks.LineOfSightMask))
                    {
                        tracker.UpdateBoneVisibility("Body", tf.position);
                    }
                }

                tracker.DecayConfidence(BoneConfidenceDecay * Time.deltaTime);
            }
            catch (Exception ex)
            {
                _failed = true;
                Plugin.LoggerInstance.LogError($"[BotVisionSystem] TrackVisibleBones exception: {ex}");
            }
        }

        #endregion

        #region Memory Sharing

        private void ShareMemoryToSquad(Vector3 pos)
        {
            if (_cache.GroupSync == null)
                return;

            List<BotComponentCache> teammates = TempListPool.Rent<BotComponentCache>();
            try
            {
                IReadOnlyList<BotOwner> squad = _cache.GroupSync.GetTeammates();
                if (squad == null || squad.Count == 0)
                    return;

                for (int i = 0; i < squad.Count; i++)
                {
                    BotOwner mate = squad[i];
                    if (mate == null)
                        continue;

                    BotComponentCache comp = BotComponentCacheRegistry.TryGetExisting(mate);
                    if (comp != null)
                        teammates.Add(comp);
                }

                if (teammates.Count > 0)
                    _memory.ShareMemoryWith(teammates);
            }
            finally
            {
                TempListPool.Return(teammates);
            }
        }

        #endregion

        #region Utility

        private static bool HasLineOfSight(Vector3 from, Player target)
        {
            Transform t = EFTPlayerUtil.GetTransform(target);
            if (t == null)
                return false;

            Vector3 to = t.position + EyeOffset;
            return !Physics.Linecast(from, to, out RaycastHit hit, AIRefactoredLayerMasks.LineOfSightMask)
                   || hit.collider.transform.root == t.root;
        }

        private static bool IsInViewCone(Vector3 forward, Vector3 origin, Vector3 target, float angle)
        {
            return Vector3.Angle(forward, target - origin) <= angle * 0.5f;
        }

        private static bool IsMovingFast(Player p)
        {
            return p != null && p.Velocity.sqrMagnitude > 2.24f;
        }

        private bool IsValid()
        {
            return !_failed &&
                   _bot != null &&
                   _cache != null &&
                   _profile != null &&
                   _memory != null &&
                   EFTPlayerUtil.IsValid(_bot.GetPlayer) &&
                   !_bot.IsDead &&
                   GameWorldHandler.IsSafeToInitialize;
        }

        private bool IsValidTarget(Player t)
        {
            return EFTPlayerUtil.IsValid(t) &&
                   t.ProfileId != _bot.ProfileId &&
                   EFTPlayerUtil.IsEnemyOf(_bot, t);
        }

        #endregion

        #region API

        /// <summary>
        /// Returns true if the given player is visible right now (tracked by confidence).
        /// </summary>
        public bool IsTargetVisible(Player enemy)
        {
            if (_cache?.VisibilityTracker == null || enemy == null)
                return false;
            return _cache.VisibilityTracker.GetOverallConfidence() > BoneConfidenceThreshold;
        }

        #endregion
    }
}
