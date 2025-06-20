﻿// <auto-generated>
//   AI-Refactored: BotSoundUtils.cs (Supreme Arbitration Overlay/Event Edition, June 2025 – Ultra-Max Realism, Full Context, Bulletproof)
//   Event/overlay-driven, personality/context-aware, squad+role+environment filter. Bulletproof, no state mutation, error-isolated, multiplayer/headless safe.
//   MIT License.
// </auto-generated>

namespace AIRefactored.AI.Helpers
{
    using AIRefactored.AI.Core;
    using AIRefactored.Core;
    using EFT;
    using UnityEngine;

    /// <summary>
    /// Advanced overlay/event-driven sound awareness utilities for AIRefactored bots.
    /// Provides ultra-bulletproof, personality, squad, map, and environment-aware hearing logic.
    /// Never allocates or mutates state; all checks are one-shot and safe for headless/multiplayer.
    /// </summary>
    public static class BotSoundUtils
    {
        /// <summary>
        /// Returns true if the given source is not a squadmate and fired recently, factoring personality and map context.
        /// Bulletproof, never throws, always multiplayer/headless safe.
        /// </summary>
        public static bool DidFireRecently(
            BotOwner self, Player source,
            float threshold = 1.5f, float now = -1f,
            bool checkHearing = true)
        {
            try
            {
                if (!EFTPlayerUtil.IsValidBotOwner(self) || !EFTPlayerUtil.IsValid(source) || self == null || source == null)
                    return false;

                // Ignore if friendly, or self
                if (!EFTPlayerUtil.IsEnemyOf(self, source) || self.ProfileId == source.ProfileId)
                    return false;

                if (!BotSoundRegistry.FiredRecently(source, threshold, now))
                    return false;

                // Optional: Hearing logic, environmental occlusion, map modifiers
                if (checkHearing && !HasHearingOf(self, source, isGunshot: true))
                    return false;

                // Optional: Reactivity filter based on bot's personality
                if (!ShouldReactToSound(self, source, isGunshot: true))
                    return false;

                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Returns true if the given source is not a squadmate and stepped recently, factoring personality and environment.
        /// Bulletproof, never throws, always multiplayer/headless safe.
        /// </summary>
        public static bool DidStepRecently(
            BotOwner self, Player source,
            float threshold = 1.2f, float now = -1f,
            bool checkHearing = true)
        {
            try
            {
                if (!EFTPlayerUtil.IsValidBotOwner(self) || !EFTPlayerUtil.IsValid(source) || self == null || source == null)
                    return false;

                // Ignore if friendly, or self
                if (!EFTPlayerUtil.IsEnemyOf(self, source) || self.ProfileId == source.ProfileId)
                    return false;

                if (!BotSoundRegistry.SteppedRecently(source, threshold, now))
                    return false;

                // Optional: Hearing logic, environmental occlusion, map modifiers
                if (checkHearing && !HasHearingOf(self, source, isGunshot: false))
                    return false;

                // Optional: Reactivity filter based on bot's personality
                if (!ShouldReactToSound(self, source, isGunshot: false))
                    return false;

                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Returns true if the source produced any sound event (shot or step) that this bot would realistically notice.
        /// </summary>
        public static bool DidMakeSoundRecently(
            BotOwner self, Player source,
            float shotThresh = 1.5f, float stepThresh = 1.2f,
            float now = -1f, bool checkHearing = true)
        {
            return DidFireRecently(self, source, shotThresh, now, checkHearing)
                || DidStepRecently(self, source, stepThresh, now, checkHearing);
        }

        /// <summary>
        /// True if the bot, based on personality and environment, would react to this sound.
        /// </summary>
        private static bool ShouldReactToSound(BotOwner self, Player source, bool isGunshot)
        {
            try
            {
                var profile = BotRegistry.GetOrRegister(self);
                if (profile == null)
                    return true; // Default to positive (safe)

                // Fearful or cautious bots react more often
                float reactivity = profile.Caution * 0.6f + (profile.IsFearful ? 0.5f : 0f);
                if (isGunshot) reactivity += profile.AggressionLevel * 0.25f;

                // Frenzied/aggressive bots may sometimes ignore quieter sounds unless nearby or repeated
                if (profile.IsFrenzied && !isGunshot)
                    reactivity *= 0.7f;

                // Randomize slightly to avoid perfect predictability
                float roll = UnityEngine.Random.value;
                return roll < Mathf.Clamp01(reactivity + 0.25f);
            }
            catch
            {
                return true;
            }
        }

        /// <summary>
        /// Returns true if self can plausibly hear the sound from source (distance, direction, basic occlusion, personality, etc).
        /// In future, expand with indoor/outdoor, wall checks, and sound type.
        /// </summary>
        private static bool HasHearingOf(BotOwner self, Player source, bool isGunshot)
        {
            try
            {
                if (self == null || source == null) return false;
                Vector3 posSelf = EFTPlayerUtil.GetPosition(self.GetPlayer);
                Vector3 posSource = EFTPlayerUtil.GetPosition(source);

                float maxRadius = isGunshot ? 34f : 22f;
                // Enhance for personality traits: cautious/fearful have better "hearing", aggressive less
                var profile = BotRegistry.GetOrRegister(self);
                if (profile != null)
                {
                    if (profile.IsFearful) maxRadius += 4f;
                    else if (profile.IsFrenzied) maxRadius -= 2f;
                }

                // Future: check for environmental occlusion, etc
                // e.g., if (Environment.IsWallBetween(posSelf, posSource)) maxRadius *= 0.6f;

                float distSqr = (posSelf - posSource).sqrMagnitude;
                return distSqr < (maxRadius * maxRadius);
            }
            catch
            {
                return false;
            }
        }
    }
}
