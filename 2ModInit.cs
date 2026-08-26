using HarmonyLib;
using KMod;
using PeterHan.PLib.Core;
using PeterHan.PLib.Options;
using UnityEngine;

namespace QualityOfLifeONI
{
    public class ModInit : UserMod2
    {
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

            // 3. Register Buildings to Plan Menu
            ModUtil.AddBuildingToPlanScreen("Base", SelfTimerPneumaticDoorConfig.ID);
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
    //#region Rocket's Module Patches
    //// --- AUTO ARTIFACT DROPPER COMPONENT ---
    //public class AutoArtifactDropper : KMonoBehaviour, ISim1000ms
    //{
    //    [MyCmpGet]
    //    private Storage storage;

    //    [MyCmpGet]
    //    private RocketModuleCluster rocketModule;

    //    [MyCmpGet]
    //    private SingleEntityReceptacle receptacle;

    //    private float timer = 0f;
    //    private bool hasDroppedForThisLanding = false;

    //    public void Sim1000ms(float dt)
    //    {
    //        if (ModInit.Config == null || !ModInit.Config.ArtifactAutoDropEnabled)
    //            return;

    //        bool isGrounded = IsGrounded();

    //        if (!isGrounded)
    //        {
    //            hasDroppedForThisLanding = false;
    //            timer = 0f;
    //            return;
    //        }

    //        if (!hasDroppedForThisLanding && HasArtifact())
    //        {
    //            timer += dt;
    //            float targetDelay = ModInit.Config.ArtifactDropDelaySeconds;

    //            if (timer >= targetDelay)
    //            {
    //                DropArtifact();
    //                hasDroppedForThisLanding = true;
    //                timer = 0f;
    //            }
    //        }
    //        else if (!HasArtifact())
    //        {
    //            timer = 0f;
    //        }
    //    }

    //    private bool IsGrounded()
    //    {
    //        if (rocketModule != null && rocketModule.CraftInterface != null)
    //        {
    //            var craft = rocketModule.CraftInterface.GetComponent<Clustercraft>();
    //            return craft != null && craft.Status == Clustercraft.CraftStatus.Grounded;
    //        }
    //        return false;
    //    }

    //    private bool HasArtifact()
    //    {
    //        if (receptacle != null && receptacle.Occupant != null)
    //            return true;

    //        return storage != null && !storage.IsEmpty();
    //    }

    //    private void DropArtifact()
    //    {
    //        if (receptacle != null && receptacle.Occupant != null)
    //        {
    //            // .GetValue() executes the method in Harmony Traverse
    //            HarmonyLib.Traverse.Create(receptacle).Method("ClearOccupant").GetValue();
    //        }
    //        else if (storage != null && !storage.IsEmpty())
    //        {
    //            storage.DropAll();
    //        }
    //    }
    //}
    //#endregion
}