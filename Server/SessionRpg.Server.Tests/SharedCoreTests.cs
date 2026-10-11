using SessionRpg.Core;
using Xunit;

namespace SessionRpg.Server.Tests
{
    public sealed class SharedCoreTests
    {
        [Fact]
        public void ExistingCoreRunsStandaloneAtThirtyHertzWithoutUnity()
        {
            CombatSimulation simulation = new(CombatRules.LevelingDefault);
            Assert.Equal(30, simulation.Rules.TickRate);
            for (int tick = 0; tick < 30; tick++) { simulation.Tick(); }
            Assert.Equal(30, simulation.CurrentTick);
            Assert.Equal("SessionRpg.Core", typeof(CombatSimulation).Assembly.GetName().Name);
            Assert.DoesNotContain(typeof(CombatSimulation).Assembly.GetReferencedAssemblies(),
                reference => reference.Name!.StartsWith("Unity", StringComparison.Ordinal));
        }
    }
}
