using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using KMod;
using PeterHan.PLib.Core;
using PeterHan.PLib.Options;
using STRINGS;
using static QualityOfLifeONI.QoLConfig;

namespace QualityOfLifeONI
{
    [HarmonyPatch(typeof(BaseHatchConfig), "FoodDiet")]
    public class ModInit : UserMod2
    {
        public static AssemblyName AssemblyName => Assembly.GetExecutingAssembly().GetName();
        public static Version Version => AssemblyName.Version;
        public static string Name => AssemblyName.Name;

        // Centralized config instance
        public static QoLConfig Config;

        public override void OnLoad(Harmony harmony)
        {
            base.OnLoad(harmony);

            PUtil.InitLibrary();

            // Register master config class with PLib
            new POptions().RegisterOptions(this, typeof(QoLConfig));

            // Load settings into memory at startup
            Config = POptions.ReadSettings<QoLConfig>() ?? new QoLConfig();

            Console.WriteLine($"Mod <{Name}> loaded: {Version}");
        }

        // Hatches Don't Eat Meat
        private static void Postfix(ref List<Diet.Info> __result)
        {
            for (int i = 0; i < __result.Count; i++)
            {
                bool flag = __result[i].consumedTags.First<Tag>().ToString() == "Meat";
                if (flag)
                {
                    __result.RemoveAt(i);
                }
            }
        }
    }

    // --- CENTRAL STRINGS AND BUILDINGS REGISTRY ---
    [HarmonyPatch(typeof(GeneratedBuildings), "LoadGeneratedBuildings")]
    public static class QoL_GeneratedBuildings_Patch
    {
        public static void Prefix()
        {
            // 1. Custom Buildings Strings
            Strings.Add("STRINGS.BUILDINGS.PREFABS.SELFTIMERPNEUMATICDOOR.NAME", "(Beta) Self-Timer Pneumatic Door");
            Strings.Add("STRINGS.BUILDINGS.PREFABS.SELFTIMERPNEUMATICDOOR.DESC", "An internal door with an integrated cycle timer.");
            Strings.Add("STRINGS.BUILDINGS.PREFABS.SELFTIMERPNEUMATICDOOR.EFFECT", "Automatically opens and locks according to the time of day, completely bypassing the need for automation wire.");

            // 2. UI Tool Filters Strings (Ladders & Doors Tool)
            Strings.Add("STRINGS.UI.TOOLS.FILTERLAYERS.OVERRIDE_MODE", "Override Mode");
            Strings.Add("STRINGS.UI.TOOLS.FILTERLAYERS.OVERRIDE_MODE.NAME", "Override Mode");
            Strings.Add("STRINGS.UI.TOOLS.FILTERLAYERS.OVERRIDE_MODE.TOOLTIP", "Allows ladders and doors to replace solid tiles.");

            Strings.Add("STRINGS.UI.TOOLS.FILTERLAYERS.BACKGROUND_MODE", "Background Mode");
            Strings.Add("STRINGS.UI.TOOLS.FILTERLAYERS.BACKGROUND_MODE.NAME", "Background Mode");
            Strings.Add("STRINGS.UI.TOOLS.FILTERLAYERS.BACKGROUND_MODE.TOOLTIP", "Places ladders and doors on the background layer.");

            Strings.Add("STRINGS.UI.TOOLS.FILTERLAYERS.VANILLA_MODE", "Vanilla Mode");
            Strings.Add("STRINGS.UI.TOOLS.FILTERLAYERS.VANILLA_MODE.NAME", "Vanilla Mode");
            Strings.Add("STRINGS.UI.TOOLS.FILTERLAYERS.VANILLA_MODE.TOOLTIP", "Restores default vanilla building rules.");

            // 3. Cryo Consender Strings
            Strings.Add($"STRINGS.BUILDINGS.PREFABS.{CryoCondenserConfig.ID.ToUpper()}.NAME", UI.FormatAsLink("Cryo Condenser", CryoCondenserConfig.ID));
            Strings.Add($"STRINGS.BUILDINGS.PREFABS.{CryoCondenserConfig.ID.ToUpper()}.DESC", "A high-powered condenser that cools gas into its liquid state and outputs the thermal heat into its surroundings.");
            Strings.Add($"STRINGS.BUILDINGS.PREFABS.{CryoCondenserConfig.ID.ToUpper()}.EFFECT", $"Condenses incoming {UI.FormatAsLink("Gas", "ELEMENTS_GAS")} into {UI.FormatAsLink("Liquid", "ELEMENTS_LIQUID")} while outputting {UI.FormatAsLink("Heat", "HEAT")} in its immediate vicinity.");


            // 4. Register Buildings to Plan Menu
            ModUtil.AddBuildingToPlanScreen("Base", SelfTimerPneumaticDoorConfig.ID);
            ModUtil.AddBuildingToPlanScreen("Utilities", CryoCondenserConfig.ID);
        }
    }

    // --- TECH TREE UNLOCKS ---
    [HarmonyPatch(typeof(Db), "Initialize")]
    public static class QoL_TechTree_Patch
    {
        public static void Postfix()
        {
            Db.Get().Techs.Get("AnimalControl")?.unlockedItemIDs.Add(SelfTimerPneumaticDoorConfig.ID);
        }
    }

    // --- CRYO CONSENDER TECH ---
    [HarmonyPatch(typeof(Database.Techs), "Init")]
    public static class Techs_Init_Patch
    {
        public static void Postfix(Database.Techs __instance)
        {
            Tech tech = null;

            if (PlayerConfig.Instance?.Difficulty == TechDifficulty.Hard)
            {
                // DLC: CryoFuelPropulsion | Base Game: HydrogenEngine
                string[] hardTechCandidates = new string[]
                {
                        "CryoFuelPropulsion",
                        "HydrogenEngine"
                };

                foreach (string techId in hardTechCandidates)
                {
                    tech = __instance.TryGet(techId);
                    if (tech != null) break;
                }
            }

            // Fallback to Easy Mode (LiquidTemperature / Aquatuner) if not set or found
            if (tech == null)
            {
                tech = __instance.TryGet("LiquidTemperature");
            }

            // Add building to the resolved tech node
            tech?.unlockedItemIDs.Add(CryoCondenserConfig.ID);
        }
    }
}