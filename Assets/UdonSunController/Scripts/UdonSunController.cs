
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
#if !COMPILER_UDONSHARP && UNITY_EDITOR
using UnityEngine.SceneManagement;
using System.Linq;
using UnityEditor;
using UdonSharpEditor;
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

        [Space][Header("Settings")]
        public float probeRenderingDelay = 0.5f;

        [Space][Header("References")]
        public Light directionalLight;
        public ReflectionProbe[] probes = { };

        [Space][Header("Event")]
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
            foreach (var probe in probes) probe.RenderProbe();

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
    }
#endif
}
