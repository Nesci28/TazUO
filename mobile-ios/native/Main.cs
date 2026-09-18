#nullable enable
using System;
using System.Threading.Tasks;
using Foundation;
using IronPython.Hosting;
using System.Runtime.InteropServices;
using UIKit;
using CoreGraphics;
using ObjCRuntime;
using ClassicUO.Game.Data;
using ClassicUO.Input;

namespace TazUO.iOS;

internal static class MainClass
{
    public static void Main(string[] args) => UIApplication.Main(args, null, typeof(AppDelegate));
}

[Register("TazUOAppDelegate")]
internal sealed class AppDelegate : UIApplicationDelegate
{
    public override UIWindow? Window { get; set; }

    public override bool FinishedLaunching(UIApplication application, NSDictionary? launchOptions)
    {
        string dataRoot = ClassicUO.Utility.FileSystemHelper.GetWritableDataDirectory();
        System.IO.Directory.CreateDirectory(dataRoot);
        System.IO.Directory.CreateDirectory(System.IO.Path.Combine(dataRoot, "LegionScripts"));
        Environment.CurrentDirectory = dataRoot;
        Window = new UIWindow(UIScreen.MainScreen.Bounds)
        {
            RootViewController = new DiagnosticViewController()
        };
        Window.MakeKeyAndVisible();
        return true;
    }

    public override void OnResignActivation(UIApplication application)
    {
        MobileControlBridge.SetDirection(Direction.NONE);
        // This optional UIApplicationDelegate callback has no base implementation:
        // the generated UIKit binding throws You_Should_Not_Call_base_In_This_Method.
    }
}

/// <summary>
/// Temporary native gate. It proves the linked TazUO assembly and Legion's real
/// IronPython engine run inside an iOS process before the FNA window is started.
/// </summary>
internal sealed class DiagnosticViewController : UIViewController
{
    private UILabel? _status;
    private Microsoft.Xna.Framework.Game? _graphics;
    private CoreAnimation.CADisplayLink? _displayLink;
    private bool _joystickInstalled;
    private bool _longPressInstalled;
    private JoystickOverlayView? _joystick;
    private UIWindow? _gameWindow;
    private DateTime _nextStateLog;
    private string? _lastWorldState;
    private bool _legionSmokeReported;
    private CGRect _keyboardFrame;
    private bool _keyboardFrameValid;
    private NSObject? _keyboardFrameObserver;
    private NSObject? _keyboardHiddenObserver;
    private UIButton? _keyboardDismissButton;
    private KeyboardDismissOverlayWindow? _keyboardDismissOverlay;
    private bool _keyboardVisible;
    private UITapGestureRecognizer? _keyboardDismissTap;
#if DEBUG
    private NSObject? _keyboardShownObserver;
    private DateTime _nextAnimationLog, _animationLogUntil;
#endif

