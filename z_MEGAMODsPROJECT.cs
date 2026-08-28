using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using JetBrains.Annotations;
using KMod;
using UnityEngine;
using System.Runtime.CompilerServices;
using TMPro;

namespace MegaModsProject
{
    #region Mod: Always Show Capacity
    [HarmonyPatch(typeof(SimpleInfoScreen), "RefreshStoragePanel")]
    public class SimpleInfoScreen_RefreshStoragePanel_Patches
    {
        [UsedImplicitly]
        public static void Postfix(CollapsibleDetailContentPanel targetPanel, [CanBeNull] GameObject targetEntity)
        {
            if (targetEntity != null)
            {
                IStorage[] componentsInChildren = targetEntity.GetComponentsInChildren<IStorage>();
                float num = componentsInChildren.Sum((IStorage storage) => storage.RemainingCapacity());
                float num2 = componentsInChildren.Sum((IStorage storage) => storage.Capacity());
                LocText headerLabel = targetPanel.HeaderLabel;
                headerLabel.text = string.Concat(new string[]
                {
                    headerLabel.text,
                    ": ",
                    GameUtil.GetFormattedMass(num2 - num, GameUtil.TimeSlice.None, GameUtil.MetricMassFormat.UseThreshold, true, "{0:0.#}"),
                    " / ",
                    GameUtil.GetFormattedMass(num2, GameUtil.TimeSlice.None, GameUtil.MetricMassFormat.UseThreshold, true, "{0:0.#}")
                });
            }
        }
    }
    #endregion

    #region Mod: No Suit Wear
    [HarmonyPatch]
    public class SuitDurabilityPatch
    {
        private static MethodInfo TargetMethod()
        {
            return typeof(Durability).GetMethod("DeltaDurability", BindingFlags.Instance | BindingFlags.NonPublic);
        }

        public static bool Prefix(ref float ___durability)
        {
            ___durability = 1f;
            return false;
        }
    }
    #endregion

    #region Mod: Drywall Hide Pipes
    [HarmonyPatch(typeof(ExteriorWallConfig), "CreateBuildingDef")]
    public static class ExteriorWallConfig_CreateBuildingDef_Path
    {
        public static void Postfix(BuildingDef __result)
        {
            __result.SceneLayer = Grid.SceneLayer.LogicGatesFront;
        }
    }

    [HarmonyPatch(typeof(ThermalBlockConfig), "CreateBuildingDef")]
    public static class ThermalBlockConfig_CreateBuildingDef_Path
    {
        public static void Postfix(BuildingDef __result)
        {
            __result.SceneLayer = Grid.SceneLayer.LogicGatesFront;
        }
    }
    #endregion

    #region Mod: Hatches Dont Eat Meat
    // Sent to ModInit
    #endregion

    #region Mod: Automatic Desalinator
    [HarmonyPatch(typeof(Desalinator.StatesInstance), "UpdateStorageLeft")]
    public class DesalinatorStatesInstanceUpdateStorageLeft
    {
        public static void Postfix(Desalinator.StatesInstance __instance)
        {
            bool flag = __instance.master.SaltStorageLeft < 900f;
            if (flag)
            {
                Storage value = Traverse.Create(__instance.master).Field("storage").GetValue<Storage>();
                __instance.emptyChore = null;
                Tag tag = GameTagExtensions.Create(SimHashes.Salt);
                ListPool<GameObject, Desalinator>.PooledList pooledList = ListPool<GameObject, Desalinator>.Allocate();
                value.Find(tag, pooledList);
                foreach (GameObject go in pooledList)
                {
                    value.Drop(go, true);
                }
                pooledList.Recycle();
            }
        }
    }
    #endregion

    #region Mod: Better Air Filters (Rotatable)
    [HarmonyPatch(typeof(AirFilterConfig))]
    [HarmonyPatch("CreateBuildingDef")]
    public class patch_flip_air_filter
    {
        public static void Postfix(BuildingDef __result)
        {
            __result.PermittedRotations = PermittedRotations.R360;
            __result.BuildLocationRule = BuildLocationRule.OnFoundationRotatable;
        }
    }
    #endregion

    #region Mod: DisplayPOIReplenishRate
    public static class DisplayPOIReplenishRatePatches
    {
        private static void RefreshHeader(SimpleInfoScreen simpleInfoRoot, CollapsibleDetailContentPanel spacePOIPanel, string title, string value)
        {
            foreach (Transform child in spacePOIPanel.Content.transform)
            {
                HierarchyReferences refs = child.GetComponent<HierarchyReferences>();
                if (refs != null && refs.GetReference<LocText>("NameLabel").text == title)
                {
                    SetHeaderReferences(refs, title, value);
                    return;
                }
            }

            GameObject gameObject = Util.KInstantiateUI(simpleInfoRoot.iconLabelRow, spacePOIPanel.Content.gameObject, true);
            gameObject.SetActive(true);
            SetHeaderReferences(gameObject.GetComponent<HierarchyReferences>(), title, value);
        }

        private static void SetHeaderReferences(HierarchyReferences refs, string title, string value)
        {
            if (refs == null) return;
            refs.GetReference<LocText>("NameLabel").text = title;

            LocText valueLabel = refs.GetReference<LocText>("ValueLabel");
            valueLabel.text = value;
            valueLabel.alignment = TextAlignmentOptions.MidlineRight;
        }

        [HarmonyPatch(typeof(SpacePOISimpleInfoPanel), "RefreshMassHeader")]
        public static class SpacePOISimpleInfoPanel_Refresh_Patch
        {
            public static void Postfix(SimpleInfoScreen ___simpleInfoRoot, HarvestablePOIStates.Instance harvestable, CollapsibleDetailContentPanel spacePOIPanel)
            {
                if (harvestable == null) return;

                float maxCapacity = harvestable.configuration.GetMaxCapacity();
                float rechargeCycles = harvestable.configuration.GetRechargeTime() / 600f;
                float replenishRate = maxCapacity / rechargeCycles;

                RefreshHeader(___simpleInfoRoot, spacePOIPanel, "Max Capacity", $"{maxCapacity / 1000f:F2} t");
                RefreshHeader(___simpleInfoRoot, spacePOIPanel, "Full Replenish Time", $"{rechargeCycles:F2} cycles");
                RefreshHeader(___simpleInfoRoot, spacePOIPanel, "Replenish Rate", $"{replenishRate:F2} kg/cycle");
            }
        }
    }
    #endregion

    #region Mod: 
    #endregion

    #region Mod: 
    #endregion
}
