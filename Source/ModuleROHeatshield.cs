using UnityEngine;
using ROLib;
using System;

namespace ROHeatshields
{
    public class ModuleROHeatshield : PartModule, IPartMassModifier, IPartCostModifier
    {
        private const string GroupDisplayName = "RO-Heatshields";
        private const string GroupName = "ModuleROHeatshield";

        #region KSPFields

        [KSPField(guiActiveEditor = true, guiActive = true, guiName = "Type:", groupName = GroupName, groupDisplayName = GroupDisplayName)]
        public string heatShieldType = "LEO";

        [KSPField] public float heatShieldDensity = 0.0f;
        [KSPField] public float heatShieldAblator = 0.0f;
        [KSPField] public float heatShieldBaseCost = 0.0f; // base part cost should be used here
        [KSPField] public float heatShieldAreaCost = 0.0f;
        [KSPField] public float heatShieldDiameterCost = 0.0f;

        #endregion KSPFields

        #region Private Variables

        private ModuleAblator modAblator;
        private ModuleROTank modularPart;
        private float hsMass = 0.0f;
        private float hsCost = 0.0f;
        private string ablatorResourceName;
        private string outputResourceName;

        #endregion Private Variables

        public float CurrentDiameter => modularPart?.currentDiameter ?? 0f;

        public float HeatShieldMass => Mathf.Pow(CurrentDiameter, 2.0f) * heatShieldDensity;

        public float HeatShieldAblator => Mathf.Round(Mathf.Pow(CurrentDiameter, 2.0f) * heatShieldAblator);

        // Removes base part cost to replace it with our internal calculation instead.
        // There's a heatShieldBaseCost fixed term, a diameter based linear term with coefficient HeatShieldDiameterCost,
        // and a (diameter based) quadratic term with coefficient HeatShieldAreaCost.
        public float HeatShieldCost => -part.partInfo.cost + heatShieldBaseCost + CurrentDiameter * heatShieldDiameterCost + Mathf.Pow(CurrentDiameter, 2.0f) * heatShieldAreaCost;

        #region Standard KSP Overrides

        public override void OnStartFinished(StartState state)
        {
            base.OnStartFinished(state);
            modAblator = part.FindModuleImplementing<ModuleAblator>();
            modularPart = part.FindModuleImplementing<ModuleROTank>();
            if (!(modAblator is ModuleAblator && modularPart is ModuleROTank))
            {
                ROLLog.error($"{part} Unable to find ModuleAblator or ModuleROTank modules");
                isEnabled = enabled = false;
                return;
            }
            else
            {
                if (modularPart is ModuleROTank)
                {
                    modularPart.Fields[nameof(modularPart.currentDiameter)].uiControlEditor.onFieldChanged += OnDiameterChange;
                }

                ablatorResourceName = modAblator.ablativeResource;
                outputResourceName = modAblator.outputResource;

                UpdateHeatshieldValues();
            }
        }

        #endregion Standard KSP Overrides

        #region Custom Methods

        public ModifierChangeWhen GetModuleMassChangeWhen() => ModifierChangeWhen.FIXED;
        public float GetModuleMass(float defaultMass, ModifierStagingSituation sit) => Mathf.Max(0, hsMass);

        public ModifierChangeWhen GetModuleCostChangeWhen() => ModifierChangeWhen.FIXED;
        public float GetModuleCost(float defaultCost, ModifierStagingSituation sit) => Mathf.Max(0, hsCost);

        private void OnDiameterChange(BaseField bf, object obj) => UpdateHeatshieldValues();

        public void UpdateHeatshieldValues()
        {
            if(!HighLogic.LoadedSceneIsEditor)
                return;

            ROLLog.debug($"ROHeatshield {part.name} UpdateHeatshieldValues()");
            ROLLog.debug($"HeatShieldAblator: {HeatShieldAblator}");
            ROLLog.debug($"heatShieldDensity: {heatShieldDensity}");
            ROLLog.debug($"HeatShieldMass: {HeatShieldMass}");
            ROLLog.debug($"HeatShieldCost: {HeatShieldCost}");
            hsMass = HeatShieldMass;
            hsCost = HeatShieldCost;

            if (part.Resources[ablatorResourceName] is PartResource ab)
            {
                double ratio = ab.maxAmount > 0 ? ab.amount / ab.maxAmount : 1.0;
                ab.maxAmount = HeatShieldAblator;
                ab.amount = Math.Min(ratio * ab.maxAmount, ab.maxAmount);
            }

            if (part.Resources[outputResourceName] is PartResource ca)
            {
                double ratio = ca.maxAmount > 0 ? ca.amount / ca.maxAmount : 1.0;
                ca.maxAmount = HeatShieldAblator;
                ca.amount = Math.Max(ratio * ca.maxAmount, 0);
            }

            DirtyPAW();
        }

        private void DirtyPAW()
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
