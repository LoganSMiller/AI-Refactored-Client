﻿// <auto-generated>
//   AI-Refactored: BotMemoryExtensions.cs (Supreme Arbitration Overlay, Ultra-Platinum+++, Max Realism, June 2025)
//   All navigation is NavMesh-pooled, intent-driven, deduped, event-only. No tick spam. Full safety, fallback, squad logic, and arbitration. 
//   MIT License.
// </auto-generated>

namespace AIRefactored.AI.Memory
{
    using System;
    using AIRefactored.AI.Core;
    using AIRefactored.AI.Helpers;
    using AIRefactored.AI.Movement;
    using AIRefactored.AI.Threads;
    using AIRefactored.Core;
    using BepInEx.Logging;
    using EFT;
    using UnityEngine;
    using UnityEngine.AI;

    /// <summary>
    /// Overlay/event-driven navigation, tactical memory, and fallback helpers for bots.
    /// Bulletproof: All moves are pooled, deduped, intent-queued, event-only, arbitration-guarded, squad and personality aware.
    /// </summary>
    public static class BotMemoryExtensions
    {
        #region Constants

        private const float MinMoveThreshold = 0.5f;
        private const float NavSampleRadius = 1.5f;
        private const float FallbackRetreatDistance = 6.5f;
        private const float FlankMinDist = 5.0f;
        private const float FlankMaxDist = 18.0f;
        private const float InvestigateRangeSqr = 625f; // 25m
        private const float CoverReevalCooldown = 6f;
        private const float FlankDotThreshold = 0.23f;
        private const float MoveCooldown = 1.25f;
        private const float AnticipationDelayMin = 0.06f;
        private const float AnticipationDelayMax = 0.24f;
        private const float SquadSafeDist = 2.2f;

        #endregion

        #region Fields

        private static readonly ManualLogSource Logger = Plugin.LoggerInstance;

        #endregion

        #region Public Movement Extensions

        public static void ClearLastHeardSound(this BotOwner bot)
        {
            try { if (bot != null) BotMemoryStore.ClearHeardSound(bot.ProfileId); }
            catch (Exception ex) { Logger.LogError($"[BotMemoryExtensions] ClearLastHeardSound failed: {ex}"); }
        }

        /// <summary>
        /// Event-only fallback retreat, arbitration/dedupe/intent/overlay. NavMesh + safety validation.
        /// </summary>
        public static void FallbackTo(this BotOwner bot, Vector3 fallbackPosition, string context = "Fallback")
        {
            try
            {
                if (!EFTPlayerUtil.IsValidBotOwner(bot) || fallbackPosition.sqrMagnitude < MinMoveThreshold) return;
                if (!BotOverlayManager.CanIssueMove(bot, BotOverlayType.Fallback)) return;

                if (!TrySampleNavMesh(fallbackPosition, out Vector3 safe)) return;

                var cache = BotCacheUtility.GetCache(bot);
                Vector3 drifted = BotMovementHelper.ApplyMicroDrift(safe, bot.ProfileId, Time.frameCount, cache?.PersonalityProfile);

                // Dedupe: don't re-issue fallback if same as last
                if (BotMoveCache.IsMoveRedundant(bot, drifted, BotOverlayType.Fallback)) return;

                // Squad avoidance: don't collide with squadmates
                if (SquadOverlapDetected(bot, drifted, SquadSafeDist)) return;

                float anticipation = UnityEngine.Random.Range(AnticipationDelayMin, AnticipationDelayMax);
                BotBrain.ScheduleAfter(bot, anticipation, () =>
                {
                    BotMovementHelper.SmoothMoveToSafe(bot, drifted, slow: true, cohesion: 1f);
                    BotOverlayManager.RegisterMove(bot, BotOverlayType.Fallback);
                    BotMoveCache.RegisterMove(bot, drifted, BotOverlayType.Fallback, Time.time, context);
                    TrySay(bot, EPhraseTrigger.GetInCover, Time.time);
                });
            }
            catch (Exception ex)
            {
                Logger.LogError($"[BotMemoryExtensions] FallbackTo failed: {ex}");
            }
        }

        public static void ForceMoveTo(this BotOwner bot, Vector3 position, string context = "ForceMove")
        {
            try
            {
                if (!EFTPlayerUtil.IsValidBotOwner(bot) || position.sqrMagnitude < MinMoveThreshold) return;
                if (!BotOverlayManager.CanIssueMove(bot, BotOverlayType.Special)) return;

                if (!TrySampleNavMesh(position, out Vector3 safe)) return;

                var cache = BotCacheUtility.GetCache(bot);
                Vector3 drifted = BotMovementHelper.ApplyMicroDrift(safe, bot.ProfileId, Time.frameCount, cache?.PersonalityProfile);

                if (BotMoveCache.IsMoveRedundant(bot, drifted, BotOverlayType.Special)) return;
                if (SquadOverlapDetected(bot, drifted, SquadSafeDist)) return;

                float anticipation = UnityEngine.Random.Range(AnticipationDelayMin, AnticipationDelayMax);
                BotBrain.ScheduleAfter(bot, anticipation, () =>
                {
                    BotMovementHelper.SmoothMoveToSafe(bot, drifted, slow: false, cohesion: 1f);
                    BotOverlayManager.RegisterMove(bot, BotOverlayType.Special);
                    BotMoveCache.RegisterMove(bot, drifted, BotOverlayType.Special, Time.time, context);
                    TrySay(bot, EPhraseTrigger.Roger, Time.time);
                });
            }
            catch (Exception ex)
            {
                Logger.LogError($"[BotMemoryExtensions] ForceMoveTo failed: {ex}");
            }
        }