    public override void ViewDidLoad()
    {
        base.ViewDidLoad();
        _keyboardFrameValid = true;
        _keyboardFrameObserver = UIKeyboard.Notifications.ObserveWillChangeFrame(
            (_, args) =>
            {
                _keyboardFrame = args.FrameEnd;
                _keyboardFrameValid = true;
            });
        _keyboardHiddenObserver = UIKeyboard.Notifications.ObserveDidHide(
            (_, _) =>
            {
                _keyboardFrame = CGRect.Empty;
                _keyboardFrameValid = true;
            });
#if DEBUG
        // Verify the actual UIKit keyboard, independently of SDL's requested
        // text-input state. Never log the contents of an input field.
        _keyboardShownObserver = UIKeyboard.Notifications.ObserveDidShow(
            (_, args) =>
            {
                _keyboardFrame = args.FrameEnd;
                _keyboardFrameValid = true;
                GraphicsProbe.Log("UIKit keyboard: VISIBLE\n");
            });
#endif
        View!.BackgroundColor = UIColor.FromRGB(12, 20, 32);
        _status = new UILabel(View.Bounds)
        {
            Text = "TazUO native iOS\nStarting Legion runtime…",
            TextColor = UIColor.White,
            Lines = 0,
            Font = UIFont.SystemFontOfSize(22),
            TextAlignment = UITextAlignment.Center,
            AutoresizingMask = UIViewAutoresizing.FlexibleWidth | UIViewAutoresizing.FlexibleHeight
        };
        View.AddSubview(_status);

        Task.Run(RunLegionGate);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _keyboardFrameObserver?.Dispose();
            _keyboardHiddenObserver?.Dispose();
        }
#if DEBUG
        if (disposing)
        {
            _keyboardShownObserver?.Dispose();
        }
#endif
        base.Dispose(disposing);
    }

    private void RunLegionGate()
    {
        try
        {
            NativeLibrary.SetDllImportResolver(typeof(Mono.Unix.Native.Syscall).Assembly,
                static (name, _, _) => name == "Mono.Unix" ? NativeLibrary.GetMainProgramHandle() : IntPtr.Zero);
            var engine = Python.CreateEngine(new System.Collections.Generic.Dictionary<string, object>
            {
                ["RecursionLimit"] = 100,
                ["ConsoleSupportLevel"] = "None"
            });
            object result = engine.Execute("sum(i * i for i in range(5))");
            var version = typeof(ClassicUO.LegionScripting.LegionAPI).Assembly.GetName().Version;
            System.IO.File.WriteAllText(System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "native-runtime.log"), $"Legion engine: PASS\nPython result: {result}\nClient assembly: {version}\n");
            SetStatus($"TazUO native iOS\nLegion engine: PASS\nPython result: {result}\nClient assembly: {version}");
            UIApplication.SharedApplication.BeginInvokeOnMainThread(StartTazUO);
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
            System.IO.File.WriteAllText(System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "native-runtime.log"), $"Legion engine: FAIL\n{ex}\n");
            SetStatus($"TazUO native iOS\nLegion engine: FAIL\n{ex.GetType().Name}: {ex.Message}");
        }
    }

    private void StartGraphics()
    {
        try
        {
            // First verify the same FNA renderer as TazUO on iOS OpenGL ES.
            // UIKit retains control of the main loop through CADisplayLink.
            Environment.SetEnvironmentVariable("FNA_PLATFORM_BACKEND", "SDL3");
            Environment.SetEnvironmentVariable("FNA3D_FORCE_DRIVER", "OpenGL");
            Environment.SetEnvironmentVariable("FNA3D_OPENGL_FORCE_ES3", "1");
            SDL3.SDL.SDL_SetHint("FNA3D_FORCE_DRIVER", "OpenGL");
            SDL3.SDL.SDL_SetHint("FNA3D_OPENGL_FORCE_ES3", "1");
            Microsoft.Xna.Framework.FNALoggerEXT.LogInfo = text => GraphicsProbe.Log($"FNA: {text}\n");
            Microsoft.Xna.Framework.FNALoggerEXT.LogWarn = text => GraphicsProbe.Log($"FNA warning: {text}\n");
            Microsoft.Xna.Framework.FNALoggerEXT.LogError = text => GraphicsProbe.Log($"FNA error: {text}\n");
            _graphics = new GraphicsProbe();
            _displayLink = CoreAnimation.CADisplayLink.Create(DrawFrame);
            _displayLink.AddToRunLoop(NSRunLoop.Main, NSRunLoopMode.Common);
        }
        catch (Exception ex)
        {
            GraphicsFailed(ex);
        }
    }

    private void StartTazUO()
    {
        try
        {
            GraphicsProbe.Log("Native Bootstrap: START\n");
            var clientLog = new System.IO.StreamWriter(System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "native-client.log"), false)
                { AutoFlush = true };
            Console.SetOut(System.IO.TextWriter.Synchronized(clientLog));
            Console.SetError(Console.Out);
            Microsoft.Xna.Framework.FNALoggerEXT.LogInfo = text => GraphicsProbe.Log($"FNA: {text}\n");
            Microsoft.Xna.Framework.FNALoggerEXT.LogWarn = text => GraphicsProbe.Log($"FNA warning: {text}\n");
            Microsoft.Xna.Framework.FNALoggerEXT.LogError = text => GraphicsProbe.Log($"FNA error: {text}\n");
            // Bootstrap is the same entry point used by the desktop TazUO
            // client. It initializes settings, loads UO files and starts the
            // actual GameController; no proxy or mock scene is involved.
            SDL3.SDL.SDL_SetHint("FNA3D_FORCE_DRIVER", "OpenGL");
            SDL3.SDL.SDL_SetHint("FNA3D_OPENGL_FORCE_ES3", "1");
            string uoPath = Foundation.NSBundle.MainBundle.PathForResource("UO", null)
                ?? Foundation.NSBundle.MainBundle.BundlePath;
            var args = new System.Collections.Generic.List<string>
                { "-uopath", uoPath, "-force_driver", "1" };
            string testArgsPath = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "native-test-args.txt");
            if (System.IO.File.Exists(testArgsPath))
            {
                foreach (string line in System.IO.File.ReadAllLines(testArgsPath))
                {
                    string trimmed = line.Trim();
                    if (trimmed.Length == 0 || trimmed.StartsWith("#", StringComparison.Ordinal))
                        continue;
                    args.Add(trimmed);
                }
                System.IO.File.Delete(testArgsPath);
                GraphicsProbe.Log("Native test connection arguments: LOADED\n");
            }
            ClassicUO.Bootstrap.NativeGameReady = game =>
            {
                _graphics = game;
                _displayLink = CoreAnimation.CADisplayLink.Create(DrawFrame);
                _displayLink.AddToRunLoop(NSRunLoop.Main, NSRunLoopMode.Common);
                SDL3.SDL.SDL_ShowWindow(game.Window.Handle);
                // The temporary diagnostics window must not cover SDL's UIKit
                // window once the real game is ready.
                View.Window!.Hidden = true;
                GraphicsProbe.Log("Native GameController: READY\n");
            };
            ClassicUO.Bootstrap.RunNative(args.ToArray());
        }
        catch (Exception ex)
        {
            GraphicsProbe.Log($"Native Bootstrap: FAIL\n{ex}\n");
            GraphicsFailed(ex);
        }
    }

    private void InstallJoystick(IntPtr sdlWindow)
    {
        try
        {
            uint properties = SDL3.SDL.SDL_GetWindowProperties(sdlWindow);
            IntPtr windowPointer = SDL3.SDL.SDL_GetPointerProperty(
                properties, SDL3.SDL.SDL_PROP_WINDOW_UIKIT_WINDOW_POINTER, IntPtr.Zero);
            UIWindow? gameWindow = windowPointer == IntPtr.Zero
                ? null
                : Runtime.GetNSObject<UIWindow>(windowPointer);
            if (gameWindow == null)
            {
                GraphicsProbe.Log("Mobile joystick: UIKit window not found\n");
                return;
            }

            _gameWindow = gameWindow;

            var joystick = new JoystickOverlayView(gameWindow.Bounds)
            {
                AutoresizingMask = UIViewAutoresizing.FlexibleWidth | UIViewAutoresizing.FlexibleHeight,
                BackgroundColor = UIColor.Clear,
                UserInteractionEnabled = true
            };
            gameWindow.AddSubview(joystick);
            gameWindow.BringSubviewToFront(joystick);
            _joystick = joystick;
            GraphicsProbe.Log("Mobile joystick: READY\n");
        }
        catch (Exception ex)
        {
            GraphicsProbe.Log($"Mobile joystick: FAIL {ex.Message}\n");
        }
    }

    private void InstallLongPressGesture(IntPtr sdlWindow)
    {
        try
        {
            uint properties = SDL3.SDL.SDL_GetWindowProperties(sdlWindow);
            IntPtr windowPointer = SDL3.SDL.SDL_GetPointerProperty(
                properties, SDL3.SDL.SDL_PROP_WINDOW_UIKIT_WINDOW_POINTER, IntPtr.Zero);
            UIWindow? gameWindow = windowPointer == IntPtr.Zero
                ? null
                : Runtime.GetNSObject<UIWindow>(windowPointer);
            if (gameWindow == null)
            {
                GraphicsProbe.Log("Mobile long press: UIKit window not found\n");
                return;
            }

            _gameWindow = gameWindow;
            var longPress = new UILongPressGestureRecognizer(HandleLongPress)
            {
                MinimumPressDuration = 0.45,
                CancelsTouchesInView = true,
                ShouldReceiveTouch = (_, touch) => touch.View is not JoystickOverlayView
            };
            gameWindow.AddGestureRecognizer(longPress);
            GraphicsProbe.Log("Mobile long press: READY\n");
        }
        catch (Exception ex)
        {
            GraphicsProbe.Log($"Mobile long press: FAIL {ex.Message}\n");
        }
    }

    private void HandleLongPress(UILongPressGestureRecognizer gesture)
    {
        if (gesture.State != UIGestureRecognizerState.Began || _graphics == null)
            return;

        CGPoint point = gesture.LocationInView(gesture.View);
        nfloat sx = point.X * _graphics.Window.ClientBounds.Width / gesture.View!.Bounds.Width;
        nfloat sy = point.Y * _graphics.Window.ClientBounds.Height / gesture.View.Bounds.Height;
        SDL3.SDL.SDL_WarpMouseInWindow(_graphics.Window.Handle, (float)sx, (float)sy);

        SDL3.SDL.SDL_Event down = new();
        down.type = (uint)SDL3.SDL.SDL_EventType.SDL_EVENT_MOUSE_BUTTON_DOWN;
        down.button.button = (byte)MouseButtonType.Right;
        down.button.down = true;
        down.button.clicks = 1;
        down.button.x = (float)sx;
        down.button.y = (float)sy;
        SDL3.SDL.SDL_PushEvent(ref down);

        SDL3.SDL.SDL_Event up = new();
        up.type = (uint)SDL3.SDL.SDL_EventType.SDL_EVENT_MOUSE_BUTTON_UP;
        up.button.button = (byte)MouseButtonType.Right;
        up.button.down = false;
        up.button.clicks = 1;
        up.button.x = (float)sx;
        up.button.y = (float)sy;
        SDL3.SDL.SDL_PushEvent(ref up);
        GraphicsProbe.Log("Mobile touch: RIGHT_CLICK\n");
    }

    private void DrawFrame()
    {
        // Desktop TazUO has no synchronization context. UIKit's context would
        // deadlock existing network waits when their continuations need this
        // same main thread. Restore UIKit's context before returning to iOS.
        var uiContext = System.Threading.SynchronizationContext.Current;
        try
        {
            System.Threading.SynchronizationContext.SetSynchronizationContext(null);
            if (_gameWindow is { } window && window.Bounds.Width > 0 && window.Bounds.Height > 0)
            {
                var insets = window.SafeAreaInsets;
                MobileControlBridge.SetSafeArea(
                    (float)(insets.Left / window.Bounds.Width), (float)(insets.Top / window.Bounds.Height),
                    (float)(insets.Right / window.Bounds.Width), (float)(insets.Bottom / window.Bounds.Height));

                UpdateKeyboardInset(window);
            }
            _graphics?.RunOneFrame();
#if DEBUG
            DateTime now = DateTime.UtcNow;
            if (MobileControlBridge.Direction != Direction.NONE)
                _animationLogUntil = now.AddMilliseconds(500);
            if (now < _animationLogUntil && now >= _nextAnimationLog)
            {
                _nextAnimationLog = now.AddMilliseconds(100);
                GraphicsProbe.Log($"Mobile animation: {MobileControlBridge.DescribeAnimation()}\n");
            }
#endif
            if (!_longPressInstalled && _graphics != null)
            {
                // The login screen is not a READY world yet, but it still needs
                // long press to emulate right click on the account field.
                InstallLongPressGesture(_graphics.Window.Handle);
                _longPressInstalled = true;
            }
            if (!_joystickInstalled && _graphics != null
                && MobileControlBridge.DescribeWorld().StartsWith("READY", StringComparison.Ordinal))
            {
                // FNA creates its final UIKit rendering view in the first frame.
                InstallJoystick(_graphics.Window.Handle);
                _joystickInstalled = true;
            }
            if (DateTime.UtcNow >= _nextStateLog)
            {
                _nextStateLog = DateTime.UtcNow.AddSeconds(1);
                string state = MobileControlBridge.DescribeWorld();
                if (_joystickInstalled && !state.StartsWith("READY", StringComparison.Ordinal))
                {
                    _joystick?.RemoveFromSuperview();
                    _joystick = null;
                    _joystickInstalled = false;
                }
                if (state.StartsWith("READY", StringComparison.Ordinal))
                {
                    RunRequestedLegionSmokeTest();
                }
                if (state != _lastWorldState)
                {
                    GraphicsProbe.Log($"Native world: {state}\n");
                    _lastWorldState = state;
                }
            }
        }
        catch (Exception ex) { GraphicsFailed(ex); }
        finally { System.Threading.SynchronizationContext.SetSynchronizationContext(uiContext); }
    }

    private void UpdateKeyboardInset(UIWindow window)
    {
        if (!_keyboardFrameValid)
            return;

        // UIKit reports the keyboard frame in screen coordinates. The SDL window
        // is fullscreen, so intersecting both frames also handles the keyboard
        // animation and devices with a home-indicator inset without guessing a
        // fixed keyboard height.
        CGRect windowFrame = window.Frame;
        nfloat windowBottom = windowFrame.Y + windowFrame.Height;
        nfloat keyboardBottom = _keyboardFrame.Y + _keyboardFrame.Height;
        nfloat overlapTop = windowFrame.Y > _keyboardFrame.Y ? windowFrame.Y : _keyboardFrame.Y;
        nfloat overlapBottom = windowBottom < keyboardBottom ? windowBottom : keyboardBottom;
        nfloat overlap = overlapBottom - overlapTop;
        nfloat bottom = overlap > 0 && overlapBottom >= windowBottom - 1 ? overlap : 0;
        _keyboardVisible = bottom > 0;
        MobileControlBridge.SetKeyboardBottomInset((float)(bottom / windowFrame.Height));

        {
            if (_keyboardDismissTap == null)
            {
                _keyboardDismissTap = new UITapGestureRecognizer(HandleKeyboardDismissTap)
                {
                    CancelsTouchesInView = false
                };
                window.AddGestureRecognizer(_keyboardDismissTap);
            }
            if (_keyboardDismissButton == null)
            {
                _keyboardDismissButton = new KeyboardDismissButton();
                _keyboardDismissButton.SetImage(UIImage.GetSystemImage("chevron.down"), UIControlState.Normal);
                _keyboardDismissButton.TintColor = UIColor.White;
                _keyboardDismissButton.AccessibilityLabel = "Masquer le clavier";
                _keyboardDismissButton.BackgroundColor = UIColor.FromRGBA(25, 25, 25, 220);
                _keyboardDismissButton.Layer.CornerRadius = 7;
                _keyboardDismissButton.TouchUpInside += (_, _) =>
                {
                    if (_keyboardVisible)
                    {
                        // SDL owns a hidden UITextField. Resign it directly so
                        // UIKit closes even when Myra captures the game input.
                        window.EndEditing(true);
                        MobileControlBridge.RequestKeyboardDismiss();
                    }
                    else
                    {
                        window.MakeKeyWindow();
                        MobileControlBridge.RequestKeyboardOpen();
                    }
                };
            }

            if (_keyboardDismissOverlay == null)
            {
                _keyboardDismissOverlay = new KeyboardDismissOverlayWindow(window.Frame)
                {
                    WindowLevel = UIWindowLevel.Alert + 1,
                    BackgroundColor = UIColor.Clear,
                    UserInteractionEnabled = true
                };
                _keyboardDismissOverlay.AddSubview(_keyboardDismissButton);
            }
            _keyboardDismissOverlay.Hidden = false;

            // Keyboard frames are reported in the fullscreen window's coordinate
            // space on iOS. Keep the control just above the keyboard edge.
            nfloat buttonY = _keyboardVisible
                ? overlapTop - windowFrame.Y - 32
                : window.Bounds.Height - window.SafeAreaInsets.Bottom - 36;
            if (buttonY < 0) buttonY = 0;
            _keyboardDismissButton.SetImage(
                UIImage.GetSystemImage(_keyboardVisible ? "chevron.down" : "chevron.up"),
                UIControlState.Normal);
            _keyboardDismissButton.AccessibilityLabel = _keyboardVisible
                ? "Masquer le clavier" : "Afficher le clavier";
            _keyboardDismissButton.Frame = new CGRect(
                _keyboardDismissOverlay.Bounds.Width - window.SafeAreaInsets.Right - 32, buttonY, 28, 28);
            _keyboardDismissOverlay.DismissButton = _keyboardDismissButton;
        }
    }

    private void HandleKeyboardDismissTap(UITapGestureRecognizer gesture)
    {
        if (gesture.State != UIGestureRecognizerState.Ended || _keyboardDismissButton == null)
            return;

        CGPoint point = gesture.LocationInView(_keyboardDismissButton.Superview);
        CGRect hitFrame = _keyboardDismissButton.Frame;
        hitFrame.Inflate(8, 8);
        if (hitFrame.Contains(point))
        {
            if (_keyboardVisible)
            {
                _gameWindow?.EndEditing(true);
                MobileControlBridge.RequestKeyboardDismiss();
            }
            else
            {
                _gameWindow?.MakeKeyWindow();
                MobileControlBridge.RequestKeyboardOpen();
            }
            GraphicsProbe.Log("iOS keyboard: DISMISS_BUTTON\n");
        }
    }

    private void RunRequestedLegionSmokeTest()
    {
        string requestPath = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "native-legion-smoke.request");
        if (System.IO.File.Exists(requestPath))
        {
            var script = ClassicUO.LegionScripting.NativeLegionBridge.FindLoadedScript("NativeSmokeTest.py");
            if (script == null)
                return; // Legion initializes during scene setup; retry next frame.

            System.IO.File.Delete(requestPath);
            if (ClassicUO.LegionScripting.NativeLegionBridge.Play(script))
                GraphicsProbe.Log("Legion live script: STARTED\n");
        }

        string resultPath = System.IO.Path.Combine(Environment.CurrentDirectory, "native-legion-smoke-result.txt");
        if (!_legionSmokeReported && System.IO.File.Exists(resultPath))
        {
            GraphicsProbe.Log(System.IO.File.ReadAllText(resultPath) + "\n");
            _legionSmokeReported = true;
        }
    }

    private void GraphicsFailed(Exception ex)
    {
        _displayLink?.Invalidate();
        GraphicsProbe.Log($"FNA graphics: FAIL\n{ex}\n");
        UIApplication.SharedApplication.BeginInvokeOnMainThread(() =>
        {
            if (_status is not null)
                _status.Text = $"TazUO could not continue.\n\n{ex.GetBaseException().Message}";

            _gameWindow?.EndEditing(true);
            if (_keyboardDismissOverlay is not null)
                _keyboardDismissOverlay.Hidden = true;

            // The diagnostic window is hidden when SDL starts. Bring it back
            // above the game so a failed first frame cannot leave a black screen.
            if (UIApplication.SharedApplication.Delegate is AppDelegate { Window: { } window })
            {
                window.WindowLevel = UIWindowLevel.Alert + 2;
                window.MakeKeyAndVisible();
            }
        });
    }

    private void SetStatus(string text)
    {
        UIApplication.SharedApplication.BeginInvokeOnMainThread(() =>
        {
            if (_status is not null)
                _status.Text = text;
        });
    }
}

