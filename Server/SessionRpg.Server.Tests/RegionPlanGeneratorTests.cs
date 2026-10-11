using System.Globalization;
using SessionRpg.Server;
using Xunit;

namespace SessionRpg.Server.Tests
{
    public sealed class RegionPlanGeneratorTests
    {
        private static readonly Guid RegionId =
            Guid.Parse("70b7d563-e7b8-4d53-88db-aa29f0e50a92");

        [Fact]
        public void ZeroSeedMatchesVersionOneGoldenPlan()
        {
            RegionPlan plan = RegionPlanGenerator.Create(RegionId, 0);

            Assert.Equal(RegionId, plan.RegionInstanceId);
            Assert.Equal("ashen_frontier_plan_v1", plan.TemplateId);
            Assert.Equal(0, plan.Seed);
            Assert.Equal(1, plan.GenerationVersion);
            Assert.Equal(1, plan.ContentVersion);
            Assert.Equal(new[]
            {
                "crossroads|crossroads|",
                "old_road_camp_1|camp|frontier_plan_camp_brute_marksman_v1",
                "old_road_camp_2|camp|frontier_plan_camp_raider_prelate_v1",
                "optional_elite|elite|frontier_plan_elite_frostweaver_v1",
                "refuge|refuge|",
                "ruins_camp_1|camp|frontier_plan_camp_hexer_warder_v1",
                "shortcut|shortcut|",
                "stronghold_guardian|guardian|frontier_plan_guardian_v1",
            }, NodeDescriptions(plan));
            Assert.Equal(new[]
            {
                "crossroads|old_road_camp_1",
                "crossroads|refuge",
                "crossroads|ruins_camp_1",
                "old_road_camp_1|old_road_camp_2",
                "old_road_camp_2|optional_elite",
                "old_road_camp_2|stronghold_guardian",
                "ruins_camp_1|shortcut",
                "shortcut|stronghold_guardian",
            }, EdgeDescriptions(plan));
        }

        [Fact]
        public void RepeatedGenerationIsDeterministicAcrossManySignedSeeds()
        {
            foreach (int seed in Seeds())
            {
                RegionPlan first = RegionPlanGenerator.Create(RegionId, seed);
                RegionPlan repeated = RegionPlanGenerator.Create(RegionId, seed);

                Assert.Equal(seed, first.Seed);
                Assert.Equal(first.Nodes, repeated.Nodes);
                Assert.Equal(first.Edges, repeated.Edges);
            }
        }

        [Fact]
        public void InstanceIdentityDoesNotChangeSeededContent()
        {
            Guid otherId = Guid.Parse("cc6c91cd-ec8e-492b-826f-ecc975623ef3");
            RegionPlan first = RegionPlanGenerator.Create(RegionId, -12345);
            RegionPlan other = RegionPlanGenerator.Create(otherId, -12345);

            Assert.NotEqual(first.RegionInstanceId, other.RegionInstanceId);
            Assert.Equal(first.Nodes, other.Nodes);
            Assert.Equal(first.Edges, other.Edges);
        }

        [Fact]
        public void EveryPlanIsBoundedConnectedAndHasCanonicalUniqueConnections()
        {
            foreach (int seed in Seeds())
            {
                RegionPlan plan = RegionPlanGenerator.Create(RegionId, seed);
                var nodeIds = plan.Nodes.Select(node => node.NodeId).ToHashSet();

                Assert.InRange(plan.Nodes.Length, 8, 10);
                Assert.InRange(plan.Edges.Length, 8, 11);
                Assert.InRange(plan.Nodes.Count(node => node.Kind == "camp"), 3, 5);
                Assert.Distinct(plan.Nodes.Select(node => node.NodeId));
                Assert.Distinct(plan.Edges);
                Assert.Equal(plan.Nodes.Select(node => node.NodeId)
                    .OrderBy(id => id, StringComparer.Ordinal),
                    plan.Nodes.Select(node => node.NodeId));
                Assert.Equal(EdgeDescriptions(plan).OrderBy(edge => edge, StringComparer.Ordinal),
                    EdgeDescriptions(plan));

                Assert.All(plan.Edges, edge =>
                {
                    Assert.Contains(edge.FromNodeId, nodeIds);
                    Assert.Contains(edge.ToNodeId, nodeIds);
                    Assert.True(StringComparer.Ordinal.Compare(edge.FromNodeId, edge.ToNodeId) < 0);
                });
                Assert.All(plan.Nodes, node =>
                {
                    if (node.Kind is "camp" or "elite" or "guardian")
                    {
                        Assert.StartsWith("frontier_plan_", node.EncounterTemplateId);
                    }
                    else
                    {
                        Assert.Empty(node.EncounterTemplateId);
                    }
                });

                Assert.True(nodeIds.SetEquals(Reachable(plan)));
                Assert.Single(plan.Nodes, node => node.Kind == "refuge");
                Assert.Single(plan.Nodes, node => node.Kind == "crossroads");
                Assert.Single(plan.Nodes, node => node.Kind == "shortcut");
                Assert.Single(plan.Nodes, node => node.Kind == "guardian");
            }
        }

