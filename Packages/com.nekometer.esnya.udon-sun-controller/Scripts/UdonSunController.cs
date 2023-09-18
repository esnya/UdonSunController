
using UdonSharp;
using UnityEngine;
using VRC.Udon;
using VRC.SDK3.Components;
#if !COMPILER_UDONSHARP && UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UdonSharpEditor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using VRC.SDKBase.Editor.BuildPipeline;
#endif

namespace EsnyaFactory
{
    [RequireComponent(typeof(VRCPickup))]
    [RequireComponent(typeof(VRCObjectSync))]
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class UdonSunController : UdonSharpBehaviour
    {
        [Header("Styles")]
        public Gradient sunColor = new Gradient();
        public AnimationCurve sunIntensity = new AnimationCurve();
        [Min(0)] public float minimumSunIntensity = 0.001f;
        [Range(0, 90)] public float culminationAngle = 55;
        public Gradient fogColor = new Gradient();
        public Material[] materials = { };
        public string[] materialProperties = { };
        public Gradient[] materialColors = { };

        [Header("Handle Settings")]
        public Transform origin;
        public float maxRadius = 0.4f;
        public float minRadius = 0.2f;

        [Header("Visual Settings")]
        public Transform additionalRotationTarget;
        public Vector3 rotationForward = Vector3.forward;
        public SkinnedMeshRenderer blendshapeDriveTarget;
        public string blendshapeDriveTargetName = "Intensity";

        [Header("Settings")]
        public float probeRenderingDelay = 0.5f;
        public bool autoSetupBeforeSave = true;

        [Header("References")]
        public Light directionalLight;
        public ReflectionProbe[] probes = { };

        [Header("Event")]
        public UdonSharpBehaviour eventTarget;
        public string eventName = "RenderProbes";

        [Header("Setup")]
        public bool overrideProbeCullingMask = true;
        public LayerMask probeCullingMask = 0b100_0011_1000_1001_0010_0111;

        private bool prevUpdated;
        private float culminationScaler;
        private float lastUpdatedTime;
        private int blendshapeDriveTargetIndex;
        private Vector3 prevPosition;
        private VRCPickup pickup;

        private void Start()
        {
            pickup = (VRCPickup)GetComponent(typeof(VRCPickup));

            if (!origin) origin = transform.parent;

            culminationScaler = 1.0f / Mathf.Sin(culminationAngle * Mathf.Deg2Rad);

            if (blendshapeDriveTarget != null)
            {
                blendshapeDriveTargetIndex = blendshapeDriveTarget.sharedMesh.GetBlendShapeIndex(blendshapeDriveTargetName);
            }

            transform.position = origin.position - directionalLight.transform.forward * maxRadius;

            _ApplyUpdates();
            _ResetPosition();

            SendCustomEventDelayedSeconds(nameof(_RenderAllProbes), probeRenderingDelay);
        }

        private void Update()
        {
            var position = transform.position;
            if (position != prevPosition)
            {
                prevUpdated = true;
                prevPosition = position;
                lastUpdatedTime = Time.time;

                _ApplyUpdates();
                _RenderSingleProbe();
            }
            else if (prevUpdated)
            {
                prevUpdated = false;
                SendCustomEventDelayedSeconds(nameof(_RenderAllProbes), probeRenderingDelay);
            }
        }

        public override void OnDrop()
        {
            _ResetPosition();
        }

        public void _ResetPosition()
        {
            var relative = transform.position - origin.position;
            var radius = relative.magnitude;
            transform.position = relative.normalized * Mathf.Clamp(radius, minRadius, maxRadius) + origin.position;
        }

        public void _ApplyUpdates()
        {
            var relativePosition = transform.position - origin.position;
            var direction = relativePosition.normalized;
            var intensity = Mathf.Clamp01((relativePosition.magnitude - minRadius) / (maxRadius - minRadius));
            var time = Mathf.Clamp01((-direction.y * culminationScaler + 1.0f) * 0.5f);

            directionalLight.transform.rotation = Quaternion.FromToRotation(-Vector3.forward, direction); ;
            directionalLight.color = sunColor.Evaluate(time);
            directionalLight.intensity = Mathf.Max(sunIntensity.Evaluate(time) * intensity, minimumSunIntensity);

            RenderSettings.fogColor = fogColor.Evaluate(time);

            if (materials != null)
            {
                for (var i = 0; i < materials.Length; i++)
                {
                    var material = materials[i];
                    var materialColor = materialColors[i];
                    if (!material) continue;
                    material.SetColor(materialProperties[i], materialColor.Evaluate(time));
                }
            }

            if (additionalRotationTarget != null) additionalRotationTarget.rotation = Quaternion.FromToRotation(rotationForward, direction);
            if (blendshapeDriveTarget != null) blendshapeDriveTarget.SetBlendShapeWeight(blendshapeDriveTargetIndex, intensity * 100.0f);
        }

        public void _RenderSingleProbe()
        {
            var length = probes.Length;
            if (length > 0) probes[Time.frameCount % probes.Length].RenderProbe();
        }

        public void _RenderAllProbes()
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
        private static string SetupFromScene(UdonSunController controller)
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

                if (controller.overrideProbeCullingMask) probe.cullingMask =controller. probeCullingMask;

                probe.RenderProbe();
            }

            EditorUtility.SetDirty(controller);
            var errorMessage = directionalLight == null ? "A Realtime DirectionalLight is required. " : string.Empty;
            var result = errorMessage == "" ? "Done" : "Failed";
            return $"{result}: {errorMessage}{probes.Length} reflection probe(s) found.";
        }

        private string setupResult;
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            var controller = target as UdonSunController;

            if (GUILayout.Button("Setup From Scene")) setupResult = SetupFromScene(controller);

            if (!string.IsNullOrEmpty(setupResult))
            {
                EditorGUILayout.Space();
                using (new EditorGUILayout.VerticalScope(GUI.skin.box)) EditorGUILayout.LabelField(setupResult);
            }
        }

        private static IEnumerable<T> GetComponentsInScene<T>() where T : UdonSharpBehaviour
        {
            return FindObjectsOfType<UdonBehaviour>()
                .Where(UdonSharpEditorUtility.IsUdonSharpBehaviour)
                .Select(UdonSharpEditorUtility.GetProxyBehaviour)
                .Select(u => u as T)
                .Where(u => u != null);
        }

        private static void SetupAll()
        {
            var targets = GetComponentsInScene<UdonSunController>();
            foreach (var target in targets)
            {
                if (target?.autoSetupBeforeSave != true) continue;

                var result = SetupFromScene(target);
                Debug.Log($"[{target.gameObject.name}] Auto setup {result}");
            }
        }

        [InitializeOnLoadMethod]
        public static void RegisterCallbacks()
        {
            EditorApplication.playModeStateChanged += (PlayModeStateChange e) => {
                if (e == PlayModeStateChange.EnteredPlayMode) SetupAll();
            };
        }

        public class BuildCallback : Editor, IVRCSDKBuildRequestedCallback
        {
            public int callbackOrder => 10;

            public bool OnBuildRequested(VRCSDKRequestedBuildType requestedBuildType)
            {
                SetupAll();
                return true;
            }
        }
    }
#endif
}