/// <summary>A compact keyboard chevron with an invisible 44-point touch area.</summary>
internal sealed class KeyboardDismissButton : UIButton
{
    public override bool PointInside(CGPoint point, UIEvent? uievent)
        => new CGRect(-8, -8, Bounds.Width + 16, Bounds.Height + 16).Contains(point);
}

internal sealed class KeyboardDismissOverlayWindow : UIWindow
{
    // This window owns only the arrow. SDL's hidden text field must remain in
    // the key window to present the software keyboard.
    public override bool CanBecomeKeyWindow => false;

    internal UIButton? DismissButton { get; set; }

    public KeyboardDismissOverlayWindow(CGRect frame) : base(frame) { }

    public override UIView? HitTest(CGPoint point, UIEvent? uievent)
    {
        if (DismissButton != null)
        {
            CGRect hitFrame = DismissButton.Frame;
            hitFrame.Inflate(8, 8);
            if (hitFrame.Contains(point))
                return DismissButton;
        }

        return null;
    }
}

/// <summary>Transparent, landscape-friendly joystick overlay for the native SDL window.</summary>
internal sealed class JoystickOverlayView : UIView
{
    private static readonly nfloat BaseRadius = 78;
    private static readonly nfloat KnobRadius = 30;
    private CGPoint _center;
    private CGPoint _knob;
    private UITouch? _activeTouch;

