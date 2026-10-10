using ClassicUO.Game;
using ClassicUO.Game.Managers;
using ClassicUO.Game.UI.Gumps;
using Xunit;

namespace ClassicUO.UnitTests.Game.Managers
{
    public class TargetManagerTypedTargetTest
    {
        [Fact]
        public void RecordTypedTarget_KeepsHarmfulAndBeneficialTargetsSeparately()
        {
            var world = new World();

            world.TargetManager.RecordTypedTarget(1, TargetType.Harmful);
            world.TargetManager.RecordTypedTarget(2, TargetType.Beneficial);

            Assert.Equal(1u, world.TargetManager.LastHarmfulTarget);
            Assert.Equal(2u, world.TargetManager.LastBeneficialTarget);

            world.Clear();
        }

        [Fact]
        public void ClearTypedTarget_OnlyClearsMatchingTargets()
        {
            var world = new World();

            world.TargetManager.RecordTypedTarget(1, TargetType.Harmful);
            world.TargetManager.RecordTypedTarget(2, TargetType.Beneficial);

            world.TargetManager.ClearTypedTarget(1);

            Assert.Equal(0u, world.TargetManager.LastHarmfulTarget);
            Assert.Equal(2u, world.TargetManager.LastBeneficialTarget);

            world.Clear();
        }

        [Fact]
        public void Grabber_RestoresHarmfulAndBeneficialTargetsTogether()
        {
            var world = new World();
            HealthbarGrabberGump grabber = null;

            try
            {
                world.GetOrCreateMobile(1);
                world.GetOrCreateMobile(2);

                world.TargetManager.LastAttack = 1;
                HealthbarGrabberGump.OnTargetSelected(world, 2, TargetType.Beneficial);

                grabber = new HealthbarGrabberGump(world);

                Assert.Equal(1u, world.TargetManager.LastHarmfulTarget);
                Assert.Equal(2u, world.TargetManager.LastBeneficialTarget);
                Assert.Equal(1u, grabber.HarmfulSerial);
                Assert.Equal(2u, grabber.BeneficialSerial);
            }
            finally
            {
                grabber?.Dispose();
                world.Mobiles.Clear();
                world.Clear();
            }
        }

        [Fact]
        public void ChangingLastTarget_DoesNotPopulateGrabberTargets()
        {
            var world = new World();

            try
            {
                world.GetOrCreateMobile(1);

                world.TargetManager.LastTargetInfo.SetEntity(1);

                Assert.Equal(0u, world.TargetManager.LastHarmfulTarget);
                Assert.Equal(0u, world.TargetManager.LastBeneficialTarget);
            }
            finally
            {
                world.Mobiles.Clear();
                world.Clear();
            }
        }
    }
}
