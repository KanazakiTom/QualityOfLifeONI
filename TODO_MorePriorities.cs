using System;
using System.Collections.Generic;
using HarmonyLib;
using Klei.AI;
using STRINGS;
using UnityEngine;

namespace QualityOfLifeONI
{
    // Define UI Strings following ONI's localization structure
    public static class NEWSTRINGS
    {
        public static class NEWDUPLICANTS
        {
            public static class NEWCHOREGROUPS
            {
                public class ROBOPILOT
                {
                    public static LocString NAME = "Robo-Pilot";
                    public static LocString DESC = "Supply Data Banks to the Robo-Piloted Module.";
                }

                public class ATMOSUIT
                {
                    public static LocString NAME = "Atmo Suit";
                    public static LocString DESC = "Supply Atmo Suits to Atmo Suit Docks.";
                }
            }
        }
    }

    // --- 1. CREATE AND POSITION NEW CHOREGROUPS ---
    [HarmonyPatch(typeof(Database.ChoreGroups), MethodType.Constructor, new Type[] { typeof(ResourceSet) })]
    public static class Database_ChoreGroups_Constructor_Patch
    {
        public static void Postfix(Database.ChoreGroups __instance)
        {
            // Create Robo-Pilot ChoreGroup
            ChoreGroup roboPilotGroup = new ChoreGroup(
                "RoboPilot",
                NEWSTRINGS.NEWDUPLICANTS.NEWCHOREGROUPS.ROBOPILOT.NAME,
                Db.Get().Attributes.Strength,
                "icon_errand_supply",
                1
            );
            ModInit.RoboPilotChoreGroup = __instance.Add(roboPilotGroup);

            // Create Atmo Suit ChoreGroup
            ChoreGroup atmoSuitGroup = new ChoreGroup(
                "AtmoSuit",
                NEWSTRINGS.NEWDUPLICANTS.NEWCHOREGROUPS.ATMOSUIT.NAME,
                Db.Get().Attributes.Strength,
                "icon_errand_supply",
                1
            );
            ModInit.AtmoSuitChoreGroup = __instance.Add(atmoSuitGroup);

            // Reorder ChoreGroups in resources list to place them between "Supplying" (Hauling) and "Storing" (Storage)
            int haulingIndex = __instance.resources.FindIndex(cg => cg.Id == "Hauling");
            if (haulingIndex != -1)
            {
                __instance.resources.Remove(ModInit.RoboPilotChoreGroup);
                __instance.resources.Remove(ModInit.AtmoSuitChoreGroup);

                __instance.resources.Insert(haulingIndex + 1, ModInit.RoboPilotChoreGroup);
                __instance.resources.Insert(haulingIndex + 2, ModInit.AtmoSuitChoreGroup);
            }
        }
    }

    // --- 2. CREATE MATCHING CHORETYPES ---
    [HarmonyPatch(typeof(Database.ChoreTypes), MethodType.Constructor, new Type[] { typeof(ResourceSet) })]
    public static class Database_ChoreTypes_Constructor_Patch
    {
        public static void Postfix(Database.ChoreTypes __instance)
        {
            ModInit.SupplyRoboPilotChoreType = __instance.Add(new ChoreType(
                "SupplyRoboPilot",
                __instance,
                new string[] { "RoboPilot" }, // Group assigned via string array (Arg 3)
                "",
                NEWSTRINGS.NEWDUPLICANTS.NEWCHOREGROUPS.ROBOPILOT.NAME,
                NEWSTRINGS.NEWDUPLICANTS.NEWCHOREGROUPS.ROBOPILOT.DESC,
                NEWSTRINGS.NEWDUPLICANTS.NEWCHOREGROUPS.ROBOPILOT.NAME,
                new Tag[0], // Interrupt exclusions (Arg 8)
                10000,
                10000
            ));

            ModInit.SupplyAtmoSuitChoreType = __instance.Add(new ChoreType(
                "SupplyAtmoSuit",
                __instance,
                new string[] { "AtmoSuit" }, // Group assigned via string array (Arg 3)
                "",
                NEWSTRINGS.NEWDUPLICANTS.NEWCHOREGROUPS.ATMOSUIT.NAME,
                NEWSTRINGS.NEWDUPLICANTS.NEWCHOREGROUPS.ATMOSUIT.DESC,
                NEWSTRINGS.NEWDUPLICANTS.NEWCHOREGROUPS.ATMOSUIT.NAME,
                new Tag[0], // Interrupt exclusions (Arg 8)
                10000,
                10000
            ));
        }
    }

    // --- 3. REASSIGN BUILDING DELIVERY ERRANDS ---
    [HarmonyPatch(typeof(ManualDeliveryKG), "OnSpawn")]
    public static class ManualDeliveryKG_OnSpawn_Patch
    {
        public static void Postfix(ManualDeliveryKG __instance)
        {
            if (__instance == null) return;

            // Reassign Atmo Suit Dock supply chores
            if (__instance.GetComponent<SuitLocker>() != null ||
                __instance.PrefabID() == "SuitLocker" ||
                __instance.RequestedItemTag == GameTags.Suit)
            {
                if (ModInit.SupplyAtmoSuitChoreType != null)
                {
                    __instance.choreTypeIDHash = ModInit.SupplyAtmoSuitChoreType.IdHash;
                }
            }
            // Reassign Robo-Piloted Module Data Bank supply chores
            else if (__instance.PrefabID() == "RoboPilotModule" ||
                     __instance.PrefabID() == "RoboPilotModuleConfig" ||
                     __instance.GetComponent("RoboPilotModule") != null)
            {
                if (ModInit.SupplyRoboPilotChoreType != null)
                {
                    __instance.choreTypeIDHash = ModInit.SupplyRoboPilotChoreType.IdHash;
                }
            }
        }
    }
}