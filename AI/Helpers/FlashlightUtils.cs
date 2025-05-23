﻿// <auto-generated>
//   AI-Refactored: FlashLightUtils.cs (Beyond Diamond – Light Exposure Realism Edition)
//   All vision/light exposure logic is bulletproof, deterministic, and safe for multiplayer/headless.
//   All transforms must be pre-validated. No allocations. No invalid Unity calls.
//   MIT License.
// </auto-generated>

namespace AIRefactored.AI.Helpers
{
    using UnityEngine;

    /// <summary>
    /// Evaluates directional exposure to high-intensity light sources like flashlights and flares.
    /// Used by AI vision systems for flashblindness detection, light evasion, and behavioral reactions.
    /// Bulletproof: all logic is null-guarded and outputs are safe and deterministic.
    /// </summary>
    public static class FlashLightUtils
    {
        /// <summary>
        /// Returns a normalized visibility score [0,1] based on proximity and alignment.
        /// </summary>
        public static float CalculateFlashScore(Transform lightTransform, Transform botHeadTransform, float maxDistance = 20f)
        {
            if (!IsValid(lightTransform) || !IsValid(botHeadTransform))
                return 0f;

            Vector3 toLight = lightTransform.position - botHeadTransform.position;
            float distance = toLight.magnitude;

            if (distance < 0.01f || distance > maxDistance || float.IsNaN(distance))
                return 0f;

            float alignment = Vector3.Dot(botHeadTransform.forward, toLight.normalized);
            if (float.IsNaN(alignment))
                return 0f;

            float alignFactor = Mathf.Clamp01(alignment);
            float distFactor = 1f - Mathf.Clamp01(distance / maxDistance);

            if (alignFactor > 0.93f && distance < maxDistance * 0.7f)
                return Mathf.Clamp01(alignFactor * 1.18f * distFactor);

            return alignFactor * distFactor;
        }

        /// <summary>
        /// Returns frontal dot product (0 = behind, 1 = directly in front) of light vs bot head.
        /// </summary>
        public static float GetFlashIntensityFactor(Transform lightTransform, Transform botHeadTransform)
        {
            if (!IsValid(lightTransform) || !IsValid(botHeadTransform))
                return 0f;

            float dot = Vector3.Dot(botHeadTransform.forward, (lightTransform.position - botHeadTransform.position).normalized);
            return float.IsNaN(dot) ? 0f : Mathf.Clamp01(dot);
        }

        /// <summary>
        /// Checks whether bot is within light cone angle based on direct angle.
        /// </summary>
        public static bool IsBlindingLight(Transform lightTransform, Transform botHeadTransform, float angleThreshold = 30f)
        {
            if (!IsValid(lightTransform) || !IsValid(botHeadTransform))
                return false;

            float angle = Vector3.Angle(botHeadTransform.forward, lightTransform.position - botHeadTransform.position);
            return !float.IsNaN(angle) && angle <= angleThreshold;
        }

        /// <summary>
        /// Checks if light source is pointed directly at bot’s head.
        /// </summary>
        public static bool IsFacingTarget(Transform source, Transform target, float angleThreshold = 30f)
        {
            if (!IsValid(source) || !IsValid(target))
                return false;

            float angle = Vector3.Angle(source.forward, target.position - source.position);
            return !float.IsNaN(angle) && angle <= angleThreshold;
        }

        /// <summary>
        /// Null-safe validation for transform usage.
        /// </summary>
        private static bool IsValid(Transform tf)
        {
            return tf != null && !float.IsNaN(tf.position.x) && !float.IsNaN(tf.forward.x);
        }
    }
}
