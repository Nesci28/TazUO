using System;
using ClassicUO;
using ClassicUO.Configuration;
using ClassicUO.Game.Data;
using ClassicUO.Game.Managers;
using ClassicUO.Game.Scenes;
using ClassicUO.Game.UI.Gumps;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TazUO.Mobile
{
    internal sealed class TazUOMobileControls : MonoBehaviour
    {
        private Canvas _canvas;
        private GameObject _panel;
        private GameScene _scene;
        private RectTransform _safeArea;
        private Rect _lastSafeArea;
        private WorldViewportGump _viewport;
        private Microsoft.Xna.Framework.Point _viewportSize;
        private TouchScreenKeyboard _chatKeyboard;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            Screen.orientation = ScreenOrientation.LandscapeLeft;
            if (FindObjectOfType<TazUOMobileControls>() == null)
                new GameObject("TazUO mobile controls").AddComponent<TazUOMobileControls>();
        }

        private static void EnsureEventSystem()
        {
            var events = EventSystem.current ?? FindObjectOfType<EventSystem>();
            if (events == null)
            {
                events = new GameObject("TazUO touch events").AddComponent<EventSystem>();
            }

            var input = events.GetComponent<StandaloneInputModule>();
            if (input == null)
                input = events.gameObject.AddComponent<StandaloneInputModule>();

        }

        private void Update()
        {
            var scene = Client.Game?.Scene as GameScene;
            if (_scene != scene)
            {
                _scene?.ReleaseMobileButtons();
                _scene = scene;
                _viewport = null;
            }
            if (_scene == null) return;

            FitWorldViewport();
            var safeArea = Screen.safeArea;
            if (_safeArea != null && safeArea != _lastSafeArea)
            {
                _lastSafeArea = safeArea;
                _safeArea.anchorMin = new Vector2(safeArea.xMin / Screen.width, safeArea.yMin / Screen.height);
                _safeArea.anchorMax = new Vector2(safeArea.xMax / Screen.width, safeArea.yMax / Screen.height);
            }
        }

        private void FitWorldViewport()
        {
            var viewport = UIManager.GetGump<WorldViewportGump>();
            if (viewport == null || ProfileManager.Current == null) return;
            var bounds = Client.Game.Window.ClientBounds;
            var size = new Microsoft.Xna.Framework.Point(bounds.Width, bounds.Height);
            if (viewport == _viewport && size == _viewportSize) return;
            _viewport = viewport;
            _viewportSize = size;
            ProfileManager.Current.GameWindowFullSize = true;
            ProfileManager.Current.GameWindowLock = true;
            viewport.X = viewport.Y = -5;
            viewport.ResizeGameWindow(size);
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused) _scene?.ReleaseMobileButtons();
        }

        private void OnApplicationFocus(bool focused)
        {
            if (!focused) _scene?.ReleaseMobileButtons();
        }

        private void BuildControls()
        {
            _canvas = new GameObject("TazUO touch canvas").AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 5000;
            var scaler = _canvas.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1300, 600);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1;
            _canvas.gameObject.AddComponent<GraphicRaycaster>();
            EnsureEventSystem();

            _safeArea = new GameObject("Safe area", typeof(RectTransform)).GetComponent<RectTransform>();
            _safeArea.SetParent(_canvas.transform, false);
            _safeArea.offsetMin = _safeArea.offsetMax = Vector2.zero;

            _panel = new GameObject("TazUO controls panel");
            _panel.transform.SetParent(_safeArea, false);
            var panelRect = _panel.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(1, 0);
            panelRect.anchorMax = new Vector2(1, 0);
            panelRect.pivot = new Vector2(1, 0);
            panelRect.anchoredPosition = new Vector2(-12, 12);
            panelRect.sizeDelta = new Vector2(420, 170);
            var panelImage = _panel.AddComponent<Image>();
            panelImage.color = new Color(0.02f, 0.04f, 0.06f, 0.55f);

            AddHoldButton("▲", new Vector2(90, 92), Direction.North);
            AddHoldButton("◀", new Vector2(12, 16), Direction.West);
            AddHoldButton("▼", new Vector2(90, 16), Direction.South);
            AddHoldButton("▶", new Vector2(168, 16), Direction.East);
            AddActionButton("Target", new Vector2(250, 92), () => _scene?.BeginMobileTarget());
            AddActionButton("Context", new Vector2(332, 92), () => _scene?.ShowMobileContext());
            AddActionButton("Chat", new Vector2(250, 16), OpenChatInput);
        }

        private void AddHoldButton(string label, Vector2 position, Direction direction)
        {
            var button = AddButton(label, position);
            var hold = button.gameObject.AddComponent<MobileHoldButton>();
            hold.Pressed += () => _scene?.SetMobileDirection(direction, true);
            hold.Released += () => _scene?.SetMobileDirection(direction, false);
        }

        private void AddActionButton(string label, Vector2 position, Action action)
        {
            var button = AddButton(label, position);
            button.onClick.AddListener(() => action());
        }

        private Button AddButton(string label, Vector2 position)
        {
            var objectButton = new GameObject(label);
            objectButton.transform.SetParent(_panel.transform, false);
            var rect = objectButton.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0, 0);
            rect.anchorMax = new Vector2(0, 0);
            rect.pivot = new Vector2(0, 0);
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(72, 64);
            var image = objectButton.AddComponent<Image>();
            image.color = new Color(0.93f, 0.93f, 0.93f, 0.96f);
            var button = objectButton.AddComponent<Button>();
            var text = new GameObject("label").AddComponent<Text>();
            text.transform.SetParent(objectButton.transform, false);
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.black;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.text = label;
            text.resizeTextForBestFit = true;
            text.resizeTextMaxSize = 22;
            text.raycastTarget = false;
            var textRect = text.rectTransform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            return button;
        }

        private void OpenChatInput()
        {
            if (_chatKeyboard != null && _chatKeyboard.active) return;
            _scene?.ReleaseMobileButtons();
            _chatKeyboard = TouchScreenKeyboard.Open(string.Empty, TouchScreenKeyboardType.Default, false, false, false, false, "Say");
            if (_chatKeyboard != null)
                StartCoroutine(WaitForChat(_chatKeyboard, _scene));
        }

        private System.Collections.IEnumerator WaitForChat(TouchScreenKeyboard input, GameScene scene)
        {
            while (input.active)
                yield return null;
            if (input.status == TouchScreenKeyboard.Status.Done && scene == _scene && !string.IsNullOrWhiteSpace(input.text))
                scene?.SendMobileChat(input.text);
        }
    }

    internal sealed class MobileHoldButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        public event Action Pressed;
        public event Action Released;
        public void OnPointerDown(PointerEventData eventData) => Pressed?.Invoke();
        public void OnPointerUp(PointerEventData eventData) => Released?.Invoke();
        public void OnPointerExit(PointerEventData eventData) => Released?.Invoke();
        private void OnDisable() => Released?.Invoke();
    }
}
