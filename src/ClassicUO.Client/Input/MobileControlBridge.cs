using System.Threading;
using ClassicUO.Game.Data;

namespace ClassicUO.Input;

/// <summary>Thread-safe state shared by the native iOS touch overlay and the game scene.</summary>
public static class MobileControlBridge
{
    private static int _direction = (int)Direction.NONE;
    private static int _textInputFocused;
    private static int _softwareKeyboardVisible;
    private static int _dismissKeyboardRequested;
    private static int _openKeyboardRequested;

    /// <summary>The direction currently held by the mobile joystick.</summary>
    public static Direction Direction => (Direction)Volatile.Read(ref _direction);

    /// <summary>Sets the held direction; <see cref="Direction.NONE"/> releases the joystick.</summary>
    public static void SetDirection(Direction direction) => Volatile.Write(ref _direction, (int)direction);

    /// <summary>Whether an editable TazUO control explicitly owns text focus.</summary>
    public static bool TextInputFocused => Volatile.Read(ref _textInputFocused) != 0;

    public static void SetTextInputFocused(bool focused) => Volatile.Write(ref _textInputFocused, focused ? 1 : 0);

    /// <summary>Whether UIKit currently presents the on-screen keyboard.</summary>
    public static bool SoftwareKeyboardVisible => Volatile.Read(ref _softwareKeyboardVisible) != 0;

    public static void SetSoftwareKeyboardVisible(bool visible)
        => Volatile.Write(ref _softwareKeyboardVisible, visible ? 1 : 0);

    /// <summary>Requests dismissal from the native keyboard accessory button.</summary>
    public static void RequestKeyboardDismiss() => Volatile.Write(ref _dismissKeyboardRequested, 1);

    internal static bool ConsumeKeyboardDismissRequest()
        => Interlocked.Exchange(ref _dismissKeyboardRequested, 0) != 0;

    /// <summary>Requests focus on the mobile chat input and opens the software keyboard.</summary>
    public static void RequestKeyboardOpen() => Volatile.Write(ref _openKeyboardRequested, 1);

    internal static bool ConsumeKeyboardOpenRequest()
        => Interlocked.Exchange(ref _openKeyboardRequested, 0) != 0;

#if TAZUO_IOS
    private static Microsoft.Xna.Framework.Vector4 _safeInsets;
    private static float _keyboardBottomInset;

    /// <summary>UIKit safe-area insets as fractions of the current window, on the UI thread.</summary>
    public static void SetSafeArea(float left, float top, float right, float bottom)
        => _safeInsets = new(left, top, right, bottom);

    /// <summary>Bottom inset occupied by the software keyboard as a fraction of the game window.</summary>
    public static void SetKeyboardBottomInset(float bottom)
        => _keyboardBottomInset = System.Math.Clamp(bottom, 0f, 1f);

    internal static Microsoft.Xna.Framework.Rectangle GetSafeUIBounds()
    {
        // Match Mouse.FinalizePosition: UIKit's window is presented from the
        // back buffer, which can have a different aspect ratio on iOS. Insets
        // must use that buffer's UI coordinates, with RenderScale removed.
        int width = System.Math.Max(1, (int)(Client.Game.LogicalBackBufferWidth / Client.Game.RenderScale));
        int height = System.Math.Max(1, (int)(Client.Game.LogicalBackBufferHeight / Client.Game.RenderScale));
        int left = (int)System.Math.Ceiling(width * _safeInsets.X);
        int top = (int)System.Math.Ceiling(height * _safeInsets.Y);
        int right = (int)System.Math.Ceiling(width * _safeInsets.Z);
        int bottom = (int)System.Math.Ceiling(height * System.Math.Clamp(
            System.Math.Max(_safeInsets.W, _keyboardBottomInset), 0f, 1f));
        return new(left, top, System.Math.Max(1, width - left - right), System.Math.Max(1, height - top - bottom));
    }

#if DEBUG
    public static string DescribeAnimation()
    {
        var game = Client.Game;
        var player = game?.UO.World.Player;
        if (player == null) return "WAITING";
        var animations = game.UO.Animations;
        byte direction = (byte)player.GetDirectionForAnimation();
        bool mirror = false;
        animations.GetAnimDirection(ref direction, ref mirror);
        ushort body = player.GetGraphicForAnimation();
        byte group = Game.GameObjects.Mobile.GetGroupForAnimation(player, body, true);
        int count = animations.GetAnimationFrames(body, group, direction, out _, out _).Length;
        string mount = "none";
        if (player.Mount != null)
        {
            ushort graphic = player.Mount.GetGraphicForAnimation();
            byte action = Game.GameObjects.Mobile.GetGroupForAnimation(player, graphic);
            int frames = animations.GetAnimationFrames(graphic, action, direction, out _, out _).Length;
            mount = $"{graphic}/{action}/{frames}";
        }
        return $"index={player.AnimIndex} body={body}/{group}/{count} mount={mount} " +
            $"steps={player.Steps.Count} walking={player.IsWalking} dt={Time.Delta:F3} " +
            $"window={game.Window.ClientBounds} buffer={game.LogicalBackBufferWidth}x{game.LogicalBackBufferHeight}";
    }
#endif

    /// <summary>Reports live world coordinates and unacknowledged walking requests for native smoke tests.</summary>
    public static string DescribeWorld()
    {
        var world = Client.Game?.UO.World;
        if (world?.InGame != true || world.Player == null)
            return "WAITING";

        var player = world.Player;
        return $"READY player={player.Serial:X8} position={player.X},{player.Y},{player.Z} serverPosition={world.RangeSize.X},{world.RangeSize.Y} pendingMoves={player.Walker.UnacceptedPacketsCount}";
    }
#endif
}