        public static void RetreatFromEnemy(this BotOwner bot)
        {
            try
            {
                if (!EFTPlayerUtil.IsValidBotOwner(bot) || bot.Memory?.GoalEnemy == null) return;
                if (!BotOverlayManager.CanIssueMove(bot, BotOverlayType.Fallback)) return;

                Vector3 enemyPos = bot.Memory.GoalEnemy.CurrPosition;
                Vector3 retreatDir = (bot.Position - enemyPos).normalized;
                Vector3 fallbackTarget = bot.Position + retreatDir * FallbackRetreatDistance;
                if (!TrySampleNavMesh(fallbackTarget, out Vector3 safe)) return;
                if (BotMoveCache.IsMoveRedundant(bot, safe, BotOverlayType.Fallback)) return;
                if (SquadOverlapDetected(bot, safe, SquadSafeDist)) return;

                BotMovementHelper.SmoothMoveToSafe(bot, safe, slow: true, cohesion: 1f);
                BotOverlayManager.RegisterMove(bot, BotOverlayType.Fallback);
                BotMoveCache.RegisterMove(bot, safe, BotOverlayType.Fallback, Time.time, "RetreatFromEnemy");
                TrySay(bot, EPhraseTrigger.GetInCover, Time.time);
            }
            catch (Exception ex) { Logger.LogError($"[BotMemoryExtensions] RetreatFromEnemy failed: {ex}"); }
        }

        public static void FlankEnemy(this BotOwner bot)
        {
            try
            {
                if (!EFTPlayerUtil.IsValidBotOwner(bot) || bot.Memory?.GoalEnemy == null) return;
                if (!BotOverlayManager.CanIssueMove(bot, BotOverlayType.Special)) return;

                Vector3 toEnemy = bot.Memory.GoalEnemy.CurrPosition - bot.Position;
                float dist = toEnemy.magnitude;
                if (dist < FlankMinDist || dist > FlankMaxDist) return;

                Vector3 flankDir = Vector3.Cross(toEnemy.normalized, Vector3.up);
                if (UnityEngine.Random.value < 0.5f) flankDir = -flankDir;
                Vector3 flankTarget = bot.Position + flankDir * Mathf.Min(dist, 5f);

                if (!TrySampleNavMesh(flankTarget, out Vector3 safe)) return;
                if (BotMoveCache.IsMoveRedundant(bot, safe, BotOverlayType.Special)) return;
                if (SquadOverlapDetected(bot, safe, SquadSafeDist)) return;

                BotMovementHelper.SmoothMoveToSafe(bot, safe, slow: false, cohesion: 1.1f);
                BotOverlayManager.RegisterMove(bot, BotOverlayType.Special);
                BotMoveCache.RegisterMove(bot, safe, BotOverlayType.Special, Time.time, "FlankEnemy");
                TrySay(bot, EPhraseTrigger.OnSix, Time.time);
            }
            catch (Exception ex) { Logger.LogError($"[BotMemoryExtensions] FlankEnemy failed: {ex}"); }
        }

        public static void InvestigateSound(this BotOwner bot, Vector3 soundPos)
        {
            try
            {
                if (!EFTPlayerUtil.IsValidBotOwner(bot)) return;
                if (!BotOverlayManager.CanIssueMove(bot, BotOverlayType.Investigate)) return;
                if ((bot.Position - soundPos).sqrMagnitude > InvestigateRangeSqr) return;

                Vector3 cautious = soundPos + (bot.Position - soundPos).normalized * 3.3f;
                if (!TrySampleNavMesh(cautious, out Vector3 safe)) return;

                if (BotMoveCache.IsMoveRedundant(bot, safe, BotOverlayType.Investigate)) return;
                if (SquadOverlapDetected(bot, safe, SquadSafeDist)) return;

                BotMovementHelper.SmoothMoveToSafe(bot, safe, slow: true, cohesion: 1.25f);
                BotOverlayManager.RegisterMove(bot, BotOverlayType.Investigate);
                BotMoveCache.RegisterMove(bot, safe, BotOverlayType.Investigate, Time.time, "InvestigateSound");
                TrySay(bot, EPhraseTrigger.OnEnemyShot, Time.time);
            }
            catch (Exception ex) { Logger.LogError($"[BotMemoryExtensions] InvestigateSound failed: {ex}"); }
        }

