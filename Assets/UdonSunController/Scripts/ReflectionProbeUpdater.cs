
using UdonSharp;
using UdonToolkit;
using UnityEngine;

namespace EsnyaFactory.UdonSunController
{
    [CustomName("Reflection Probe Updater")]
    [HelpMessage("Updates ReflectionProbe at runtime. Currently, only the \"RenderProbe\" event is available to update the in real-time mode.")]
    public class ReflectionProbeUpdater : UdonSharpBehaviour
    {
        public ReflectionProbe reflectionProbe;
        public bool renderOnStart;

        void Start()
        {
            if (renderOnStart) RenderProbe();
        }

        public void RenderProbe()
        {
            if (reflectionProbe == null) return;
            Debug.Log($"[{gameObject.name}] ReflectionProbe rendering");
            reflectionProbe.RenderProbe();
        }
    }
}
