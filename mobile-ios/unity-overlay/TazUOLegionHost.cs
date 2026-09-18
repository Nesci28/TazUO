using System;
using System.Collections;
using System.Runtime.InteropServices;
using ClassicUO;
using ClassicUO.Game.Data;
using ClassicUO.Game.Scenes;
using UnityEngine;

namespace TazUO.Mobile
{
    // Runs Legion scripts inside the app's local Pyodide WebView. No network
    // URL or desktop process is involved. Calls are marshalled back to the
    // real GameScene on Unity's main thread.
    internal sealed class TazUOLegionHost : MonoBehaviour
    {
        [Serializable]
        private sealed class LegionMessage
        {
            public string type;
            public int id;
            public string name;
            public string[] args;
        }

        [Serializable]
        private sealed class LegionResponse
        {
            public int id;
            public bool ok;
            public bool result;
            public string error;
        }

        [DllImport("__Internal")] private static extern void TazUO_LegionStart(string htmlPath, string receiver);
        [DllImport("__Internal")] private static extern void TazUO_LegionSend(string javascript);

        private const string Receiver = "TazUO Legion Host";
        private GameScene _scene;
        private int _nextRequest;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (FindObjectOfType<TazUOLegionHost>() == null)
                new GameObject(Receiver).AddComponent<TazUOLegionHost>();
        }

        private void Start()
        {
#if UNITY_IOS && !UNITY_EDITOR
            // iOS packs StreamingAssets under Data/Raw. Using dataPath avoids
            // URL-style values on some Unity versions that cannot be passed
            // through the native string bridge.
            var html = System.IO.Path.Combine(Application.dataPath, "Raw/legion/index.html");
            if (!System.IO.File.Exists(html))
                html = System.IO.Path.Combine(Application.streamingAssetsPath, "legion/index.html");
            Debug.Log($"Legion WebView asset: {html} (exists={System.IO.File.Exists(html)})");
            if (System.IO.File.Exists(html))
                TazUO_LegionStart(html, Receiver);
#endif
        }

        private void Update() => _scene = Client.Game?.Scene as GameScene;

        public void OnLegionMessage(string message)
        {
            if (string.IsNullOrWhiteSpace(message) || _scene == null)
                return;

            try
            {
                var root = JsonUtility.FromJson<LegionMessage>(message);
                if (root == null || root.type != "api")
                    return;
                StartCoroutine(Dispatch(root.id, root.name ?? string.Empty, root.args ?? Array.Empty<string>()));
            }
            catch (Exception exception)
            {
                Debug.LogError($"Legion bridge message failed: {exception}");
            }
        }

        public void RunScript(string source)
        {
            if (string.IsNullOrWhiteSpace(source))
                return;
#if UNITY_IOS && !UNITY_EDITOR
            TazUO_LegionSend($"window.TazUORun({JsonUtility.ToJson(source)})");
#endif
        }

        private IEnumerator Dispatch(int id, string name, string[] args)
        {
            var result = false;
            string error = null;
            switch (name)
            {
                case "walk":
                    var direction = ParseDirection(args.Length > 0 ? args[0] : string.Empty);
                    _scene.SetMobileDirection(direction, true);
                    yield return new WaitForSecondsRealtime(0.12f);
                    _scene.SetMobileDirection(direction, false);
                    result = true;
                    break;
                case "say":
                    _scene.SendMobileChat(args.Length > 0 ? args[0] : string.Empty);
                    result = true;
                    break;
                case "target":
                    _scene.BeginMobileTarget();
                    result = true;
                    break;
                default:
                    error = $"Unsupported iOS Legion API method: {name}";
                    break;
            }

            var payload = JsonUtility.ToJson(new LegionResponse
            {
                id = id,
                ok = error == null,
                result = result,
                error = error ?? string.Empty
            });
#if UNITY_IOS && !UNITY_EDITOR
            TazUO_LegionSend($"window.__tazuoResolve({id}, {JsonUtility.ToJson(payload)})");
#endif
        }

        private static Direction ParseDirection(string value) => value?.ToLowerInvariant() switch
        {
            "north" or "up" => Direction.North,
            "south" or "down" => Direction.South,
            "east" or "right" => Direction.East,
            "west" or "left" => Direction.West,
            _ => Direction.NONE
        };
    }
}
