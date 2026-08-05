using ClassicUO.Game;
using ClassicUO.Game.Data;
using ClassicUO.Game.GameObjects;
using ClassicUO.Game.Managers;
using Xunit;

namespace ClassicUO.UnitTests.Game.Managers
{
    public class AutoLootCorpseResolutionTest
    {
        [Fact]
        public void GetContainingCorpse_ResolvesStandardCorpseHierarchy()
        {
            Client.UnitTestingActive = true;
            var world = new World();

            try
            {
                Item corpse = AddItem(world, 0x40000001, 0x2006);
                Item container = AddItem(world, 0x40000002, container: corpse.Serial);
                Item loot = AddItem(world, 0x40000003, container: container.Serial);

                Assert.Same(corpse, AutoLootManager.GetContainingCorpse(world, loot));
            }
            finally
            {
                ClearWorld(world);
            }
        }

        [Fact]
        public void GetContainingCorpse_ResolvesOsiDeadMobileHierarchy()
        {
            Client.UnitTestingActive = true;
            var world = new World();

            try
            {
                const uint corpseObjectSerial = 0x80000001;
                Item corpse = AddItem(world, 0x40000001, 0x2006);
                Item container = AddItem(world, 0x40000002, container: corpseObjectSerial);
                Item loot = AddItem(world, 0x40000003, container: container.Serial);

                corpse.CorpseParent = corpseObjectSerial;
                world.CorpseManager.Add(corpse.Serial, corpseObjectSerial, Direction.South, false);

                Assert.Same(corpse, AutoLootManager.GetContainingCorpse(world, loot));
            }
            finally
            {
                ClearWorld(world);
            }
        }

        [Fact]
        public void GetContainingCorpse_ReturnsNullForRegularContainerHierarchy()
        {
            Client.UnitTestingActive = true;
            var world = new World();

            try
            {
                Item container = AddItem(world, 0x40000001);
                Item item = AddItem(world, 0x40000002, container: container.Serial);

                Assert.Null(AutoLootManager.GetContainingCorpse(world, item));
            }
            finally
            {
                ClearWorld(world);
            }
        }

        private static Item AddItem(World world, uint serial, ushort graphic = 0, uint container = uint.MaxValue)
        {
            var item = new Item(world)
            {
                Serial = serial,
                Graphic = graphic,
                Container = container
            };

            world.Items.Add(serial, item);
            return item;
        }

        private static void ClearWorld(World world)
        {
            world.Items.Clear();
            world.Clear();
        }
    }
}
