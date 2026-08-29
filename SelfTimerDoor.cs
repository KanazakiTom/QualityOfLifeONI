using System;
using HarmonyLib;
using KSerialization;
using UnityEngine;

namespace QualityOfLifeONI
{
    [HarmonyPatch(typeof(LogicTimeOfDaySensor), "UpdateVisualState")]
    public static class PreventSensorAnimCrash
    {
        public static bool Prefix(LogicTimeOfDaySensor __instance)
        {
            // If this cycle sensor is attached to our Door, SKIP the visual update
            if (__instance.GetComponent<SelfTimerDoor>() != null)
            {
                return false;
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(LogicTimeOfDaySensor), "UpdateLogicCircuit")]
    public static class PreventSensorLogicCrash
    {
        public static bool Prefix(LogicTimeOfDaySensor __instance)
        {
            // If this cycle sensor is attached to our Door, SKIP sending logic signals
            if (__instance.GetComponent<SelfTimerDoor>() != null)
            {
                return false;
            }
            return true;
        }
    }

    // --- REPLACE YOUR OLD SELFTIMERDOOR CLASS WITH THIS ---
    [SerializationConfig(MemberSerialization.OptIn)]
    [AddComponentMenu("KMonoBehaviour/scripts/SelfTimerDoor")]
    public class SelfTimerDoor : KMonoBehaviour
    {
        [MyCmpReq]
        private readonly Door door;

        [MyCmpReq]
        private readonly LogicTimeOfDaySensor timeSensor;

        protected override void OnSpawn()
        {
            base.OnSpawn();

            // Listen to the native timer's switch toggled event
            timeSensor.OnToggle += this.OnTimerToggled;

            // Initialize the door's state immediately upon loading/building
            OnTimerToggled(timeSensor.IsSwitchedOn);
        }

        private void OnTimerToggled(bool is_on)
        {
            // If timer is Active (Green), Door is Opened. If Inactive (Red), Door is Locked.
            Door.ControlState newState = is_on ? Door.ControlState.Opened : Door.ControlState.Locked;

            door?.QueueStateChange(newState);
        }
    }
}