    public JoystickOverlayView(CGRect frame) : base(frame)
    {
        MultipleTouchEnabled = false;
        Opaque = false;
        _center = new CGPoint(120, frame.Height - 120);
        _knob = _center;
    }

    public override void LayoutSubviews()
    {
        base.LayoutSubviews();
        _center = new CGPoint(Math.Max(BaseRadius + 20, Bounds.Width * 0.13),
            Bounds.Height - SafeAreaInsets.Bottom - BaseRadius - 28);
        if (_activeTouch == null)
            _knob = _center;
        SetNeedsDisplay();
    }

    public override void Draw(CGRect rect)
    {
        using var context = UIGraphics.GetCurrentContext();
        if (context == null) return;
        context.SetFillColor(UIColor.FromRGBA(5, 12, 20, 150).CGColor);
        context.FillEllipseInRect(new CGRect(_center.X - BaseRadius, _center.Y - BaseRadius,
            BaseRadius * 2, BaseRadius * 2));
        context.SetStrokeColor(UIColor.FromRGBA(220, 180, 70, 210).CGColor);
        context.SetLineWidth(2);
        context.StrokeEllipseInRect(new CGRect(_center.X - BaseRadius, _center.Y - BaseRadius,
            BaseRadius * 2, BaseRadius * 2));
        context.SetFillColor(UIColor.FromRGBA(240, 240, 240, 220).CGColor);
        context.FillEllipseInRect(new CGRect(_knob.X - KnobRadius, _knob.Y - KnobRadius,
            KnobRadius * 2, KnobRadius * 2));
    }

