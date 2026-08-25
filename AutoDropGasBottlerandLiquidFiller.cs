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
        private Storage storage;

        // Persist the button state in the player's save file
        [Serialize]
        public bool autoDropEnabled = true;

        public void Sim1000ms(float dt)
        {
            if (!autoDropEnabled || storage == null || storage.IsEmpty()) return;

            // Trigger drop as soon as the storage hits maximum capacity
            if (storage.IsFull() || storage.RemainingCapacity() <= 0.01f)
            {
                storage.DropAll();
            }
        }

        // =========================================================================
        // ISidescreenButtonControl Interface Implementation (UI Button)
        // =========================================================================

        // FIX FOR CS8702: Satisfies old .NET 4.8 runtime interface bindings
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

        // FIX FOR CS0535: Implements the newly added method required by the game engine update
        public void SetButtonTextOverride(ButtonMenuTextOverride textOverride)
        {
            // Left empty as no runtime text re-routing is required
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
}