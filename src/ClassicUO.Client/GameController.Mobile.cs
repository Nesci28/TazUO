// SPDX-License-Identifier: BSD-2-Clause

using ClassicUO.Game.UI;
using ClassicUO.Game.UI.Controls;
using ClassicUO.Game.Scenes;
using ClassicUO.Game.Managers;
using ClassicUO.Input;
using Microsoft.Xna.Framework;
using SDL3;
using static SDL3.SDL;
using System;
using System.Collections.Generic;

namespace ClassicUO;

/// <summary>
/// Platform hooks kept separate from the main controller so upstream input changes remain easy
/// to rebase. The non-iOS implementation intentionally preserves the desktop SDL behavior.
/// </summary>
internal unsafe partial class GameController
{
#if TAZUO_IOS
    private bool _iosTextInputActive;
    private bool _myraBackspaceKeyEventDispatched;
    private readonly MobileSdlEventQueue _mobileEvents = new();
    private SDL_EventFilter _mobileEventHandler;
    private readonly MobileTouchGestures _touchGestures = new();
    private readonly Queue<SDL_TouchFingerEvent> _touchEvents = new();
    private bool _touchSelectionPrepared, _dispatchingTouchPointer;
    internal bool IsTouchPointer { get; private set; }
    private Vector2 _touchPointerPosition, _previousTouchDown;
    private GameScene _pinchScene;
    private float _pinchStartZoom;
#endif

    private SDL_EventFilter GetPlatformEventFilter(SDL_EventFilter handler)
    {
#if TAZUO_IOS
        // GCKeyboard invokes SDL's filter on a background queue. Editing a field
        // there can wait for OpenGL while the render thread waits for SDL's lock.
        _mobileEventHandler = handler;
        return _mobileEvents.Enqueue;
#else
        return handler;
#endif
    }

    private void ProcessMobileEvents()
    {
#if TAZUO_IOS
        _mobileEvents.Drain(_mobileEventHandler);
        bool selectionReady = _touchSelectionPrepared;
        _touchSelectionPrepared = false;
        while (_touchEvents.TryPeek(out var touch))
        {
            Vector2 point = new(touch.x * Window.ClientBounds.Width, touch.y * Window.ClientBounds.Height);
            if (touch.type == SDL_EventType.SDL_EVENT_FINGER_DOWN && _touchGestures.FingerCount == 0)
            {
                SetMobilePointer(point);
                if (!selectionReady && Scene is GameScene scene && UIManager.IsMouseOverWorld)
                {
                    // A finger has no hover. Let the normal world selection pass
                    // resolve the object here before pressing or dragging it.
                    scene.Camera.Update(false, Time.Delta, Mouse.Position);
                    _touchSelectionPrepared = true;
                    break;
                }
            }
            selectionReady = false;
            _touchEvents.Dequeue();
            switch (touch.type)
            {
                case SDL_EventType.SDL_EVENT_FINGER_DOWN:
                    bool allowPinch = false;
                    if (Scene is GameScene gameScene && gameScene.CanPinchWorld)
                    {
                        SetMobilePointer(point);
                        allowPinch = UIManager.IsMouseOverWorld;
                        if (_touchGestures.FingerCount > 0) SetMobilePointer(_touchPointerPosition);
                    }
                    _touchGestures.Down(touch.touchID, touch.fingerID, point, allowPinch);
                    break;
                case SDL_EventType.SDL_EVENT_FINGER_MOTION:
                    _touchGestures.Move(touch.touchID, touch.fingerID, point);
                    break;
                default:
                    _touchGestures.Up(touch.touchID, touch.fingerID, point,
                        touch.type == SDL_EventType.SDL_EVENT_FINGER_CANCELED);
                    break;
            }
        }
#endif
    }

    private void QueueMobileTouch(SDL_TouchFingerEvent touch)
    {
#if TAZUO_IOS
        _touchEvents.Enqueue(touch);
#endif
    }

    private void FinishMouseButtonUp()
    {
#if TAZUO_IOS
        if (_dispatchingTouchPointer) return;
#endif
        Mouse.Update(resyncPosition: true);
    }

#if TAZUO_IOS
    private void SetMobilePointer(Vector2 point)
    {
        Mouse.MouseInWindow = true;
        Mouse.SetPositionFromEvent(point.X, point.Y);
        UIManager.HandleMouseInput();
    }