    public override bool PointInside(CGPoint point, UIEvent? uievent)
    {
        // UIKit keeps delivering the captured finger outside these bounds.
        // Hit-testing the entire screen here would steal additional world touches.
        nfloat dx = point.X - _center.X;
        nfloat dy = point.Y - _center.Y;
        return dx * dx + dy * dy <= BaseRadius * BaseRadius;
    }

    public override void TouchesBegan(NSSet touches, UIEvent? evt)
    {
        _activeTouch = touches.AnyObject as UITouch;
        if (_activeTouch != null) UpdateJoystick(_activeTouch.LocationInView(this));
    }

    public override void TouchesMoved(NSSet touches, UIEvent? evt)
    {
        if (_activeTouch != null) UpdateJoystick(_activeTouch.LocationInView(this));
    }

    public override void TouchesEnded(NSSet touches, UIEvent? evt) => ReleaseJoystick();
    public override void TouchesCancelled(NSSet touches, UIEvent? evt) => ReleaseJoystick();

    private void ReleaseJoystick()
    {
        _activeTouch = null;
        _knob = _center;
        MobileControlBridge.SetDirection(Direction.NONE);
        SetNeedsDisplay();
    }

    private void UpdateJoystick(CGPoint point)
    {
        nfloat dx = point.X - _center.X;
        nfloat dy = point.Y - _center.Y;
        nfloat length = (nfloat)Math.Sqrt((double)(dx * dx + dy * dy));
        if (length < 18)
        {
            MobileControlBridge.SetDirection(Direction.NONE);
            _knob = _center;
            SetNeedsDisplay();
            return;
        }

        nfloat scale = (nfloat)Math.Min(1d, (double)(BaseRadius / length));
        _knob = new CGPoint(_center.X + dx * scale, _center.Y + dy * scale);
        double angle = Math.Atan2((double)dy, (double)dx);
        int sector = (int)Math.Round((angle + Math.PI / 2) / (Math.PI / 4));
        sector = ((sector % 8) + 8) % 8;
        MobileControlBridge.SetDirection(sector switch
        {
            0 => Direction.Up,
            1 => Direction.North,
            2 => Direction.Right,
            3 => Direction.East,
            4 => Direction.Down,
            5 => Direction.South,
            6 => Direction.Left,
            _ => Direction.West
        });
        SetNeedsDisplay();
    }
}
