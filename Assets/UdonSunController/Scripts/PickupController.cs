using UdonSharp;
using UdonToolkit;
using UnityEngine;
using VRC.Udon.Common.Interfaces;

namespace EsnyaFactory.UdonSunController
{
    [CustomName("Pickup Controller")]
    [HelpMessage("Enhancement VRC_Pickup such as relay events or expose Respawn event.")]
    public class PickupController : UdonSharpBehaviour
    {
        #region Public Variables
        [SectionHeader("Respawn")][HelpBox("Set null to use initial world transform")][UTEditor]
        public Transform respawnTarget;
        public bool respawnOnDrop = false;

        [Space]

        [SectionHeader("OnPickup")][UTEditor]
        public bool fireOnPickup;

        [HideIf("@!fireOnPickup")][UTEditor]
        public bool onPickupNetworked;

        [HideIf("@!fireOnPickup")][UTEditor]
        public NetworkEventTarget onPickupNetworkTarget;

        [HideIf("@!fireOnPickup")][ListView("OnPickup Target List")][UTEditor]
        public UdonSharpBehaviour[] onPickupTargets;
        [ListView("OnPickup Target List")][Popup("behaviour", "onPickupTargets", true)][UTEditor]
        public string[] onPickupEvents;

        [Space]

        [SectionHeader("OnDrop")][UTEditor]
        public bool fireOnDrop;

        [HideIf("@!fireOnDrop")][UTEditor]
        public bool onDropNetworked;

        [HideIf("@!fireOnDrop")][UTEditor]
        public NetworkEventTarget onDropNetworkTarget;

        [HideIf("@!fireOnDrop")][ListView("OnDrop Target List")][UTEditor]
        public UdonSharpBehaviour[] onDropTargets;
        [ListView("OnDrop Target List")][Popup("behaviour", "onDropTargets", true)][UTEditor]
        public string[] onDropEvents;


        #endregion

        #region Private Variables
        Vector3 initialPosition;
        Quaternion initialRotation;
        #endregion


        #region Unity Events
        void Start()
        {
            initialPosition = transform.position;
            initialRotation = transform.rotation;
        }
        #endregion

        #region VRChat Events
        public override void OnPickup()
        {
            if (fireOnPickup) BroadcastCustomEvent(onPickupTargets, onPickupEvents, onPickupNetworked, onPickupNetworkTarget);
        }

        public override void OnDrop()
        {
            if (respawnOnDrop) Respawn();
            if (fireOnDrop) BroadcastCustomEvent(onDropTargets, onDropEvents, onDropNetworked, onDropNetworkTarget);
        }
        #endregion

        #region Public Events
        public void Respawn()
        {
            if (respawnTarget != null)
            {
                transform.position = respawnTarget.position;
                transform.rotation = respawnTarget.rotation;
            }
            else
            {
                transform.position = initialPosition;
                transform.rotation = initialRotation;
            }
        }
        #endregion

        #region Logics
        void BroadcastCustomEvent(UdonSharpBehaviour[] targets, string[] events, bool networked, NetworkEventTarget target)
        {
            if (targets == null || events == null) return;

            var length = Mathf.Min(targets.Length, events.Length);

            for (var i = 0; i < length; i++) {
                if (networked) targets[i].SendCustomNetworkEvent(target, events[i]);
                else targets[i].SendCustomEvent(events[i]);
            }
        }
        #endregion
    }
}
