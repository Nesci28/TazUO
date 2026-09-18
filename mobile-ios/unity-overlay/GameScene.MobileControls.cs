using ClassicUO.Game;
using ClassicUO.Game.Data;
using ClassicUO.Game.Managers;
using ClassicUO.Game.UI.Controls;
using Microsoft.Xna.Framework;

namespace ClassicUO.Game.Scenes
{
    // Compiled into the same Unity assembly as ClassicUO, this partial keeps
    // the mobile buttons on the real game/input path instead of synthesizing
    // packets in a separate transport.
    internal partial class GameScene
    {
        public Vector2 MobileButtonInput { get; private set; }

        public void SetMobileDirection(Direction direction, bool pressed)
        {
            var vector = DirectionToVector(direction);
            if (pressed) MobileButtonInput = vector;
            else if (MobileButtonInput == vector) MobileButtonInput = Vector2.Zero;
        }

        public void ReleaseMobileButtons() => MobileButtonInput = Vector2.Zero;

        public void BeginMobileTarget()
        {
            if (!TargetManager.IsTargeting)
                TargetManager.SetTargeting(CursorTarget.SetTargetClientSide, CursorType.Target, TargetType.Neutral);
            else
                TargetManager.CancelTarget();
        }

        public void SendMobileChat(string message)
        {
            if (!string.IsNullOrWhiteSpace(message))
                GameActions.Say(message);
        }

        public void ShowMobileContext()
        {
            var menu = new ContextMenuControl();
            menu.Add("Paperdoll", () => GameActions.OpenPaperdoll(World.Player.Serial));
            menu.Add("War mode", () => GameActions.ChangeWarMode());
            menu.Add("Cancel target", () => TargetManager.CancelTarget());
            menu.Show();
        }

        private static Vector2 DirectionToVector(Direction direction)
        {
            return direction switch
            {
                Direction.North => new Vector2(0, -1),
                Direction.South => new Vector2(0, 1),
                Direction.East => new Vector2(1, 0),
                Direction.West => new Vector2(-1, 0),
                _ => Vector2.Zero
            };
        }
    }
}
