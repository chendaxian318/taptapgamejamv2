using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
namespace HabitatIsland
{
    public sealed class HabitatUiButtonMotion : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        bool hover, down; Button button;
        void Awake() { button = GetComponent<Button>(); }
        public void OnPointerEnter(PointerEventData e) { hover = true; }
        public void OnPointerExit(PointerEventData e) { hover = down = false; }
        public void OnPointerDown(PointerEventData e) { if (e.button == PointerEventData.InputButton.Left) down = true; }
        public void OnPointerUp(PointerEventData e) { down = false; }
        void Update()
        {
            float target = !button || !button.interactable ? 1 : down ? .97f : hover ? 1.035f : 1;
            transform.localScale = Vector3.Lerp(transform.localScale, Vector3.one * target, 1-Mathf.Exp(-18*Time.unscaledDeltaTime));
        }
        void OnDisable() { hover = down = false; transform.localScale = Vector3.one; }
    }
}
