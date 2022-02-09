
using UdonSharp;
using UnityEngine;
using VRC.SDK3.Components;
using VRC.SDKBase;
using VRC.Udon.Common.Interfaces;

namespace EsnyaFactory
{
    [
        DefaultExecutionOrder(1000),
        RequireComponent(typeof(VRCPickup)),
        RequireComponent(typeof(VRCObjectSync)),
        UdonBehaviourSyncMode(BehaviourSyncMode.Continuous),
    ]
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
        private AnimationCurve sunIntensity;
        private Light directionalLight;
        private int blendshapeDriveTargetIndex;
        private float culminationScaler;
        private void UpdateParameterCache()
        {
            origin = controller.transform;
            sunColor = controller.sunColor;
            fogColor = controller.fogColor;
            sunIntensity = controller.sunIntensity;
            directionalLight = controller.directionalLight;
            culminationScaler = 1.0f / Mathf.Sin(controller.culminationAngle * Mathf.Deg2Rad);

            if (blendshapeDriveTarget != null)
            {
                blendshapeDriveTargetIndex = blendshapeDriveTarget.sharedMesh.GetBlendShapeIndex(blendshapeDriveTargetName);
            }
        }

        private void ApplyUpdates()
        {
            var relativePosition = transform.position - origin.position;
            var direction = relativePosition.normalized;
            var intensity = Mathf.Clamp01((relativePosition.magnitude - minRadius) / (maxRadius - minRadius));
            var time = Mathf.Clamp01((-direction.y * culminationScaler + 1.0f) * 0.5f);

            directionalLight.transform.rotation = Quaternion.FromToRotation(-Vector3.forward, direction);;
            directionalLight.color = sunColor.Evaluate(time);
            directionalLight.intensity = sunIntensity.Evaluate(time) * intensity;

            RenderSettings.fogColor = fogColor.Evaluate(time);

            if (additionalRotationTarget != null) additionalRotationTarget.rotation = Quaternion.FromToRotation(rotationForward, direction);
            if (blendshapeDriveTarget != null) blendshapeDriveTarget.SetBlendShapeWeight(blendshapeDriveTargetIndex, intensity * 100.0f);

            controller.RenderSingleProbe();
        }

        [UdonSynced] bool updating;
        private VRCPickup pickup;
        private void Start()
        {
            if (!controller) controller = GetComponentInParent<UdonSunController>();
            pickup = (VRCPickup)GetComponent(typeof(VRCPickup));
            UpdateParameterCache();
            ApplyUpdates();
        }

        private bool localUpdating = true;
        private void Update()
        {
            if (updating) localUpdating = true;
            if (localUpdating) ApplyUpdates();
            if (!updating) localUpdating = false;
        }

        public override void OnPickup()
        {
            UpdateParameterCache();
            Networking.SetOwner(Networking.LocalPlayer, controller.gameObject);
            updating = true;
        }

        public override void OnDrop()
        {
            var relative = transform.position - origin.position;
            var radius = relative.magnitude;
            transform.position = relative.normalized * Mathf.Clamp(radius, minRadius, maxRadius) + origin.position;

            SendCustomEventDelayedSeconds(nameof(_StopUpdating), updateDelay);
            SendCustomEventDelayedSeconds(nameof(_RequestRenderProbes), controller.probeRenderingDelay);
        }

        public void _StopUpdating()
        {
            updating = pickup.IsHeld;
        }

        public void _RequestRenderProbes()
        {
            SendCustomNetworkEvent(NetworkEventTarget.All, nameof(controller.RenderAllProbes));
        }

        private bool isFirstSync = true;
        public override void OnDeserialization()
        {
            if (!isFirstSync) return;
            isFirstSync = false;
            UpdateParameterCache();
            ApplyUpdates();
        }
    }
}
