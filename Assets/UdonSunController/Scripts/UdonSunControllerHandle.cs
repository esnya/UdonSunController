
using UdonSharp;
using UnityEngine;
using UnityEngine.UIElements;
using VRC.SDK3.Components;
using VRC.SDKBase;
using VRC.Udon.Common.Interfaces;

namespace EsnyaFactory
{
    [DefaultExecutionOrder(1000)]
    [RequireComponent(typeof(VRCPickup))]
    [RequireComponent(typeof(VRCObjectSync))]
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class UdonSunControllerHandle : UdonSharpBehaviour
    {
        [Header("Handle Settings")]
        public float maxRadius = 0.4f;
        public float minRadius = 0.2f;

        [Space, Header("Visual Settings")]
        public Transform additionalRotationTarget;
        public Vector3 rotationForward = Vector3.forward;
        public SkinnedMeshRenderer blendshapeDriveTarget;
        public string blendshapeDriveTargetName = "Intensity";

        [Space, Header("Internal Settings")]
        public float updateDelay = 1.0f;
        public UdonSunController controller;

        private Transform origin;
        private Gradient sunColor, fogColor;
        private Material[] materials;
        private string[] materialProperties;
        private Gradient[] materialColors;
        private AnimationCurve sunIntensity;
        private Light directionalLight;
        private int blendshapeDriveTargetIndex;
        private float culminationScaler;
        private VRCPickup pickup;
        private Vector3 prevPosition;
        private bool prevUpdated;
        private float lastUpdatedTime;
        private ReflectionProbe[] probes;
        private UdonSharpBehaviour eventTarget;
        private string eventName;
        private float probeRenderingDelay;

        private void UpdateParameterCache()
        {
            origin = controller.transform;
            sunColor = controller.sunColor;
            fogColor = controller.fogColor;
            materials = controller.materials;
            materialProperties = controller.materialProperties;
            materialColors = controller.materialColors;
            sunIntensity = controller.sunIntensity;
            directionalLight = controller.directionalLight;
            culminationScaler = 1.0f / Mathf.Sin(controller.culminationAngle * Mathf.Deg2Rad);
            probeRenderingDelay = controller.probeRenderingDelay;

            if (blendshapeDriveTarget != null)
            {
                blendshapeDriveTargetIndex = blendshapeDriveTarget.sharedMesh.GetBlendShapeIndex(blendshapeDriveTargetName);
            }

            probes = controller.probes;
            eventTarget = controller.eventTarget;
            eventName = controller.eventName;
        }

        private void ApplyUpdates()
        {
            var relativePosition = transform.position - origin.position;
            var direction = relativePosition.normalized;
            var intensity = Mathf.Clamp01((relativePosition.magnitude - minRadius) / (maxRadius - minRadius));
            var time = Mathf.Clamp01((-direction.y * culminationScaler + 1.0f) * 0.5f);

            directionalLight.transform.rotation = Quaternion.FromToRotation(-Vector3.forward, direction); ;
            directionalLight.color = sunColor.Evaluate(time);
            directionalLight.intensity = sunIntensity.Evaluate(time) * intensity;

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

            controller.RenderSingleProbe();
        }

        private void Start()
        {
            if (!controller) controller = GetComponentInParent<UdonSunController>();
            pickup = (VRCPickup)GetComponent(typeof(VRCPickup));
            UpdateParameterCache();
            ApplyUpdates();

            SendCustomEventDelayedSeconds(nameof(_RenderAllProbes), probeRenderingDelay);
        }

        private void Update()
        {
            var position = transform.position;
            if (position != prevPosition)
            {
                if (!prevUpdated) UpdateParameterCache();

                prevUpdated = true;
                prevPosition = position;
                lastUpdatedTime = Time.time;

                ApplyUpdates();

            }
            else if (prevUpdated)
            {
                prevUpdated = false;
                SendCustomEventDelayedSeconds(nameof(_RenderAllProbes), probeRenderingDelay);
            }
        }

        public override void OnPickup()
        {
            UpdateParameterCache();
        }

        public override void OnDrop()
        {
            var relative = transform.position - origin.position;
            var radius = relative.magnitude;
            transform.position = relative.normalized * Mathf.Clamp(radius, minRadius, maxRadius) + origin.position;
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
}
