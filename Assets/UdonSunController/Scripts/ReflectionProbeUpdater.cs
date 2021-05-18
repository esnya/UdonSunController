
using UdonSharp;
using UdonToolkit;
using UnityEngine;

namespace EsnyaFactory.UdonSunController
{
    [
        CustomName("Reflection Probe Updater"),
        HelpMessage("Updates ReflectionProbe at runtime. Currently, only the \"RenderProbe\" event is available to update the in real-time mode."),
        RequireComponent(typeof(ReflectionProbe)),
    ]
    public class ReflectionProbeUpdater : UdonSharpBehaviour
    {
        public bool renderOnStart;
        private ReflectionProbe reflectionProbe;

        private void Start()
        {
            reflectionProbe = GetComponent<ReflectionProbe>();
            if (renderOnStart) RenderProbe();
        }

        public void RenderProbe()
        {
            Debug.Log($"[{gameObject.name}] ReflectionProbe rendering");
            reflectionProbe.RenderProbe();
        }
    }
}
