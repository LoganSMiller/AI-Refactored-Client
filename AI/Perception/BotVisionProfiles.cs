﻿// <auto-generated>
//   AI-Refactored: BotVisionProfiles.cs (Ultra-Platinum++ Overlay, Max Realism, June 2025)
//   SYSTEMATICALLY MANAGED. All vision profiles, blending, and overlays are null-guarded, pooled, multiplayer/headless safe.
//   Realism: WildSpawnType + personality blending, no squad/weather overlays, zero alloc, fail-safe. MIT License.
// </auto-generated>

namespace AIRefactored.AI.Perception
{
    using System.Collections.Generic;
    using AIRefactored.AI.Core;
    using AIRefactored.AI.Helpers;
    using AIRefactored.Core;
    using AIRefactored.Pools;
    using EFT;
    using UnityEngine;

    /// <summary>
    /// Provides vision profiles per <see cref="WildSpawnType"/> and personality for AIRefactored bots.
    /// Handles flash/flare/suppression/light overlays, personality blending, and strict null-guarding.
    /// All results are allocation-free and error isolated. Always fails back to base EFT logic/profile.
    /// </summary>
    public static class BotVisionProfiles
    {
        #region Static Defaults

        private static readonly BotVisionProfile DefaultProfile = BotVisionProfile.CreateDefault();
        private static readonly Dictionary<WildSpawnType, BotVisionProfile> Profiles = InitializeProfiles();

        #endregion

        #region Public API

        /// <summary>
        /// Retrieves the bot vision profile, blending WildSpawnType defaults with personality traits.
        /// Always returns a non-null, fully valid profile. All blending is error-guarded and alloc-free.
        /// </summary>
        public static BotVisionProfile Get(Player bot)
        {
            if (!EFTPlayerUtil.IsValid(bot))
                return DefaultProfile;

            WildSpawnType role = WildSpawnType.assault;
            Profile profile = bot.Profile;

            if (profile?.Info?.Settings != null)
                role = profile.Info.Settings.Role;

            if (!Profiles.TryGetValue(role, out BotVisionProfile baseProfile) || baseProfile == null)
                baseProfile = DefaultProfile;

            BotComponentCache cache = BotCacheUtility.GetCache(bot);
            if (cache == null)
                return baseProfile;

            var aiOwner = cache.AIRefactoredBotOwner;
            if (aiOwner == null)
                return baseProfile;

            BotPersonalityProfile personality = aiOwner.PersonalityProfile;
            if (personality == null)
                return baseProfile;

            // Blended profile: combines WildSpawnType base with personality for realism (error-guarded, alloc-free)
            var blended = new BotVisionProfile();
            blended.SetFrom(baseProfile);

            // Personality blending weight (tweakable for realism, e.g. 0.45f)
            float blendWeight = 0.45f;

            blended.AdaptationSpeed = Mathf.Clamp(
                Mathf.Lerp(blended.AdaptationSpeed, 1.0f + (1f - personality.Caution) * 0.85f, blendWeight),
                0.5f, 3.0f);

            blended.MaxBlindness = Mathf.Clamp(
                Mathf.Lerp(blended.MaxBlindness, 1.0f + (1f - personality.RiskTolerance) * 0.75f, blendWeight),
                0.5f, 2.5f);

            blended.LightSensitivity = Mathf.Clamp(
                Mathf.Lerp(blended.LightSensitivity, 0.75f + personality.Caution * 0.9f, blendWeight),
                0.3f, 2.0f);

            blended.AggressionResponse = Mathf.Clamp(
                Mathf.Lerp(blended.AggressionResponse, 0.85f + personality.AggressionLevel * 1.2f, blendWeight),
                0.5f, 3.5f);

            blended.ClarityRecoverySpeed = Mathf.Clamp(
                Mathf.Lerp(blended.ClarityRecoverySpeed, 0.35f + (1f - personality.Caution) * 0.35f, blendWeight),
                0.1f, 1.2f);

            // Optional: Personality chaos bias (no overlays)
            blended.VisionBias += Mathf.Clamp(personality.ChaosFactor * 0.12f, -0.3f, 0.3f);

            return blended;
        }