        [Fact]
        public void EliteIsAnOptionalBranchAndBothRoutesReachGuardianWithoutIt()
        {
            foreach (int seed in Seeds())
            {
                RegionPlan plan = RegionPlanGenerator.Create(RegionId, seed);
                RegionNode elite = Assert.Single(plan.Nodes, node => node.Kind == "elite");
                Assert.Equal("optional_elite", elite.NodeId);
                Assert.Single(plan.Edges, edge =>
                    edge.FromNodeId == elite.NodeId || edge.ToNodeId == elite.NodeId);

                var noElite = new HashSet<string> { elite.NodeId };
                Assert.Contains("stronghold_guardian", Reachable(plan, noElite));

                var roadOnly = plan.Nodes
                    .Where(node => node.NodeId.StartsWith("ruins_camp_", StringComparison.Ordinal))
                    .Select(node => node.NodeId).ToHashSet();
                roadOnly.Add(elite.NodeId);
                roadOnly.Add("shortcut");
                Assert.Contains("stronghold_guardian", Reachable(plan, roadOnly));

                var ruinsOnly = plan.Nodes
                    .Where(node => node.NodeId.StartsWith("old_road_camp_", StringComparison.Ordinal))
                    .Select(node => node.NodeId).ToHashSet();
                ruinsOnly.Add(elite.NodeId);
                Assert.Contains("stronghold_guardian", Reachable(plan, ruinsOnly));
            }
        }

        [Fact]
        public void SeedsVaryTopologyCampCountAndEncounterComposition()
        {
            var topologies = new HashSet<string>();
            var encounterSets = new HashSet<string>();
            var campCounts = new HashSet<int>();

            foreach (int seed in Enumerable.Range(0, 128))
            {
                RegionPlan plan = RegionPlanGenerator.Create(RegionId, seed);
                topologies.Add(string.Join("\n", EdgeDescriptions(plan)));
                encounterSets.Add(string.Join("\n", NodeDescriptions(plan)));
                campCounts.Add(plan.Nodes.Count(node => node.Kind == "camp"));
            }

            Assert.True(topologies.Count >= 4);
            Assert.True(encounterSets.Count >= 16);
            Assert.True(campCounts.SetEquals(new[] { 3, 4, 5 }));
        }

        [Fact]
        public void GeneratedArraysAreNotSharedAcrossCalls()
        {
            RegionPlan altered = RegionPlanGenerator.Create(RegionId, 0);
            RegionPlan original = RegionPlanGenerator.Create(RegionId, 0);
            altered.Nodes[0] = new RegionNode("changed", "changed", "changed");
            altered.Edges[0] = new RegionEdge("changed", "changed");
            RegionPlan next = RegionPlanGenerator.Create(RegionId, 0);

            Assert.Equal(original.Nodes, next.Nodes);
            Assert.Equal(original.Edges, next.Edges);
            Assert.NotSame(original.Nodes, next.Nodes);
            Assert.NotSame(original.Edges, next.Edges);
        }

        [Fact]
        public void CurrentCultureDoesNotChangeThePlan()
        {
            CultureInfo previous = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
                RegionPlan invariant = RegionPlanGenerator.Create(RegionId, int.MinValue);
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR");
                RegionPlan localized = RegionPlanGenerator.Create(RegionId, int.MinValue);

                Assert.Equal(invariant.Nodes, localized.Nodes);
                Assert.Equal(invariant.Edges, localized.Edges);
            }
            finally
            {
                CultureInfo.CurrentCulture = previous;
            }
        }

        [Fact]
        public void EmptyRegionIdentityIsRejected()
        {
            ArgumentException error = Assert.Throws<ArgumentException>(() =>
                RegionPlanGenerator.Create(Guid.Empty, 0));
            Assert.Equal("regionId", error.ParamName);
        }

        private static IEnumerable<int> Seeds()
        {
            yield return int.MinValue;
            yield return int.MaxValue;
            for (int seed = -512; seed <= 512; seed++)
            {
                yield return seed;
            }
        }

        private static string[] NodeDescriptions(RegionPlan plan) => plan.Nodes.Select(node =>
            node.NodeId + "|" + node.Kind + "|" + node.EncounterTemplateId).ToArray();

        private static string[] EdgeDescriptions(RegionPlan plan) => plan.Edges.Select(edge =>
            edge.FromNodeId + "|" + edge.ToNodeId).ToArray();

        private static HashSet<string> Reachable(RegionPlan plan, HashSet<string>? excluded = null)
        {
            var reached = new HashSet<string>();
            var pending = new Queue<string>();
            pending.Enqueue("refuge");

            while (pending.TryDequeue(out string? current))
            {
                if (excluded?.Contains(current) == true || !reached.Add(current))
                {
                    continue;
                }

                foreach (RegionEdge edge in plan.Edges)
                {
                    if (edge.FromNodeId == current)
                    {
                        pending.Enqueue(edge.ToNodeId);
                    }
                    else if (edge.ToNodeId == current)
                    {
                        pending.Enqueue(edge.FromNodeId);
                    }
                }
            }

            return reached;
        }
    }
}
