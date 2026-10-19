using UnityEngine;

namespace HabitatIsland
{
    /// <summary>Authored swimming poses follow visual travel, never grid simulation.</summary>
    [DisallowMultipleComponent]
    public sealed class HabitatJellyfishVisual : MonoBehaviour
    {
        [SerializeField] float groundClearance;
        HabitatEquipmentAnimation clips;
        float still = 1f;
        public float GroundClearance => groundClearance;
        public string CurrentClip => clips ? clips.CurrentClipName : "";
        public void ConfigureClearance(float value) { groundClearance = Mathf.Max(0f, value); }

        public void Render(float dt, Vector3 position, Vector3 before, bool reset, bool carried)
        {
            if (!clips) clips = GetComponent<HabitatEquipmentAnimation>();
            if (!clips) return;
            Vector3 delta = position - before; delta.y = 0f;
            if (reset || delta.sqrMagnitude > 4f)
            {
                still = 1f;
                clips.Play("Idle", restart: true);
                clips.Tick(0f);
                return;
            }
            // Zero display time preserves both the selected pose and its phase.
            if (dt <= 0f) return;
            if (!carried && delta.magnitude / dt > .08f) still = 0f;
            else still += dt;
            clips.Play(!carried && still < .12f ? "Swim" : "Idle");
            clips.Tick(dt);
        }
    }
}