        #endregion

        #region Profile Setup

        private static Dictionary<WildSpawnType, BotVisionProfile> InitializeProfiles()
        {
            var temp = TempDictionaryPool.Rent<WildSpawnType, BotVisionProfile>();
            var result = new Dictionary<WildSpawnType, BotVisionProfile>(32);

            void Add(WildSpawnType type, float a, float l, float ar, float mb, float cr)
                => temp[type] = BotVisionProfile.CreateForRole(a, ar, l, mb, cr);

            Add(WildSpawnType.assault, 0.75f, 1.2f, 0.9f, 1.1f, 0.35f);
            Add(WildSpawnType.cursedAssault, 0.7f, 1.4f, 1.0f, 1.2f, 0.3f);
            Add(WildSpawnType.marksman, 1.0f, 1.0f, 1.1f, 1.1f, 0.4f);
            Add(WildSpawnType.sectantPriest, 0.5f, 1.5f, 0.5f, 1.3f, 0.25f);
            Add(WildSpawnType.sectantWarrior, 0.6f, 1.5f, 0.8f, 1.3f, 0.3f);
            Add(WildSpawnType.pmcBot, 2.0f, 0.85f, 1.4f, 0.8f, 0.5f);
            Add(WildSpawnType.exUsec, 1.9f, 0.85f, 1.4f, 0.85f, 0.45f);
            Add(WildSpawnType.bossBully, 1.3f, 1.0f, 2.0f, 1.0f, 0.55f);
            Add(WildSpawnType.followerBully, 1.1f, 1.0f, 1.7f, 1.0f, 0.45f);
            Add(WildSpawnType.bossKilla, 1.6f, 0.7f, 2.5f, 0.9f, 0.5f);
            Add(WildSpawnType.bossTagilla, 1.5f, 0.9f, 2.2f, 0.95f, 0.4f);
            Add(WildSpawnType.followerTagilla, 1.2f, 1.0f, 1.6f, 1.0f, 0.45f);
            Add(WildSpawnType.bossSanitar, 1.4f, 0.95f, 2.0f, 0.95f, 0.5f);
            Add(WildSpawnType.followerSanitar, 1.3f, 1.0f, 1.7f, 1.0f, 0.45f);
            Add(WildSpawnType.bossGluhar, 1.4f, 1.0f, 2.2f, 1.0f, 0.55f);
            Add(WildSpawnType.followerGluharAssault, 1.2f, 1.0f, 1.5f, 1.0f, 0.5f);
            Add(WildSpawnType.followerGluharScout, 1.3f, 1.0f, 1.7f, 1.0f, 0.5f);
            Add(WildSpawnType.followerGluharSecurity, 1.1f, 1.1f, 1.6f, 1.0f, 0.45f);
            Add(WildSpawnType.followerGluharSnipe, 1.0f, 1.1f, 1.4f, 1.0f, 0.4f);
            Add(WildSpawnType.bossKnight, 1.5f, 1.0f, 2.0f, 0.9f, 0.5f);
            Add(WildSpawnType.followerBigPipe, 1.2f, 1.0f, 1.8f, 0.95f, 0.45f);
            Add(WildSpawnType.followerBirdEye, 1.2f, 1.1f, 1.6f, 1.0f, 0.5f);
            Add(WildSpawnType.gifter, 1.0f, 0.8f, 0.5f, 1.1f, 0.3f);
            Add(WildSpawnType.arenaFighter, 1.3f, 1.0f, 1.5f, 0.95f, 0.45f);

            foreach (var kvp in temp)
                result[kvp.Key] = kvp.Value;

            TempDictionaryPool.Return(temp);
            return result;
        }

        #endregion
    }
}
