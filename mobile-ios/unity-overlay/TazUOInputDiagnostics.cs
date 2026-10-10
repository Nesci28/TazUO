#if UNITY_IOS && !UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TazUO.Mobile
{
    internal sealed class TazUOInputDiagnostics : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (Debug.isDebugBuild)
                new GameObject("TazUO input diagnostics").AddComponent<TazUOInputDiagnostics>();
        }

        private void Update()
        {
            if (Input.touchCount == 0 || EventSystem.current == null) return;
            var touch = Input.GetTouch(0);
            if (touch.phase != TouchPhase.Began) return;
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = touch.position }, hits);
            foreach (var hit in hits)
            {
                var field = hit.gameObject.GetComponentInParent<InputField>();
                Debug.Log($"Input hit={hit.gameObject.name} field={field != null} supported={TouchScreenKeyboard.isSupported} selected={EventSystem.current.currentSelectedGameObject?.name}");
                if (field != null)
                    Debug.Log($"Input interactable={field.IsInteractable()} focused={field.isFocused} readOnly={field.readOnly} font={field.textComponent?.font?.name}");
            }
        }
    }
}
#endif