    private void DispatchTouchButton(Vector2 point, bool down)
    {
        SDL_Event e = new();
        e.button.type = down ? SDL_EventType.SDL_EVENT_MOUSE_BUTTON_DOWN : SDL_EventType.SDL_EVENT_MOUSE_BUTTON_UP;
        e.button.button = (byte)MouseButtonType.Left;
        e.button.down = down;
        e.button.x = point.X;
        e.button.y = point.Y;
        _dispatchingTouchPointer = true;
        try { HandleSdlEvent(IntPtr.Zero, &e); }
        finally { _dispatchingTouchPointer = false; }
    }

    private void BeginTouchPointer(Vector2 point)
    {
        IsTouchPointer = true;
        _touchPointerPosition = point;
        if (Vector2.DistanceSquared(_previousTouchDown, point) > 24 * 24)
            Mouse.LastLeftButtonClickTime = 0;
        _previousTouchDown = point;
        DispatchTouchButton(point, true);
    }

    private void MoveTouchPointer(Vector2 point)
    {
        _touchPointerPosition = point;
        SetMobilePointer(point);
        Mouse.CancelDoubleClick = true;
        Mouse.LastLeftButtonClickTime = 0;
        if (Scene is GameScene gameScene) gameScene.MoveMobilePointer();
        // Dispatch before a release in the same SDL batch so a quick drag works too.
        _pendingMouseMotion = false;
        if (Scene != null && !Scene.OnMouseDragging()) UIManager.OnMouseDragging();
    }

    private void EndTouchPointer(Vector2 point)
    {
        DispatchTouchButton(point, false);
        IsTouchPointer = false;
        if (Scene is GameScene gameScene) gameScene.CancelMobilePointer();
    }

    private void CancelTouchPointer()
    {
        if (Scene is GameScene gameScene) gameScene.CancelMobilePointer();
        UIManager.CancelMobilePointer();
        Mouse.ButtonRelease(MouseButtonType.Left);
        Mouse.LastLeftButtonClickTime = 0;
        Mouse.CancelDoubleClick = true;
        _pendingMouseMotion = false;
        IsTouchPointer = false;
    }
#endif

    private void PrepareMouseButtonDown(SDL_MouseButtonEvent mouse, MouseButtonType button)
    {
#if TAZUO_IOS
        if (button == MouseButtonType.Right && IsTouchPointer)
            _touchGestures.Reset(); // UIKit long press supersedes the pending left click.
        // Touch has no preceding hover. Resolve this event's position before
        // the scene decides whether the press belongs to the world or a gump.
        // Polling SDL here can read a later event from the deferred input queue.
        RefreshMobilePointer(mouse);
        Mouse.ButtonPress(button);
#else
        Mouse.ButtonPress(button);
        Mouse.Update(resyncPosition: true);
#endif
    }

    private void RefreshMobilePointer(SDL_MouseButtonEvent mouse)
    {
#if TAZUO_IOS
        Mouse.SetPositionFromEvent(mouse.x, mouse.y);
        UIManager.HandleMouseInput();
#endif
    }

    private void InitializeMobileInput()
    {
#if TAZUO_IOS
        SDL.SDL_SetHint(SDL_HINT_ENABLE_SCREEN_KEYBOARD, "1");
        SDL.SDL_SetHint(SDL_HINT_RETURN_KEY_HIDES_IME, "1");
        SDL.SDL_SetHint(SDL_HINT_TOUCH_MOUSE_EVENTS, "0");
        SDL.SDL_SetHint(SDL_HINT_MOUSE_TOUCH_EVENTS, "0");
        _touchGestures.PointerDown += BeginTouchPointer;
        _touchGestures.PointerMove += MoveTouchPointer;
        _touchGestures.PointerUp += EndTouchPointer;
        _touchGestures.PointerCancel += CancelTouchPointer;
        _touchGestures.PinchStarted += () =>
        {
            _pinchScene = Scene as GameScene;
            _pinchStartZoom = _pinchScene?.Camera.Zoom ?? 1f;
            Utility.Logging.Log.Debug("iOS touch: PINCH_BEGIN");
        };
        _touchGestures.PinchScale += scale =>
        {
            if (_pinchScene != null && Scene == _pinchScene && _pinchScene.CanPinchWorld)
                _pinchScene.Camera.Zoom = _pinchStartZoom / scale;
        };
#else
        SDL.SDL_SetHint(SDL_HINT_ENABLE_SCREEN_KEYBOARD, "0");
        SDL.SDL_StartTextInput(Window.Handle);
#endif
    }

