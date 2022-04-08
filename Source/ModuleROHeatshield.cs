using ROLib;
using System;
using System.Collections.Generic;
using System.Linq;
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
        private bool onLoadFiredInEditor;

        [SerializeField] private string[] availablePresetNames = new string[] { "default" };

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
        public float HeatShieldCost => -origCost + HeatShieldBaseCost + CurrentDiameter * HeatShieldDiameterCost + Mathf.Pow(CurrentDiameter, 1.5f) * HeatShieldAreaCost;

        private static bool? _RP1Found = null;
        public static bool RP1Found
        {
            get
            {
                if (!_RP1Found.HasValue)
                {
                    var assembly = AssemblyLoader.loadedAssemblies.FirstOrDefault(a => a.assembly.GetName().Name == "RP0")?.assembly;
                    _RP1Found = assembly != null;
                }
                return _RP1Found.Value;
            }
        }

        #region Standard KSP Overrides

        public override void OnStart(StartState state)
        {
            HeatShieldPreset.LoadPresets();
            if (!HeatShieldPreset.Initialized)
                return;

            if (availablePresetNames.Length > 0 && HighLogic.LoadedSceneIsEditor)
            {
                // RP-1 allows selecting all configs but will run validation when trying to add the vessel to build queue
                string[] unlockedPresetsName = RP1Found ? availablePresetNames : GetUnlockedPresets(availablePresetNames);
                UpdatePresetsList(unlockedPresetsName);
                Fields[nameof(heatShieldType)].uiControlEditor.onFieldChanged =
                Fields[nameof(heatShieldType)].uiControlEditor.onSymmetryFieldChanged =
                    (bf, ob) => ApplyPreset(ActivePreset);

                if (!onLoadFiredInEditor)
                {
                    EnsureBestAvailableConfigSelected();
                }
            }

            if (ActivePreset is null)
            {
                Debug.Log("[ROHeatshields] ActivePreset is null");
                heatShieldType = "default";
            }
        }

        public override void OnLoad(ConfigNode node)
        {
            onLoadFiredInEditor = HighLogic.LoadedSceneIsEditor;

            if (node.TryGetValue("presets", ref availablePresetNames))
                Debug.Log("[ROHeatshields] available presets loaded");
        }

        public override void OnStartFinished(StartState state)
        {
            base.OnStartFinished(state);

            if (!HeatShieldPreset.Initialized)
                return;

            modAblator = part.FindModuleImplementing<ModuleAblator>();
            modularPart = part.FindModuleImplementing<ModuleROTank>();

            if (HighLogic.LoadedSceneIsEditor && modularPart is ModuleROTank)
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

            if (modularPart != null && modAblator != null && modAblator.enabled)
            {
                if (ablatorResourceName != null)
                {
                    var ab = EnsureAblatorResource(ablatorResourceName);
                    double ratio = ab.maxAmount > 0 ? ab.amount / ab.maxAmount : 1.0;
                    ab.maxAmount = HeatShieldAblator;
                    ab.amount = Math.Min(ratio * ab.maxAmount, ab.maxAmount);
                }

                if (outputResourceName != null)
                {
                    var ca = EnsureAblatorResource(outputResourceName);
                    ca.maxAmount = HeatShieldAblator;
                    ca.amount = 0;
                }
            }

            if (HighLogic.LoadedSceneIsEditor && EditorLogic.fetch?.ship != null)
                GameEvents.onEditorShipModified.Fire(EditorLogic.fetch.ship);

            UpdatePAW();

            // ModuleAblator's Start runs before this PM overrides the ablator values and will precalculate some parameters.
            // Run this precalculation again after we've finished configuring everything.
            if (HighLogic.LoadedSceneIsFlight)
                modAblator?.Start();
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
            if (p.thermalMassModifierOverride > 0)
                part.thermalMassModifier = p.thermalMassModifierOverride;
            if (p.skinThermalMassModifierOverride > 0)
                part.skinThermalMassModifier = p.skinThermalMassModifierOverride;
            if (p.skinMassPerAreaOverride > 0)
                part.skinMassPerArea = p.skinMassPerAreaOverride;
            if (p.skinInternalConductionMultOverride > 0)
                part.skinInternalConductionMult = p.skinInternalConductionMultOverride;
            if (p.skinSkinConductionMultOverride > 0)
                part.skinSkinConductionMult = p.skinSkinConductionMultOverride;
            if (p.emissiveConstantOverride > 0)
                part.emissiveConstant = p.emissiveConstantOverride;
            if (p.heatConductivityOverride > 0)
                part.heatConductivity = p.heatConductivityOverride;

            // update ModuleAblator parameters, if present and used
            if (modAblator != null && !p.disableModAblator)
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
            }

            if (modAblator != null)
            {
                if (p.AblativeResource == null || ablatorResourceName != p.AblativeResource ||
                    p.OutputResource == null || outputResourceName != p.OutputResource ||
                    p.disableModAblator)
                {
                    RemoveAblatorResources();
                }

                ablatorResourceName = p.AblativeResource;
                outputResourceName = p.OutputResource;

                modAblator.isEnabled = modAblator.enabled = !p.disableModAblator;
            }

            maxTemp = part.maxTemp;
            skinMaxTemp = part.skinMaxTemp;

            //prevent DRE from ruining everything
            if (DREHandler.Found && HighLogic.LoadedSceneIsFlight)
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
            foreach (string s in all)
            {
                if (IsConfigUnlocked(s))
                {
                    Debug.Log($"[ROHeatshields] preset {s} is unlocked");
                    unlocked.AddUnique(s);
                }
            }

            if (unlocked.Count == 0)
                unlocked.Add("default");

            return unlocked.ToArray();
        }

        public bool IsConfigUnlocked(string configName)
        {
            if (!PartUpgradeManager.Handler.CanHaveUpgrades()) return true;

            PartUpgradeHandler.Upgrade upgd = PartUpgradeManager.Handler.GetUpgrade(configName);
            if (upgd == null) return true;

            if (PartUpgradeManager.Handler.IsEnabled(configName)) return true;

            if (upgd.entryCost < 1.1 && PartUpgradeManager.Handler.IsAvailableToUnlock(configName) &&
                PurchaseConfig(upgd))
            {
                return true;
            }

            return false;
        }

        public bool PurchaseConfig(PartUpgradeHandler.Upgrade upgd)
        {
            if (Funding.CanAfford(upgd.entryCost))
            {
                Funding.Instance?.AddFunds(-upgd.entryCost, TransactionReasons.RnDPartPurchase);
                PartUpgradeManager.Handler.SetUnlocked(upgd.name, true);
                return true;
            }

            return false;
        }

        private void UpdatePresetsList(string[] options)
        {
            BaseField bf = Fields[nameof(heatShieldType)];

            if (options.Length == 0) { options = new string[] { "NONE" }; }

            var uiControlEditor = bf.uiControlEditor as UI_ChooseOption;
            uiControlEditor.display = uiControlEditor.options = options;

            Debug.Log($"[ROHeatshields] available presets on part {part.name}: " + string.Join(",", uiControlEditor.options));
        }

        private void EnsureBestAvailableConfigSelected()
        {
            if (IsConfigUnlocked(heatShieldType)) return;

            string bestConfigMatch = null;
            for (int i = availablePresetNames.IndexOf(heatShieldType) - 1; i >= 0; i--)
            {
                bestConfigMatch = availablePresetNames[i];
                if (IsConfigUnlocked(bestConfigMatch)) break;
            }

            if (bestConfigMatch != null)
            {
                heatShieldType = bestConfigMatch;
                ApplyPreset(ActivePreset);
            }
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

        private void RemoveAblatorResources()
        {
            if (ablatorResourceName != null)
            {
                part.Resources.Remove(ablatorResourceName);
            }

            if (outputResourceName != null)
            {
                part.Resources.Remove(outputResourceName);
            }

            // The part can spawn with a set of default resources.
            // If no resources have been configured through this PartModule yet then need to strip everything that the prefab has.
            if (part.Resources.Count > 0)
            {
                foreach (AvailablePart.ResourceInfo resInf in part.partInfo.resourceInfos)
                {
                    part.Resources.Remove(resInf.resourceName);
                }
            }
        }

        private PartResource EnsureAblatorResource(string name)
        {
            PartResource res = part.Resources[name];
            if (res == null)
            {
                PartResourceDefinition resDef = PartResourceLibrary.Instance.GetDefinition(name);
                if (resDef == null)
                {
                    Debug.LogError($"[ROHeatshields] Resource {name} not found!");
                    return null;
                }

                res = new PartResource(part);
                res.resourceName = name;
                res.SetInfo(resDef);
                res._flowState = true;
                res.isTweakable = resDef.isTweakable;
                res.isVisible = resDef.isVisible;
                res.hideFlow = false;
                res._flowMode = PartResource.FlowMode.Both;
                part.Resources.dict.Add(resDef.id, res);
            }

            return res;
        }

        /// <summary>
        /// Called from RP0KCT
        /// </summary>
        /// <param name="validationError"></param>
        /// <param name="canBeResolved"></param>
        /// <param name="costToResolve"></param>
        /// <returns></returns>
        public virtual bool Validate(out string validationError, out bool canBeResolved, out float costToResolve)
        {
            validationError = null;
            canBeResolved = false;
            costToResolve = 0;

            if (IsConfigUnlocked(heatShieldType)) return true;

            PartUpgradeHandler.Upgrade upgd = PartUpgradeManager.Handler.GetUpgrade(heatShieldType);
            if (PartUpgradeManager.Handler.IsAvailableToUnlock(heatShieldType))
            {
                canBeResolved = true;
                costToResolve = upgd.entryCost;
                validationError = $"purchase config {upgd.title}";
            }
            else
            {
                validationError = $"unlock tech {ResearchAndDevelopment.GetTechnologyTitle(upgd.techRequired)}";
            }

            return false;
        }

        /// <summary>
        /// Called from RP0KCT
        /// </summary>
        /// <returns></returns>
        public virtual bool ResolveValidationError()
        {
            PartUpgradeHandler.Upgrade upgd = PartUpgradeManager.Handler.GetUpgrade(heatShieldType);
            if (upgd == null) return false;

            return PurchaseConfig(upgd);
        }

        #endregion Custom Methods
    }
}
