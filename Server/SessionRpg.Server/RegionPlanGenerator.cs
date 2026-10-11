namespace SessionRpg.Server
{
    /// <summary>
    /// Creates a bounded server-prototype connectivity plan, not walkable terrain,
    /// Core world geometry, a Unity manifest, or an implemented unlock rule. Edges
    /// describe undirected connections and are stored once in ordinal endpoint
    /// order. The complete result must be saved; seeds alone are not save data.
    /// </summary>
    public static class RegionPlanGenerator
    {
        public const string TemplateId = "ashen_frontier_plan_v1";
        public const int GenerationVersion = 1;
        public const int ContentVersion = 1;

        private static readonly string[] RoadEncounterTemplates =
        {
            "frontier_plan_camp_raider_marksman_v1",
            "frontier_plan_camp_raider_prelate_v1",
            "frontier_plan_camp_brute_marksman_v1",
        };

        private static readonly string[] RuinsEncounterTemplates =
        {
            "frontier_plan_camp_hexer_warder_v1",
            "frontier_plan_camp_prelate_frostweaver_v1",
            "frontier_plan_camp_brute_hexer_v1",
        };

        private static readonly string[] EliteEncounterTemplates =
        {
            "frontier_plan_elite_warder_v1",
            "frontier_plan_elite_brute_v1",
            "frontier_plan_elite_frostweaver_v1",
        };

        public static RegionPlan Create(Guid regionId, int seed)
        {
            if (regionId == Guid.Empty)
            {
                throw new ArgumentException(
                    "A saved region requires a non-empty instance ID.", nameof(regionId));
            }

            var random = new PlanRandom(seed);
            int roadCampCount = 2 + random.Next(2);
            int ruinsCampCount = 1 + random.Next(2);
            bool eliteOnRoad = random.Next(2) == 0;
            bool shortcutConnectsToRoad = random.Next(2) == 1;

            var nodes = new List<RegionNode>(10)
            {
                new("refuge", "refuge", ""),
                new("crossroads", "crossroads", ""),
                new("shortcut", "shortcut", ""),
                new("optional_elite", "elite",
                    EliteEncounterTemplates[random.Next(EliteEncounterTemplates.Length)]),
                new("stronghold_guardian", "guardian", "frontier_plan_guardian_v1"),
            };
            var edges = new List<RegionEdge>(12);
            AddConnection(edges, "refuge", "crossroads");

            string previous = "crossroads";
            for (int index = 1; index <= roadCampCount; index++)
            {
                string nodeId = "old_road_camp_" +
                    index.ToString(System.Globalization.CultureInfo.InvariantCulture);
                nodes.Add(new RegionNode(nodeId, "camp",
                    RoadEncounterTemplates[random.Next(RoadEncounterTemplates.Length)]));
                AddConnection(edges, previous, nodeId);
                previous = nodeId;
            }

            string finalRoadCamp = previous;
            AddConnection(edges, finalRoadCamp, "stronghold_guardian");

            previous = "crossroads";
            for (int index = 1; index <= ruinsCampCount; index++)
            {
                string nodeId = "ruins_camp_" +
                    index.ToString(System.Globalization.CultureInfo.InvariantCulture);
                nodes.Add(new RegionNode(nodeId, "camp",
                    RuinsEncounterTemplates[random.Next(RuinsEncounterTemplates.Length)]));
                AddConnection(edges, previous, nodeId);
                previous = nodeId;
            }

            AddConnection(edges, previous, "shortcut");
            AddConnection(edges, "shortcut", "stronghold_guardian");
            AddConnection(edges,
                eliteOnRoad ? finalRoadCamp : previous, "optional_elite");

            if (shortcutConnectsToRoad)
            {
                AddConnection(edges, "old_road_camp_1", "shortcut");
            }

            // Stable ordering is part of generation version 1, independent of
            // runtime collection enumeration and the caller's current culture.
            nodes.Sort((left, right) =>
                StringComparer.Ordinal.Compare(left.NodeId, right.NodeId));
            edges.Sort((left, right) =>
            {
                int from = StringComparer.Ordinal.Compare(left.FromNodeId, right.FromNodeId);
                return from != 0
                    ? from
                    : StringComparer.Ordinal.Compare(left.ToNodeId, right.ToNodeId);
            });

            return new RegionPlan(regionId, TemplateId, seed,
                GenerationVersion, ContentVersion, nodes.ToArray(), edges.ToArray());
        }

        private static void AddConnection(List<RegionEdge> edges, string from, string to)
        {
            edges.Add(StringComparer.Ordinal.Compare(from, to) < 0
                ? new RegionEdge(from, to)
                : new RegionEdge(to, from));
        }

        /// <summary>
        /// Fixed SplitMix32 mixing with explicit unchecked uint arithmetic. This
        /// contract must not be replaced with runtime-dependent System.Random or
        /// string.GetHashCode. Modulo selection is sufficient for a tiny layout
        /// variation set; this generator is not a security or loot RNG.
        /// </summary>
        private struct PlanRandom
        {
            private uint _state;

            public PlanRandom(int seed)
            {
                _state = unchecked((uint)seed);
            }

            public int Next(int exclusiveMaximum)
            {
                unchecked
                {
                    _state += 0x9e3779b9u;
                    uint value = _state;
                    value = (value ^ (value >> 16)) * 0x21f0aaadu;
                    value = (value ^ (value >> 15)) * 0x735a2d97u;
                    value ^= value >> 15;
                    return (int)(value % (uint)exclusiveMaximum);
                }
            }
        }
    }
}
