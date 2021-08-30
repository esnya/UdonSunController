
using UdonSharp;
using UnityEngine;
using VRC.Udon;
#if !COMPILER_UDONSHARP && UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UdonSharpEditor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
#endif

namespace EsnyaFactory
{
    [
        UdonBehaviourSyncMode(BehaviourSyncMode.NoVariableSync),
    ]
    public class UdonSunController : UdonSharpBehaviour
    {
        [Header("Styles")]
        public Gradient sunColor = new Gradient();
        public AnimationCurve sunIntensity = new AnimationCurve();
        [Range(0, 90)] public float culminationAngle = 55;

        [Header("Settings")]
        public float probeRenderingDelay = 0.5f;
        public bool autoSetupBeforeSave = true;

        [Header("References")]
        public Light directionalLight;
        public ReflectionProbe[] probes = { };

        [Header("Event")]
        public UdonSharpBehaviour eventTarget;
        public string eventName = "RenderProbes";

        private void Start()
        {
            SendCustomEventDelayedSeconds(nameof(RenderAllProbes), probeRenderingDelay);
        }

        public void RenderSingleProbe()
        {
            var length = probes.Length;
            if (length > 0) probes[Time.frameCount % probes.Length].RenderProbe();
        }

        public void RenderAllProbes()
        {
            foreach (var probe in probes)
            {
                if (probe) probe.RenderProbe();
            }

            if (eventTarget == null) return;
            eventTarget.SendCustomEvent(eventName);
        }
    }

#if !COMPILER_UDONSHARP && UNITY_EDITOR
    [CustomEditor(typeof(UdonSunController))]
    public class UdonSunControllerEditor : Editor
    {
        private static string SetupFromScene(UdonSunController controller, bool overrideProbeCullingMask, int probeCullingMask)
        {
            Undo.RecordObject(controller, "Setup UdonSunCotnroller");
            var rootObjects = SceneManager.GetActiveScene().GetRootGameObjects();

            var directionalLight = rootObjects.SelectMany(o => o.GetComponentsInChildren<Light>()).Where(l => l.type == LightType.Directional && l.enabled && l.lightmapBakeType == LightmapBakeType.Realtime).FirstOrDefault();
            var probes = rootObjects.SelectMany(o => o.GetComponentsInChildren<ReflectionProbe>()).ToArray();

            controller.directionalLight = directionalLight;
            controller.probes = probes;

            RenderSettings.sun = directionalLight;

            foreach (var probe in probes)
            {
                probe.mode = UnityEngine.Rendering.ReflectionProbeMode.Realtime;
                probe.refreshMode = UnityEngine.Rendering.ReflectionProbeRefreshMode.ViaScripting;

                if (overrideProbeCullingMask) probe.cullingMask = probeCullingMask;

                probe.RenderProbe();
            }

            var handles = controller.GetUdonSharpComponentsInChildren<UdonSunControllerHandle>();
            foreach (var handle in handles)
            {
                handle.SetProgramVariable(nameof(handle.controller), controller);
            }

            var errorMessage = controller.directionalLight == null ? "A Realtime DirectionalLight is required. " : handles.Length == 0 ? "A UdonSunControllerHandle is required. " : "";
            var result = errorMessage == "" ? "Done" : "Failed";
            return $"{result}: {errorMessage}{controller.probes.Length} reflection probe(s) found.";
        }

        private string setupResult;
        public bool overrideProbeCullingMask = true;
        public int probeCullingMask = 0b100_0011_1000_1001_0010_0111;
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            var controller = target as UdonSunController;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Setup", EditorStyles.boldLabel);

            overrideProbeCullingMask = EditorGUILayout.Toggle("Override Probe CullingMask", overrideProbeCullingMask);
            probeCullingMask = EditorGUILayout.MaskField(
                "ProbeCullingMask",
                probeCullingMask,
                 Enumerable.Range(0, 32).Select(LayerMask.LayerToName).ToArray()
            );

            if (GUILayout.Button("Setup From Scene")) setupResult = SetupFromScene(controller, overrideProbeCullingMask, probeCullingMask);

            if (!string.IsNullOrEmpty(setupResult))
            {
                EditorGUILayout.Space();
                using (new EditorGUILayout.VerticalScope(GUI.skin.box)) EditorGUILayout.LabelField(setupResult);
            }
        }


        [InitializeOnLoadMethod]
        static public void RegisterCallback()
        {
            EditorSceneManager.sceneSaving += (_, __) => SetupAll();
        }

        private static IEnumerable<T> GetUdonSharpComponentsInScene<T>() where T : UdonSharpBehaviour
        {
            return FindObjectsOfType<UdonBehaviour>()
                .Where(UdonSharpEditorUtility.IsUdonSharpBehaviour)
                .Select(UdonSharpEditorUtility.GetProxyBehaviour)
                .Select(u => u as T)
                .Where(u => u != null);
        }

        private static void SetupAll()
        {
            var targets = GetUdonSharpComponentsInScene<UdonSunController>();
            foreach (var target in targets)
            {
                if (target?.autoSetupBeforeSave != true) continue;

                var result = SetupFromScene(target, false, 0b100_0011_1000_1001_0010_0111);
                Debug.Log($"[{target.gameObject.name}] Auto setup {result}");

                EditorUtility.SetDirty(UdonSharpEditorUtility.GetBackingUdonBehaviour(target));
            }
        }
    }
#endif
}