    private void UpdateMobileInput()
    {
#if TAZUO_IOS
        bool keyboardOpenRequested = MobileControlBridge.ConsumeKeyboardOpenRequest();
        if (keyboardOpenRequested)
        {
            UIManager.OpenMobileTextInput();
            // SDL can retain its active flag after UIKit dismissed the hidden
            // UITextField. Reset that state so the next branch calls the native
            // startTextInput implementation again and presents the keyboard.
            if (_iosTextInputActive)
            {
                SDL.SDL_StopTextInput(Window.Handle);
                _iosTextInputActive = false;
            }
            Utility.Logging.Log.Debug("iOS keyboard: OPEN_BUTTON");
        }
        if (MobileControlBridge.ConsumeKeyboardDismissRequest())
            UIManager.DismissMobileTextInput();
        bool wantsTextInput = UIManager.MobileTextInputRequested;
        MobileControlBridge.SetTextInputFocused(wantsTextInput);
        if (wantsTextInput && !_iosTextInputActive)
        {
            _iosTextInputActive = SDL.SDL_StartTextInput(Window.Handle);
            Utility.Logging.Log.Debug($"iOS keyboard: SHOW field={UIManager.KeyboardFocusControl?.GetType().Name} started={_iosTextInputActive}");
        }
        else if (!wantsTextInput && _iosTextInputActive)
        {
            SDL.SDL_StopTextInput(Window.Handle);
            _iosTextInputActive = false;
            MobileControlBridge.SetSoftwareKeyboardVisible(false);
            MobileControlBridge.SetKeyboardBottomInset(0f);
            Utility.Logging.Log.Debug("iOS keyboard: HIDE");
        }
#endif
    }

    private void ConfigureMobileRenderTarget(ref int width, ref int height)
    {
#if TAZUO_IOS
        if (Scene is LoginScene)
        {
            width = ToPhysicalPixels(bufferRect.Width);
            height = ToPhysicalPixels(bufferRect.Height);
        }
#endif
    }

    private bool ConfigureMobilePresentation(ref Rectangle destRect)
    {
#if TAZUO_IOS
        if (Scene is LoginScene)
        {
            float fit = Math.Min(
                GraphicsDevice.Viewport.Width / (float)destRect.Width,
                GraphicsDevice.Viewport.Height / (float)destRect.Height);
            int fitWidth = Math.Max(1, (int)Math.Round(destRect.Width * fit));
            int fitHeight = Math.Max(1, (int)Math.Round(destRect.Height * fit));
            destRect = new Rectangle(
                GraphicsDevice.Viewport.X + (GraphicsDevice.Viewport.Width - fitWidth) / 2,
                GraphicsDevice.Viewport.Y + (GraphicsDevice.Viewport.Height - fitHeight) / 2,
                fitWidth,
                fitHeight);
            return true;
        }
#endif
        return false;
    }

    private bool IsMobileMyraBackspace(SDL_Keycode key)
    {
#if TAZUO_IOS
        return key == SDL_Keycode.SDLK_BACKSPACE
            && UIManager.KeyboardFocusControl is MyraControl { HasFocusedTextInput: true };
#else
        return false;
#endif
    }

    private void RecordMobileBackspace(bool dispatched)
    {
#if TAZUO_IOS
        _myraBackspaceKeyEventDispatched = dispatched;
#endif
    }

    private bool HandleFilteredMobileBackspace(SDL_Keycode key, SDL_Keymod mod, bool isMyraBackspace)
    {
#if TAZUO_IOS
        if (key == SDL_Keycode.SDLK_BACKSPACE)
        {
            UIManager.KeyboardFocusControl?.InvokeKeyDown(key, mod);
            RecordMobileBackspace(isMyraBackspace);
            return true;
        }
#endif
        return false;
    }

    private bool ConsumeMobileMyraBackspace()
    {
#if TAZUO_IOS
        bool handled = _myraBackspaceKeyEventDispatched;
        _myraBackspaceKeyEventDispatched = false;
        return handled;
#else
        return false;
#endif
    }

    private void ResetMobileInputState()
    {
#if TAZUO_IOS
        _myraBackspaceKeyEventDispatched = false;
        _touchGestures.Reset();
        _touchEvents.Clear();
        _touchSelectionPrepared = false;
        _pinchScene = null;
#endif
    }
}
