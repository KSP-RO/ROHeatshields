﻿using ROLib;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace ROHeatshields
{
    public class ModuleROHeatshield : PartModule, IPartMassModifier, IPartCostModifier
    {
        private const string GroupDisplayName = "RO-Heatshields";
        private const string GroupName = "ModuleROHeatshield";

        #region KSPFields

        [KSPField(isPersistant = true, guiName = "Type:", guiActiveEditor = true, groupName = GroupName, groupDisplayName = GroupDisplayName), UI_ChooseOption(scene = UI_Scene.Editor)]
        public string heatShieldType = "default";
        [KSPField(guiActiveEditor = true, guiName = "Description", groupName = GroupName, groupDisplayName = GroupDisplayName)]
        public string description = "";

        [KSPField(guiActiveEditor = true, guiName = "Max Temp", guiFormat = "F0", guiUnits = "K", groupName = GroupName, groupDisplayName = GroupDisplayName)]
        public double maxTemp = 0.0f;
        [KSPField(guiActiveEditor = true, guiName = "Max Skin Temp", guiFormat = "F0", guiUnits = "K", groupName = GroupName, groupDisplayName = GroupDisplayName)]
        public double skinMaxTemp = 0.0f;

        #endregion KSPFields

        #region Private Variables

        private ModuleAblator modAblator;
        private ModuleROTank modularPart;
        private float hsMass = 0.0f;
        private float hsCost = 0.0f;
        private float origMass = 0.0f;
        private float origCost = 0.0f;
        private string ablatorResourceName;
        private string outputResourceName;
        [SerializeField] private string[] availablePresetsNames = new string[] { "default" };

        #endregion Private Variables
        HeatShieldPreset ActivePreset => HeatShieldPreset.Presets[heatShieldType];

        public float HeatShieldDensity => ActivePreset.heatShieldDensity;
        public float HeatShieldBaseCost => ActivePreset.heatShieldBaseCost;
        public float HeatShieldDiameterCost => ActivePreset.heatShieldDiameterCost;
        public float HeatShieldAreaCost => ActivePreset.heatShieldAreaCost;

        public float CurrentDiameter => modularPart?.currentDiameter ?? 0f;
        public float CurrentVScale => modularPart?.currentVScale ?? 0f;
        public float HeatShieldMass => (ActivePreset.massOverride > 0 ? -origMass + ActivePreset.massOverride : 0) + Mathf.Pow(CurrentDiameter, 2.0f) * HeatShieldDensity;
        // Ablator = Round( D² * density * (vScale + 2)/2 )
        public float HeatShieldAblator => Mathf.Round(Mathf.Pow(CurrentDiameter, 2.0f) * ActivePreset.heatShieldAblator * ((CurrentVScale + 2) / 2) * 10f) / 10f;

        // Removes base part cost to replace it with our internal calculation instead.
        // There's a heatShieldBaseCost fixed term, a diameter based linear term with coefficient HeatShieldDiameterCost,
        // and a (diameter based) quadratic term with coefficient HeatShieldAreaCost.
        public float HeatShieldCost => -origCost + HeatShieldBaseCost + CurrentDiameter * HeatShieldDiameterCost + Mathf.Pow(CurrentDiameter, 2.0f) * HeatShieldAreaCost;

        #region Standard KSP Overrides

        public override void OnStart(StartState state)
        {
            HeatShieldPreset.LoadPresets();
            if (!HeatShieldPreset.Initialized)
                return;

            if (availablePresetsNames.Length > 0 && HighLogic.LoadedSceneIsEditor)
            {
                string[] unlockedPresetsName = GetUnlockedPresets(availablePresetsNames);
                UpdatePresetsList(unlockedPresetsName);
                Fields[nameof(heatShieldType)].uiControlEditor.onFieldChanged =
                Fields[nameof(heatShieldType)].uiControlEditor.onSymmetryFieldChanged =
                    (bf, ob) => ApplyPreset(ActivePreset);
            }

            if (ActivePreset is null)
            {
                Debug.Log("[ROHeatshields] ActivePreset is null");
                heatShieldType = "default";
            }
        }

        public override void OnLoad(ConfigNode node)
        {
            if (node.TryGetValue("presets", ref availablePresetsNames))
                Debug.Log("[ROHeatshields] available presets loaded");
        }

        public override void OnStartFinished(StartState state)
        {
            base.OnStartFinished(state);

            if (!HeatShieldPreset.Initialized)
                return;

            modAblator = part.FindModuleImplementing<ModuleAblator>();
            modularPart = part.FindModuleImplementing<ModuleROTank>();

            if (modularPart is ModuleROTank)
            {
                modularPart.Fields[nameof(modularPart.currentDiameter)].uiControlEditor.onFieldChanged += (bf, ob) => UpdateHeatshieldValues();
                modularPart.Fields[nameof(modularPart.currentVScale)].uiControlEditor.onFieldChanged += (bf, ob) => UpdateHeatshieldValues();
                modularPart.Fields[nameof(modularPart.currentDiameter)].uiControlEditor.onSymmetryFieldChanged += (bf, ob) => UpdateHeatshieldValues();
                modularPart.Fields[nameof(modularPart.currentVScale)].uiControlEditor.onSymmetryFieldChanged += (bf, ob) => UpdateHeatshieldValues();
            }

            origCost = part.partInfo.cost + 0.01f;  // sum eps to avoid 0 cost
            origMass = part.prefabMass + 0.00001f;  // sum eps to avoid 0 mass

            ApplyPreset(ActivePreset);
        }

        public ModifierChangeWhen GetModuleMassChangeWhen() => ModifierChangeWhen.FIXED;
        public float GetModuleMass(float defaultMass, ModifierStagingSituation sit) => Mathf.Max(-origMass, hsMass);

        public ModifierChangeWhen GetModuleCostChangeWhen() => ModifierChangeWhen.FIXED;
        public float GetModuleCost(float defaultCost, ModifierStagingSituation sit) => Mathf.Max(-origCost, hsCost);

        #endregion Standard KSP Overrides

        #region Custom Methods

        public void UpdateHeatshieldValues()
        {
            hsMass = HeatShieldMass;
            hsCost = HeatShieldCost;

            if(modularPart != null && modAblator != null && modAblator.enabled)
            {
                if (ablatorResourceName != null && part.Resources.Contains(ablatorResourceName))
                {
                    var ab = part.Resources[ablatorResourceName];
                    double ratio = ab.maxAmount > 0 ? ab.amount / ab.maxAmount : 1.0;
                    ab.maxAmount = HeatShieldAblator;
                    ab.amount = Math.Min(ratio * ab.maxAmount, ab.maxAmount);
                }
                else
                    Debug.Log("[ROHeatshields] " + (ablatorResourceName != null
                        ? $"Resource {ablatorResourceName} not found!"
                        : "Ablator Resource is null!"));

                if (outputResourceName != null && part.Resources.Contains(outputResourceName))
                {
                    var ca = part.Resources[outputResourceName];
                    double ratio = ca.maxAmount > 0 ? ca.amount / ca.maxAmount : 1.0;
                    ca.maxAmount = HeatShieldAblator;
                    ca.amount = Math.Max(ratio * ca.maxAmount, 0);
                }
                else
                    Debug.Log("[ROHeatshields] " + (outputResourceName != null
                        ? $"Resource {outputResourceName} not found!"
                        : "Output Resource is null!"));
            }

            if (HighLogic.LoadedSceneIsEditor && EditorLogic.fetch?.ship != null)
                GameEvents.onEditorShipModified.Fire(EditorLogic.fetch.ship);

            UpdatePAW();
        }

        public void ApplyPreset(HeatShieldPreset p)
        {
            if (p is null)
            {
                Debug.Log("[ROHeatshields] invalid preset");
                return;
            }

            ResetPartToOriginal();

            if (p.maxTempOverride > 0)
                part.maxTemp = p.maxTempOverride;
            if (p.skinMaxTempOverride > 0)
                part.skinMaxTemp = p.skinMaxTempOverride;

            // update ModuleAblator parameters, if present and used
            if(modAblator != null && !p.disableModAblator)
            {
                if (!string.IsNullOrWhiteSpace(p.AblativeResource))
                    modAblator.ablativeResource = p.AblativeResource;
                if (!string.IsNullOrWhiteSpace(p.OutputResource))
                    modAblator.outputResource = p.OutputResource;

                if (!string.IsNullOrWhiteSpace(p.NodeName))
                    modAblator.nodeName = p.NodeName;
                if (!string.IsNullOrWhiteSpace(p.CharModuleName))
                    modAblator.charModuleName = p.CharModuleName;
                if (!string.IsNullOrWhiteSpace(p.UnitsName))
                    modAblator.unitsName = p.UnitsName;

                if (p.LossExp.HasValue)
                    modAblator.lossExp = p.LossExp.Value;
                if (p.LossConst.HasValue)
                    modAblator.lossConst = p.LossConst.Value;
                if (p.PyrolysisLossFactor.HasValue)
                    modAblator.pyrolysisLossFactor = p.PyrolysisLossFactor.Value;
                if (p.AblationTempThresh.HasValue)
                    modAblator.ablationTempThresh = p.AblationTempThresh.Value;
                if (p.ReentryConductivity.HasValue)
                    modAblator.reentryConductivity = p.ReentryConductivity.Value;
                if (p.UseNode.HasValue)
                    modAblator.useNode = p.UseNode.Value;
                if (p.CharAlpha.HasValue)
                    modAblator.charAlpha = p.CharAlpha.Value;
                if (p.CharMax.HasValue)
                    modAblator.charMax = p.CharMax.Value;
                if (p.CharMin.HasValue)
                    modAblator.charMin = p.CharMin.Value;
                if (p.UseChar.HasValue)
                    modAblator.useChar = p.UseChar.Value;
                if (p.OutputMult.HasValue)
                    modAblator.outputMult = p.OutputMult.Value;
                if (p.InfoTemp.HasValue)
                    modAblator.infoTemp = p.InfoTemp.Value;
                if (p.Usekg.HasValue)
                    modAblator.usekg = p.Usekg.Value;
                if (p.NominalAmountRecip.HasValue)
                    modAblator.nominalAmountRecip = p.NominalAmountRecip.Value;

                ablatorResourceName = modAblator.ablativeResource;
                outputResourceName = modAblator.outputResource;
            }

            if(modAblator != null)
                modAblator.isEnabled = modAblator.enabled = !p.disableModAblator;

            maxTemp = part.maxTemp;
            skinMaxTemp = part.skinMaxTemp;

            //prevent DRE from ruining everything
            if(DREHandler.Found && HighLogic.LoadedSceneIsFlight)
                DREHandler.SetOperationalTemps(part, maxTemp, skinMaxTemp);

            if (!string.IsNullOrEmpty(p.description))
            {
                Fields[nameof(description)].guiActiveEditor = true;
                description = p.description;
            }
            else
                Fields[nameof(description)].guiActiveEditor = false;

            Debug.Log($"[ROHeatshields] loaded preset {p.name} for part {part.name}");
            UpdateHeatshieldValues();
        }

        public void ResetPartToOriginal()
        {
            Part prefab = part.partInfo.partPrefab;

            part.maxTemp = prefab.maxTemp;
            part.skinMaxTemp = prefab.skinMaxTemp;

            if (modAblator is null)
                return;

            ConfigNode[] moduleNodes = part.partInfo.partConfig.GetNodes("MODULE");
            int i = part.Modules.IndexOf(modAblator);
            string moduleName = string.Empty;
            if (i < moduleNodes.Length && moduleNodes[i].TryGetValue("name", ref moduleName) && moduleName == "ModuleAblator")
            {
                modAblator.Load(moduleNodes[i]);
            }
        }

        public string[] GetUnlockedPresets(string[] all)
        {
            if (HighLogic.CurrentGame.Mode != Game.Modes.CAREER &&
                HighLogic.CurrentGame.Mode != Game.Modes.SCIENCE_SANDBOX)
            {
                Debug.Log($"[ROHeatshields] All presets unlocked");
                return all;
            }

            var unlocked = new List<string>();
            foreach (var s in all)
            {
                if (PartUpgradeManager.Handler.GetUpgrade(s) is null || PartUpgradeManager.Handler.IsEnabled(s))
                {
                    Debug.Log($"[ROHeatshields] preset {s} is unlocked");
                    unlocked.AddUnique(s);
                }
            }

            if(unlocked.Count == 0)
                unlocked.Add("default");

            return unlocked.ToArray();
        }

        private void UpdatePresetsList(string[] options)
        {
            BaseField bf = Fields[nameof(heatShieldType)];

            if (options.Length == 0) { options = new string[] { "NONE" }; }

            var uiControlEditor = bf.uiControlEditor as UI_ChooseOption;
            uiControlEditor.display = uiControlEditor.options = options;

            Debug.Log($"[ROHeatshields] available presets on part {part.name}: " + string.Join(",", uiControlEditor.options));
        }

        public void UpdatePAW()
        {
            foreach (UIPartActionWindow window in UIPartActionController.Instance.windows)
            {
                if (window.part == this.part)
                {
                    window.displayDirty = true;
                }
            }
        }

        #endregion Custom Methods
    }
}
