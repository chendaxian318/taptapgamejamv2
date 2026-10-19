using System;
using UnityEngine;
using UnityEngine.EventSystems;
namespace HabitatIsland
{
    public sealed class HabitatUiBackdropDismiss : MonoBehaviour, IPointerClickHandler
    {
        public Action Clicked;
        public void OnPointerClick(PointerEventData e)
        {
            if (e.button == PointerEventData.InputButton.Left && e.pointerCurrentRaycast.gameObject == gameObject) Clicked?.Invoke();
        }
    }
}
