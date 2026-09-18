// SPDX-License-Identifier: BSD-2-Clause

using ClassicUO.Game.GameObjects;
using ClassicUO.Input;
using Microsoft.Xna.Framework;

namespace ClassicUO.Game.Scenes;

public partial class GameScene
{
#if TAZUO_IOS
    private bool _mobileSelectionCandidate;
    private Point _mobileSelectionStart;

    internal bool CanPinchWorld => _world.InGame && _world.CustomHouseManager == null
        && !Client.Game.UO.GameCursor.ItemHold.Enabled;

    internal void MoveMobilePointer()
    {
        if (_mobileSelectionCandidate && !_world.TargetManager.IsTargeting
            && !Client.Game.UO.GameCursor.ItemHold.Enabled)
        {
            _selectionStart = _mobileSelectionStart;
            if (!_isSelectionActive)
                Utility.Logging.Log.Debug("iOS touch: DRAG_SELECT_BEGIN");
            _isSelectionActive = true;
        }
    }

    internal void CancelMobilePointer()
    {
        _mobileSelectionCandidate = _isSelectionActive = _isMouseLeftDown = false;
        _holdMouse2secOverItemTime = 0;
        SelectedObject.LastLeftDownObject = null;
    }
#endif

    private bool BeginMobileDragSelection()
    {
#if TAZUO_IOS
        if (Client.Game.IsTouchPointer)
        {
            _mobileSelectionStart = Mouse.Position;
            _mobileSelectionCandidate = !_world.TargetManager.IsTargeting
                && !Client.Game.UO.GameCursor.ItemHold.Enabled
                && CanDragSelectOnObject(SelectedObject.Object as GameObject);
            _isSelectionActive = false;
            _isMouseLeftDown = !_mobileSelectionCandidate;
            _holdMouse2secOverItemTime = _isMouseLeftDown ? Time.Ticks : 0;
            return true;
        }
#endif
        return false;
    }
}
