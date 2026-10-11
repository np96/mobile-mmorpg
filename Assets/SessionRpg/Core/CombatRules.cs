using System;

namespace SessionRpg.Core
{
    public readonly struct CombatRules
    {
        public CombatRules(
            int tickRate,
            float moveSpeed,
            float safeRadius,
            float outerKillRadius,
            float lavaDamagePerSecond)
            : this(
                tickRate,
                moveSpeed,
                safeRadius,
                outerKillRadius,
                lavaDamagePerSecond,
                1.5f,
                15f,
                5,
                ArenaCollisionMap.None)
        {
        }

        public CombatRules(
            int tickRate,
            float moveSpeed,
            float safeRadius,
            float outerKillRadius,
            float lavaDamagePerSecond,
            float defaultGlobalCooldownSeconds,
            float diminishingReturnResetSeconds,
            int maximumComboPoints)
            : this(
                tickRate,
                moveSpeed,
                safeRadius,
                outerKillRadius,
                lavaDamagePerSecond,
                defaultGlobalCooldownSeconds,
                diminishingReturnResetSeconds,
                maximumComboPoints,
                ArenaCollisionMap.None)
        {
        }

        public CombatRules(
            int tickRate,
            float moveSpeed,
            float safeRadius,
            float outerKillRadius,
            float lavaDamagePerSecond,
            float defaultGlobalCooldownSeconds,
            float diminishingReturnResetSeconds,
            int maximumComboPoints,
            ArenaCollisionMap collisionMap,
            bool radialHazardsEnabled = true,
            TeamId abilityProgressionTeam = default,
            PlayableModeId playableMode = PlayableModeId.None)
        {
            if (tickRate <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(tickRate));
            }

            if (defaultGlobalCooldownSeconds < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(defaultGlobalCooldownSeconds));
            }

            if (diminishingReturnResetSeconds <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(diminishingReturnResetSeconds));
            }

            if (maximumComboPoints <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maximumComboPoints));
            }

            TickRate = tickRate;
            MoveSpeed = moveSpeed;
            SafeRadius = safeRadius;
            OuterKillRadius = outerKillRadius;
            LavaDamagePerSecond = lavaDamagePerSecond;
            DefaultGlobalCooldownSeconds = defaultGlobalCooldownSeconds;
            DiminishingReturnResetSeconds = diminishingReturnResetSeconds;
            MaximumComboPoints = maximumComboPoints;
            CollisionMap = collisionMap;
            RadialHazardsEnabled = radialHazardsEnabled;
            AbilityProgressionTeam = abilityProgressionTeam;
            PlayableMode = playableMode;
        }

        public int TickRate { get; }

        public float TickSeconds => 1f / TickRate;

        public float MoveSpeed { get; }

        public float SafeRadius { get; }

        public float OuterKillRadius { get; }

        public float LavaDamagePerSecond { get; }

        public float DefaultGlobalCooldownSeconds { get; }

        public float DiminishingReturnResetSeconds { get; }

        public int MaximumComboPoints { get; }

        public ArenaCollisionMap CollisionMap { get; }

        /// <summary>
        /// Whether arena-style radial lava and outer-ring death rules apply.
        /// World routes can retain authoritative collision bounds without being
        /// interpreted as circles around the global origin.
        /// </summary>
        public bool RadialHazardsEnabled { get; }

        /// <summary>
        /// The team whose actions use the bounded leveling ability profile.
        /// An invalid identity disables progression gating for modes with full
        /// specialization kits. Leveling enemies remain free to use their authored
        /// NPC loadouts regardless of player level.
        /// </summary>
        public TeamId AbilityProgressionTeam { get; }

        /// <summary>
        /// Selects explicit mode loadout extensions while keeping Arena kits
        /// normalized. None preserves compatibility for tests and legacy callers.
        /// </summary>
        public PlayableModeId PlayableMode { get; }

        public static CombatRules ArenaDefault => new CombatRules(
            30,
            5.4f,
            ArenaLayout.SafeRadius,
            ArenaLayout.OuterKillRadius,
            24f,
            1.5f,
            15f,
            5,
            ArenaCollisionMap.ArenaDefault,
            playableMode: PlayableModeId.Arena);

        /// <summary>
        /// Hazard-free authoritative combat rules for the bounded Siege arena.
        /// Mode identity enables Siege-only loadout actions such as Blade Tempest.
        /// </summary>
        public static CombatRules HeroSiegeDefault => new CombatRules(
            30,
            5.4f,
            ArenaLayout.SafeRadius,
            ArenaLayout.OuterKillRadius,
            0f,
            1.5f,
            15f,
            5,
            ArenaCollisionMap.SiegeDefault,
            false,
            playableMode: PlayableModeId.HeroSiege);

        public static CombatRules BossDefault => new CombatRules(30, 5.4f, 13f, 19f, 28f);

        public static CombatRules DungeonArenaDefault => new CombatRules(
            30,
            5.4f,
            ArenaLayout.SafeRadius,
            ArenaLayout.OuterKillRadius,
            24f,
            1.5f,
            15f,
            5,
            ArenaCollisionMap.ArenaDefault,
            playableMode: PlayableModeId.Dungeon);

        public static CombatRules DungeonBossDefault => new CombatRules(
            30,
            5.4f,
            13f,
            19f,
            28f,
            1.5f,
            15f,
            5,
            ArenaCollisionMap.None,
            playableMode: PlayableModeId.Dungeon);

        /// <summary>
        /// A hazard-free deterministic playspace for the first leveling route.
        /// The Core collision map owns a conservative rectangular walkable lane;
        /// Unity walls, cinder, and lava remain presentation only.
        /// </summary>
        public static CombatRules LevelingDefault => new CombatRules(
            30,
            5.4f,
            32f,
            38f,
            0f,
            1.5f,
            15f,
            5,
            ArenaCollisionMap.LevelingDefault,
            false,
            new TeamId(1));

        /// <summary>
        /// Ashen Causeway topology with normalized full specialization kits for
        /// cooperative Dungeon traversal encounters.
        /// </summary>
        public static CombatRules DungeonTraversalDefault => new CombatRules(
            30,
            5.4f,
            32f,
            38f,
            0f,
            1.5f,
            15f,
            5,
            ArenaCollisionMap.LevelingDefault,
            false,
            playableMode: PlayableModeId.Dungeon);

        /// <summary>
        /// Persistent local PvE uses Sunbreak Basin's authoritative region map,
        /// full specialization kits, and its own progression curve. It is not
        /// Training and does not inherit Training's learned-action restrictions.
        /// </summary>
        public static CombatRules PveLevelingDefault => new CombatRules(
            30,
            5.4f,
            32f,
            38f,
            0f,
            1.5f,
            15f,
            5,
            ArenaCollisionMap.PveLevelingDefault,
            false,
            playableMode: PlayableModeId.PveLeveling);
    }
}
