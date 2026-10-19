using UnityEngine;
using UnityEngine.Rendering;

namespace HabitatIsland
{
    [CreateAssetMenu(menuName = "Habitat Island/Lighting Profile")]
    public sealed class HabitatLightingProfile : ScriptableObject
    {
        public Color sunlight = new Color(1f, .95f, .84f);
        [Range(0f, 3f)] public float sunIntensity = 1.25f;
        public Vector3 sunEuler = new Vector3(48f, -35f, 0f);
        [Range(0f, 1f)] public float shadowStrength = .72f;
        public Color ambientSky = new Color(.57f, .71f, .80f);
        public Color ambientHorizon = new Color(.57f, .64f, .62f);
        public Color ambientGround = new Color(.32f, .38f, .38f);
        public Material skybox;
        public VolumeProfile atmosphere;
        public Color deepWater = new Color(.16f, .48f, .56f);
        public Color shallowWater = new Color(.24f, .59f, .62f);
        public Color foam = new Color(.79f, .92f, .85f);
        [Range(0f, .15f)] public float waveStrength = .035f;
        [Range(0f, 1f)] public float foamStrength = .35f;
        [Header("Painted water movement")]
        [Range(0f, 2f)] public float waterMotionSpeed = 1.1f;
        [Tooltip("Visibility of the original painted water details; no repeated wave strokes.")]
        [Range(0f, .6f)] public float waterRippleStrength = .14f;
        [Range(0f, .01f)] public float waterFlowStrength = .008f;
    }
}