        public static void ReevaluateCurrentCover(this BotOwner bot)
        {
            try
            {
                if (!EFTPlayerUtil.IsValidBotOwner(bot)) return;
                var goal = bot.Memory?.GoalEnemy;
                if (goal == null || !goal.IsVisible) return;

                Vector3 toEnemy = goal.CurrPosition - bot.Position;
                if (toEnemy.magnitude < 2.3f) return;

                float angle = Vector3.Angle(bot.LookDirection, toEnemy);
                if (angle > 65f || toEnemy.sqrMagnitude > InvestigateRangeSqr) return;
                if (!BotOverlayManager.CanIssueMove(bot, BotOverlayType.Cover)) return;

                Vector3 retreat = bot.Position - (toEnemy.normalized * FallbackRetreatDistance);
                if (!TrySampleNavMesh(retreat, out Vector3 safeTarget)) return;
                if (BotMoveCache.IsMoveRedundant(bot, safeTarget, BotOverlayType.Cover)) return;
                if (SquadOverlapDetected(bot, safeTarget, SquadSafeDist)) return;

                BotMovementHelper.SmoothMoveToSafe(bot, safeTarget, slow: true, cohesion: 1.25f);
                BotOverlayManager.RegisterMove(bot, BotOverlayType.Cover);
                BotMoveCache.RegisterMove(bot, safeTarget, BotOverlayType.Cover, Time.time, "ReevalCover");
                TrySay(bot, EPhraseTrigger.GetInCover, Time.time);
            }
            catch (Exception ex) { Logger.LogError($"[BotMemoryExtensions] ReevaluateCurrentCover failed: {ex}"); }
        }

        #endregion

        #region Tactical/Mode Extensions

        public static void SetCautiousSearchMode(this BotOwner bot)
        {
            try
            {
                if (bot?.Memory != null)
                {
                    bot.Memory.AttackImmediately = false;
                    bot.Memory.IsPeace = false;
                }
            }
            catch (Exception ex) { Logger.LogError($"[BotMemoryExtensions] SetCautiousSearchMode failed: {ex}"); }
        }

        public static void SetCombatAggressionMode(this BotOwner bot)
        {
            try
            {
                if (bot?.Memory != null)
                {
                    bot.Memory.AttackImmediately = true;
                    bot.Memory.IsPeace = false;
                }
            }
            catch (Exception ex) { Logger.LogError($"[BotMemoryExtensions] SetCombatAggressionMode failed: {ex}"); }
        }

        public static void SetPeaceMode(this BotOwner bot)
        {
            try
            {
                if (bot?.Memory != null)
                {
                    bot.Memory.AttackImmediately = false;
                    bot.Memory.IsPeace = true;
                    bot.Memory.CheckIsPeace();
                }
            }
            catch (Exception ex) { Logger.LogError($"[BotMemoryExtensions] SetPeaceMode failed: {ex}"); }
        }

        #endregion

        #region Flanking and Utility

        public static Vector3 TryGetFlankDirection(this BotOwner bot, out bool success)
        {
            success = false;
            try
            {
                if (!EFTPlayerUtil.IsValidBotOwner(bot)) return Vector3.zero;
                var goal = bot.Memory?.GoalEnemy;
                if (goal == null) return Vector3.zero;

                Vector3 toEnemy = goal.CurrPosition - bot.Position;
                if (toEnemy.magnitude < FlankMinDist) return Vector3.zero;

                Vector3 enemyDir = toEnemy.normalized;
                Vector3 botDir = bot.LookDirection.normalized;
                if (Vector3.Dot(botDir, enemyDir) < FlankDotThreshold) return Vector3.zero;

                success = true;
                Vector3 cross = Vector3.Cross(enemyDir, Vector3.up).normalized * UnityEngine.Random.Range(3.0f, 6.0f);
                if (UnityEngine.Random.value < 0.5f) cross = -cross;
                return cross;
            }
            catch (Exception ex)
            {
                Logger.LogError($"[BotMemoryExtensions] TryGetFlankDirection failed: {ex}");
                success = false;
                return Vector3.zero;
            }
        }

        private static bool TrySampleNavMesh(Vector3 pos, out Vector3 result)
        {
            result = pos;
            if (NavMesh.SamplePosition(pos, out NavMeshHit hit, NavSampleRadius, NavMesh.AllAreas))
            {
                result = hit.position;
                return true;
            }
            return false;
        }

        private static bool SquadOverlapDetected(BotOwner bot, Vector3 target, float radius)
        {
            try
            {
                var group = bot.BotsGroup;
                if (group == null) return false;
                for (int i = 0; i < group.MembersCount; i++)
                {
                    BotOwner mate = group.Member(i);
                    if (mate == null || mate == bot || mate.IsDead) continue;
                    if ((EFTPlayerUtil.GetPosition(mate) - target).sqrMagnitude < (radius * radius))
                        return true;
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"[BotMemoryExtensions] SquadOverlapDetected failed: {ex}");
            }
            return false;
        }

        private static void TrySay(BotOwner bot, EPhraseTrigger phrase, float now)
        {
            try
            {
                if (bot?.BotTalk != null)
                {
                    bot.BotTalk.TrySay(phrase);
                }
            }
            catch (Exception ex) { Logger.LogError($"[BotMemoryExtensions] TrySay failed: {ex}"); }
        }

        #endregion
    }
}
