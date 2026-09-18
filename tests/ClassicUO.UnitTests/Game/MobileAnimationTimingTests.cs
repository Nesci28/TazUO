using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using ClassicUO.Assets;
using ClassicUO.Game;
using ClassicUO.Game.Data;
using ClassicUO.Game.GameObjects;
using ClassicUO.Renderer;
using ClassicUO.Renderer.Animations;
using Xunit;

namespace ClassicUO.UnitTests.Game;

[Collection(WorldStateTestCollection.Name)]
public sealed class MobileAnimationTimingTests : IDisposable
{
    private readonly GameController _previousGame = Client.Game;
    private readonly uint _previousTicks = Time.Ticks;
    private readonly Mobile _mobile;

    public MobileAnimationTimingTests()
    {
        // Supply cached animation frames without opening an SDL window or GPU.
        var animations = new Animations(null, null);
        var indices = (Array)typeof(Animations)
            .GetField("_dataIndex", BindingFlags.NonPublic | BindingFlags.Instance)
            .GetValue(animations);
        var index = Activator.CreateInstance(indices.GetType().GetElementType(), true);
        var group = new AnimationGroup();
        for (int direction = 0; direction < group.Direction.Length; direction++)
        {
            group.Direction[direction].FrameCount = 5;
            group.Direction[direction].SpriteInfos = new SpriteInfo[5];
        }
        index.GetType().GetField("Groups").SetValue(index, new[] { group });
        index.GetType().GetField("Type").SetValue(index, AnimationGroupsType.Animal);
        index.GetType().GetField("FileIndex").SetValue(index, 1);
        indices.SetValue(index, 0);

        var game = (GameController)RuntimeHelpers.GetUninitializedObject(typeof(GameController));
        var uo = new UltimaOnline();
        typeof(UltimaOnline).GetProperty("Animations").SetValue(uo, animations);
        typeof(GameController).GetField("<UO>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance)
            .SetValue(game, uo);
        typeof(GameController).GetField("FrameDelay").SetValue(game, new uint[2]);
        typeof(Client).GetProperty("Game").SetValue(null, game);

        Time.Ticks = 1000;
        var world = (World)RuntimeHelpers.GetUninitializedObject(typeof(World));
        var player = (PlayerMobile)RuntimeHelpers.GetUninitializedObject(typeof(PlayerMobile));
        player.Serial = 2;
        typeof(World).GetField("<Player>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance)
            .SetValue(world, player);
        _mobile = new Mobile(world, 1) { X = 100, Y = 100, LastStepTime = Time.Ticks };
    }

    [Fact]
    public void CompletingTheLastStepStillAdvancesAndLoopsTheAnimation()
    {
        for (int step = 1; step <= 7; step++)
        {
            QueueStep();
            Time.Ticks += 101;
            _mobile.ProcessAnimation(true);

            Assert.Equal(100 + step, _mobile.X);
            Assert.Equal(0, _mobile.Steps.Count);
            Assert.Equal(step % 5, _mobile.AnimIndex);
        }
    }

    [Fact]
    public void StoppingStillHoldsTheLastWalkingFrame()
    {
        _mobile.AnimIndex = 2;
        Time.Ticks += 101;
        _mobile.ProcessAnimation(true);

        Assert.Equal(2, _mobile.AnimIndex);
    }

    [Fact]
    public void DisabledAnimationsStayFrozenWhenAStepCompletes()
    {
        _mobile.ExecuteAnimation = false;
        _mobile.AnimIndex = 2;
        QueueStep();
        Time.Ticks += 101;
        _mobile.ProcessAnimation(true);

        Assert.Equal(101, _mobile.X);
        Assert.Equal(0, _mobile.Steps.Count);
        Assert.Equal(2, _mobile.AnimIndex);
    }

    private void QueueStep() => _mobile.Steps.AddToBack(new Mobile.Step
    {
        X = _mobile.X + 1,
        Y = _mobile.Y,
        Direction = (byte)Direction.East,
        TimeDiff = 100
    });

    public void Dispose()
    {
        typeof(Client).GetProperty("Game").SetValue(null, _previousGame);
        Time.Ticks = _previousTicks;
    }
}
