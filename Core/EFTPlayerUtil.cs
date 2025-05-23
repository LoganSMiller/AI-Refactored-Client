﻿// <auto-generated>
//   This file is part of AI-Refactored, an open-source project focused on realistic AI behavior in Escape from Tarkov.
//   Licensed under the MIT License. See LICENSE in the repository root for more information.
//
//   THIS FILE IS SYSTEMATICALLY MANAGED.
//   Failures in AIRefactored logic must always trigger safe fallback to EFT base AI, never break global logic.
//   Bulletproof: All failures are locally contained, never break other subsystems.
// </auto-generated>

namespace AIRefactored.Core
{
    using System.Collections.Generic;
    using AIRefactored.Bootstrap;
    using AIRefactored.Runtime;
    using EFT;
    using EFT.HealthSystem;
    using UnityEngine;

    /// <summary>
    /// Provides null-safe and Dissonance-free helpers for resolving EFT.Player and BotOwner references.
    /// Used throughout AI-Refactored for profile resolution, spatial lookups, and bot filtering.
    /// Bulletproof: All errors are locally contained and cannot break other systems.
    /// </summary>
    public static class EFTPlayerUtil
    {
        #region Resolution

        public static Player AsEFTPlayer(IPlayer raw)
        {
            Player cast = raw as Player;
            return cast != null ? cast : null;
        }

        public static bool TryGetValidPlayer(IPlayer raw, out Player player)
        {
            player = raw as Player;
            return player != null && IsValid(player);
        }

        public static IPlayer AsSafeIPlayer(Player player)
        {
            if (player == null)
                return null;
            object obj = player;
            IPlayer cast = obj as IPlayer;
            return cast != null ? cast : null;
        }

        public static Player ResolvePlayer(BotOwner bot)
        {
            return bot != null ? bot.GetPlayer as Player : null;
        }

        public static Player ResolvePlayerById(string profileId)
        {
            if (string.IsNullOrEmpty(profileId))
                return null;

            if (!WorldInitState.IsInPhase(WorldPhase.WorldReady))
                return null;

            GameWorld world = GameWorldHandler.Get();
            if (world == null || world.AllAlivePlayersList == null || world.AllAlivePlayersList.Count == 0)
                return null;

            for (int i = 0; i < world.AllAlivePlayersList.Count; i++)
            {
                Player p = world.AllAlivePlayersList[i];
                if (p != null && p.ProfileId == profileId)
                    return p;
            }

            return null;
        }

        #endregion

        #region Validity + Info

        public static bool IsValid(Player player)
        {
            return player != null
                   && player.HealthController != null
                   && player.HealthController.IsAlive
                   && player.Transform != null
                   && player.Transform.Original != null;
        }

        public static bool IsValidGroupPlayer(Player player)
        {
            return IsValid(player);
        }

        public static bool IsBot(Player player)
        {
            return player != null && player.IsAI;
        }

        public static EPlayerSide GetSide(Player player)
        {
            return player != null ? player.Side : EPlayerSide.Savage;
        }

        public static string GetProfileId(BotOwner bot)
        {
            if (bot == null)
                return string.Empty;

            Player player = bot.GetPlayer;
            if (player == null)
                return string.Empty;

            return player.ProfileId;
        }

        public static bool IsValidBotOwner(BotOwner bot)
        {
            return bot != null
                   && bot.GetPlayer != null
                   && bot.Memory != null
                   && bot.WeaponManager != null
                   && bot.BotsGroup != null;
        }

        public static bool HasValidMovementContext(BotOwner bot)
        {
            return bot != null
                   && bot.GetPlayer != null
                   && bot.GetPlayer.MovementContext != null;
        }

        public static bool IsFikaHeadlessSafe(BotOwner bot)
        {
            Player player = bot != null ? bot.GetPlayer : null;
            return player != null
                   && player.IsAI
                   && player.HealthController != null
                   && player.HealthController.IsAlive;
        }

        #endregion

        #region Spatial

        public static Transform GetTransform(Player player)
        {
            return player != null && player.Transform != null ? player.Transform.Original : null;
        }

        public static Vector3 GetPosition(Player player)
        {
            Transform t = GetTransform(player);
            return t != null ? t.position : Vector3.zero;
        }

        public static Vector3 GetPosition(BotOwner bot)
        {
            return GetPosition(ResolvePlayer(bot));
        }

        #endregion

        #region Combat Logic

        public static bool IsEnemyOf(BotOwner self, Player target)
        {
            if (self == null || target == null)
                return false;

            Player selfPlayer = self.GetPlayer;
            if (!IsValid(selfPlayer) || !IsValid(target))
                return false;

            if (selfPlayer.ProfileId == target.ProfileId)
                return false;

            if (selfPlayer.Side == EPlayerSide.Usec || selfPlayer.Side == EPlayerSide.Bear)
            {
                if (target.Side == EPlayerSide.Usec || target.Side == EPlayerSide.Bear)
                {
                    var selfInfo = selfPlayer.Profile?.Info;
                    var targetInfo = target.Profile?.Info;
                    if (selfInfo != null && targetInfo != null)
                    {
                        if (!string.IsNullOrEmpty(selfInfo.GroupId) &&
                            selfInfo.GroupId == targetInfo.GroupId)
                            return false;
                    }
                    return true;
                }
            }

            if (selfPlayer.Side != target.Side)
                return true;

            if (selfPlayer.Side == EPlayerSide.Savage && target.Side == EPlayerSide.Savage)
            {
                var selfInfo = selfPlayer.Profile?.Info;
                var targetInfo = target.Profile?.Info;
                if (selfInfo != null && targetInfo != null)
                {
                    if (!string.IsNullOrEmpty(selfInfo.GroupId) &&
                        selfInfo.GroupId == targetInfo.GroupId)
                        return false;
                }
                return true;
            }

            if (self.BotsGroup != null)
            {
                IPlayer cast = AsSafeIPlayer(target);
                if (cast != null && self.BotsGroup.IsEnemy(cast))
                    return true;
            }

            return false;
        }

        public static bool AreEnemies(Player a, Player b)
        {
            if (a == null || b == null)
                return false;

            if (a.ProfileId == b.ProfileId)
                return false;

            if (a.Side != b.Side)
                return true;

            if ((a.Side == EPlayerSide.Usec || a.Side == EPlayerSide.Bear) &&
                (b.Side == EPlayerSide.Usec || b.Side == EPlayerSide.Bear))
            {
                var aInfo = a.Profile?.Info;
                var bInfo = b.Profile?.Info;
                if (aInfo != null && bInfo != null)
                {
                    if (!string.IsNullOrEmpty(aInfo.GroupId) &&
                        aInfo.GroupId == bInfo.GroupId)
                        return false;
                }
                return true;
            }

            if (a.Side == EPlayerSide.Savage && b.Side == EPlayerSide.Savage)
            {
                var aInfo = a.Profile?.Info;
                var bInfo = b.Profile?.Info;
                if (aInfo != null && bInfo != null)
                {
                    if (!string.IsNullOrEmpty(aInfo.GroupId) &&
                        aInfo.GroupId == bInfo.GroupId)
                        return false;
                }
                return true;
            }

            return false;
        }

        #endregion
    }
}
