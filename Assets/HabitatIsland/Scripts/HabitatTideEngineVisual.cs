using UnityEngine;

namespace HabitatIsland
{
    /// <summary>Pressure presentation only; never changes tide, boxes or rewards.</summary>
    [DisallowMultipleComponent]
    public sealed class HabitatTideEngineVisual : MonoBehaviour
    {
        [SerializeField] LineRenderer activationRing;
        HabitatEquipmentAnimation animationPlayer;
        MaterialPropertyBlock ringProperties;
        float compression, pulseTime;
        bool initialized, pressed;
        public bool IsInitialized => initialized;
        public bool IsPressed => pressed;
        public float Compression => compression;
        public string CurrentClip => Player ? Player.CurrentClipName : "";
        HabitatEquipmentAnimation Player => animationPlayer ? animationPlayer : animationPlayer = GetComponent<HabitatEquipmentAnimation>();

        public void Configure(LineRenderer ring) { activationRing = ring; }
        public void ResetVisual(bool active, float previousCompression = -1f)
        {
            initialized = true; pressed = active;
            compression = previousCompression < 0f ? (active ? 1f : 0f) : Mathf.Clamp01(previousCompression);
            pulseTime = 0f; ApplyPose();
        }
        public void Render(bool active, float dt)
        {
            if (!initialized) ResetVisual(active);
            if (!float.IsFinite(dt) || dt < 0f) dt = 0f;
            pressed = active; compression = Mathf.MoveTowards(compression, active ? 1f : 0f, dt / .4f);
            pulseTime += dt; ApplyPose();
        }
        void ApplyPose()
        {
            if (Player)
            {
                if (compression <= 0f) Player.Sample("Idle", 0f);
                else if (compression >= 1f) Player.Sample("Active", 0f);
                else if (pressed) Player.Sample("Press", compression * .4f);
                else Player.Sample("Release", (1f - compression) * .4f);
            }
            if (!activationRing) return;
            activationRing.enabled = compression > .001f;
            float pulse = .78f + .22f * Mathf.Sin(pulseTime * Mathf.PI);
            activationRing.widthMultiplier = .014f + .007f * compression * pulse;
            if (ringProperties == null) ringProperties = new MaterialPropertyBlock();
            activationRing.GetPropertyBlock(ringProperties);
            ringProperties.SetColor("_BaseColor", new Color(.08f, .78f, .86f, 1f) * (compression * (1.2f + pulse)));
            activationRing.SetPropertyBlock(ringProperties);
        }
    }
}
