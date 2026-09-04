using HarmonyLib;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace QualityOfLifeONI
{
    #region Robo-pilot Module fixed

    // Helper to add extension methods for RoboPilotModule.cs
    public static class RoboPilotModuleExtensions
    {
        // 1. New method to check if data bank is at least 80kg or full (100kg)
        public static bool IsEnough(this RoboPilotModule module)
        {
            if (module == null) return false;
            bool isFull = module.IsFull();
            bool isEnough = module.GetDataBanksStored() >= 80f; // Fixed: use correct method
            return isFull || isEnough;
                
        }
    }

    // 1. Add Logic Output Port at Offset (0, 1)
    [HarmonyPatch(typeof(RoboPilotModuleConfig), nameof(RoboPilotModuleConfig.CreateBuildingDef))]
    public static class RoboPilotModuleConfig_CreateBuildingDef_Patch
    {
        public static void Postfix(ref BuildingDef __result)
        {
            __result.LogicOutputPorts = new List<LogicPorts.Port>
            {
                LogicPorts.Port.OutputPort(
                    LogicSwitch.PORT_ID,
                    new CellOffset(0, 1),
                    "Data Bank Signal",
                    "Sends a Green signal when Data Banks are at least 80/100kg, otherwise Red signal.",
                    "Data Banks required value not met",
                    false
                )
            };
        }
    }

    // 2. Control Automation Output Signal
    [HarmonyPatch(typeof(RoboPilotModule), "OnSpawn")]
    public static class RoboPilotModule_OnSpawn_Patch
    {
        public static void Postfix(RoboPilotModule __instance)
        {
            if (__instance == null) return;

            // Update signal whenever storage changes
            __instance.Subscribe((int)GameHashes.OnStorageChange, _ => UpdateAutomationSignal(__instance));

            ScheduleRecurringSignalUpdate(__instance);
        }

        // Method to checking logic signal every 5 seconds
        private static void ScheduleRecurringSignalUpdate(RoboPilotModule module)
        {
            if (module == null || module.gameObject == null) return;
            UpdateAutomationSignal(module);
            GameScheduler.Instance.Schedule("RoboPilotSignalUpdate", 5f, _ => ScheduleRecurringSignalUpdate(module));
        }
        public static void UpdateAutomationSignal(RoboPilotModule module)
        {
            if (module == null) return;

            LogicPorts component = module.GetComponent<LogicPorts>();
            if (component != null)
            {
                bool isEnough = module.IsEnough();
                component.SendSignal(LogicSwitch.PORT_ID, isEnough ? 1 : 0);
            }
            
        }
    }

    // 3. Override Data Bank Request Logic to ALWAYS target 100/100 kg
    [HarmonyPatch(typeof(RoboPilotModule), "RequestDataBanksForDestination")]
    public static class RoboPilotModule_RequestDataBanks_Patch
    {
        public static void Postfix(RoboPilotModule __instance)
        {
            ManualDeliveryKG delivery = __instance.GetComponent<ManualDeliveryKG>();
            Storage storage = __instance.GetComponent<Storage>();

            if (delivery != null && storage != null)
            {
                float missingMass = storage.Capacity() - storage.UnitsStored();

                if (missingMass > 0f)
                {
                    delivery.refillMass = storage.Capacity();
                }
            }
        }
    }
    #endregion

    #region Automation for Artifact Transport Module
    // --- 1. ADD LOGIC PORT TO BUILDING DEF ---
    [HarmonyPatch(typeof(ArtifactCargoBayConfig), nameof(ArtifactCargoBayConfig.CreateBuildingDef))]
    public static class ArtifactCargoBayConfig_CreateBuildingDef_Patch
    {
        public static void Postfix(BuildingDef __result)
        {
            if (__result.LogicInputPorts == null)
            {
                __result.LogicInputPorts = new List<LogicPorts.Port>();
            }

            __result.LogicInputPorts.Add(
                LogicPorts.Port.InputPort(
                    ArtifactCargoBayAutomation.PORT_ID,
                    new CellOffset(1, 0), // (y=0, x=1) center port
                    "Eject Artifact",
                    "GREEN Signal: Ejects stored artifact",
                    "RED Signal: Retains stored artifact"
                )
            );
        }
    }

    // --- 2. ATTACH COMPONENT TO PREFAB ---
    [HarmonyPatch(typeof(ArtifactCargoBayConfig), nameof(ArtifactCargoBayConfig.DoPostConfigureComplete))]
    public static class ArtifactCargoBayConfig_DoPostConfigureComplete_Patch
    {
        public static void Postfix(GameObject go)
        {
            go.AddOrGet<ArtifactCargoBayAutomation>();
        }
    }

    // --- 3. AUTOMATION COMPONENT ---
    public class ArtifactCargoBayAutomation : KMonoBehaviour
    {
        public static readonly HashedString PORT_ID = new HashedString("ArtifactCargoBayDropPort");

        [MyCmpGet]
        private readonly SingleEntityReceptacle receptacle;

        private static readonly EventSystem.IntraObjectHandler<ArtifactCargoBayAutomation> OnLogicEventDelegate =
            new EventSystem.IntraObjectHandler<ArtifactCargoBayAutomation>((component, data) => component.OnLogicEvent(data));

        protected override void OnSpawn()
        {
            base.OnSpawn();
            // Subscribe to logic port signal changes
            Subscribe((int)GameHashes.LogicEvent, OnLogicEventDelegate);
        }

        private void OnLogicEvent(object data)
        {
            if (data is LogicValueChanged logicValueChanged && logicValueChanged.portID == PORT_ID)
            {
                // Value > 0 represents a GREEN signal
                if (logicValueChanged.newValue > 0 && receptacle != null && receptacle.Occupant != null)
                {
                    // Directly triggers the "Remove" button action
                    receptacle.OrderRemoveOccupant();
                }
            }
        }
    }
    #endregion
}