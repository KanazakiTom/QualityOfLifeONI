using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
//using CaiLib.Config;
//using CaiLib.Utils;
using HarmonyLib;
using KMod;
using PeterHan.PLib.Core;
using PeterHan.PLib.Options;
//using SanchozzONIMods.Lib;
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

        // More Priorities
        public static ChoreGroup RoboPilotChoreGroup;
        public static ChoreGroup AtmoSuitChoreGroup;

        public static ChoreType SupplyRoboPilotChoreType;
        public static ChoreType SupplyAtmoSuitChoreType;

        public override void OnAllModsLoaded(Harmony harmony, IReadOnlyList<Mod> mods)
        {
            base.OnAllModsLoaded(harmony, mods);

            // Initialize target types for No Sensor Limits
            NoSensorLimitsPatches.InitializeTypes();

            // Build the "which mod added this building" map. This is independent of
            // whether the WhereThisItemFrom descriptor patch itself finds a valid
            // target on this game version - so origin data is available even if the
            // in-UI display isn't.
            try
            {
                ModOriginTracker.BuildMap(mods);
            }
            catch (Exception e)
            {
                Debug.LogError("[QualityOfLifeONI] ModOriginTracker.BuildMap failed: " + e);
            }
        }

        public override void OnLoad(Harmony harmony)
        {
            // 1. MUST BE FIRST: Initialize PLib core before calling any PLib methods
            PUtil.InitLibrary();

            // Attribute-driven patches (ModInit itself, QoL_GeneratedBuildings_Patch,
            // QoL_TechTree_Patch, Techs_Init_Patch, etc.). Guarded so that a single
            // broken patch on a future game update can't take the whole mod down -
            // this is exactly the failure mode that was crashing the mod before.
            try
            {
                harmony.PatchAll();
            }
            catch (Exception e)
            {
                Debug.LogError("[QualityOfLifeONI] harmony.PatchAll() failed: " + e);
            }

            #region WhereThisItemFrom
            // Applied manually, never via PatchAll - see the no-[HarmonyPatch]-attribute
            // note on the class itself. TryApply() is internally guarded and will log +
            // skip cleanly if this game version doesn't expose a matching target method.
            WhereThisItemFrom_BuildingDef_Patch.TryApply(harmony);
            #endregion

            // 2. Safely initialize PLib patch manager and actions
            PipPlantOverlayPatches.Init(harmony);
            NoWasteWantPatches.Init(harmony);
            //ForbidItemsPatches.Init(harmony);
            //EfficientFetchPatches.Init(harmony);
            //FinishTasksPatches.Init(harmony);

            // 3. Other initializations
            MopTool.maxMopAmt = float.PositiveInfinity;

            base.OnLoad(harmony);

            new POptions().RegisterOptions(this, typeof(QoLConfig));
            Config = POptions.ReadSettings<QoLConfig>() ?? new QoLConfig();

            Console.WriteLine($"Mod <{Name}> loaded: {Version}");
        }

        // CaiLib's Config Managers
        //internal static ConfigManager<CaiLibConfig> ConfigManager;

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

        //#region Sanchozz Region
        //        // --- Sanchozz's Mods
        //        private static void PatchLater()
        //        {
        //            Utils.MuteMouthFlapSpeech("anim_rage_kanim", ModInit.rage_anims);
        //            ModInit.@this.PatchLater();
        //        }

        //        public static void AddSackSymbolOverride(GameObject dupe, GameObject pickupable)
        //        {
        //            KAnimControllerBase kanimControllerBase;
        //            SymbolOverrideController symbolOverrideController;
        //            if (dupe != null && pickupable != null && pickupable.HasTag(GameTags.Creature) && !pickupable.HasTag(GameTags.Robot) && dupe.TryGetComponent<KAnimControllerBase>(out kanimControllerBase) && dupe.TryGetComponent<SymbolOverrideController>(out symbolOverrideController))
        //            {
        //                KAnim.Build.Symbol symbol = Assets.GetAnim("creature_sack_kanim").GetData().build.GetSymbol("object");
        //                symbolOverrideController.AddSymbolOverride("snapTo_chest", symbol, 0);
        //                kanimControllerBase.SetSymbolVisiblity("snapTo_chest", true);
        //            }
        //        }
        //        public static void RemoveSackSymbolOverride(GameObject dupe)
        //        {
        //            KAnimControllerBase kanimControllerBase;
        //            SymbolOverrideController symbolOverrideController;
        //            if (dupe != null && dupe.TryGetComponent<KAnimControllerBase>(out kanimControllerBase) && dupe.TryGetComponent<SymbolOverrideController>(out symbolOverrideController))
        //            {
        //                kanimControllerBase.SetSymbolVisiblity("snapTo_chest", false);
        //                symbolOverrideController.RemoveSymbolOverride("snapTo_chest", 0);
        //            }
        //        }

        //        public static bool IsCritter(GameObject go)
        //        {
        //            CreatureBrain creatureBrain;
        //            return go != null && go.TryGetComponent<CreatureBrain>(out creatureBrain) && !go.HasTag(GameTags.Robot);
        //        }

        //        private static ModInit @this;

        //        private const string rage_kanim = "anim_rage_kanim";

        //        public static readonly HashedString[] rage_anims = new HashedString[]
        //        {
        //            "idle_pre",
        //            "rage_pre",
        //            "rage_loop",
        //            "rage_loop",
        //            "rage_pst",
        //            "idle_pst"
        //        };

        //        private const string chest = "snapTo_chest";

        //        [HarmonyPatch(typeof(ChoreTypes), "Add")]
        //        public static class ChoreTypes_Add
        //        {
        //            private static void Prefix(string id, ref bool skip_implicit_priority_change)
        //            {
        //                if (id == "CreatureFetch")
        //                {
        //                    skip_implicit_priority_change = true;
        //                }
        //            }
        //        }

        //        [HarmonyPatch]
        //        public static class Capturable_OnWork
        //        {
        //            private static IEnumerable<MethodBase> TargetMethods()
        //            {
        //                yield return typeof(Capturable).GetMethod("OnStartWork", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        //                yield return typeof(Capturable).GetMethod("OnStopWork", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        //                yield break;
        //            }

        //            private static void Postfix(Capturable __instance)
        //            {
        //                CreatureBrain creatureBrain;
        //                if (__instance.TryGetComponent<CreatureBrain>(out creatureBrain) && creatureBrain.IsRunning())
        //                {
        //                    creatureBrain.UpdateBrain();
        //                }
        //            }
        //        }

        //        [HarmonyPatch(typeof(Capturable), "OnCompleteWork")]
        //        public static class Capturable_OnCompleteWork
        //        {
        //            private static void Postfix(Capturable __instance, WorkerBase worker)
        //            {
        //                Pickupable pickupable;
        //                if (__instance.TryGetComponent<Pickupable>(out pickupable) && pickupable.IsReachable())
        //                {
        //                    return;
        //                }
        //                ChoreProvider target;
        //                if (worker != null && worker.TryGetComponent<ChoreProvider>(out target))
        //                {
        //                    new EmoteChore(target, Db.Get().ChoreTypes.EmoteHighPriority, "anim_rage_kanim", ModInit.rage_anims, null);
        //                }
        //            }
        //        }

        //        [HarmonyPatch(typeof(FetchAreaChore.States), "InitializeStates")]
        //        public static class FetchAreaChore_States_InitializeStates
        //        {
        //            private static void Postfix(FetchAreaChore.States __instance)
        //            {
        //                __instance.delivering.movetostorage.Enter(delegate (FetchAreaChore.StatesInstance smi)
        //                {
        //                    ModInit.AddSackSymbolOverride(smi.gameObject, smi.sm.deliveryObject.Get(smi));
        //                }).Exit(delegate (FetchAreaChore.StatesInstance smi)
        //                {
        //                    ModInit.RemoveSackSymbolOverride(smi.gameObject);
        //                });
        //            }
        //        }

        //        [HarmonyPatch(typeof(MovePickupableChore.States), "InitializeStates")]
        //        public static class MovePickupableChore_States_InitializeStates
        //        {
        //            private static void Postfix(MovePickupableChore.States __instance)
        //            {
        //                __instance.approachstorage.Enter(delegate (MovePickupableChore.StatesInstance smi)
        //                {
        //                    ModInit.AddSackSymbolOverride(smi.sm.deliverer.Get(smi), smi.sm.pickupablesource.Get(smi));
        //                }).Exit(delegate (MovePickupableChore.StatesInstance smi)
        //                {
        //                    ModInit.RemoveSackSymbolOverride(smi.sm.deliverer.Get(smi));
        //                });
        //            }
        //        }

        //        [HarmonyPatch(typeof(MovePickupableChore.States), "IsDeliveryComplete")]
        //        public static class MovePickupableChore_States_IsDeliveryComplete
        //        {
        //            private static void Postfix(ref bool __result, MovePickupableChore.StatesInstance smi)
        //            {
        //                if (!__result)
        //                {
        //                    GameObject gameObject = smi.sm.deliverypoint.Get(smi);
        //                    CancellableMove cancellableMove;
        //                    if (gameObject != null && gameObject.TryGetComponent<CancellableMove>(out cancellableMove))
        //                    {
        //                        GameObject nextTarget = cancellableMove.GetNextTarget();
        //                        if (nextTarget != null && ModInit.IsCritter(nextTarget) == (smi.master.choreType.IdHash == Db.Get().ChoreTypes.Fetch.IdHash))
        //                        {
        //                            __result = true;
        //                        }
        //                    }
        //                }
        //            }
        //        }
        //        #endregion

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

                // 5. Add more Priorities
                Strings.Add("STRINGS.DUPLICANTS.CHOREGROUPS.ROBOPILOT.NAME", NEWSTRINGS.NEWDUPLICANTS.NEWCHOREGROUPS.ROBOPILOT.NAME);
                Strings.Add("STRINGS.DUPLICANTS.CHOREGROUPS.ROBOPILOT.DESC", NEWSTRINGS.NEWDUPLICANTS.NEWCHOREGROUPS.ROBOPILOT.DESC);
                Strings.Add("STRINGS.DUPLICANTS.CHOREGROUPS.ATMOSUIT.NAME", NEWSTRINGS.NEWDUPLICANTS.NEWCHOREGROUPS.ATMOSUIT.NAME);
                Strings.Add("STRINGS.DUPLICANTS.CHOREGROUPS.ATMOSUIT.DESC", NEWSTRINGS.NEWDUPLICANTS.NEWCHOREGROUPS.ATMOSUIT.DESC);

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
}