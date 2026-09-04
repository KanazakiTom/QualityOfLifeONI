using HarmonyLib;
using KSerialization;
using UnityEngine;

namespace QualityOfLifeONI
{
    /// <summary>
    /// Component attached to GasBottler and LiquidBottler.
    /// Manages auto-dropping stored contents when full and renders the UI side-screen toggle button.
    /// </summary>
    [SerializationConfig(MemberSerialization.OptIn)]
    public class AutoDropBottlerComponent : KMonoBehaviour, ISim1000ms, ISidescreenButtonControl
    {
        [MyCmpReq]
        private readonly Storage storage;

        [MyCmpReq]
        private readonly Building building;

        // Persist the button state in the player's save file
        [Serialize]
        public bool autoDropEnabled = true;

        public void Sim1000ms(float dt)
        {
            if (!autoDropEnabled || storage == null || storage.IsEmpty()) return;

            float currentMass = storage.MassStored();
            if (currentMass <= 0f) return;

            float targetCapacity = GetTargetCapacity();

            // Trigger auto-drop if target capacity is reached or storage is full
            if (currentMass >= targetCapacity - 0.01f || storage.IsFull())
            {
                storage.DropAll();
            }
        }

        /// <summary>
        /// Calculates target capacity based on the building's Storage slider and engine canister limits.
        /// </summary>
        private float GetTargetCapacity()
        {
            if (storage == null) return 0f;

            // Read the user slider setting directly from the Storage component
            float target = storage.capacityKg;
            IUserControlledCapacity userCapacity = storage.GetComponent<IUserControlledCapacity>();
            if (userCapacity != null)
            {
                target = userCapacity.UserMaxCapacity; // Reads the user slider setting
            }

            // Enforce ONI hard limits per bottle/canister type
            string prefabId = building != null ? building.PrefabID().Name : "";
            if (prefabId == "GasBottler")
            {
                target = Mathf.Min(target, 1000f); // Gas canisters max out at 1000 kg
            }
            else if (prefabId == "LiquidBottler")
            {
                target = Mathf.Min(target, 1000f); // Liquid bottles max out at 1000 kg
            }

            return target;
        }

        // =========================================================================
        // ISidescreenButtonControl Interface Implementation (UI Button)
        // =========================================================================

        public string SidescreenTitle => "Auto Drop Config";

        public string SidescreenButtonText => autoDropEnabled ? "AutoDrop: Enabled" : "AutoDrop: Disabled";

        public string SidescreenButtonTooltip => autoDropEnabled
            ? "Click to disable automatically dropping bottles when full."
            : "Click to enable automatically dropping bottles when full.";

        public bool SidescreenEnabled() => true;

        public bool SidescreenButtonInteractable() => true;

        public int ButtonSideScreenSortOrder() => 20;

        public int HorizontalGroupID() => -1;

        public void OnSidescreenButtonPressed()
        {
            autoDropEnabled = !autoDropEnabled;
        }

        public void SetButtonTextOverride(ButtonMenuTextOverride textOverride)
        {
        }
    }

    /// <summary>
    /// Harmony patch to attach the AutoDrop component to Gas and Liquid Bottlers on spawn.
    /// </summary>
    [HarmonyPatch(typeof(BuildingComplete), "OnSpawn")]
    public static class BottlerAutoDrop_OnSpawn_Patch
    {
        public static void Postfix(BuildingComplete __instance)
        {
            if (__instance == null) return;

            string prefabId = __instance.PrefabID().Name;

            // Target both Gas Bottler and Liquid Bottler prefabs
            if (prefabId == "GasBottler" || prefabId == "LiquidBottler")
            {
                __instance.gameObject.AddOrGet<AutoDropBottlerComponent>();
            }
        }
    }
    
    // custom: submergible Thermo Regulator
    [HarmonyPatch(typeof(AirConditionerConfig), nameof(AirConditionerConfig.CreateBuildingDef))]   
    public static class AirConditionerConfig_CreateBuildingDef_Patch
    {
        public static void Postfix(ref BuildingDef __result)
        {
            __result.Floodable = false;
        }
    }
}