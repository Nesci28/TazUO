using ClassicUO.Game;
using ClassicUO.Game.Data;
using FluentAssertions;
using Xunit;

namespace ClassicUO.UnitTests.Game;

public class AutoFollowMovementTests
{
    [Theory]
    [InlineData(10, 10, Direction.North, 11, 10, Direction.West)]
    [InlineData(10, 10, Direction.North, 9, 10, Direction.East)]
    [InlineData(10, 10, Direction.North, 11, 11, Direction.Up)]
    [InlineData(10, 10, Direction.North, 9, 9, Direction.Down)]
    public void GetRetreatDirection_MovesAwayFromTarget(
        int followerX,
        int followerY,
        Direction followerDirection,
        int targetX,
        int targetY,
        Direction expected
    )
    {
        AutoFollowMovement.GetRetreatDirection(
                followerX,
                followerY,
                followerDirection,
                targetX,
                targetY
            )
            .Should()
            .Be(expected);
    }

    [Fact]
    public void GetRetreatDirection_UsesFacingWhenTargetOccupiesSameTile()
    {
        AutoFollowMovement.GetRetreatDirection(10, 10, Direction.Right, 10, 10)
            .Should()
            .Be(Direction.Left);
    }
}
