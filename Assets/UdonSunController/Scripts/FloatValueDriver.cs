using UdonSharp;
using UdonToolkit;
using UnityEngine;

#if !COMPILER_UDONSHARP && UNITY_EDITOR
using System.Linq;
using UnityEditor;
#endif

namespace EsnyaFactory.UdonSunController
{
    [CustomName("Float Value Driver")]
    [HelpMessage("Drives float parameters of animators by one float value calculated from scene.")]
    public class FloatValueDriver : UdonSharpBehaviour
    {
        #region Public Variables
        [SectionHeader("Value Calculation Mode")]
        [Popup("GetModeOptions")][OnValueChanged("OnModeStringChanged")][HelpBox("")][UTEditor]
        public string modeString;
        [HideInInspector] public int mode;

        [Space]

        [SectionHeader("Value Sources")]
        [HideIf("HideTransformSource")][UTEditor]
        public Transform sourceTransform;
        [HideIf("HideTransformOrigin")][UTEditor]
        public Transform transformOrigin;
        [HideIf("HideLocalVector")][UTEditor]
        public Vector3 localVector;
        [HideIf("HideWorldVector")][UTEditor]
        public Vector3 worldVector;
        [HideIf("HideAxisVector")][UTEditor]
        public Vector3 axisVector;

        [Space]
        [SectionHeader("Value Transform")][UTEditor]
        public float valueMultiplier = 1;
        public float valueBias = 0;
        public bool clampValue;
        [HideIf("@!clampValue")][UTEditor]
        public float minValue = 0;
        [HideIf("@!clampValue")][UTEditor]
        public float maxValue = 1;

        [Space]
        [SectionHeader("Drive Targets")][UTEditor]
        public bool driveAnimatorParameters ;

        [HideIf("@!driveAnimatorParameters")][ListView("Target Animators")][UTEditor]
        public Animator[] targetAnimators;
        [ListView("Target Animators")]
        [Popup("GetTargetAnimatorParameters")]
        [UTEditor]
        public string[] targetAnimatorParameters;
        #endregion

        #region Unity Events
        void Update()
        {
            var value = (CalculateValue() + valueBias) * valueMultiplier;

            if (clampValue)
            {
                value = Mathf.Clamp(value, minValue, maxValue);
            }

            Drive(value);
        }
        #endregion

        #region Drivers
        void Drive(float value)
        {
            if (driveAnimatorParameters) DriveAnimatorParameters(value);
        }

        void DriveAnimatorParameters(float value)
        {
            if (targetAnimators == null || targetAnimatorParameters == null)
            {
                Debug.LogError($"[{nameof(FloatValueDriver)}({gameObject.name})] targetAnimators and targetAnimatorParameters is requried.");
                return;
            }

            var length = Mathf.Min(targetAnimators.Length, targetAnimatorParameters.Length);
            for (int i = 0; i < length; i++)
            {
                targetAnimators[i].SetFloat(targetAnimatorParameters[i], value);
            }
        }
        #endregion

        #region Calculators
        float CalculateValue()
        {
            switch (mode)
            {
                case 0:
                    return DirectionInnerProduct();
                case 1:
                    return PositionInnerProduct();
                default:
                    Debug.LogError($"[{nameof(FloatValueDriver)}({gameObject.name})] Invalid calculation mode: {mode}.");
                    return 0;
            }
        }

        float DirectionInnerProduct()
        {
            if (sourceTransform == null) {
                Debug.LogError($"[{nameof(FloatValueDriver)}({gameObject.name})] sourceTransform is requried.");
                return 0;
            }

            return Vector3.Dot(sourceTransform.rotation * localVector, worldVector);
        }

        float PositionInnerProduct()
        {
            if (sourceTransform == null)
            {
                Debug.LogError($"[{nameof(FloatValueDriver)}({gameObject.name})] sourceTransform is requried.");
                return 0;
            }
            if (transformOrigin == null)
            {
                Debug.LogError($"[{nameof(FloatValueDriver)}({gameObject.name})] transformOrigin is requried.");
                return 0;
            }

            return Vector3.Dot(transformOrigin.InverseTransformPoint(sourceTransform.position), axisVector);
        }
        #endregion

        #region Editor Utilities
#if !COMPILER_UDONSHARP && UNITY_EDITOR
        public string[] GetModeOptions()
        {
            return new [] {"Direction Inner Product", "Position Inner Product"};
        }

        public void OnModeStringChanged(SerializedObject o, SerializedProperty prop)
        {
            o.FindProperty("mode").intValue = GetModeOptions().Select((s, i) => (s, i)).FirstOrDefault(t => t.Item1 == prop.stringValue).Item2;
        }

        public bool HideTransformSource()
        {
            switch (mode)
            {
                case 0:
                case 1:
                    return false;
                default:
                    return true;
            }
        }

        public bool HideTransformOrigin()
        {
            switch (mode)
            {
                case 1:
                    return false;
                default:
                    return true;
            }
        }

        public bool HideLocalVector()
        {
            switch (mode)
            {
                case 0:
                    return false;
                default:
                    return true;
            }
        }

        public bool HideWorldVector()
        {
            switch (mode)
            {
                case 0:
                    return false;
                default:
                    return true;
            }
        }


        public bool HideAxisVector()
        {
            switch (mode)
            {
                case 1:
                    return false;
                default:
                    return true;
            }
        }

        public string[] GetTargetAnimatorParameters(SerializedProperty prop)
        {
            var animator = prop.objectReferenceValue as Animator;
            if (animator == null) return new string[] {};

            return animator.parameters.Where(p => p.type == AnimatorControllerParameterType.Float).Select(p => p.name).ToArray();
        }
#endif
        #endregion
    }
}
