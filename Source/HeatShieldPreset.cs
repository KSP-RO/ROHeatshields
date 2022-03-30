using System.Collections.Generic;

namespace ROHeatshields
{
    public class HeatShieldPreset
    {
        public static readonly Dictionary<string, HeatShieldPreset> Presets = new Dictionary<string, HeatShieldPreset>();
        public static bool Initialized { get; private set; } = false;

        [Persistent] public string name = "";
        [Persistent] public string description = "";
        [Persistent] public bool disableModAblator = false;

        // procedural heat shield parameters
        [Persistent] public float heatShieldDensity = 0.0f;
        [Persistent] public float heatShieldAblator = 0.0f;
        [Persistent] public float heatShieldBaseCost = 0.0f;
        [Persistent] public float heatShieldAreaCost = 0.0f;
        [Persistent] public float heatShieldDiameterCost = 0.0f;

        // part parameters override
        [Persistent] public float massOverride = -1;
        [Persistent] public double maxTempOverride = -1f;
        [Persistent] public double skinMaxTempOverride = -1f;
        [Persistent] public double thermalMassModifierOverride = -1f;
        [Persistent] public double skinThermalMassModifierOverride = -1f;
        [Persistent] public double skinMassPerAreaOverride = -1f;
        [Persistent] public double skinInternalConductionMultOverride = -1f;
        [Persistent] public double emissiveConstantOverride = -1f;
        [Persistent] public double heatConductivityOverride = -1f;

        // ModuleAblator overrides
        [Persistent] public string _ablativeResource;
        [Persistent] public double _lossExp;
        [Persistent] public double _lossConst;
        [Persistent] public double _pyrolysisLossFactor;
        [Persistent] public double _ablationTempThresh;
        [Persistent] public double _reentryConductivity;
        [Persistent] public bool _useNode;
        [Persistent] public string _nodeName;
        [Persistent] public float _charAlpha;
        [Persistent] public float _charMax;
        [Persistent] public float _charMin;
        [Persistent] public bool _useChar;
        [Persistent] public string _charModuleName;
        [Persistent] public string _outputResource;
        [Persistent] public double _outputMult;
        [Persistent] public double _infoTemp;
        [Persistent] public bool _usekg;
        [Persistent] public string _unitsName;
        [Persistent] public double _nominalAmountRecip;

        public string AblativeResource;
        public double? LossExp;
        public double? LossConst;
        public double? PyrolysisLossFactor;
        public double? AblationTempThresh;
        public double? ReentryConductivity;
        public bool? UseNode;
        public string NodeName;
        public float? CharAlpha;
        public float? CharMax;
        public float? CharMin;
        public bool? UseChar;
        public string CharModuleName;
        public string OutputResource;
        public double? OutputMult;
        public double? InfoTemp;
        public bool? Usekg;
        public string UnitsName;
        public double? NominalAmountRecip;

        public HeatShieldPreset(ConfigNode node)
        {
            node.TryGetValue("name", ref name);
            node.TryGetValue("description", ref description);
            node.TryGetValue("disableModAblator", ref disableModAblator);

            node.TryGetValue("heatShieldDensity", ref heatShieldDensity);
            node.TryGetValue("heatShieldAblator", ref heatShieldAblator);
            node.TryGetValue("heatShieldBaseCost", ref heatShieldBaseCost);
            node.TryGetValue("heatShieldDiameterCost", ref heatShieldDiameterCost);
            node.TryGetValue("heatShieldAreaCost", ref heatShieldAreaCost);

            node.TryGetValue("massOverride", ref massOverride);
            node.TryGetValue("maxTempOverride", ref maxTempOverride);
            node.TryGetValue("skinMaxTempOverride", ref skinMaxTempOverride);
            node.TryGetValue("thermalMassModifierOverride", ref thermalMassModifierOverride);
            node.TryGetValue("skinThermalMassModifierOverride", ref skinThermalMassModifierOverride);
            node.TryGetValue("skinMassPerAreaOverride", ref skinMassPerAreaOverride);
            node.TryGetValue("skinInternalConductionMultOverride", ref skinInternalConductionMultOverride);
            node.TryGetValue("emissiveConstantOverride", ref emissiveConstantOverride);
            node.TryGetValue("heatConductivityOverride", ref heatConductivityOverride);

            if (node.TryGetValue("ablativeResource", ref _ablativeResource))
                AblativeResource = _ablativeResource;
            if (node.TryGetValue("lossExp", ref _lossExp))
                LossExp = _lossExp;
            if (node.TryGetValue("lossConst", ref _lossConst))
                LossConst = _lossConst;
            if (node.TryGetValue("pyrolysisLossFactor", ref _pyrolysisLossFactor))
                PyrolysisLossFactor = _pyrolysisLossFactor;
            if (node.TryGetValue("ablationTempThresh", ref _ablationTempThresh))
                AblationTempThresh = _ablationTempThresh;
            if (node.TryGetValue("reentryConductivity", ref _reentryConductivity))
                ReentryConductivity = _reentryConductivity;
            if (node.TryGetValue("useNode", ref _useNode))
                UseNode = _useNode;
            if (node.TryGetValue("nodeName", ref _nodeName))
                NodeName = _nodeName;
            if (node.TryGetValue("charAlpha", ref _charAlpha))
                CharAlpha = _charAlpha;
            if (node.TryGetValue("charMax", ref _charMax))
                CharMax = _charMax;
            if (node.TryGetValue("charMin", ref _charMin))
                CharMin = _charMin;
            if (node.TryGetValue("useChar", ref _useChar))
                UseChar = _useChar;
            if (node.TryGetValue("charModuleName", ref _charModuleName))
                CharModuleName = _charModuleName;
            if (node.TryGetValue("outputResource", ref _outputResource))
                OutputResource = _outputResource;
            if (node.TryGetValue("outputMult", ref _outputMult))
                OutputMult = _outputMult;
            if (node.TryGetValue("infoTemp", ref _infoTemp))
                InfoTemp = _infoTemp;
            if (node.TryGetValue("usekg", ref _usekg))
                Usekg = _usekg;
            if (node.TryGetValue("unitsName", ref _unitsName))
                UnitsName = _unitsName;
            if (node.TryGetValue("nominalAmountRecip", ref _nominalAmountRecip))
                NominalAmountRecip = _nominalAmountRecip;
        }

        public HeatShieldPreset(string name)
        {
            this.name = name;
        }

        public static void LoadPresets()
        {
            if (Initialized && Presets.Count > 0)
                return;

            var nodes = GameDatabase.Instance.GetConfigNodes("ROHS_PRESET");
            string s = string.Empty;
            foreach (var node in nodes)
            {
                HeatShieldPreset preset = null;

                if (node.TryGetValue("name", ref s) && !string.IsNullOrEmpty(s))
                    preset = new HeatShieldPreset(node);

                if (preset != null)
                    Presets[preset.name] = preset;

                UnityEngine.Debug.Log($"[ROHeatShields] Found and loaded preset {preset.name} ");
            }

            // initialize default fallback preset
            if (!Presets.ContainsKey("default"))
                Presets["default"] = new HeatShieldPreset("default");

            Initialized = true;
        }
    }
}
