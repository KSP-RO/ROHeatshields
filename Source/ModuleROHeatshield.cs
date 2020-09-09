using UnityEngine;
using ROLib;

namespace ROHeatshields
{
    public class ModuleROHeatshield : PartModule, IPartMassModifier
    {
        private const string GroupDisplayName = "RO-Heatshields";
        private const string GroupName = "ModuleROHeatshield";

        #region KSPFields

        [KSPField(guiActiveEditor = true, guiActive = true, guiName = "Type:", groupName = GroupName, groupDisplayName = GroupDisplayName)]
        public string heatShieldType = "LEO";

        [KSPField] public float heatShieldDensity = 0.0f;
        [KSPField] public float heatShieldAblator = 0.0f;

        #endregion KSPFields

        #region Private Variables

        private ModuleAblator modAblator;
        private ModuleROTank modularPart;
        private float hsMass = 0.0f;

        #endregion Private Variables


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
                    modularPart.Fields[nameof(modularPart.currentDiameter)].uiControlEditor.onFieldChanged += OnDiameterChange;
                UpdateHeatshieldValues();
            }
        }

        #endregion Standard KSP Overrides


        #region Custom Methods

        public ModifierChangeWhen GetModuleMassChangeWhen() => ModifierChangeWhen.FIXED;
        public float GetModuleMass(float defaultMass, ModifierStagingSituation sit) => Mathf.Max(0, hsMass);

        private void OnDiameterChange(BaseField bf, object obj) => UpdateHeatshieldValues();

        public float HeatShieldMass => Mathf.Pow(modularPart.currentDiameter, 2.0f) * heatShieldDensity;
        public float HeatShieldAblator => Mathf.Round(Mathf.Pow(modularPart.currentDiameter, 2.0f) * heatShieldAblator);

        public void UpdateHeatshieldValues()
        {
            ROLLog.debug("UpdateHeatshieldValues()");
            ROLLog.debug($"HeatShieldAblator: {HeatShieldAblator}");
            ROLLog.debug($"heatShieldDensity: {heatShieldDensity}");
            ROLLog.debug($"HeatShieldMass: {HeatShieldMass}");
            part.Resources["Ablator"].maxAmount = part.Resources["Ablator"].amount = HeatShieldAblator;
            part.Resources["CharredAblator"].maxAmount = HeatShieldAblator;
            part.Resources["CharredAblator"].amount = 0;
            hsMass = HeatShieldMass;
        }

        #endregion Custom Methods
    }
}
