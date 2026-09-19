// SPDX-License-Identifier: BSD-2-Clause

using ClassicUO.Game.Data;

namespace ClassicUO.Game;

internal static class AutoFollowMovement
{
    public static Direction GetRetreatDirection(
        int followerX,
        int followerY,
        Direction followerDirection,
        int targetX,
        int targetY
    )
    {
        Direction towardTarget = DirectionHelper.CalculateDirection(followerX, followerY, targetX, targetY);

        if (towardTarget == Direction.NONE)
            towardTarget = followerDirection & Direction.Mask;

        return DirectionHelper.Reverse(towardTarget);
    }
}
