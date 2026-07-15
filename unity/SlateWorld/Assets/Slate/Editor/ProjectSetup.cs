// SLATE — one-shot project setup, runnable headless:
//   Unity.exe -batchmode -quit -projectPath ... -executeMethod Slate.Game.Editor.ProjectSetup.BuildAll
// Creates the URP pipeline assets, render settings, and the World scene.
// Idempotent: safe to run again.
using System.IO;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Slate.Game;

namespace Slate.Game.Editor
{
    public static class ProjectSetup
    {
        [MenuItem("Slate/Setup Project")]
        public static void BuildAll()
        {
            PlayerSettings.colorSpace = ColorSpace.Linear;

            // --- URP pipeline assets.
            if (!Directory.Exists("Assets/Settings")) Directory.CreateDirectory("Assets/Settings");
            var rendererData = AssetDatabase.LoadAssetAtPath<UniversalRendererData>("Assets/Settings/SlateRendererData.asset");
            if (rendererData == null)
            {
                rendererData = ScriptableObject.CreateInstance<UniversalRendererData>();
                AssetDatabase.CreateAsset(rendererData, "Assets/Settings/SlateRendererData.asset");
            }
            // Fill the internal shader references the way URP's own asset factory does
            // (idempotent: only touches nulls).
            ResourceReloader.ReloadAllNullIn(rendererData, UniversalRenderPipelineAsset.packagePath);
            EditorUtility.SetDirty(rendererData);
            var rp = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Settings/SlateURP.asset");
            if (rp == null)
            {
                rp = UniversalRenderPipelineAsset.Create(rendererData);
                AssetDatabase.CreateAsset(rp, "Assets/Settings/SlateURP.asset");
            }
            rp.supportsCameraDepthTexture = true; // the water shader reads scene depth
            rp.supportsHDR = true;
            rp.shadowDistance = 450f;
            rp.msaaSampleCount = 4;
            EditorUtility.SetDirty(rp);
            GraphicsSettings.defaultRenderPipeline = rp;
            QualitySettings.renderPipeline = rp;

            // --- The World scene.
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var camGO = new GameObject("Main Camera");
            camGO.tag = "MainCamera";
            var cam = camGO.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.Skybox;
            camGO.AddComponent<AudioListener>();
            camGO.AddComponent<CameraRig>();
            camGO.transform.position = new Vector3(760, 900, -200);
            camGO.transform.rotation = Quaternion.Euler(75, 0, 0);

            var sunGO = new GameObject("Sun");
            var sun = sunGO.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.95f, 0.84f);
            sun.intensity = 1.25f;
            sun.shadows = LightShadows.Soft;
            sunGO.transform.rotation = Quaternion.Euler(48f, -38f, 0f);

            var worldGO = new GameObject("World");
            worldGO.AddComponent<WorldRunner>();
            worldGO.AddComponent<SlateBootstrap>();

            // --- Atmosphere: warm sky, aerial-perspective fog.
            var sky = new Material(Shader.Find("Skybox/Procedural"));
            sky.SetFloat("_SunSize", 0.035f);
            sky.SetFloat("_AtmosphereThickness", 0.95f);
            sky.SetColor("_SkyTint", new Color(0.55f, 0.62f, 0.70f));
            sky.SetColor("_GroundColor", new Color(0.42f, 0.40f, 0.36f));
            sky.SetFloat("_Exposure", 1.15f);
            AssetDatabase.CreateAsset(sky, "Assets/Settings/SlateSky.mat");
            RenderSettings.skybox = sky;
            RenderSettings.sun = sun;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = 950f;
            RenderSettings.fogEndDistance = 3600f;
            RenderSettings.fogColor = new Color(0.70f, 0.74f, 0.79f);
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.52f, 0.58f, 0.68f);
            RenderSettings.ambientEquatorColor = new Color(0.42f, 0.43f, 0.41f);
            RenderSettings.ambientGroundColor = new Color(0.24f, 0.22f, 0.20f);

            if (!Directory.Exists("Assets/Scenes")) Directory.CreateDirectory("Assets/Scenes");
            EditorSceneManager.SaveScene(scene, "Assets/Scenes/World.unity");
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene("Assets/Scenes/World.unity", true) };

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Slate] Project setup complete: URP assigned, World scene saved.");
        }
    }
}
