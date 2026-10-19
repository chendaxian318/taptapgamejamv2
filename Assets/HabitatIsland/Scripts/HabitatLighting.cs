using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace HabitatIsland
{
    /// <summary>Applies the art direction once. Story tracks retain control of the sun.</summary>
    public sealed class HabitatLighting : MonoBehaviour
    {
        Material runtimeSky;
        Material previousSky;
        AmbientMode previousMode;
        Color previousTop, previousHorizon, previousBottom;
        bool applied;
        Light activeSun;

        public void Apply(Camera camera, Material water)
        {
            var profile = Resources.Load<HabitatLightingProfile>("Environment/IslandDaylight");
            if (!profile || applied) return;
            previousSky = RenderSettings.skybox; previousMode = RenderSettings.ambientMode;
            previousTop = RenderSettings.ambientSkyColor; previousHorizon = RenderSettings.ambientEquatorColor;
            previousBottom = RenderSettings.ambientGroundColor;
            applied = true;
            var sun = RenderSettings.sun;
            if (!sun)
                foreach (var light in FindObjectsByType<Light>())
                    if (light.type == LightType.Directional) { sun = light; break; }
            if (!sun) sun = new GameObject("Island daylight", typeof(Light)).GetComponent<Light>();
            sun.type = LightType.Directional; sun.color = profile.sunlight; sun.intensity = profile.sunIntensity;
            sun.transform.rotation = Quaternion.Euler(profile.sunEuler);
            sun.shadows = LightShadows.Soft; sun.shadowStrength = profile.shadowStrength;
            RenderSettings.sun = sun;
            activeSun = sun;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = profile.ambientSky;
            RenderSettings.ambientEquatorColor = profile.ambientHorizon;
            RenderSettings.ambientGroundColor = profile.ambientGround;
            if (profile.skybox)
            {
                runtimeSky = new Material(profile.skybox);
                runtimeSky.SetVector("_SunDirection", -sun.transform.forward);
                RenderSettings.skybox = runtimeSky;
                camera.clearFlags = CameraClearFlags.Skybox;
            }
            var cameraData = camera.GetUniversalAdditionalCameraData();
            cameraData.requiresDepthTexture = true;
            cameraData.renderPostProcessing = true;
            if (profile.atmosphere)
            {
                var volume = new GameObject("Island daylight grading", typeof(Volume)).GetComponent<Volume>();
                volume.transform.SetParent(transform, false); volume.isGlobal = true;
                volume.priority = 10f; volume.sharedProfile = profile.atmosphere;
            }
            if (water && water.shader.name == "Habitat/QuietPaintedWater")
            {
                water.SetColor("_BaseColor", profile.deepWater);
                water.SetColor("_ShallowColor", profile.shallowWater);
                water.SetColor("_FoamColor", profile.foam);
                water.SetFloat("_WaveStrength", profile.waveStrength);
                water.SetFloat("_FoamStrength", profile.foamStrength);
                water.SetFloat("_MotionSpeed", profile.waterMotionSpeed);
                water.SetFloat("_RippleStrength", profile.waterRippleStrength);
                water.SetFloat("_FlowStrength", profile.waterFlowStrength);
            }
        }

        void LateUpdate()
        {
            // Read the story's sun, never overwrite a Timeline lighting change.
            if(runtimeSky && activeSun)
            {
                runtimeSky.SetVector("_SunDirection",-activeSun.transform.forward);
                runtimeSky.SetColor("_SunColor",activeSun.color);
            }
        }

        void OnDestroy()
        {
            if (!applied) return;
            if (RenderSettings.skybox == runtimeSky) RenderSettings.skybox = previousSky;
            RenderSettings.ambientMode = previousMode;
            RenderSettings.ambientSkyColor = previousTop; RenderSettings.ambientEquatorColor = previousHorizon;
            RenderSettings.ambientGroundColor = previousBottom;
            if (runtimeSky) Destroy(runtimeSky);
        }
    }
}
