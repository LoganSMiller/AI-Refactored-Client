﻿// <auto-generated>
//   AI-Refactored: CombatState.cs (Systematically Managed – Realism FSM Integration)
//   ENUM: Describes combat state transitions for CombatStateMachine handlers.
//   MIT License. No nullable context. StyleCop/ReSharper compliant.
// </auto-generated>

namespace AIRefactored.AI.Combat.States
{
    /// <summary>
    /// Describes the bot’s current combat behavior mode.
    /// Used by state handlers and the state machine to determine bot actions in different scenarios.
    /// </summary>
    public enum CombatState
    {
        /// <summary>
        /// Bot is patrolling the area, typically looking for threats or objectives.
        /// </summary>
        Patrol,

        /// <summary>
        /// Bot is investigating a noise, anomaly, or potential threat.
        /// </summary>
        Investigate,

        /// <summary>
        /// Bot has detected a threat and is preparing to engage.
        /// </summary>
        Engage,

        /// <summary>
        /// Bot is actively attacking an enemy, either by moving to the target or shooting.
        /// </summary>
        Attack,

        /// <summary>
        /// Bot is retreating from combat or seeking cover to avoid further engagement.
        /// </summary>
        Fallback
    }
}
