using ClassicUO.Game;
using ClassicUO.Game.Data;
using ClassicUO.Game.GameObjects;
using ClassicUO.Game.Managers;
using ClassicUO.Game.UI.Gumps.GridHighLight;
using Xunit;

namespace ClassicUO.UnitTests.Game.Managers
{
    [Collection(WorldStateTestCollection.Name)]
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
        public void GetContainingCorpse_ResolvesDirectOsiDeadMobileParent()
        {
            Client.UnitTestingActive = true;
            var world = new World();

            try
            {
                const uint corpseObjectSerial = 0x80000001;
                Item corpse = AddItem(world, 0x40000001, 0x2006);
                Item loot = AddItem(world, 0x40000002, container: corpseObjectSerial);

                corpse.CorpseParent = corpseObjectSerial;
                world.CorpseManager.Add(corpse.Serial, corpseObjectSerial, Direction.South, false);

                Assert.True(loot.OnGround);
                Assert.Same(corpse, AutoLootManager.GetContainingCorpse(world, loot));
            }
            finally
            {
                ClearWorld(world);
            }
        }

        [Fact]
        public void GridHighlightEligibility_AllowsDirectOsiDeadMobileParent()
        {
            Client.UnitTestingActive = true;
            var world = new World();

            try
            {
                const uint corpseObjectSerial = 0x80000001;
                Item corpse = AddItem(world, 0x40000001, 0x2006);
                Item loot = AddItem(world, 0x40000002, container: corpseObjectSerial);

                corpse.CorpseParent = corpseObjectSerial;
                world.CorpseManager.Add(corpse.Serial, corpseObjectSerial, Direction.South, false);

                Assert.True(loot.OnGround);
                Assert.True(GridHighlightData.IsEligibleItem(world, loot));
            }
            finally
            {
                ClearWorld(world);
            }
        }

        [Fact]
        public void GridHighlightEligibility_RejectsActualGroundItem()
        {
            Client.UnitTestingActive = true;
            var world = new World();

            try
            {
                Item item = AddItem(world, 0x40000001);

                Assert.True(item.OnGround);
                Assert.False(GridHighlightData.IsEligibleItem(world, item));
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
