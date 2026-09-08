using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.Serialization;
using System.Threading;
using Database;
using HarmonyLib;
using ImGuiObjectDrawer;
using JetBrains.Annotations;
using Klei;
using Klei.AI;
using KMod; 
using KSerialization;
using Microsoft.CSharp;
using Newtonsoft.Json;
using PeterHan.PLib.Actions;
using PeterHan.PLib.AVC;
using PeterHan.PLib.Buildings;
using PeterHan.PLib.Core;
using PeterHan.PLib.Database;
using PeterHan.PLib.Detours;
using PeterHan.PLib.Options;
using PeterHan.PLib.PatchManager;
using PeterHan.PLib.UI;
using STRINGS;
using TMPro;
using TUNING;
using Unity.Collections;
using UnityEngine;
using static OverlayModes;

namespace QualityOfLifeONI
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
        private static T TryGetReference<T>(HierarchyReferences refs, string name) where T : Component
        {
            if (refs == null || refs.references == null) return null;

            foreach (var element in refs.references)
            {
                if (element.Name == name && element.behaviour is T component)
                {
                    return component;
                }
            }
            return null;
        }

        private static void RefreshHeader(SimpleInfoScreen simpleInfoRoot, CollapsibleDetailContentPanel spacePOIPanel, string title, string value)
        {
            foreach (Transform child in spacePOIPanel.Content.transform)
            {
                HierarchyReferences refs = child.GetComponent<HierarchyReferences>();
                if (refs != null)
                {
                    LocText nameLabel = TryGetReference<LocText>(refs, "NameLabel");
                    if (nameLabel != null && nameLabel.text == title)
                    {
                        SetHeaderReferences(refs, title, value);
                        return;
                    }
                }
            }

            GameObject gameObject = Util.KInstantiateUI(simpleInfoRoot.iconLabelRow, spacePOIPanel.Content.gameObject, true);
            gameObject.SetActive(true);
            SetHeaderReferences(gameObject.GetComponent<HierarchyReferences>(), title, value);
        }

        private static void SetHeaderReferences(HierarchyReferences refs, string title, string value)
        {
            if (refs == null) return;

            LocText nameLabel = TryGetReference<LocText>(refs, "NameLabel");
            if (nameLabel != null)
            {
                nameLabel.text = title;
            }

            LocText valueLabel = TryGetReference<LocText>(refs, "ValueLabel");
            if (valueLabel != null)
            {
                valueLabel.text = value;
                valueLabel.alignment = TextAlignmentOptions.MidlineRight;
            }
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

    #region Mod: Ach/Res Names
    public class achName
    {
        public static string getName(ColonyAchievement colonyAchievement)
        {
            return colonyAchievement.Name;
        }

        public static string getDescription(ColonyAchievement colonyAchievement)
        {
            return colonyAchievement.description;
        }
    }

    public class achNamePatch
    {
        public static string Ach;

        [HarmonyPatch(typeof(ColonyAchievementTracker), "TriggerNewAchievementCompleted")]
        private class TriggerNewAchievementCompleted_Patch
        {
            [HarmonyPrefix]
            public static void getAchId(string achievement)
            {
                achNamePatch.Ach = achName.getName(Db.Get().ColonyAchievements.Get(achievement));
            }
        }

        [HarmonyPatch(typeof(AchievementEarnedMessage), "GetTitle")]
        private class GetTitlePatch
        {
            private static void Postfix(ref string __result)
            {
                __result = __result + "<br><b>" + achNamePatch.Ach + "</b>";
            }
        }
    }

    public class resNamePatch
    {
        [HarmonyPatch(typeof(ResearchCompleteMessage), "GetTitle")]
        private class Patch1
        {
            [HarmonyPostfix]
            public static void getResName(ref string __result, ref ResourceRef<Tech> ___tech)
            {
                __result = __result + "<br><b>" + ___tech.Get().Name + "<b>";
            }
        }

        [HarmonyPatch(typeof(MessageDialogFrame), "SetMessage")]
        private class Patch2
        {
            [HarmonyPostfix]
            public static void delBr(ref LocText ___title)
            {
                if (___title.text.Contains("<BR>"))
                {
                    ___title.text = ___title.text.Substring(0, ___title.text.IndexOf("<BR>"));
                }
            }
        }
    }
    #endregion

    #region Mod: Automatic Geyser Calculation
    [HarmonyPatch(typeof(Geyser))]
    [HarmonyPatch("GetDescriptors")]
    public class GeyserInformationPatch
    {
        public static void Postfix(ref Geyser __instance, ref List<Descriptor> __result)
        {
            __result.Add(new Descriptor(GeyserInformationPatch.CategoryFlowLabel, GeyserInformationPatch.CategoryFlowTooltip, Descriptor.DescriptorType.Effect, false));
            float emitRate = __instance.configuration.GetEmitRate();
            float iterationPercent = __instance.configuration.GetIterationPercent();
            float num = emitRate * iterationPercent;
            float yearPercent = __instance.configuration.GetYearPercent();
            float num2 = num * yearPercent;
            float offDuration = __instance.configuration.GetOffDuration();
            float mass = num * offDuration;
            float yearOffDuration = __instance.configuration.GetYearOffDuration();
            float mass2 = num2 * yearOffDuration;
            float onDuration = __instance.configuration.GetOnDuration();
            string formattedMass = GameUtil.GetFormattedMass(num, GameUtil.TimeSlice.PerSecond, GameUtil.MetricMassFormat.UseThreshold, true, "{0:0.#}");
            List<Descriptor> list = __result;
            Descriptor descriptor = new Descriptor(string.Format(GeyserInformationPatch.ActiveFlowLabel, formattedMass), string.Format(GeyserInformationPatch.ActiveFlowTooltip, formattedMass), Descriptor.DescriptorType.Effect, false);
            list.Add(descriptor.IncreaseIndent());
            string formattedMass2 = GameUtil.GetFormattedMass(mass, GameUtil.TimeSlice.None, GameUtil.MetricMassFormat.UseThreshold, true, "{0:0.#}");
            List<Descriptor> list2 = __result;
            Descriptor descriptor2 = new Descriptor(string.Format(GeyserInformationPatch.EruptionBufferLabel, formattedMass2), string.Format(GeyserInformationPatch.EruptionBufferTooltip, formattedMass2, formattedMass), Descriptor.DescriptorType.Effect, false);
            list2.Add(descriptor2.IncreaseIndent());
            Studyable component = __instance.GetComponent<Studyable>();
            bool flag = component && !component.Studied;
            if (flag)
            {
                List<Descriptor> list3 = __result;
                Descriptor descriptor3 = new Descriptor(GeyserInformationPatch.HiddenTotalFlowLabel, GeyserInformationPatch.HiddenTotalFlowTooltip, Descriptor.DescriptorType.Effect, false);
                list3.Add(descriptor3.IncreaseIndent());
                List<Descriptor> list4 = __result;
                Descriptor descriptor4 = new Descriptor(GeyserInformationPatch.HiddenDormancyBufferLabel, GeyserInformationPatch.HiddenDormancyBufferTooltip, Descriptor.DescriptorType.Effect, false);
                list4.Add(descriptor4.IncreaseIndent());
            }
            else
            {
                string formattedMass3 = GameUtil.GetFormattedMass(num2, GameUtil.TimeSlice.PerSecond, GameUtil.MetricMassFormat.UseThreshold, true, "{0:0.#}");
                List<Descriptor> list5 = __result;
                Descriptor descriptor5 = new Descriptor(string.Format(GeyserInformationPatch.TotalFlowLabel, formattedMass3), string.Format(GeyserInformationPatch.TotalFlowTooltip, formattedMass3), Descriptor.DescriptorType.Effect, false);
                list5.Add(descriptor5.IncreaseIndent());
                string formattedMass4 = GameUtil.GetFormattedMass(mass2, GameUtil.TimeSlice.None, GameUtil.MetricMassFormat.UseThreshold, true, "{0:0.#}");
                List<Descriptor> list6 = __result;
                Descriptor descriptor6 = new Descriptor(string.Format(GeyserInformationPatch.DormancyBufferLabel, formattedMass4), string.Format(GeyserInformationPatch.DormancyBufferTooltip, formattedMass4, formattedMass3), Descriptor.DescriptorType.Effect, false);
                list6.Add(descriptor6.IncreaseIndent());
            }
            Element element = ElementLoader.FindElementByHash(__instance.configuration.GetElement());
            float specificHeatCapacity = element.specificHeatCapacity;
            float temperature = __instance.configuration.GetTemperature();
            string formattedTemperature = GameUtil.GetFormattedTemperature(368.15f, GameUtil.TimeSlice.None, GameUtil.TemperatureInterpretation.Absolute, true, false);
            string formattedTemperature2 = GameUtil.GetFormattedTemperature(293.15f, GameUtil.TimeSlice.None, GameUtil.TemperatureInterpretation.Absolute, true, false);
            float num3 = temperature - 368.15f;
            if (num3 > 0f)
            {
                __result.Add(new Descriptor(string.Format(GeyserInformationPatch.CategoryHeatLabel, formattedTemperature), string.Format(GeyserInformationPatch.CategoryHeatTooltip, formattedTemperature), Descriptor.DescriptorType.Effect, false));
                float num4 = specificHeatCapacity * num3 * emitRate * 1000f;
                float num5 = num4 * iterationPercent;
                float num6 = num5 * yearPercent;
                float dtu = num4 * onDuration;
                bool isSteam = element.id == SimHashes.Steam;
                string arg = GameUtil.GetFormattedHeatEnergyRate(num4, GameUtil.HeatEnergyFormatterUnit.Automatic);
                string txt = string.Format(GeyserInformationPatch.PeakHeatLabel, arg);
                string text = string.Format(GeyserInformationPatch.PeakHeatTooltip, arg);
                text += GeyserInformationPatch.SteamTurbineFootnote(num4, temperature, emitRate, isSteam);
                List<Descriptor> list7 = __result;
                Descriptor descriptor7 = new Descriptor(txt, text, Descriptor.DescriptorType.Effect, false);
                list7.Add(descriptor7.IncreaseIndent());
                arg = GameUtil.GetFormattedHeatEnergyRate(num5, GameUtil.HeatEnergyFormatterUnit.Automatic);
                txt = string.Format(GeyserInformationPatch.ActiveHeatLabel, arg);
                text = string.Format(GeyserInformationPatch.ActiveHeatTooltip, arg);
                text += GeyserInformationPatch.SteamTurbineFootnote(num5, temperature, num, isSteam);
                List<Descriptor> list8 = __result;
                Descriptor descriptor8 = new Descriptor(txt, text, Descriptor.DescriptorType.Effect, false);
                list8.Add(descriptor8.IncreaseIndent());
                if (flag)
                {
                    List<Descriptor> list9 = __result;
                    Descriptor descriptor9 = new Descriptor(GeyserInformationPatch.HiddenTotalHeatLabel, GeyserInformationPatch.HiddenTotalHeatTooltip, Descriptor.DescriptorType.Effect, false);
                    list9.Add(descriptor9.IncreaseIndent());
                }
                else
                {
                    arg = GameUtil.GetFormattedHeatEnergyRate(num6, GameUtil.HeatEnergyFormatterUnit.Automatic);
                    txt = string.Format(GeyserInformationPatch.TotalHeatLabel, arg);
                    text = string.Format(GeyserInformationPatch.TotalHeatTooltip, arg);
                    text += GeyserInformationPatch.SteamTurbineFootnote(num6, temperature, num2, isSteam);
                    List<Descriptor> list10 = __result;
                    Descriptor descriptor10 = new Descriptor(txt, text, Descriptor.DescriptorType.Effect, false);
                    list10.Add(descriptor10.IncreaseIndent());
                }
                arg = GameUtil.GetFormattedHeatEnergy(dtu, GameUtil.HeatEnergyFormatterUnit.Automatic);
                txt = string.Format(GeyserInformationPatch.HeatMassLabel, arg);
                text = string.Format(GeyserInformationPatch.HeatMassTooltip, arg);
                List<Descriptor> list11 = __result;
                Descriptor descriptor11 = new Descriptor(txt, text, Descriptor.DescriptorType.Effect, false);
                list11.Add(descriptor11.IncreaseIndent());
            }
            float num7 = 293.15f - temperature;
            if (num7 > 0f)
            {
                __result.Add(new Descriptor(string.Format(GeyserInformationPatch.CategoryCoolLabel, formattedTemperature2), string.Format(GeyserInformationPatch.CategoryCoolTooltip, formattedTemperature2), Descriptor.DescriptorType.Effect, false));
                float num8 = specificHeatCapacity * num7 * emitRate * 1000f;
                float num9 = num8 * iterationPercent;
                float dtu_s = num9 * yearPercent;
                float dtu2 = num8 * onDuration;
                string formattedHeatEnergyRate = GameUtil.GetFormattedHeatEnergyRate(num8, GameUtil.HeatEnergyFormatterUnit.Automatic);
                List<Descriptor> list12 = __result;
                Descriptor descriptor12 = new Descriptor(string.Format(GeyserInformationPatch.PeakCoolLabel, formattedHeatEnergyRate), string.Format(GeyserInformationPatch.PeakCoolTooltip, formattedHeatEnergyRate), Descriptor.DescriptorType.Effect, false);
                list12.Add(descriptor12.IncreaseIndent());
                string formattedHeatEnergyRate2 = GameUtil.GetFormattedHeatEnergyRate(num9, GameUtil.HeatEnergyFormatterUnit.Automatic);
                List<Descriptor> list13 = __result;
                Descriptor descriptor13 = new Descriptor(string.Format(GeyserInformationPatch.ActiveCoolLabel, formattedHeatEnergyRate2), string.Format(GeyserInformationPatch.ActiveCoolTooltip, formattedHeatEnergyRate2), Descriptor.DescriptorType.Effect, false);
                list13.Add(descriptor13.IncreaseIndent());
                if (flag)
                {
                    List<Descriptor> list14 = __result;
                    Descriptor descriptor14 = new Descriptor(GeyserInformationPatch.HiddenTotalCoolLabel, GeyserInformationPatch.HiddenTotalCoolTooltip, Descriptor.DescriptorType.Effect, false);
                    list14.Add(descriptor14.IncreaseIndent());
                }
                else
                {
                    string formattedHeatEnergyRate3 = GameUtil.GetFormattedHeatEnergyRate(dtu_s, GameUtil.HeatEnergyFormatterUnit.Automatic);
                    List<Descriptor> list15 = __result;
                    Descriptor descriptor15 = new Descriptor(string.Format(GeyserInformationPatch.TotalCoolLabel, formattedHeatEnergyRate3), string.Format(GeyserInformationPatch.TotalCoolTooltip, formattedHeatEnergyRate3), Descriptor.DescriptorType.Effect, false);
                    list15.Add(descriptor15.IncreaseIndent());
                }
                string formattedHeatEnergy = GameUtil.GetFormattedHeatEnergy(dtu2, GameUtil.HeatEnergyFormatterUnit.Automatic);
                List<Descriptor> list16 = __result;
                Descriptor descriptor16 = new Descriptor(string.Format(GeyserInformationPatch.CoolMassLabel, formattedHeatEnergy), string.Format(GeyserInformationPatch.CoolMassTooltip, formattedHeatEnergy), Descriptor.DescriptorType.Effect, false);
                list16.Add(descriptor16.IncreaseIndent());
            }
        }

        public static string SteamTurbineFootnote(float heatEnergy, float temperature, float flow, bool isSteam = false)
        {
            string text = "\n\n";
            float num;
            if (!isSteam)
            {
                num = heatEnergy / 877590f;
                text += string.Format(GeyserInformationPatch.SteamTurbinePower, num);
                if (temperature < 398.15f)
                {
                    text += "\n\n";
                    text += GeyserInformationPatch.SteamTurbineColdWarning;
                }
                return text;
            }
            float num2 = 2f;
            int num3;
            if (temperature <= 473.15f)
            {
                num3 = 5;
            }
            else if (temperature <= 499.4f)
            {
                num3 = 4;
                num2 = 1.6f;
            }
            else if (temperature <= 543.15f)
            {
                num3 = 3;
                num2 = 1.2f;
            }
            else if (temperature <= 630.65f)
            {
                num3 = 2;
                num2 = 0.8f;
            }
            else
            {
                num3 = 2;
                num2 = 0.8f;
            }
            num = flow / num2;
            if (num3 == 5)
            {
                text += string.Format(GeyserInformationPatch.SteamTurbineUnrestricted, num);
            }
            else
            {
                text += string.Format(GeyserInformationPatch.SteamTurbineRestricted, num, num3);
            }
            if (temperature < 398.15f)
            {
                text += "\n\n";
                text += GeyserInformationPatch.SteamTurbineColdWarning;
            }
            if (temperature > 630.65f)
            {
                text += "\n\n";
                text += GeyserInformationPatch.SteamTurbineHotWarning;
            }
            return text;
        }

        public static LocString CategoryFlowLabel = "<b>Flow Information:</b>";
        public static LocString CategoryFlowTooltip = "Calculated information relating to the rate of flow of this geyser";
        public static LocString ActiveFlowLabel = "Average Active Flow: {0}";
        public static LocString ActiveFlowTooltip = "This geyser outputs {0} on average during its active (non-dormant) period";
        public static LocString EruptionBufferLabel = "Eruption Buffer: {0}";
        public static LocString EruptionBufferTooltip = "{0} should be buffered to maintain {1} constant flow during the active period";
        public static LocString TotalFlowLabel = "Total Average Flow: {0}";
        public static LocString TotalFlowTooltip = "This geyser outputs {0} on average, taking into account eruption times and dormancy";
        public static LocString HiddenTotalFlowLabel = "Total Average Flow: (Requires Analysis)";
        public static LocString HiddenTotalFlowTooltip = "The total average output flow of this geyser, taking into account eruption times and dormancy";
        public static LocString DormancyBufferLabel = "Dormancy Buffer: {0}";
        public static LocString DormancyBufferTooltip = "{0} of output should be buffered to sustain a constant flow of {1} across the dormancy period";
        public static LocString HiddenDormancyBufferLabel = "Dormancy Buffer: (Requires Analysis)";
        public static LocString HiddenDormancyBufferTooltip = "How much output should be buffered to sustain average flow across the dormancy period";
        public static LocString CategoryHeatLabel = "<b>Heat Production (over {0}):</b>";
        public static LocString CategoryHeatTooltip = "How much thermal energy is produced during the given phase, relative to a target temperature of {0}";
        public static LocString PeakHeatLabel = "Erupting: {0}";
        public static LocString PeakHeatTooltip = "{0} of thermal energy is produced while erupting";
        public static LocString ActiveHeatLabel = "Active: {0}";
        public static LocString ActiveHeatTooltip = "On average {0} of thermal energy is produced during the active (non-dormant) period";
        public static LocString TotalHeatLabel = "Total Average: {0}";
        public static LocString TotalHeatTooltip = "In total {0} of thermal energy is produced by this geyser, averaging across its entire lifetime";
        public static LocString HiddenTotalHeatLabel = "Total Average: (Requires Analysis)";
        public static LocString HiddenTotalHeatTooltip = "Total average thermal energy output, including the dormancy period";
        public static LocString HeatMassLabel = "Eruption Thermal Mass: {0}";
        public static LocString HeatMassTooltip = "One eruption outputs {0} of heat in total";
        public static LocString CategoryCoolLabel = "<b>Cooling Output (to {0}):</b>";
        public static LocString CategoryCoolTooltip = "How much cooling this geyser provides during the given phase, relative to a target temperature of {0}";
        public static LocString PeakCoolLabel = "Erupting: {0}";
        public static LocString PeakCoolTooltip = "This geyser provides {0} of cooling while erupting";
        public static LocString ActiveCoolLabel = "Active: {0}";
        public static LocString ActiveCoolTooltip = "On average this geyser provides {0} of cooling during its active (non-dormant) period";
        public static LocString TotalCoolLabel = "Total Average: {0}";
        public static LocString TotalCoolTooltip = "In total this geyser provides {0} of cooling, averaging across its entire lifetime";
        public static LocString HiddenTotalCoolLabel = "Total Average: (Requires Analysis)";
        public static LocString HiddenTotalCoolTooltip = "Total average cooling output, including the dormancy period";
        public static LocString CoolMassLabel = "Eruption Thermal Mass: {0}";
        public static LocString CoolMassTooltip = "One eruption outputs {0} of cooling in total";
        public static LocString SteamTurbinePower = "This amount of heat energy could fully power {0:N1} steam turbines, if directed appropriately";
        public static LocString SteamTurbineRestricted = "Steam output during this period can directly feed {0:N1} steam turbines restricted to {1} open vents each";
        public static LocString SteamTurbineUnrestricted = "Steam output during this period can directly feed {0:N1} steam turbines";
        public static LocString SteamTurbineColdWarning = "The output of this geyser is cold and will require heating";
        public static LocString SteamTurbineHotWarning = "The output of this geyser is hot and some energy may be wasted";
    }
    #endregion

    #region Mod: No Mop Limit
    [HarmonyPatch(typeof(MopTool), "OnDragTool")]
    public static class MopTool_OnDragTool_Patch
    {
        [UsedImplicitly]
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> orig)
        {
            List<CodeInstruction> list = orig.ToList<CodeInstruction>();
            int num = list.FindIndex(delegate (CodeInstruction ci)
            {
                MethodInfo methodInfo = ci.operand as MethodInfo;
                return methodInfo != null && methodInfo == MopTool_OnDragTool_Patch.CellBelowInfo;
            });
            if (num == -1)
            {
                Debug.LogWarning("[NoMopLimits] Unable to find Grid.CellBelow");
                return list;
            }
            int num2 = list.FindIndex(num, delegate (CodeInstruction ci)
            {
                MethodInfo methodInfo = ci.operand as MethodInfo;
                return methodInfo != null && methodInfo == MopTool_OnDragTool_Patch.SolidIndexer;
            });
            if (num2 == -1)
            {
                Debug.LogWarning("[NoMopLimits] Unable to find Grid.Solid[]");
                return list;
            }
            list.Insert(num2 + 1, new CodeInstruction(OpCodes.Pop, null));
            list.Insert(num2 + 2, new CodeInstruction(OpCodes.Ldc_I4_1, null));
            return list;
        }

        private static readonly MethodInfo CellBelowInfo = AccessTools.Method(typeof(Grid), "CellBelow", null, null);

        private static readonly MethodInfo SolidIndexer = AccessTools.DeclaredMethod(typeof(Grid.BuildFlagsSolidIndexer), "get_Item", null, null);
    }
    #endregion

    #region Mod: Smart Drag Tool
    public class Patches
    {
        public static bool _panLeft;
        public static bool _panRight;
        public static bool _panUp;
        public static bool _panDown;
        public static bool _isMousePanning;
        public static bool _isLogged;
        public static Guid areaVisText;
        public static int clampSize;

        [HarmonyPatch(typeof(DragTool))]
        [HarmonyPatch("OnLeftClickUp")]
        public class DragTool_OnLeftClickUp_Patch
        {
            public static void Prefix()
            {
                Patches.areaVisText = Guid.Empty;
            }
        }

        [HarmonyPatch(typeof(DragTool))]
        [HarmonyPatch("OnDeactivateTool")]
        public class DragTool_OnDeactivateTool_Patch
        {
            public static void Prefix()
            {
                Patches.areaVisText = Guid.Empty;
            }
        }

        [HarmonyPatch(typeof(DragTool))]
        [HarmonyPatch("CancelDragging")]
        public class DragTool_CancelDragging_Patch
        {
            public static void Prefix()
            {
                Patches.areaVisText = Guid.Empty;
            }
        }

        [HarmonyPatch(typeof(DragTool))]
        [HarmonyPatch("OnLeftClickDown")]
        public class DragTool_OnLeftClickDown_Patch
        {
            public static void Postfix(ref Guid ___areaVisualizerText)
            {
                if (___areaVisualizerText != Guid.Empty)
                {
                    NameDisplayScreen.Instance.GetWorldText(___areaVisualizerText).GetComponent<LocText>().color = new Color(1f, 1f, 1f);
                }
            }
        }

        [HarmonyPatch(typeof(DragTool))]
        [HarmonyPatch("OnMouseMove")]
        public class DragTool_OnMouseMove_Patch
        {
            public static void Postfix(Vector3 ___previousCursorPos, Vector3 ___downPos, ref Guid ___areaVisualizerText, bool ___dragging, SpriteRenderer ___areaVisualizerSpriteRenderer)
            {
                Patches._panRight = false;
                Patches._panLeft = false;
                Patches._panUp = false;
                Patches._panDown = false;
                if (!___dragging)
                {
                    return;
                }
                Vector3 vector = Camera.main.ScreenToWorldPoint(KInputManager.GetMousePos());
                vector = Vector3.Max(ClusterManager.Instance.activeWorld.minimumBounds, vector);
                vector = Vector3.Min(ClusterManager.Instance.activeWorld.maximumBounds, vector);
                vector = Camera.main.WorldToViewportPoint(vector);
                if (vector.x > 0.95f)
                {
                    Patches._panRight = true;
                }
                else if (vector.x < 0.05f)
                {
                    Patches._panLeft = true;
                }
                if (vector.y > 0.95f)
                {
                    Patches._panUp = true;
                }
                else if (vector.y < 0.05f)
                {
                    Patches._panDown = true;
                }
                Patches._isMousePanning = (Patches._panRight || Patches._panLeft || Patches._panUp || Patches._panDown);
                Patches.areaVisText = ___areaVisualizerText;
                if (___areaVisualizerText != Guid.Empty && ___areaVisualizerSpriteRenderer != null)
                {
                    LocText component = NameDisplayScreen.Instance.GetWorldText(___areaVisualizerText).GetComponent<LocText>();
                    Vector2 vector2 = ___areaVisualizerSpriteRenderer.size;
                    Patches.clampSize = Mathf.Min(Mathf.CeilToInt((float)(Mathf.RoundToInt(vector2.x) * 2 / 3)), Mathf.RoundToInt(vector2.y));
                    if (CameraController.Instance.IsVisiblePos(___downPos))
                    {
                        return;
                    }
                    Vector2 rhs = Camera.main.ViewportToWorldPoint(new Vector3(1f, 1f, Camera.main.transform.GetPosition().z));
                    Vector2 rhs2 = Camera.main.ViewportToWorldPoint(new Vector3(0f, 0f, Camera.main.transform.GetPosition().z));
                    Vector2 lhs = Vector3.Max(___downPos, ___previousCursorPos);
                    Vector2 lhs2 = Vector3.Min(___downPos, ___previousCursorPos);
                    Vector2 vector3 = Vector2.Min(lhs, rhs);
                    Vector2 vector4 = Vector2.Max(lhs2, rhs2);
                    vector3.x = Mathf.Clamp(vector3.x, ClusterManager.Instance.activeWorld.minimumBounds.x, ClusterManager.Instance.activeWorld.maximumBounds.x);
                    vector3.y = Mathf.Clamp(vector3.y, ClusterManager.Instance.activeWorld.minimumBounds.y, ClusterManager.Instance.activeWorld.maximumBounds.y);
                    vector4.x = Mathf.Clamp(vector4.x, ClusterManager.Instance.activeWorld.minimumBounds.x, ClusterManager.Instance.activeWorld.maximumBounds.x);
                    vector4.y = Mathf.Clamp(vector4.y, ClusterManager.Instance.activeWorld.minimumBounds.y, ClusterManager.Instance.activeWorld.maximumBounds.y);
                    Vector3 b = new Vector3(Grid.HalfCellSizeInMeters, Grid.HalfCellSizeInMeters, 0f);
                    vector3 = Grid.CellToPosCCC(Grid.PosToCell(vector3), Grid.SceneLayer.Background) + b;
                    vector4 = Grid.CellToPosCCC(Grid.PosToCell(vector4), Grid.SceneLayer.Background) - b;
                    Vector2 v = (vector3 + vector4) * 0.5f;
                    vector2 = vector3 - vector4;
                    Patches.clampSize = Mathf.Min(Mathf.CeilToInt((float)(Mathf.RoundToInt(vector2.x) * 2 / 3)), Mathf.RoundToInt(vector2.y));
                    component.transform.SetPosition(v);
                }
            }
        }

        [HarmonyPatch(typeof(PlayerController))]
        [HarmonyPatch("StopDrag")]
        public class PlayerController_StopDrag_Patch
        {
            public static void Prefix()
            {
                Patches._panRight = false;
                Patches._panLeft = false;
                Patches._panUp = false;
                Patches._panDown = false;
                Patches._isMousePanning = false;
            }
        }

        [HarmonyPatch(typeof(CameraController))]
        [HarmonyPatch("NormalCamUpdate")]
        public class CameraController_NormalCamUpdate_Patch
        {
            public static void Prefix(ref bool ___panLeft, ref bool ___panRight, ref bool ___panUp, ref bool ___panDown, out Patches.CameraController_NormalCamUpdate_Patch.oldPanning __state)
            {
                __state.r = ___panRight;
                __state.l = ___panLeft;
                __state.u = ___panUp;
                __state.d = ___panDown;
                if (Patches._isMousePanning)
                {
                    ___panLeft = Patches._panLeft;
                    ___panRight = Patches._panRight;
                    ___panUp = Patches._panUp;
                    ___panDown = Patches._panDown;
                }
            }

            public static void Postfix(ref bool ___panLeft, ref bool ___panRight, ref bool ___panUp, ref bool ___panDown, Patches.CameraController_NormalCamUpdate_Patch.oldPanning __state)
            {
                if (Patches._isMousePanning)
                {
                    ___panLeft = __state.l;
                    ___panRight = __state.r;
                    ___panUp = __state.u;
                    ___panDown = __state.d;
                }
                if (Patches.areaVisText != Guid.Empty)
                {
                    TMP_Text component = NameDisplayScreen.Instance.GetWorldText(Patches.areaVisText).GetComponent<LocText>();
                    Camera main = Camera.main;
                    float num = Mathf.Clamp(Mathf.Min((float)(Patches.clampSize * 10), main.orthographicSize), 8f, 60f) * 0.0025f;
                    component.transform.localScale = new Vector3(num, num, 1f);
                }
            }

            public struct oldPanning
            {
                public bool r;
                public bool l;
                public bool u;
                public bool d;
            }
        }
    }
    #endregion

    #region Mod: No 'Long Commutes'
    [HarmonyPatch(typeof(Tutorial))]
    [HarmonyPatch("LongTravelTimes")]
    public class Tutorial_LongTravelTimes_Patch
    {
        public static bool Prefix(ref bool __result)
        {
            __result = true;
            return false;
        }
    }
    #endregion

    #region Mod: Bigger Camera Zoom Out
    public static class BiggerCameraZoomOutPatches
    {
        private static readonly float _maxZoom = 200f;

        [HarmonyPatch(typeof(CameraController))]
        [HarmonyPatch("OnPrefabInit")]
        public static class CameraController_OnPrefabInit_Patch
        {
            public static void Prefix(CameraController __instance)
            {
                Traverse.Create(__instance).Field("maxOrthographicSize").SetValue(BiggerCameraZoomOutPatches._maxZoom);
            }
        }

        [HarmonyPatch(typeof(CameraController))]
        [HarmonyPatch("SetMaxOrthographicSize")]
        public static class CameraController_SetMaxOrthographicSize_Patch
        {
            public static void Prefix(ref float size)
            {
                size = BiggerCameraZoomOutPatches._maxZoom;
            }
        }

        [HarmonyPatch(typeof(CameraController))]
        [HarmonyPatch("ConstrainToWorld")]
        public static class CameraController_ConstrainToWorld_Patch
        {
            public static bool Prefix()
            {
                return false;
            }
        }

        [HarmonyPatch(typeof(WattsonMessage))]
        [HarmonyPatch("OnDeactivate")]
        public static class WattsonMessage_OnDeactivate_Patch
        {
            public static void Postfix()
            {
                UIScheduler instance = UIScheduler.Instance;
                if (instance == null)
                {
                    return;
                }
                instance.Schedule("zoomConfig", 0.7f, delegate (object data)
                {
                    CameraController.Instance.SetMaxOrthographicSize(BiggerCameraZoomOutPatches._maxZoom);
                }, null, null);
            }
        }

        [HarmonyPatch(typeof(ClusterMapScreen))]
        [HarmonyPatch("OnKeyDown")]
        public static class ClusterMapScreen_OnKeyDown_Patch
        {
            public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                List<CodeInstruction> list = new List<CodeInstruction>(instructions);
                for (int i = 1; i < list.Count; i++)
                {
                    if (list[i].opcode == OpCodes.Ldc_R4 && (float)list[i].operand == 50f)
                    {
                        list[i].operand = 20f;
                        break;
                    }
                }
                return list.AsEnumerable<CodeInstruction>();
            }
        }
    }
    #endregion

    #region Mod: Bigger Building Menu *TODO*
    //public class BiggerBuildingMenuPatches
    //{
    //    [HarmonyPatch(typeof(PlanScreen))]
    //    [HarmonyPatch("ConfigurePanelSize")]
    //    public static class PlanScreen_ConfigurePanelSize_Patch
    //    {
    //        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    //        {
    //            List<CodeInstruction> list = new List<CodeInstruction>(instructions);
    //            for (int i = 1; i < list.Count; i++)
    //            {
    //                if (list[i].opcode == OpCodes.Ldc_I4_6)
    //                {
    //                    list[i].opcode = OpCodes.Ldc_I4;
    //                    list[i].operand = ModInit.ConfigManager.Config.Height;
    //                    break;
    //                }
    //            }
    //            return list.AsEnumerable<CodeInstruction>();
    //        }
    //    }
    //}
    #endregion

    #region Mod: Plan Buildings Without Materials
    public class PlanBuildingsWithoutMaterialsPatches
    {
        [HarmonyPatch(typeof(MaterialSelector))]
        [HarmonyPatch("AllowInsufficientMaterialBuild")]
        public static class MaterialSelector_AllowInsufficientMaterialBuild_Patch
        {
            public static bool Prefix(ref bool __result)
            {
                __result = true;
                return false;
            }
        }

        [HarmonyPatch(typeof(PlanScreen))]
        [HarmonyPatch("GetBuildableStateForDef")]
        public static class PlanScreen_GetBuildableStateForDef_Patch
        {
            public static void Postfix(ref PlanScreen.RequirementsState __result)
            {
                if (__result == PlanScreen.RequirementsState.Materials)
                {
                    __result = PlanScreen.RequirementsState.Complete;
                }
            }
        }
    }
    #endregion

    #region Mod: Conveyor Rail Filter
    // TODO
    #endregion

    #region Mod: Oil Well - Any Water
    [HarmonyPatch(typeof(OilWellCapConfig))]
    [HarmonyPatch("ConfigureBuildingTemplate")]
    public class OilWellCapConfig_ConfigureBuildingTemplate_Patch
    {
        public static void Postfix(ref GameObject go)
        {
            go.AddOrGet<ElementConverter>().consumedElements = new ElementConverter.ConsumedElement[]
            {
                    new ElementConverter.ConsumedElement(GameTags.AnyWater, 1f, true)
            };
            go.AddOrGet<ConduitConsumer>().capacityTag = GameTags.AnyWater;
        }
    }
    #endregion

    #region Mod: Wounded Go To Med Bed
    public class WoundedGoToMedBedPatches
    {
        [HarmonyPatch(typeof(WoundMonitor))]
        [HarmonyPatch("InitializeStates")]
        public class WoundMonitorInitializeStates
        {
            public static void Postfix(ref WoundMonitor __instance)
            {
                __instance.wounded.Exit(new StateMachine<WoundMonitor, WoundMonitor.Instance, IStateMachineTarget, object>.State.Callback(WoundedGoToMedBedPatches.WoundMonitorInitializeStates.UnassignClinic));
                __instance.wounded.light.ToggleUrge(Db.Get().Urges.Heal).Update("AutoAssignClinic", delegate (WoundMonitor.Instance smi, float dt)
                {
                    WoundedGoToMedBedPatches.WoundMonitorInitializeStates.AutoAssignClinic(smi);
                }, UpdateRate.SIM_1000ms, false);
                __instance.wounded.medium.ToggleUrge(Db.Get().Urges.Heal).Update("AutoAssignClinic", delegate (WoundMonitor.Instance smi, float dt)
                {
                    WoundedGoToMedBedPatches.WoundMonitorInitializeStates.AutoAssignClinic(smi);
                }, UpdateRate.SIM_1000ms, false);
                __instance.wounded.heavy.ToggleUrge(Db.Get().Urges.Heal).Update("AutoAssignClinic", delegate (WoundMonitor.Instance smi, float dt)
                {
                    WoundedGoToMedBedPatches.WoundMonitorInitializeStates.AutoAssignClinic(smi);
                }, UpdateRate.SIM_1000ms, false);
            }
            public static void AutoAssignClinic(WoundMonitor.Instance smi)
            {
                Ownables soleOwner = smi.sm.masterTarget.Get(smi).GetComponent<MinionIdentity>().GetSoleOwner();
                AssignableSlot clinic = Db.Get().AssignableSlots.Clinic;
                AssignableSlotInstance slot = soleOwner.GetSlot(clinic);
                if (slot == null || slot.assignable != null)
                {
                    return;
                }
                soleOwner.AutoAssignSlot(clinic);
            }

            public static void UnassignClinic(WoundMonitor.Instance smi)
            {
                AssignableSlotInstance slot = smi.sm.masterTarget.Get(smi).GetComponent<MinionIdentity>().GetSoleOwner().GetSlot(Db.Get().AssignableSlots.Clinic);
                if (slot == null)
                {
                    return;
                }
                slot.Unassign(true);
            }
        }
    }
    #endregion

    #region Mod: Geyser Calculated Average Output Tooltip
    public class GeyserCalculatedAvgOutputTooltipPatches
    {
        private static readonly LocString GeyserAvgOutputAnalyse = "Calculated Average Output: (Requires Analysis)";

        private static readonly LocString GeyserAvgOutputAnalyseTooltip = "A researcher must analyze this geyser to determine its average output.";

        private static readonly LocString GeyserAvgOutput = "Calculated Average Output: {0} {1}";

        private static readonly LocString GeyserAvgOutputTooltip = "Taking into account its eruption rates and dormant times, this geyser average output is {0} {1}";

        [HarmonyPatch(typeof(Geyser))]
        [HarmonyPatch("GetDescriptors")]
        public static class Geyser_GetDescriptors_Patch
        {
            public static void Postfix(ref Geyser __instance, ref List<Descriptor> __result)
            {
                Studyable component = __instance.GetComponent<Studyable>();
                if (component && !component.Studied)
                {
                    __result.Add(new Descriptor(GeyserCalculatedAvgOutputTooltipPatches.GeyserAvgOutputAnalyse, GeyserCalculatedAvgOutputTooltipPatches.GeyserAvgOutputAnalyseTooltip, Descriptor.DescriptorType.Effect, false));
                    return;
                }
                float num = __instance.configuration.GetEmitRate() * 1000f;
                float onDuration = __instance.configuration.GetOnDuration();
                float iterationLength = __instance.configuration.GetIterationLength();
                float num2 = __instance.configuration.GetYearOnDuration() / 600f;
                float num3 = __instance.configuration.GetYearLength() / 600f;
                float num4 = onDuration / iterationLength * (num2 / num3) * num;
                string arg = "g/s";
                if (num4 > 1000f)
                {
                    num4 /= 1000f;
                    arg = "kg/s";
                }
                string arg2 = num4.ToString("0.00");
                __result.Add(new Descriptor(string.Format(GeyserCalculatedAvgOutputTooltipPatches.GeyserAvgOutput, arg2, arg), string.Format(GeyserCalculatedAvgOutputTooltipPatches.GeyserAvgOutputTooltip, arg2, arg), Descriptor.DescriptorType.Effect, false));
            }
        }
    }
    #endregion

    #region Mod: Clothing Locker
    public class ClothingLockerConfig : IBuildingConfig
    {
        public override BuildingDef CreateBuildingDef()
        {
            BuildingDef buildingDef = BuildingTemplates.CreateBuildingDef("asquared31415_ClothingLockerConfig", 1, 2, "setpiece_locker_kanim", 50, 30f, TUNING.BUILDINGS.CONSTRUCTION_MASS_KG.TIER2, MATERIALS.RAW_METALS, 1600f, BuildLocationRule.OnFloor, DECOR.PENALTY.TIER1, NOISE_POLLUTION.NONE, 0.2f);
            buildingDef.Floodable = false;
            buildingDef.Overheatable = false;
            return buildingDef;
        }

        public override void ConfigureBuildingTemplate(GameObject go, Tag prefabTag)
        {
            SoundEventVolumeCache.instance.AddVolume("storagelocker_kanim", "StorageLocker_Hit_metallic_low", NOISE_POLLUTION.NOISY.TIER1);
            Prioritizable.AddRef(go);
            Storage storage = go.AddOrGet<Storage>();
            storage.showInUI = true;
            storage.allowItemRemoval = true;
            storage.showDescriptor = true;
            storage.storageFilters = new List<Tag>
            {
                GameTags.Clothes
            };
            storage.storageFullMargin = STORAGE.STORAGE_LOCKER_FILLED_MARGIN;
            storage.fetchCategory = Storage.FetchCategory.GeneralStorage;
            go.AddOrGet<CopyBuildingSettings>().copyGroupTag = "asquared31415_ClothingLockerConfig";
            go.AddOrGet<StorageLocker>();
            go.AddOrGet<UserNameable>();
        }

        public override void DoPostConfigureComplete(GameObject go)
        {
            go.AddOrGetDef<StorageController.Def>();
        }

        public const string Id = "asquared31415_ClothingLockerConfig";

        public const string Anim = "setpiece_locker_kanim";

        public const string Name = "Clothing Locker";

        public const string Effect = "Stores the clothing of your choosing.";

        public const string Desc = "Duplicants decided that putting clothes in with their debris was a bad idea.  So they invented a storage bin specifically for storing clothing!";
    }
    #endregion

    #region Mod: Wrangle & Carryno
    // TODO
    #endregion

    #region Mod: Customizable Speed
    [HarmonyPatch(typeof(SpeedControlScreen), "OnChanged")]
    public static class SpeedControlPatchOnChanged
    {
        public static bool Prefix(SpeedControlScreen __instance)
        {
            if (__instance.IsPaused)
            {
                Time.timeScale = 0f;
            }
            else
            {
                switch (__instance.GetSpeed())
                {
                    case 0:
                        Time.timeScale = ModInit.Config.SlowSpeed;
                        break;
                    case 1:
                        Time.timeScale = ModInit.Config.NormalSpeed;
                        break;
                    case 2:
                        Time.timeScale = ModInit.Config.SuperSpeed;
                        break;
                }
            }
            return false;
        }
    }
    #endregion

    #region Mod: Better Rad Pills Threshold
    public static class BetterRadPillsPatches
    {
        [HarmonyPatch(typeof(MedicinalPillWorkable), "CanBeTakenBy")]
        public static class MedicinalPillWorkable_CanBeTakenBy_Patch
        {
            public static void Postfix(MedicinalPill ___pill, GameObject consumer, ref bool __result)
            {
                if (!__result || ___pill == null || ___pill.info.id != "BasicRadPill") return;

                var radiationAmount = consumer.GetAmounts()?.Get(Db.Get().Amounts.RadiationBalance.Id);
                if (radiationAmount != null && radiationAmount.value < ModInit.Config.Rads)
                {
                    __result = false;
                }
            }
        }

        [HarmonyPatch(typeof(MedicinalPillWorkable), "OnSpawn")]
        public static class MedicinalPillWorkable_OnSpawn_Patch
        {
            public static void Postfix(MedicinalPillWorkable __instance)
            {
                if (__instance?.pill?.info != null && __instance.pill.info.id == "BasicRadPill" && ModInit.Config.FasterAnim)
                {
                    __instance.SetWorkTime(1f);
                }
            }
        }
    }
    #endregion

    #region Mod: No Sensor Limits
    public static class NoSensorLimitsPatches
    {
        private const float AFFECT_LIMITS_BELOW = 9000f;
        private static Type[] AFFECT_TYPES;
        private static Type[] CAPACITY_TYPES;

        private delegate void UpdateTargetThresholdLabel(ThresholdSwitchSideScreen screen);
        private delegate void UpdateMaxCapacityLabel(CapacityControlSideScreen screen);

        private static readonly UpdateMaxCapacityLabel UPDATE_MAX_CAPACITY_LABEL =
            typeof(CapacityControlSideScreen).Detour<UpdateMaxCapacityLabel>();
        private static readonly UpdateTargetThresholdLabel UPDATE_TARGET_THRESHOLD_LABEL =
            typeof(ThresholdSwitchSideScreen).Detour<UpdateTargetThresholdLabel>();

        public static void InitializeTypes()
        {
            AFFECT_TYPES = new Type[]
            {
                typeof(LogicWattageSensor),
                typeof(LogicDiseaseSensor),
                typeof(ConduitDiseaseSensor),
                typeof(LogicLightSensor),
                PPatchTools.GetTypeSafe("ResourceSensor.LogicResourceSensor", null)
            };
            CAPACITY_TYPES = new Type[]
            {
                typeof(CreatureDeliveryPoint),
                PPatchTools.GetTypeSafe("BaggableCritterCapacityTracker", "Assembly-CSharp")
            };
        }

        private static bool HasCompatibilityType(GameObject target, out Component result)
        {
            result = null;
            if (target == null || CAPACITY_TYPES == null) return false;
            for (int i = 0; i < CAPACITY_TYPES.Length; i++)
            {
                Type type = CAPACITY_TYPES[i];
                if (type != null && target.TryGetComponent(type, out result))
                    return true;
            }
            return false;
        }

        private static bool IsCompatibilityType(object target)
        {
            if (target == null || CAPACITY_TYPES == null) return false;
            Type targetType = target.GetType();
            for (int i = 0; i < CAPACITY_TYPES.Length; i++)
            {
                Type type = CAPACITY_TYPES[i];
                if (type != null && type.IsAssignableFrom(targetType))
                    return true;
            }
            return false;
        }

        private static bool ShouldAffect(float normalMax, object target)
        {
            bool flag = normalMax <= AFFECT_LIMITS_BELOW;
            if (target != null && AFFECT_TYPES != null)
            {
                Type targetType = target.GetType();
                for (int i = 0; i < AFFECT_TYPES.Length && !flag; i++)
                {
                    Type type = AFFECT_TYPES[i];
                    if (type != null && type.IsAssignableFrom(targetType))
                        flag = true;
                }
            }
            return flag;
        }

        [HarmonyPatch(typeof(CapacityControlSideScreen), "SetTarget")]
        public static class CapacityControlSideScreen_SetTarget_Patch
        {
            internal static void Postfix(KNumberInputField ___numberInput, GameObject new_target)
            {
                float normalMax = ___numberInput != null ? ___numberInput.maxValue : 0f;
                if (new_target != null && HasCompatibilityType(new_target, out Component target) && ShouldAffect(normalMax, target))
                {
                    ___numberInput.maxValue = float.MaxValue;
                }
            }
        }

        [HarmonyPatch(typeof(CapacityControlSideScreen), "UpdateMaxCapacity")]
        public static class CapacityControlSideScreen_UpdateMaxCapacity_Patch
        {
            internal static bool Prefix(IUserControlledCapacity ___target, float newValue, KSlider ___slider, CapacityControlSideScreen __instance)
            {
                float maxCapacity = ___target.MaxCapacity;
                bool flag = newValue > maxCapacity && IsCompatibilityType(___target) && ShouldAffect(maxCapacity, ___target);
                if (flag)
                {
                    ___target.UserMaxCapacity = newValue;
                    ___slider.value = maxCapacity;
                    UPDATE_MAX_CAPACITY_LABEL?.Invoke(__instance);
                }
                return !flag;
            }
        }

        [HarmonyPatch(typeof(ThresholdSwitchSideScreen), "SetTarget")]
        public static class ThresholdSwitchSideScreen_SetTarget_Patch
        {
            internal static void Postfix(KNumberInputField ___numberInput, GameObject new_target)
            {
                float normalMax = ___numberInput != null ? ___numberInput.maxValue : 0f;
                if (new_target != null && new_target.TryGetComponent<IThresholdSwitch>(out var target) && ShouldAffect(normalMax, target))
                {
                    ___numberInput.maxValue = float.MaxValue;
                }
            }
        }

        [HarmonyPatch(typeof(ThresholdSwitchSideScreen), "UpdateThresholdValue")]
        public static class ThresholdSwitchSideScreen_UpdateThresholdValue_Patch
        {
            internal static bool Prefix(float newValue, IThresholdSwitch ___thresholdSwitch, NonLinearSlider ___thresholdSlider, ThresholdSwitchSideScreen __instance)
            {
                float rangeMax = ___thresholdSwitch.RangeMax;
                bool flag = newValue > rangeMax && ShouldAffect(rangeMax, ___thresholdSwitch);
                if (flag)
                {
                    ___thresholdSwitch.Threshold = newValue;
                    if (___thresholdSlider != null)
                    {
                        ___thresholdSlider.value = ___thresholdSlider.GetPercentageFromValue(rangeMax);
                    }
                    UPDATE_TARGET_THRESHOLD_LABEL?.Invoke(__instance);
                }
                return !flag;
            }
        }
    }
    #endregion

    #region Mod: Longer Arms
    public static class LongerArmsPatches
    {
        private static bool OffsetsExpanded;
        private static readonly bool EnableDebugLogs;

        private static List<CellOffset[]> GenerateVerticalReachOffsets(int additionalReach, int vanillaMax)
        {
            List<CellOffset[]> list = new List<CellOffset[]>();
            if (additionalReach <= 0) return list;

            for (int i = 1; i <= additionalReach; i++)
            {
                int num = vanillaMax + i;
                List<CellOffset> list2 = new List<CellOffset>();
                for (int j = num; j >= 1; j--)
                {
                    list2.Add(new CellOffset(0, -j));
                }
                list.Add(list2.ToArray());
            }
            return list;
        }

        private static List<CellOffset[]> GenerateHorizontalReachOffsets(int additionalReach, int vanillaMax, int totalVerticalReach, bool safeMode)
        {
            List<CellOffset[]> list = new List<CellOffset[]>();
            if (additionalReach <= 0) return list;

            if (EnableDebugLogs && !safeMode && additionalReach > 2)
            {
                Debug.LogWarning($"[LongerArms] Safe mode disabled: Using full horizontal reach of {additionalReach} with unlimited diagonal paths - reach-through-walls may occur");
            }

            for (int i = 1; i <= additionalReach; i++)
            {
                int num = vanillaMax + i;
                List<CellOffset> list2 = new List<CellOffset>
                {
                    new CellOffset(num, 0),
                    new CellOffset(1, 0)
                };
                if (num >= 3)
                {
                    for (int j = 2; j < num; j++)
                    {
                        list2.Add(new CellOffset(j, 0));
                    }
                }
                list.Add(list2.ToArray());
            }

            if (safeMode)
            {
                int num2 = 3;
                int num3 = Math.Min(totalVerticalReach, num2);
                for (int k = 1; k <= num3; k++)
                {
                    int num4 = num2;
                    for (int l = 1; l <= num4; l++)
                    {
                        if (Math.Max(l, k) <= num2)
                        {
                            List<CellOffset> list3 = new List<CellOffset>
                            {
                                new CellOffset(l, -k),
                                new CellOffset(1, 0)
                            };
                            if (l >= 2)
                            {
                                for (int m = 2; m <= l; m++)
                                {
                                    list3.Add(new CellOffset(m, 0));
                                }
                            }
                            for (int n = 1; n < k; n++)
                            {
                                list3.Add(new CellOffset(l, -n));
                            }
                            list.Add(list3.ToArray());
                        }
                    }
                }
            }
            else
            {
                for (int num5 = 1; num5 <= totalVerticalReach; num5++)
                {
                    for (int num6 = 1; num6 <= additionalReach; num6++)
                    {
                        List<CellOffset> list4 = new List<CellOffset>
                        {
                            new CellOffset(num6, -num5),
                            new CellOffset(1, 0)
                        };
                        if (num6 >= 2)
                        {
                            for (int num7 = 2; num7 <= num6; num7++)
                            {
                                list4.Add(new CellOffset(num7, 0));
                            }
                        }
                        for (int num8 = 1; num8 < num5; num8++)
                        {
                            list4.Add(new CellOffset(num6, -num8));
                        }
                        list.Add(list4.ToArray());
                    }
                }
            }
            return list;
        }

        [HarmonyPatch(typeof(Game), "OnPrefabInit")]
        public static class GamePatch
        {
            public static void Postfix()
            {
                ExpandTables();
            }

            public static void ExpandTables()
            {
                if (!OffsetsExpanded)
                {
                    int verticalReach = ModInit.Config.VerticalReach;
                    int horizontalReach = ModInit.Config.HorizontalReach;
                    bool safeMode = ModInit.Config.SafeMode;

                    if (EnableDebugLogs)
                    {
                        Debug.Log($"[LongerArms] ExpandTables called, verticalReach = {verticalReach}, horizontalReach = {horizontalReach}, safeMode = {safeMode}");
                    }

                    int totalVerticalReach = 3 + verticalReach;
                    List<CellOffset[]> list = GenerateHorizontalReachOffsets(horizontalReach, 3, totalVerticalReach, safeMode);
                    List<CellOffset[]> list2 = GenerateVerticalReachOffsets(verticalReach, 3);
                    List<CellOffset[]> list3 = new List<CellOffset[]>();
                    list3.AddRange(list);
                    list3.AddRange(list2);

                    if (list3.Count > 0)
                    {
                        if (EnableDebugLogs)
                        {
                            Debug.Log($"[LongerArms] Generated {list.Count} horizontal path(s) and {list2.Count} vertical path(s) for additional reach");
                        }
                        ExpandTable(ref OffsetGroups.InvertedStandardTable, list3);
                        ExpandTable(ref OffsetGroups.InvertedStandardTableWithCorners, list3);
                        if (EnableDebugLogs)
                        {
                            Debug.Log($"[LongerArms] Tables expanded successfully with {list3.Count} total path(s)");
                        }
                    }
                    else if (EnableDebugLogs)
                    {
                        Debug.LogWarning("[LongerArms] No paths generated!");
                    }
                    OffsetsExpanded = true;
                }
            }

            public static void ExpandTable(ref CellOffset[][] inputTable, List<CellOffset[]> newPaths)
            {
                if (newPaths == null || newPaths.Count == 0) return;
                CellOffset[][] array = OffsetTable.Mirror(inputTable.ToList().Concat(newPaths).ToArray());
                inputTable = array;
            }
        }
    }
    #endregion

    #region Mod: Show Buildings Range
    public static class ShowRangePatches
    {
        private const string IGNORE_WALLPUMPS = "WallPumps.RotatableElementConsumer";

        private static void AddConsumerPreview(BuildingDef def)
        {
            GameObject buildingComplete = def.BuildingComplete;
            GameObject buildingPreview = def.BuildingPreview;
            GameObject buildingUnderConstruction = def.BuildingUnderConstruction;
            ElementConsumer[] components = buildingComplete.GetComponents<ElementConsumer>();
            var pooledDictionary = DictionaryPool<CellOffset, int, ElementConsumer>.Allocate();
            foreach (ElementConsumer elementConsumer in components)
            {
                if (elementConsumer.GetType().FullName != IGNORE_WALLPUMPS)
                {
                    int num = (int)(elementConsumer.consumptionRadius & byte.MaxValue);
                    Vector3 sampleCellOffset = elementConsumer.sampleCellOffset;
                    CellOffset key = new CellOffset(Mathf.RoundToInt(sampleCellOffset.x), Mathf.RoundToInt(sampleCellOffset.y));
                    if (!pooledDictionary.TryGetValue(key, out int num2) || num != num2)
                    {
                        PUtil.LogDebug("Visualizer added to {0}, range {1:D}".F(def.PrefabID, num));
                        if (num > num2)
                        {
                            pooledDictionary[key] = num;
                        }
                    }
                }
            }
            int count = pooledDictionary.Count;
            if (count > 0)
            {
                SimVisualizer[] array2 = new SimVisualizer[count];
                int num3 = 0;
                int num4 = 0;
                foreach (KeyValuePair<CellOffset, int> keyValuePair in pooledDictionary)
                {
                    CellOffset key2 = keyValuePair.Key;
                    int value = keyValuePair.Value;
                    int num5 = Mathf.Abs(key2.x) + value;
                    int num6 = Mathf.Abs(key2.y) + value;
                    array2[num3++] = new SimVisualizer(key2, value);
                    if (num5 > num4) num4 = num5;
                    if (num6 > num4) num4 = num6;
                }
                SimRangeVisualizer.Create(buildingComplete, array2, num4, default);
                if (buildingPreview != null)
                {
                    SimRangeVisualizer.Create(buildingPreview, array2, num4, default);
                }
                if (buildingUnderConstruction != null)
                {
                    SimRangeVisualizer.Create(buildingUnderConstruction, array2, num4, default);
                }
            }
            pooledDictionary.Recycle();
        }

        private static void AddRangePreviews(BuildingDef def)
        {
            AddConsumerPreview(def);
        }

        [HarmonyPatch(typeof(CameraController), "OnPrefabInit")]
        public static class CameraController_OnPrefabInit_Patch
        {
            internal static void Postfix(CameraController __instance)
            {
                __instance.overlayNoDepthCamera.gameObject.AddComponent<SimRangeVisualizer>();
            }
        }

        [HarmonyPatch(typeof(GeneratedBuildings), "LoadGeneratedBuildings")]
        public static class GeneratedBuildings_LoadGeneratedBuildings_Patch
        {
            internal static void Postfix()
            {
                foreach (BuildingDef buildingDef in Assets.BuildingDefs)
                {
                    if (buildingDef != null && buildingDef.BuildingComplete != null)
                    {
                        AddRangePreviews(buildingDef);
                    }
                }
            }
        }
    }

    public readonly struct SimVisualizer : IEquatable<SimVisualizer>
    {
        public readonly CellOffset offset;
        public readonly int radius;

        public SimVisualizer(CellOffset offset, int radius)
        {
            this.offset = offset;
            this.radius = radius;
        }

        public override bool Equals(object obj)
        {
            return obj is SimVisualizer other && Equals(other);
        }

        public bool Equals(SimVisualizer other)
        {
            return other.offset.Equals(offset) && other.radius == radius;
        }

        public override int GetHashCode()
        {
            return (offset.GetHashCode() << 8) + radius;
        }

        public override string ToString()
        {
            return $"SimVisualizer[offset=({offset.x},{offset.y}),radius={radius}";
        }
    }

    [SkipSaveFileSerialization]
    public sealed class SimVisualizerParams : MonoBehaviour
    {
        public Color highlightColor;
        public SimVisualizer[] visualizers;
        public int worstCaseRadius;
    }

    internal sealed class ElementConsumerVisualizer : ColoredRangeVisualizer
    {
        private Color color;
        private CellOffset offset;
        private readonly PriorityDictionary<int, int> queue;
        private int radius;

        public static void Create(GameObject template, CellOffset offset, int radius, Color color = default)
        {
            if (color == default)
            {
                color = Color.white;
            }
            if (template == null)
            {
                throw new ArgumentNullException(nameof(template));
            }
            if (radius > 0 && template.TryGetComponent<KPrefabID>(out var kprefabID))
            {
                kprefabID.instantiateFn += delegate (GameObject obj)
                {
                    ElementConsumerVisualizer elementConsumerVisualizer = obj.AddComponent<ElementConsumerVisualizer>();
                    elementConsumerVisualizer.color = color;
                    elementConsumerVisualizer.offset = offset;
                    elementConsumerVisualizer.radius = radius;
                };
            }
        }

        internal ElementConsumerVisualizer()
        {
            color = Color.white;
            offset = default;
            queue = new PriorityDictionary<int, int>(64);
            radius = 1;
        }

        protected override void VisualizeCells(ICollection<ColoredRangeVisualizer.VisCellData> newCells)
        {
            int num = RotateOffsetCell(Grid.PosToCell(gameObject), offset);
            if (Grid.IsValidCell(num))
            {
                Element element = Grid.Element[num];
                if (element != null && !element.IsSolid)
                {
                    var pooledHashSet = HashSetPool<int, ElementConsumerVisualizer>.Allocate();
                    try
                    {
                        SimRangeVisualizer.FindReachableCells(queue, pooledHashSet, num, radius);
                        foreach (int cell in pooledHashSet)
                        {
                            newCells.Add(new ColoredRangeVisualizer.VisCellData(cell, color));
                        }
                    }
                    finally
                    {
                        pooledHashSet.Recycle();
                    }
                }
            }
        }
    }

    internal sealed class SimRangeVisualizer : MonoBehaviour
    {
        private const int OCCLUSION_HEIGHT = 64;
        private const int OCCLUSION_WIDTH = 64;

        private Camera cachedCamera;
        private int lastCell;
        private Transform lastTransform;
        private readonly string moveSound;
        private Texture2D occlusionTexture;
        private readonly int propHighlightColor;
        private readonly int propOcclusionParams;
        private readonly int propOcclusionTex;
        private readonly int propRangeParams;
        private readonly int propUVOffsetScale;
        private readonly int propWorldParams;
        private readonly PriorityDictionary<int, int> queue;
        private Material shader;

        private static Vector3 CalculateRaycast(Camera viewingCamera, Vector3 point)
        {
            Ray ray = viewingCamera.ViewportPointToRay(point);
            return ray.GetPoint(Mathf.Abs(ray.origin.z / ray.direction.z));
        }

        public static void Create(GameObject template, SimVisualizer[] visualizers, int worstCaseRadius, Color color = default)
        {
            if (color == default)
            {
                color = new Color(0f, 1f, 0.8f, 1f);
            }
            if (template == null)
            {
                throw new ArgumentNullException(nameof(template));
            }
            if (visualizers != null && template.TryGetComponent<KPrefabID>(out var kprefabID))
            {
                kprefabID.instantiateFn += delegate (GameObject obj)
                {
                    SimVisualizerParams simVisualizerParams = obj.AddComponent<SimVisualizerParams>();
                    simVisualizerParams.highlightColor = color;
                    simVisualizerParams.visualizers = visualizers;
                    simVisualizerParams.worstCaseRadius = worstCaseRadius;
                };
            }
        }

        private static void EnqueueIfPassable(ICollection<int> seen, int newCell, int cost, PriorityDictionary<int, int> queue)
        {
            if (Grid.IsValidCell(newCell))
            {
                Element element = Grid.Element[newCell];
                if (element != null && !element.IsSolid && !seen.Contains(newCell))
                {
                    seen.Add(newCell);
                    queue.Enqueue(cost, newCell);
                }
            }
        }

        internal static void FindReachableCells(PriorityDictionary<int, int> queue, ICollection<int> seen, int startCell, int radius)
        {
            queue.Enqueue(0, startCell);
            seen.Add(startCell);
            do
            {
                queue.Dequeue(out int num, out int cell);
                if (num < radius - 1)
                {
                    EnqueueIfPassable(seen, Grid.CellLeft(cell), num + 1, queue);
                    EnqueueIfPassable(seen, Grid.CellRight(cell), num + 1, queue);
                    EnqueueIfPassable(seen, Grid.CellAbove(cell), num + 1, queue);
                    EnqueueIfPassable(seen, Grid.CellBelow(cell), num + 1, queue);
                }
            }
            while (queue.Count > 0);
        }

        private static bool GetSelectedTarget(out Transform target, out SimVisualizerParams visualizerParams)
        {
            KSelectable selected = SelectTool.Instance.selected;
            bool result = false;
            if (selected == null)
            {
                GameObject visualizer = BuildTool.Instance.visualizer;
                target = (visualizer != null) ? visualizer.transform : null;
            }
            else
            {
                target = selected.transform;
            }
            if (target == null)
            {
                visualizerParams = null;
            }
            else
            {
                result = target.TryGetComponent(out visualizerParams);
            }
            return result;
        }

        internal SimRangeVisualizer()
        {
            lastCell = 0;
            lastTransform = null;
            moveSound = GlobalAssets.GetSound("RangeVisualization_movement", false);
            propHighlightColor = Shader.PropertyToID("_HighlightColor");
            propOcclusionParams = Shader.PropertyToID("_OcclusionParams");
            propOcclusionTex = Shader.PropertyToID("_OcclusionTex");
            propRangeParams = Shader.PropertyToID("_RangeParams");
            propUVOffsetScale = Shader.PropertyToID("_UVOffsetScale");
            propWorldParams = Shader.PropertyToID("_WorldParams");
            queue = new PriorityDictionary<int, int>(64);
        }

        internal void OnPostRender()
        {
            if (GetSelectedTarget(out Transform transform, out SimVisualizerParams simVisualizerParams))
            {
                Vector3 position = transform.position;
                int widthInCells = Grid.WidthInCells;
                int heightInCells = Grid.HeightInCells;
                int num = Grid.PosToCell(position);
                int num2 = Mathf.Min(simVisualizerParams.worstCaseRadius, 64);
                Grid.PosToXY(position, out int num3, out int num4);
                if (lastCell != num || lastTransform != transform)
                {
                    SoundEvent.PlayOneShot(moveSound, position, 1f);
                    lastCell = num;
                    lastTransform = transform;
                    UpdateLocation(simVisualizerParams.visualizers, num2);
                    shader.SetColor(propHighlightColor, simVisualizerParams.highlightColor);
                    shader.SetVector(propOcclusionParams, new Vector4(0.015625f, 0.015625f, 0f, 0f));
                    shader.SetVector(propWorldParams, new Vector4((float)widthInCells, (float)heightInCells, 1f / (float)widthInCells, 1f / (float)heightInCells));
                    shader.SetTexture(propOcclusionTex, occlusionTexture);
                }
                Vector3 vector = CalculateRaycast(cachedCamera, Vector3.zero);
                Vector3 vector2 = CalculateRaycast(cachedCamera, Vector3.one);
                float x = vector.x;
                float y = vector.y;
                shader.SetVector(propUVOffsetScale, new Vector4(x, y, vector2.x - x, vector2.y - y));
                shader.SetVector(propRangeParams, new Vector4((float)(num3 - num2), (float)(num4 - num2), (float)(num3 + num2), (float)(num4 + num2)));
                GL.PushMatrix();
                shader.SetPass(0);
                GL.LoadOrtho();
                GL.Begin(5);
                GL.Color(Color.white);
                GL.Vertex3(0f, 0f, 0f);
                GL.Vertex3(0f, 1f, 0f);
                GL.Vertex3(1f, 0f, 0f);
                GL.Vertex3(1f, 1f, 0f);
                GL.End();
                GL.PopMatrix();
                return;
            }
            lastCell = Grid.InvalidCell;
            lastTransform = null;
        }

        internal void OnDestroy()
        {
            if (shader != null)
            {
                UnityEngine.Object.Destroy(shader);
            }
            if (occlusionTexture != null)
            {
                UnityEngine.Object.Destroy(occlusionTexture);
            }
            cachedCamera = null;
            lastTransform = null;
        }

        internal void Start()
        {
            lastCell = Grid.InvalidCell;
            lastTransform = null;
            if (shader == null)
            {
                shader = new Material(Shader.Find("Klei/PostFX/Range"));
            }
            if (cachedCamera == null)
            {
                TryGetComponent(out cachedCamera);
            }
            if (occlusionTexture == null)
            {
                occlusionTexture = new Texture2D(64, 64, TextureFormat.Alpha8, false)
                {
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Clamp
                };
            }
        }

        private void UpdateLocation(SimVisualizer[] visualizers, int radius)
        {
            int num = visualizers.Length;
            int num2 = lastCell;
            bool flag = lastTransform.TryGetComponent(out Rotatable rotatable);
            var pooledHashSet = HashSetPool<int, SimRangeVisualizer>.Allocate();
            for (int i = 0; i < num; i++)
            {
                SimVisualizer simVisualizer = visualizers[i];
                CellOffset offset = simVisualizer.offset;
                if (flag)
                {
                    offset = rotatable.GetRotatedCellOffset(offset);
                }
                int num3 = Grid.OffsetCell(num2, offset);
                if (Grid.IsValidCell(num3))
                {
                    Element element = Grid.Element[num3];
                    if (element != null && !element.IsSolid)
                    {
                        FindReachableCells(queue, pooledHashSet, num3, simVisualizer.radius);
                    }
                }
            }
            NativeArray<byte> pixelData = occlusionTexture.GetPixelData<byte>(0);
            int num4 = 1 + (radius << 1);
            int num5 = 0;
            int num6 = Grid.WidthInCells - num4;
            int num7 = 64 - num4;
            int num8 = num2 - radius * (Grid.WidthInCells + 1);
            for (int j = num4; j > 0; j--)
            {
                for (int k = num4; k > 0; k--)
                {
                    pixelData[num5++] = (byte)(pooledHashSet.Contains(num8++) ? byte.MaxValue : 0);
                }
                num5 += num7;
                num8 += num6;
            }
            occlusionTexture.Apply(false, false);
            pooledHashSet.Recycle();
        }
    }
    #endregion

    #region Mod: Queue For Sinks #TODO
    public static class QueueForSinkPatches
    {
        [HarmonyPatch(typeof(HandSanitizer.Work), "OnPrefabInit")]
        public static class HandSanitizer_Work_OnPrefabInit_Patch
        {
            internal static void Postfix(HandSanitizer.Work __instance)
            {
                __instance.gameObject.AddOrGet<SinkCheckpoint>();
            }
        }

        [HarmonyPatch(typeof(OreScrubber.Work), "OnPrefabInit")]
        public static class OreScrubberConfig_OnPrefabInit_Patch
        {
            internal static void Postfix(OreScrubber.Work __instance)
            {
                __instance.gameObject.AddOrGet<ScrubberCheckpoint>();
            }
        }
    }

    public sealed class ScrubberCheckpoint : WorkCheckpoint<OreScrubber.Work>
    {
        private readonly int buildingLayer;

        public ScrubberCheckpoint()
        {
            buildingLayer = (int)PGameUtils.GetObjectLayer("Building", ObjectLayer.Building);
        }

        private bool CheckForOtherScrubber(bool dir)
        {
            GameObject gameObject = base.gameObject;
            bool flag = true;
            if (gameObject != null && Grid.IsValidCell(Grid.PosToCell(gameObject)))
            {
                int cell = Grid.PosToCell(gameObject);
                int num = 3;
                if (gameObject.TryGetComponent<Building>(out var building))
                {
                    num = building.Def.WidthInCells;
                }
                cell = Grid.OffsetCell(cell, new CellOffset(dir ? num : -num, 0));
                if (Grid.IsValidBuildingCell(cell) && (Grid.Objects[cell, buildingLayer] is GameObject gameObject2))
                {
                    flag = gameObject.PrefabID() != gameObject2.PrefabID() ||
                           !gameObject2.TryGetComponent<Operational>(out var operational) || !operational.IsOperational ||
                           !gameObject2.TryGetComponent<DirectionControl>(out var directionControl) ||
                           directionControl.allowedDirection != direction.allowedDirection;

                    if (!flag && gameObject2.TryGetComponent<ScrubberCheckpoint>(out var scrubberCheckpoint) && scrubberCheckpoint.inUse && gameObject2 != gameObject)
                    {
                        flag = scrubberCheckpoint.CheckForOtherScrubber(dir);
                    }
                }
            }
            return flag;
        }

        protected override bool MustStop(GameObject reactor, float dir)
        {
            bool flag = false;
            if (reactor.TryGetComponent<Storage>(out var storage))
            {
                foreach (GameObject item in storage.items)
                {
                    if (item.TryGetComponent<PrimaryElement>(out var primaryElement) &&
                        primaryElement.DiseaseIdx != SimUtil.DiseaseInfo.Invalid.idx &&
                        !item.HasTag(GameTags.Edible))
                    {
                        flag = true;
                        break;
                    }
                }
            }
            return flag && CheckForOtherScrubber(dir > 0f);
        }
    }

    public sealed class SinkCheckpoint : WorkCheckpoint<HandSanitizer.Work>
    {
        private readonly int buildingLayer;

        [MyCmpReq]
        private readonly HandSanitizer handSanitizer;

        public SinkCheckpoint()
        {
            buildingLayer = (int)PGameUtils.GetObjectLayer("Building", ObjectLayer.Building);
        }

        private bool CheckForOtherSink(bool dir)
        {
            GameObject gameObject = base.gameObject;
            bool flag = true;
            if (gameObject != null && Grid.IsValidCell(Grid.PosToCell(gameObject)))
            {
                int cell = Grid.PosToCell(gameObject);
                int num = 2;
                if (gameObject.TryGetComponent<Building>(out var building))
                {
                    num = building.Def.WidthInCells;
                }
                cell = Grid.OffsetCell(cell, new CellOffset(dir ? num : -num, 0));
                if (Grid.IsValidBuildingCell(cell) && (Grid.Objects[cell, buildingLayer] is GameObject gameObject2))
                {
                    flag = gameObject.PrefabID() != gameObject2.PrefabID() ||
                           !gameObject2.TryGetComponent<Operational>(out var operational) || !operational.IsOperational ||
                           !gameObject2.TryGetComponent<DirectionControl>(out var directionControl) ||
                           directionControl.allowedDirection != direction.allowedDirection;

                    if (!flag && gameObject2.TryGetComponent<SinkCheckpoint>(out var sinkCheckpoint) && sinkCheckpoint.inUse && gameObject2 != gameObject)
                    {
                        flag = sinkCheckpoint.CheckForOtherSink(dir);
                    }
                }
            }
            return flag;
        }

        private bool NeedsToUse(GameObject dupe)
        {
            return handSanitizer.alwaysUse || (dupe.TryGetComponent<PrimaryElement>(out var primaryElement) && primaryElement.DiseaseIdx != SimUtil.DiseaseInfo.Invalid.idx);
        }

        protected override bool MustStop(GameObject reactor, float dir)
        {
            return NeedsToUse(reactor) && CheckForOtherSink(dir > 0f);
        }
    }

    public abstract class WorkCheckpoint<T> : KMonoBehaviour where T : Workable
    {
        private static readonly IDetouredField<Workable, KMonoBehaviour> WORKER = PDetours.DetourField<Workable, KMonoBehaviour>("worker");

        [MyCmpReq]
        protected DirectionControl direction;

        protected bool inUse;
        private WorkCheckpointReactable reactable;
        private volatile int token;
        private T workable;

        protected WorkCheckpoint()
        {
            token = 0;
        }

        private void ClearReactable()
        {
            if (reactable != null)
            {
                reactable.Cleanup();
                reactable = null;
            }
        }

        private void CreateNewReactable()
        {
            reactable = new WorkCheckpointReactable(this);
        }

        private void HandleWorkableAction(Workable _, Workable.WorkableEvent evt)
        {
            if (evt == Workable.WorkableEvent.WorkStarted)
            {
                token = 1;
                inUse = true;
                return;
            }
            if (evt - Workable.WorkableEvent.WorkCompleted > 1)
            {
                return;
            }
            inUse = false;
        }

        protected abstract bool MustStop(GameObject reactor, float direction);

        protected override void OnCleanUp()
        {
            base.OnCleanUp();
            ClearReactable();
            if (workable != null)
            {
                workable.OnWorkableEventCB = (Action<Workable, Workable.WorkableEvent>)Delegate.Remove(workable.OnWorkableEventCB, new Action<Workable, Workable.WorkableEvent>(HandleWorkableAction));
            }
            token = 0;
        }

        protected override void OnSpawn()
        {
            base.OnSpawn();
            if (base.gameObject.TryGetComponent(out workable))
            {
                workable.OnWorkableEventCB = (Action<Workable, Workable.WorkableEvent>)Delegate.Combine(workable.OnWorkableEventCB, new Action<Workable, Workable.WorkableEvent>(HandleWorkableAction));
            }
            CreateNewReactable();
        }

        private IEnumerator ReleaseToken()
        {
            yield return null;
            yield return null;
            if (workable != null && WORKER.Get(workable) == null)
            {
                token = 1;
            }
        }

        internal bool TryTakeToken()
        {
            bool flag = Interlocked.CompareExchange(ref token, 0, 1) == 1;
            if (flag)
            {
                StartCoroutine(ReleaseToken());
            }
            return flag;
        }

        private sealed class WorkCheckpointReactable : Reactable
        {
            private static readonly ObjectLayer NUM_LAYERS = PGameUtils.GetObjectLayer("NumLayers", ObjectLayer.NumLayers);

            private bool begun;
            private readonly WorkCheckpoint<T> checkpoint;
            private readonly KAnimFile distractedAnim;
            private Navigator nav;

            internal WorkCheckpointReactable(WorkCheckpoint<T> checkpoint)
                : base(checkpoint.gameObject, "WorkCheckpointReactable", Db.Get().ChoreTypes.Checkpoint, 1, 1, false, 0f, 0f, float.PositiveInfinity, 0f, NUM_LAYERS)
            {
                begun = false;
                this.checkpoint = checkpoint;
                distractedAnim = Assets.GetAnim("anim_idle_distracted_kanim");
                preventChoreInterruption = false;
            }

            private bool InQueue()
            {
                return checkpoint.workable.GetWorker() != null || (begun && !checkpoint.TryTakeToken());
            }

            protected override void InternalBegin()
            {
                reactor.TryGetComponent(out nav);
                if (reactor.TryGetComponent<KBatchedAnimController>(out var kbatchedAnimController))
                {
                    kbatchedAnimController.AddAnimOverrides(distractedAnim, 1f);
                    kbatchedAnimController.Play("idle_pre", KAnim.PlayMode.Once, 1f, 0f);
                    kbatchedAnimController.Queue("idle_default", KAnim.PlayMode.Loop, 1f, 0f);
                }
                checkpoint.CreateNewReactable();
                begun = true;
            }

            public override bool InternalCanBegin(GameObject newReactor, Navigator.ActiveTransition transition)
            {
                bool flag = checkpoint == null || checkpoint.workable == null;
                if (flag)
                {
                    base.Cleanup();
                }
                bool flag2 = !flag && reactor == null;
                if (flag2)
                {
                    flag2 = MustStop(newReactor, (float)transition.x);
                }
                return flag2;
            }

            protected override void InternalCleanup()
            {
                if (reactor != null && reactor.TryGetComponent<StateMachineController>(out var stateMachineController))
                {
                    ReactionMonitor.Instance smi = stateMachineController.GetSMI<ReactionMonitor.Instance>();
                    smi?.ClearLastReaction();
                }
                nav = null;
                begun = false;
            }

            protected override void InternalEnd()
            {
                if (reactor != null && reactor.TryGetComponent<KBatchedAnimController>(out var kbatchedAnimController))
                {
                    kbatchedAnimController.RemoveAnimOverrides(distractedAnim);
                }
            }

            private bool MustStop(GameObject dupe, float x)
            {
                WorkableReactable.AllowedDirection allowedDirection = checkpoint.direction.allowedDirection;
                return (allowedDirection == WorkableReactable.AllowedDirection.Any || (allowedDirection == WorkableReactable.AllowedDirection.Left) == (x < 0f))
                       && dupe != null
                       && checkpoint.MustStop(dupe, x)
                       && !(dupe.GetSMI<SuffocationMonitor.Instance>() is SuffocationMonitor.Instance smi && smi.IsSuffocating())
                       && InQueue();
            }

            public override void Update(float dt)
            {
                if (checkpoint == null || checkpoint.workable == null || nav == null)
                {
                    base.Cleanup();
                    return;
                }
                nav.AdvancePath(false);
                if (!nav.path.IsValid() || !MustStop(reactor, (float)nav.GetNextTransition().x))
                {
                    base.Cleanup();
                }
            }
        }
    }
    #endregion

    #region Mod: Research Queue
    public static class ResearchQueueStrings
    {
        public static LocString QueueFormat = "{0} [{1}]";
        public static LocString QueueTooltip = string.Concat("Hold ", UI.PRE_KEYWORD, "{0}", UI.PST_KEYWORD, " and click to add to research queue");
    }

    [SerializationConfig(KSerialization.MemberSerialization.OptIn)]
    public sealed class SavedResearchQueue : KMonoBehaviour, ISaveLoadable
    {
        [Serialize]
        private List<string> techQueue = new List<string>(16);

        [OnSerializing]
        internal void OnSerializing()
        {
            Research instance = Research.Instance;
            techQueue.Clear();
            if (instance != null)
            {
                foreach (TechInstance techInstance in instance.GetResearchQueue())
                {
                    techQueue.Add(techInstance.tech.Id);
                }
            }
        }

        [OnDeserialized]
        internal void OnDeserialized()
        {
            Research instance = Research.Instance;
            Techs techs = Db.Get().Techs;
            if (instance != null && techQueue != null)
            {
                Tech tech = null;
                instance.SetActiveResearch(null, true);
                foreach (string id in techQueue)
                {
                    Tech tech2 = techs.Get(id);
                    if (tech2 != null)
                    {
                        ResearchQueuePatches.ADD_TECH(instance, tech2);
                        tech = tech2;
                    }
                }
                instance.SetActiveResearch(tech, false);
            }
        }
    }

    internal sealed class TechTierSorter : IComparer<TechInstance>
    {
        public static readonly TechTierSorter Instance = new TechTierSorter();

        private TechTierSorter() { }

        public int Compare(TechInstance x, TechInstance y)
        {
            Tech tech = x?.tech;
            Tech tech2 = y?.tech;
            int num = 0;
            if (tech != null && tech2 != null)
            {
                num = tech.tier.CompareTo(tech2.tier);
                if (num == 0)
                {
                    num = tech.Id.CompareTo(tech2.Id);
                }
            }
            return num;
        }
    }

    public static class ResearchQueuePatches
    {
        private static readonly IDetouredField<KInputController, Modifier> ACTIVE_MODIFIERS =
            PDetours.DetourField<KInputController, Modifier>("mActiveModifiers");

        internal static readonly Action<Research, Tech> ADD_TECH =
            typeof(Research).Detour<Action<Research, Tech>>("AddTechToQueue");

        private static readonly IDetouredField<ResearchEntry, LocText> RESEARCH_NAME =
            PDetours.DetourField<ResearchEntry, LocText>("researchName");

        private static readonly IDetouredField<ManagementMenu, ResearchScreen> RESEARCH_SCREEN =
            PDetours.DetourField<ManagementMenu, ResearchScreen>("researchScreen");

        private static void AddTechToQueue(IList<TechInstance> queuedTech, IDictionary<string, TechInstance> techsToAdd, Tech tech)
        {
            TechInstance orAdd = Research.Instance.GetOrAdd(tech);
            string id = tech.Id;
            if (!techsToAdd.ContainsKey(id) && !orAdd.IsComplete())
            {
                bool flag = false;
                foreach (TechInstance item in queuedTech)
                {
                    if (item.tech.Id == id)
                    {
                        flag = true;
                        break;
                    }
                }
                if (!flag)
                {
                    orAdd.tech.requiredTech.ForEach(delegate (Tech newTech)
                    {
                        AddTechToQueue(queuedTech, techsToAdd, newTech);
                    });
                    techsToAdd.Add(id, orAdd);
                }
            }
        }

        private static KInputController GetInputController()
        {
            return Global.GetInputManager().GetDefaultController();
        }

        private static bool OnResearchCanceled(Tech targetTech)
        {
            ManagementMenu instance = ManagementMenu.Instance;
            ResearchScreen researchScreen = (instance == null) ? null : RESEARCH_SCREEN.Get(instance);
            Research instance2 = Research.Instance;
            bool result = true;
            if (instance2 != null && researchScreen != null && !targetTech.IsComplete())
            {
                researchScreen.CancelResearch();
                instance2.CancelResearch(targetTech, true);
                List<TechInstance> researchQueue = instance2.GetResearchQueue();
                int count = researchQueue.Count;
                instance2.SetActiveResearch((count > 0) ? researchQueue[count - 1].tech : null, false);
                result = false;
            }
            return result;
        }

        private static bool OnResearchClicked(Tech targetTech)
        {
            KInputController inputController = GetInputController();
            ManagementMenu instance = ManagementMenu.Instance;
            ResearchScreen researchScreen = (instance == null) ? null : RESEARCH_SCREEN.Get(instance);
            Research instance2 = Research.Instance;
            string id = targetTech.Id;
            bool result = true;
            bool flag = inputController != null && (ACTIVE_MODIFIERS.Get(inputController) & Modifier.Shift) > Modifier.None;
            if (inputController != null && instance2 != null && !DebugHandler.InstantBuildMode && researchScreen != null)
            {
                List<TechInstance> researchQueue = instance2.GetResearchQueue();
                int num = -1;
                int count = researchQueue.Count;
                int num2 = 0;
                while (num2 < count && num < 0)
                {
                    if (researchQueue[num2].tech.Id == id)
                    {
                        num = num2;
                    }
                    num2++;
                }
                researchScreen.CancelResearch();
                if (num >= 0)
                {
                    instance2.CancelResearch(targetTech, true);
                    researchQueue = instance2.GetResearchQueue();
                    count = researchQueue.Count;
                    instance2.SetActiveResearch((count > 0) ? researchQueue[count - 1].tech : null, false);
                    result = false;
                }
                else
                {
                    if (flag)
                    {
                        ADD_TECH(instance2, targetTech);
                    }
                    instance2.SetActiveResearch(targetTech, !flag);
                    result = false;
                }
            }
            return result;
        }

        private static void UpdateResearchOrder(IList<TechInstance> queuedTech)
        {
            ManagementMenu instance = ManagementMenu.Instance;
            ResearchScreen researchScreen = (instance == null) ? null : RESEARCH_SCREEN.Get(instance);
            if (queuedTech == null)
            {
                throw new ArgumentNullException(nameof(queuedTech));
            }
            int count = queuedTech.Count;
            if (researchScreen != null && RESEARCH_NAME != null)
            {
                var pooledDictionary = DictionaryPool<string, int, ResearchScreen>.Allocate();
                for (int i = 0; i < count; i++)
                {
                    pooledDictionary.Add(queuedTech[i].tech.Id, i + 1);
                }
                foreach (Tech tech in Db.Get().Techs.resources)
                {
                    ResearchEntry entry = researchScreen.GetEntry(tech);
                    if (entry != null && RESEARCH_NAME.Get(entry) is LocText locText)
                    {
                        if (pooledDictionary.TryGetValue(tech.Id, out int num))
                        {
                            locText.SetText(string.Format(ResearchQueueStrings.QueueFormat, tech.Name, num));
                        }
                        else
                        {
                            locText.SetText(tech.Name);
                        }
                    }
                }
                pooledDictionary.Recycle();
            }
        }

        [HarmonyPatch(typeof(Research), "AddTechToQueue")]
        public static class Research_AddTechToQueue_Patch
        {
            internal static bool Prefix(List<TechInstance> ___queuedTech, Tech tech)
            {
                var pooledDictionary = DictionaryPool<string, TechInstance, Research>.Allocate();
                var pooledList = ListPool<TechInstance, Research>.Allocate();
                AddTechToQueue(___queuedTech, pooledDictionary, tech);
                pooledList.AddRange(pooledDictionary.Values);
                pooledList.Sort(TechTierSorter.Instance);
                ___queuedTech.AddRange(pooledList);
                UpdateResearchOrder(___queuedTech);
                pooledDictionary.Recycle();
                pooledList.Recycle();
                return false;
            }
        }

        [HarmonyPatch(typeof(Research), "CancelResearch")]
        public static class Research_CancelResearch_Patch
        {
            internal static void Postfix(List<TechInstance> ___queuedTech)
            {
                UpdateResearchOrder(___queuedTech);
            }
        }

        [HarmonyPatch(typeof(Research), "SetActiveResearch")]
        public static class Research_SetActiveResearch_Patch
        {
            internal static void Postfix(List<TechInstance> ___queuedTech)
            {
                UpdateResearchOrder(___queuedTech);
            }

            internal static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> method)
            {
                return PPatchTools.RemoveMethodCall(method, typeof(List<TechInstance>).GetMethodSafe("Sort", false, new Type[]
                {
                    typeof(Comparison<TechInstance>)
                }));
            }
        }

        [HarmonyPatch(typeof(ResearchEntry), "OnResearchCanceled")]
        public static class ResearchEntry_OnResearchCanceled_Patch
        {
            internal static bool Prefix(Tech ___targetTech)
            {
                return OnResearchCanceled(___targetTech);
            }
        }

        [HarmonyPatch(typeof(ResearchEntry), "OnResearchClicked")]
        public static class ResearchEntry_OnResearchClicked_Patch
        {
            internal static bool Prefix(Tech ___targetTech)
            {
                return OnResearchClicked(___targetTech);
            }
        }

        [HarmonyPatch(typeof(ResearchEntry), "SetTech")]
        public static class ResearchEntry_SetTech_Patch
        {
            internal static void Postfix(LocText ___researchName, Tech newTech)
            {
                if (newTech != null && ___researchName != null && ___researchName.TryGetComponent<ToolTip>(out var toolTip))
                {
                    string multiString = toolTip.GetMultiString(0);
                    string text = GameUtil.GetKeycodeLocalized(KKeyCode.LeftShift) ?? "SHIFT";
                    toolTip.SetSimpleTooltip(multiString + "\r\n\r\n" + string.Format(ResearchQueueStrings.QueueTooltip.text, text));
                }
            }
        }

        [HarmonyPatch(typeof(ResearchScreen), "OnSpawn")]
        public static class ResearchScreen_OnSpawn_Patch
        {
            internal static void Postfix()
            {
                Research instance = Research.Instance;
                if (instance != null)
                {
                    List<TechInstance> researchQueue = instance.GetResearchQueue();
                    int count = researchQueue.Count;
                    if (count > 0)
                    {
                        TechInstance techInstance = researchQueue[count - 1];
                        instance.SetActiveResearch((techInstance != null) ? techInstance.tech : null, false);
                    }
                }
            }
        }

        [HarmonyPatch(typeof(SaveGame), "OnPrefabInit")]
        public static class SaveGame_OnPrefabInit_Patch
        {
            internal static void Postfix(SaveGame __instance)
            {
                __instance.gameObject.AddOrGet<SavedResearchQueue>();
            }
        }
    }
    #endregion

    #region Mod: Pip Plant Overlay
    public static class PipPlantOverlayStrings
    {
        public const string OVERLAY_ACTION = "action_overlay_pip";
        public const string OVERLAY_ICON = "overlay_pip";

        public static class INPUT_BINDINGS
        {
            public static class ROOT
            {
                public static LocString PIPPLANT = "Open Pip Planting Overlay";
            }
        }

        public static class UI
        {
            public static class OVERLAYS
            {
                public static class PIPPLANTING
                {
                    public static LocString NAME = "PIP PLANTING OVERLAY";
                    public static LocString DESCRIPTION = "Displays the locations where Pips can plant seeds. The conditions which must be met include:\n1. No more than 2 other plants within 6 left, 6 down, 5 right, 5 up from the tile.\n2. It is a \"natural tile\" of less than 150 hardness, a farm tile or a hydroponic farm.\n3. No buildings obstructing the plant's location.\n4. The atmospheric pressure is greater than 100g.\n5. The atmospheric temperature is within 50 to 100 C of the plant's requirements.\n";
                    public static LocString BUTTON = "Pip Planting Overlay";
                    public static LocString TOOLTIP = "Displays locations where Pips can plant seeds";
                    public static LocString CANPLANT = "Can plant here";
                    public static LocString HARDNESS = "Tile is too hard";
                    public static LocString PLANTCOUNT = "Too many plants";
                    public static LocString PRESSURE = "Pressure too low";
                    public static LocString TEMPERATURE = "Temperature too extreme";

                    public static class TOOLTIPS
                    {
                        public static LocString CANPLANT = "Seeds can be planted here, if there is room and temperature is valid";
                        public static LocString HARDNESS = "Natural tile hardness is above {0:D}!";
                        public static LocString PLANTCOUNT_1 = "More than one other plant is within the range of {1:D}!";
                        public static LocString PLANTCOUNT = "More than {0:D} other plants are within the range of {1:D}!";
                        public static LocString PRESSURE = "Atmospheric pressure is below {0} or tile is flooded!";
                        public static LocString TEMPERATURE = "Temperature is below {0} or above {1}!";
                    }
                }
            }
        }
    }

    public enum PipPlantFailedReasons
    {
        CanPlant,
        NoPlantablePlot,
        Hardness,
        PlantCount,
        Pressure,
        Temperature
    }

    internal static class PipPlantOverlayTests
    {
        private delegate int CountNearbyPlants(int cell, int radius);

        internal const float PRESSURE_THRESHOLD = 0.1f;
        internal const float TEMP_MAX = 563.15f;
        internal const float TEMP_MIN = 118.15f;

        private static readonly int BUILDINGS_LAYER = (int)PGameUtils.GetObjectLayer("Building", ObjectLayer.Building);
        private static readonly CountNearbyPlants COUNT_PLANTS = typeof(PlantableCellQuery).Detour<CountNearbyPlants>();
        private static readonly IDetouredField<PlantableCellQuery, int> GET_PLANT_RADIUS = PDetours.DetourField<PlantableCellQuery, int>("plantDetectionRadius");
        private static readonly IDetouredField<PlantableCellQuery, int> GET_PLANT_COUNT = PDetours.DetourField<PlantableCellQuery, int>("maxPlantsInRadius");

        internal static int PlantCount { get; private set; }
        internal static int PlantRadius { get; private set; }
        internal static bool SymmetricalRadius { get; set; }

        internal static PipPlantFailedReasons CheckCell(int cell)
        {
            PipPlantFailedReasons pipPlantFailedReasons = PipPlantFailedReasons.NoPlantablePlot;
            if (Grid.IsValidCell(cell) && !Grid.Solid[cell] && Grid.Objects[cell, BUILDINGS_LAYER] == null)
            {
                int num = Grid.CellAbove(cell);
                int num2 = Grid.CellBelow(cell);
                if (IsPlantable(num2, SingleEntityReceptacle.ReceptacleDirection.Top))
                {
                    pipPlantFailedReasons = CheckCellInternal(cell, num2);
                }
                if (pipPlantFailedReasons == PipPlantFailedReasons.NoPlantablePlot && IsPlantable(num, SingleEntityReceptacle.ReceptacleDirection.Bottom))
                {
                    pipPlantFailedReasons = CheckCellInternal(cell, num);
                }
            }
            return pipPlantFailedReasons;
        }

        private static PipPlantFailedReasons CheckCellInternal(int cell, int plant)
        {
            float num = Grid.Temperature[cell];
            PipPlantFailedReasons result;
            if (IsTooHard(plant))
            {
                result = PipPlantFailedReasons.Hardness;
            }
            else if (COUNT_PLANTS(plant, PlantRadius) > PlantCount)
            {
                result = PipPlantFailedReasons.PlantCount;
            }
            else if (IsUnderPressure(cell))
            {
                result = PipPlantFailedReasons.Pressure;
            }
            else if ((num < 118.15f || num > 563.15f) && num > 0f)
            {
                result = PipPlantFailedReasons.Temperature;
            }
            else
            {
                result = PipPlantFailedReasons.CanPlant;
            }
            return result;
        }

        internal static bool IsAcceptableCell(int cell, SingleEntityReceptacle.ReceptacleDirection direction)
        {
            return IsPlantable(cell, direction) && !IsTooHard(cell);
        }

        private static bool IsPlantable(int cell, SingleEntityReceptacle.ReceptacleDirection direction)
        {
            bool result = false;
            if (Grid.IsSolidCell(cell))
            {
                GameObject gameObject = Grid.Objects[cell, BUILDINGS_LAYER];
                if (gameObject == null)
                {
                    result = true;
                }
                else if (gameObject.TryGetComponent<PlantablePlot>(out var plantablePlot))
                {
                    result = (plantablePlot.Direction == direction);
                }
            }
            return result;
        }

        private static bool IsTooHard(int cell)
        {
            Element element = Grid.Element[cell];
            return element == null || element.hardness >= 150;
        }

        private static bool IsUnderPressure(int cell)
        {
            Element element = Grid.Element[cell];
            return element == null || (element.id != SimHashes.Vacuum && (Grid.Mass[cell] < 0.1f || Grid.IsNavigatableLiquid(cell)));
        }

        internal static void UpdatePlantCriteria()
        {
            PlantableCellQuery plantableCellQuery = PathFinderQueries.plantableCellQuery;
            PlantRadius = GET_PLANT_RADIUS.Get(plantableCellQuery);
            PlantCount = GET_PLANT_COUNT.Get(plantableCellQuery);
        }
    }

    public class PipPlantOverlay : OverlayModes.Mode
    {
        public static readonly HashedString ID = new HashedString("PIPPLANT");

        internal static PipPlantOverlay Instance { get; private set; }

        private readonly int cameraLayerMask;
        private readonly PipPlantFailedReasons[] cells;
        private readonly OverlayModes.ColorHighlightCondition[] conditions;
        private readonly ICollection<Uprootable> layerTargets;
        private UniformGrid<Uprootable> partition;
        private readonly List<LegendEntry> pipPlantLegend;
        private readonly ICollection<Tag> plants;
        private readonly int selectionMask;
        private readonly int targetLayer;

        public PipPlantOverlay()
        {
            ColorSet colorSet = GlobalAssets.Instance.colorSet;
            int plantCount = PipPlantOverlayTests.PlantCount;
            PipPlantOverlayTests.UpdatePlantCriteria();
            string desc = string.Format((plantCount == 1) ? PipPlantOverlayStrings.UI.OVERLAYS.PIPPLANTING.TOOLTIPS.PLANTCOUNT_1 : PipPlantOverlayStrings.UI.OVERLAYS.PIPPLANTING.TOOLTIPS.PLANTCOUNT, plantCount, PipPlantOverlayTests.PlantRadius);
            cameraLayerMask = LayerMask.GetMask("MaskedOverlay", "MaskedOverlayBG");
            cells = new PipPlantFailedReasons[Grid.CellCount];
            for (int i = 0; i < Grid.CellCount; i++)
            {
                cells[i] = PipPlantFailedReasons.NoPlantablePlot;
            }
            conditions = new OverlayModes.ColorHighlightCondition[]
            {
                new OverlayModes.ColorHighlightCondition(GetHighlightColor, ShouldHighlight)
            };
            layerTargets = new HashSet<Uprootable>();
            InitDefaultFilters();
            Instance = this;
            pipPlantLegend = new List<LegendEntry>
            {
                new LegendEntry(PipPlantOverlayStrings.UI.OVERLAYS.PIPPLANTING.CANPLANT, PipPlantOverlayStrings.UI.OVERLAYS.PIPPLANTING.TOOLTIPS.CANPLANT, colorSet.cropGrown, null, null, true),
                new LegendEntry(PipPlantOverlayStrings.UI.OVERLAYS.PIPPLANTING.HARDNESS, string.Format(PipPlantOverlayStrings.UI.OVERLAYS.PIPPLANTING.TOOLTIPS.HARDNESS, 150), colorSet.cropHalted, null, null, true),
                new LegendEntry(PipPlantOverlayStrings.UI.OVERLAYS.PIPPLANTING.PLANTCOUNT, desc, colorSet.cropGrowing, null, null, true),
                new LegendEntry(PipPlantOverlayStrings.UI.OVERLAYS.PIPPLANTING.PRESSURE, string.Format(PipPlantOverlayStrings.UI.OVERLAYS.PIPPLANTING.TOOLTIPS.PRESSURE, GameUtil.GetFormattedMass(0.1f, GameUtil.TimeSlice.None, GameUtil.MetricMassFormat.UseThreshold, true, "{0:0.#}")), colorSet.heatflowThreshold0, null, null, true),
                new LegendEntry(PipPlantOverlayStrings.UI.OVERLAYS.PIPPLANTING.TEMPERATURE, string.Format(PipPlantOverlayStrings.UI.OVERLAYS.PIPPLANTING.TOOLTIPS.TEMPERATURE, GameUtil.GetFormattedTemperature(118.15f, GameUtil.TimeSlice.None, GameUtil.TemperatureInterpretation.Absolute, true, false), GameUtil.GetFormattedTemperature(563.15f, GameUtil.TimeSlice.None, GameUtil.TemperatureInterpretation.Absolute, true, false)), colorSet.cropHalted, null, null, true)
            };
            partition = null;
            plants = new HashSet<Tag>(Assets.GetPrefabTagsWithComponent<Uprootable>());
            selectionMask = LayerMask.GetMask("MaskedOverlay");
            targetLayer = LayerMask.NameToLayer("MaskedOverlay");
        }

        internal static Color GetColor(SimDebugView _, int cell)
        {
            Color result = Color.black;
            ColorSet colorSet = GlobalAssets.Instance.colorSet;
            switch (Instance.cells[cell])
            {
                case PipPlantFailedReasons.CanPlant:
                    return colorSet.cropGrown;
                case PipPlantFailedReasons.NoPlantablePlot:
                    return result;
                case PipPlantFailedReasons.PlantCount:
                    return colorSet.cropGrowing;
                case PipPlantFailedReasons.Pressure:
                    return colorSet.heatflowThreshold0;
            }
            return colorSet.cropHalted;
        }

        public override void Disable()
        {
            Camera main = Camera.main;
            UnregisterSaveLoadListeners();
            DisableHighlightTypeOverlay(layerTargets);
            CameraController.Instance.ToggleColouredOverlayView(false);
            if (main != null)
            {
                main.cullingMask &= ~cameraLayerMask;
            }
            partition?.Clear();
            layerTargets.Clear();
            SelectTool.Instance.ClearLayerMask();
            base.Disable();
        }

        public override void Enable()
        {
            Camera main = Camera.main;
            base.Enable();
            RegisterSaveLoadListeners();
            partition = PopulatePartition<Uprootable>(plants);
            CameraController.Instance.ToggleColouredOverlayView(true);
            if (main != null)
            {
                main.cullingMask |= cameraLayerMask;
            }
            SelectTool.Instance.SetLayerMask(selectionMask);
        }

        public override List<LegendEntry> GetCustomLegendData()
        {
            return pipPlantLegend;
        }

        private Color GetHighlightColor(KMonoBehaviour _)
        {
            Color result = Color.black;
            int num = Grid.PosToCell(CameraController.Instance.baseCamera.ScreenToWorldPoint(KInputManager.GetMousePos()));
            ColorSet colorSet = GlobalAssets.Instance.colorSet;
            if (Grid.IsValidCell(num))
            {
                result = (cells[num] == PipPlantFailedReasons.PlantCount) ? colorSet.cropHalted : colorSet.cropGrown;
            }
            return result;
        }

        public override string GetSoundName()
        {
            return "Harvest";
        }

        private void InitDefaultFilters()
        {
            FieldInfo fieldSafe = typeof(OverlayModes.Mode).GetFieldSafe("legendFilters", false);
            MethodInfo methodSafe = typeof(OverlayModes.Mode).GetMethodSafe("CreateDefaultFilters", false, Array.Empty<Type>());
            if (fieldSafe != null && methodSafe != null)
            {
                fieldSafe.SetValue(this, methodSafe.Invoke(this, null));
            }
        }

        protected override void OnSaveLoadRootRegistered(SaveLoadRoot root)
        {
            Tag saveLoadTag = root.GetComponent<KPrefabID>().GetSaveLoadTag();
            if (plants.Contains(saveLoadTag))
            {
                Uprootable component = root.GetComponent<Uprootable>();
                if (component != null)
                {
                    partition.Add(component);
                }
            }
        }

        protected override void OnSaveLoadRootUnregistered(SaveLoadRoot root)
        {
            if (root != null && root.gameObject != null)
            {
                Uprootable component = root.GetComponent<Uprootable>();
                if (component != null)
                {
                    layerTargets.Remove(component);
                    partition.Remove(component);
                }
            }
        }

        private bool ShouldHighlight(KMonoBehaviour plant)
        {
            bool result = false;
            int cell;
            if (plant != null && Grid.IsValidCell(cell = Grid.PosToCell(plant)))
            {
                int num = Grid.PosToCell(CameraController.Instance.baseCamera.ScreenToWorldPoint(KInputManager.GetMousePos()));
                if (Grid.IsValidCell(num) && cells[num] != PipPlantFailedReasons.NoPlantablePlot)
                {
                    int plantRadius = PipPlantOverlayTests.PlantRadius;
                    int num2 = plantRadius;
                    int num3 = plantRadius;
                    OccupyArea component = plant.GetComponent<OccupyArea>();
                    Grid.CellToXY(num, out int num4, out int num5);
                    Grid.CellToXY(cell, out int num6, out int num7);
                    int num8 = num6;
                    int num9 = num7;
                    if (component != null)
                    {
                        Extents extents = component.GetExtents();
                        num8 = extents.x;
                        num9 = extents.y;
                        num6 = extents.x + extents.width - 1;
                        num7 = extents.y + extents.height - 1;
                    }
                    if (PipPlantOverlayTests.SymmetricalRadius)
                    {
                        num8--;
                        num9--;
                    }
                    bool flag = PipPlantOverlayTests.IsAcceptableCell(Grid.CellBelow(num), SingleEntityReceptacle.ReceptacleDirection.Top);
                    if (PipPlantOverlayTests.IsAcceptableCell(Grid.CellAbove(num), SingleEntityReceptacle.ReceptacleDirection.Bottom))
                    {
                        num3++;
                        if (flag)
                        {
                            num2++;
                        }
                        else
                        {
                            num2--;
                        }
                    }
                    else if (flag)
                    {
                        num3--;
                        num2++;
                    }
                    result = (num4 > num8 - plantRadius && num4 <= num6 + plantRadius && num5 > num9 - num3 && num5 <= num7 + num2);
                }
            }
            return result;
        }

        public override void Update()
        {
            var pooledHashSet = HashSetPool<Uprootable, PipPlantOverlay>.Allocate();
            base.Update();
            Grid.GetVisibleExtents(out Vector2I vector2I, out Vector2I vector2I2);
            int x = vector2I.x;
            int x2 = vector2I2.x;
            int y = vector2I.y;
            int y2 = vector2I2.y;
            RemoveOffscreenTargets(layerTargets, vector2I, vector2I2, null);
            partition.GetAllIntersecting(new Vector2(x, y), new Vector2(x2, y2), pooledHashSet);
            foreach (Uprootable instance in pooledHashSet)
            {
                AddTargetIfVisible(instance, vector2I, vector2I2, layerTargets, targetLayer, null, null);
            }
            for (int i = y; i <= y2; i++)
            {
                for (int j = x; j <= x2; j++)
                {
                    int num = Grid.XYToCell(j, i);
                    if (Grid.IsValidCell(num))
                    {
                        cells[num] = PipPlantOverlayTests.CheckCell(num);
                    }
                }
            }
            UpdateHighlightTypeOverlay(vector2I, vector2I2, layerTargets, plants, conditions, BringToFrontLayerSetting.Constant, targetLayer);
            pooledHashSet.Recycle();
        }

        public override HashedString ViewMode()
        {
            return ID;
        }
    }

    public sealed class PipPlantOverlayPatches
    {
        private delegate void RegisterMode(OverlayScreen screen, OverlayModes.Mode mode);

        private const BindingFlags INSTANCE_ALL = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private static PAction OpenOverlay;
        private static readonly Type OVERLAY_TYPE = typeof(OverlayMenu).GetNestedType("OverlayToggleInfo", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        private static readonly RegisterMode REGISTER_MODE = typeof(OverlayScreen).Detour<RegisterMode>();

        [PLibMethod(3U)]
        internal static void AfterDbInit()
        {
            try
            {
                var assembly = System.Reflection.Assembly.GetExecutingAssembly();

                // 1. Search the compiled DLL manifest for any embedded resource ending in "pip.png"
                string resourceName = assembly.GetManifestResourceNames()
                    .FirstOrDefault(n => n.EndsWith("pip.png", StringComparison.OrdinalIgnoreCase));

                Sprite pipSprite = null;

                if (!string.IsNullOrEmpty(resourceName))
                {
                    // Pass the path string as the first argument
                    pipSprite = PUIUtils.LoadSprite(resourceName, default, false);
                }

                // 2. Fallback to vanilla overlay_farming if the sprite isn't found
                pipSprite = pipSprite ?? Assets.GetSprite("overlay_farming");

                if (pipSprite != null)
                {
                    Assets.Sprites["overlay_pip"] = pipSprite;
                }

                // 3. Mod compatibility check
                Type type = PPatchTools.GetTypeSafe("MightyVincent.Patches.SimplerPipPlantRule", "SimplerPipPlantRule");
                PipPlantOverlayTests.SymmetricalRadius = (type != null);
                if (PipPlantOverlayTests.SymmetricalRadius)
                {
                    PUtil.LogDebug("Detected Simpler Pip Plant Overlay, adjusting radius");
                }
            }
            catch (Exception ex)
            {
                PUtil.LogError($"Exception in AfterDbInit: {ex}");
            }
        }

        private static KIconToggleMenu.ToggleInfo CreateOverlayInfo(string text, string iconName, HashedString simView, Action openKey, string tooltip)
        {
            KIconToggleMenu.ToggleInfo result = null;
            ConstructorInfo[] constructors;
            if (OVERLAY_TYPE == null || (constructors = OVERLAY_TYPE.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)).Length != 1)
            {
                PUtil.LogWarning("Unable to add PipPlantOverlay - missing constructor");
            }
            else
            {
                ConstructorInfo constructorInfo = constructors[0];
                ParameterInfo[] parameters = constructorInfo.GetParameters();
                int num = parameters.Length;
                if (num < 7)
                {
                    PUtil.LogWarning("Unable to add PipPlantOverlay - parameters missing");
                }
                else
                {
                    object[] array = new object[num];
                    array[0] = text;
                    array[1] = iconName;
                    array[2] = simView;
                    array[3] = "";
                    array[4] = openKey;
                    array[5] = tooltip;
                    array[6] = text;
                    for (int i = 7; i < num; i++)
                    {
                        ParameterInfo parameterInfo = parameters[i];
                        if (parameterInfo.IsOptional)
                        {
                            array[i] = parameterInfo.DefaultValue;
                        }
                        else
                        {
                            PUtil.LogWarning("Unable to add PipPlantOverlay - new parameters");
                            array[i] = null;
                        }
                    }
                    result = constructorInfo.Invoke(array) as KIconToggleMenu.ToggleInfo;
                }
            }
            return result;
        }

        public static void Init(Harmony harmony)
        {
            new PPatchManager(harmony).RegisterPatchClass(typeof(PipPlantOverlayPatches));
            LocString.CreateLocStringKeys(typeof(PipPlantOverlayStrings.INPUT_BINDINGS), "STRINGS.");
            PipPlantOverlayTests.SymmetricalRadius = false;
            OpenOverlay = new PActionManager().CreateAction("action_overlay_pip", PipPlantOverlayStrings.INPUT_BINDINGS.ROOT.PIPPLANT, null);
            new PLocalization().Register(null);
            if (PPatchTools.TryGetFieldValue<IDictionary<HashedString, StatusItem.StatusItemOverlays>>(typeof(StatusItem), "overlayBitfieldMap", out var dictionary))
            {
                dictionary.Add(PipPlantOverlay.ID, StatusItem.StatusItemOverlays.Farming);
            }
        }

        [HarmonyPatch(typeof(OverlayLegend), "OnSpawn")]
        public static class OverlayLegend_OnSpawn_Patch
        {
            internal static void Prefix(ICollection<OverlayLegend.OverlayInfo> ___overlayInfoList)
            {
                ___overlayInfoList.Add(new OverlayLegend.OverlayInfo
                {
                    infoUnits = new List<OverlayLegend.OverlayInfoUnit>(1)
                    {
                        new OverlayLegend.OverlayInfoUnit(Assets.GetSprite("overlay_pip"), "STRINGS.UI.OVERLAYS.PIPPLANTING.DESCRIPTION", Color.white, Color.white, null, false)
                    },
                    isProgrammaticallyPopulated = true,
                    mode = PipPlantOverlay.ID,
                    name = "STRINGS.UI.OVERLAYS.PIPPLANTING.NAME"
                });
            }
        }

        [HarmonyPatch(typeof(OverlayMenu), "InitializeToggles")]
        public static class OverlayMenu_InitializeToggles_Patch
        {
            internal static void Postfix(ICollection<KIconToggleMenu.ToggleInfo> ___overlayToggleInfos)
            {
                LocString.CreateLocStringKeys(typeof(PipPlantOverlayStrings.UI), "STRINGS.");
                PAction openOverlay = OpenOverlay;
                Action openKey = (openOverlay != null) ? openOverlay.GetKAction() : PAction.MaxAction;
                KIconToggleMenu.ToggleInfo toggleInfo = CreateOverlayInfo(PipPlantOverlayStrings.UI.OVERLAYS.PIPPLANTING.BUTTON, "overlay_pip", PipPlantOverlay.ID, openKey, PipPlantOverlayStrings.UI.OVERLAYS.PIPPLANTING.TOOLTIP);
                if (toggleInfo != null && ___overlayToggleInfos != null)
                {
                    ___overlayToggleInfos.Add(toggleInfo);
                }
            }
        }

        [HarmonyPatch(typeof(OverlayScreen), "RegisterModes")]
        public static class OverlayScreen_RegisterModes_Patch
        {
            internal static void Postfix(OverlayScreen __instance)
            {
                PUtil.LogDebug("Creating PipPlantOverlay");
                REGISTER_MODE(__instance, new PipPlantOverlay());
            }
        }

        [HarmonyPatch(typeof(SimDebugView), "OnPrefabInit")]
        public static class SimDebugView_OnPrefabInit_Patch
        {
            internal static void Postfix(IDictionary<HashedString, Func<SimDebugView, int, Color>> ___getColourFuncs)
            {
                ___getColourFuncs[PipPlantOverlay.ID] = PipPlantOverlay.GetColor;
            }
        }
    }
    #endregion

    #region Mod: Waste Not Want Not
    public static class NoWasteWantStrings
    {
        public static class UI
        {
            public static class UISIDESCREENS
            {
                public static class FRESHNESS_CONTROL_SIDE_SCREEN
                {
                    public static LocString TITLE = "Freshness Control";
                    public static LocString TOOLTIP = "Will accept <b>Food</b> with a <b>Freshness</b> of at least <b>{0:F0}</b> %";
                }
            }
        }
    }

    [SerializationConfig(KSerialization.MemberSerialization.OptIn)]
    public class FreshnessControl : KMonoBehaviour, ISim4000ms, ISingleSliderControl, ISliderControl
    {
        private static readonly EventSystem.IntraObjectHandler<FreshnessControl> OnCopySettingsDelegate = new EventSystem.IntraObjectHandler<FreshnessControl>(delegate (FreshnessControl component, object data)
        {
            component.OnCopySettings(data);
        });

        [Serialize]
        private float minFreshness;

        [MyCmpGet]
        private readonly Storage storage;

        public float MinFreshness
        {
            get => minFreshness;
            set
            {
                minFreshness = value;
                DropStaleItems();
            }
        }

        public string SliderTitleKey => "STRINGS.UI.UISIDESCREENS.FRESHNESS_CONTROL_SIDE_SCREEN.TITLE";
        public string SliderUnits => UI.UNITSUFFIXES.PERCENT;

        public FreshnessControl()
        {
            minFreshness = 0f;
        }

        protected override void OnPrefabInit()
        {
            base.OnPrefabInit();
            Subscribe(-905833192, OnCopySettingsDelegate);
        }

        private void OnCopySettings(object data)
        {
            FreshnessControl component = ((GameObject)data).GetComponent<FreshnessControl>();
            if (component != null)
            {
                minFreshness = component.minFreshness;
                DropStaleItems();
            }
        }

        public void DropStaleItems()
        {
            if (storage != null && minFreshness > 0f)
            {
                var pooledList = ListPool<GameObject, FreshnessControl>.Allocate();
                foreach (GameObject gameObject in storage.items)
                {
                    if (gameObject != null && !IsAcceptable(gameObject))
                    {
                        pooledList.Add(gameObject);
                    }
                }
                foreach (GameObject go in pooledList)
                {
                    storage.Drop(go, false);
                }
                pooledList.Recycle();
            }
        }

        public float GetSliderMax(int index) => 100f;
        public float GetSliderMin(int index) => 0f;
        public float GetSliderValue(int index) => MinFreshness * 100f;

        public string GetSliderTooltip()
        {
            return string.Format(Strings.Get(GetSliderTooltipKey(0)), MinFreshness * 100f);
        }

        public string GetSliderTooltip(int index)
        {
            return string.Format(Strings.Get(GetSliderTooltipKey(index)), MinFreshness * 100f);
        }

        public string GetSliderTooltipKey(int index)
        {
            return "STRINGS.UI.UISIDESCREENS.FRESHNESS_CONTROL_SIDE_SCREEN.TOOLTIP";
        }

        public bool IsAcceptable(GameObject item)
        {
            Rottable.Instance smi;
            return item != null && ((smi = item.GetSMI<Rottable.Instance>()) == null || smi.RotConstitutionPercentage >= minFreshness);
        }

        public void SetSliderValue(float percent, int index)
        {
            MinFreshness = percent * 0.01f;
        }

        public void Sim4000ms(float dt)
        {
            DropStaleItems();
        }

        public int SliderDecimalPlaces(int index) => 0;
    }

    public sealed class NoWasteWantPatches
    {
        private static readonly Tag[] EDIBLE_TAGS = new Tag[]
        {
            GameTags.CookingIngredient,
            GameTags.Edible
        };

        private const float MASS_TO_ROT = 0.01f;

        [PLibPatch(1U, "Compare", PatchType = HarmonyPatchType.Transpiler, RequireType = "PeterHan.EfficientFetch.EfficientFetchManager+FetchData", RequireAssembly = "EfficientFetch")]
        internal static IEnumerable<CodeInstruction> FixEfficientSupply(IEnumerable<CodeInstruction> method)
        {
            PUtil.LogDebug("Applying patch for Efficient Supply");
            return TranspileNegateLast(method);
        }

        public static void AddFreshnessControl(GameObject go)
        {
            go.AddOrGet<FreshnessControl>();
        }

        private static int AlignFreshness(int oldFreshness, Edible target)
        {
            if (target != null && !target.FoodInfo.CanRot)
            {
                oldFreshness = int.MaxValue;
            }
            return oldFreshness;
        }

        public static void Init(Harmony harmony)
        {
            new PPatchManager(harmony).RegisterPatchClass(typeof(NoWasteWantPatches));
            LocString.CreateLocStringKeys(typeof(NoWasteWantStrings.UI), "STRINGS.");
            new PLocalization().Register(null);
        }

        private static void ReplaceRotHandler(Rottable sm)
        {
            List<StateMachine.Action> enterActions = sm.Spoiled.enterActions;
            if (enterActions != null)
            {
                List<StateMachine<Rottable, Rottable.Instance, IStateMachineTarget, Rottable.Def>.State.Callback> targets = new List<StateMachine<Rottable, Rottable.Instance, IStateMachineTarget, Rottable.Def>.State.Callback>(enterActions.Count);
                foreach (StateMachine.Action action in enterActions)
                {
                    if (action.callback is StateMachine<Rottable, Rottable.Instance, IStateMachineTarget, Rottable.Def>.State.Callback callback)
                    {
                        targets.Add(callback);
                    }
                }
                enterActions.Clear();
                sm.Spoiled.Enter(delegate (Rottable.Instance smi)
                {
                    GameObject gameObject = smi.master.gameObject;
                    if (gameObject != null)
                    {
                        if (!gameObject.TryGetComponent<PrimaryElement>(out var primaryElement) || primaryElement.Mass > 0.01f)
                        {
                            using (var enumerator2 = targets.GetEnumerator())
                            {
                                while (enumerator2.MoveNext())
                                {
                                    var callback2 = enumerator2.Current;
                                    callback2(smi);
                                }
                                return;
                            }
                        }
                        Util.KDestroyGameObject(gameObject);
                    }
                });
            }
        }

        private static IEnumerable<CodeInstruction> TranspileNegateLast(IEnumerable<CodeInstruction> method)
        {
            List<CodeInstruction> list = new List<CodeInstruction>(method);
            int count = list.Count;
            MethodInfo methodSafe = typeof(int).GetMethodSafe("CompareTo", false, new Type[] { typeof(int) });
            for (int i = count - 1; i > 0; i--)
            {
                CodeInstruction codeInstruction = list[i];
                if (codeInstruction.opcode == OpCodes.Call && codeInstruction.operand as MethodBase == methodSafe)
                {
                    list.Insert(i + 1, new CodeInstruction(OpCodes.Neg, null));
                    break;
                }
            }
            return list;
        }

        [HarmonyPatch(typeof(FetchManager), "IsFetchablePickup_Exclude", new Type[]
        {
            typeof(KPrefabID),
            typeof(Storage),
            typeof(float),
            typeof(HashSet<Tag>),
            typeof(Tag),
            typeof(Storage)
        })]
        public static class FetchManager_IsFetchablePickupExclude_Patch
        {
            internal static void Postfix(KPrefabID pickup_id, Storage destination, ref bool __result)
            {
                if (__result && pickup_id != null && destination != null && pickup_id.HasAnyTags(EDIBLE_TAGS) && destination.TryGetComponent<FreshnessControl>(out var freshnessControl))
                {
                    __result = freshnessControl.IsAcceptable(pickup_id.gameObject);
                }
            }
        }

        [HarmonyPatch(typeof(FetchManager.FetchablesByPrefabId), "AddPickupable")]
        public static class FetchManager_FetchablesByPrefabId_AddPickupable_Patch
        {
            internal static IEnumerable<CodeInstruction> Transpiler(ILGenerator generator, IEnumerable<CodeInstruction> method)
            {
                FieldInfo targetField = typeof(FetchManager.Fetchable).GetFieldSafe("freshness", false);
                MethodInfo insertion = typeof(NoWasteWantPatches).GetMethodSafe("AlignFreshness", true, new Type[]
                {
                    typeof(int),
                    typeof(Edible)
                });
                LocalBuilder local = generator.DeclareLocal(typeof(Edible));
                yield return new CodeInstruction(OpCodes.Ldnull, null);
                yield return new CodeInstruction(OpCodes.Stloc, local.LocalIndex);
                foreach (CodeInstruction instruction in method)
                {
                    OpCode opcode = instruction.opcode;
                    if (opcode == OpCodes.Stfld)
                    {
                        FieldInfo fieldInfo = instruction.operand as FieldInfo;
                        if (fieldInfo != null && fieldInfo == targetField)
                        {
                            yield return new CodeInstruction(OpCodes.Ldloc, local.LocalIndex);
                            yield return new CodeInstruction(OpCodes.Call, insertion);
                        }
                    }
                    yield return instruction;
                    if (opcode == OpCodes.Callvirt)
                    {
                        MethodInfo methodInfo = instruction.operand as MethodInfo;
                        if (methodInfo != null && methodInfo.ReturnType == typeof(Edible) && methodInfo.Name == "GetComponent")
                        {
                            yield return new CodeInstruction(OpCodes.Dup, null);
                            yield return new CodeInstruction(OpCodes.Stloc, local.LocalIndex);
                        }
                    }
                }
            }
        }

        [HarmonyPatch(typeof(FetchManager), "IsFetchablePickup", new Type[]
        {
            typeof(Pickupable),
            typeof(FetchChore),
            typeof(Storage)
        })]
        public static class FetchManager_IsFetchablePickup_Patch
        {
            internal static void Postfix(Pickupable pickup, Storage destination, ref bool __result)
            {
                if (__result && pickup != null && destination != null && pickup.KPrefabID.HasAnyTags(EDIBLE_TAGS) && destination.TryGetComponent<FreshnessControl>(out var freshnessControl))
                {
                    __result = freshnessControl.IsAcceptable(pickup.gameObject);
                }
            }
        }

        [HarmonyPatch]
        public static class FetchManager_PickupComparerIncludingPriority_Patch
        {
            internal static MethodBase TargetMethod()
            {
                Type nestedType = typeof(FetchManager).GetNestedType("PickupComparerIncludingPriority", BindingFlags.Public | BindingFlags.NonPublic);
                if (nestedType == null)
                {
                    return null;
                }
                return nestedType.GetMethod("Compare", BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, null, new Type[]
                {
                    typeof(FetchManager.Pickup),
                    typeof(FetchManager.Pickup)
                }, null);
            }

            internal static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> method)
            {
                return TranspileNegateLast(method);
            }
        }

        [HarmonyPatch]
        public static class FetchManager_PickupComparerNoPriority_Patch
        {
            internal static MethodBase TargetMethod()
            {
                Type nestedType = typeof(FetchManager).GetNestedType("PickupComparerNoPriority", BindingFlags.Public | BindingFlags.NonPublic);
                if (nestedType == null)
                {
                    return null;
                }
                return nestedType.GetMethod("Compare", BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, null, new Type[]
                {
                    typeof(FetchManager.Pickup),
                    typeof(FetchManager.Pickup)
                }, null);
            }

            internal static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> method)
            {
                return TranspileNegateLast(method);
            }
        }

        [HarmonyPatch(typeof(RefrigeratorConfig), "DoPostConfigureComplete")]
        public static class RefrigeratorConfig_DoPostConfigureComplete_Patch
        {
            internal static void Postfix(GameObject go)
            {
                AddFreshnessControl(go);
            }
        }

        [HarmonyPatch(typeof(RationBoxConfig), "DoPostConfigureComplete")]
        public static class RationBoxConfig_DoPostConfigureComplete_Patch
        {
            internal static void Postfix(GameObject go)
            {
                AddFreshnessControl(go);
            }
        }

        [HarmonyPatch(typeof(Rottable), "InitializeStates")]
        public static class Rottable_InitializeStates_Patch
        {
            internal static void Postfix(Rottable __instance)
            {
                ReplaceRotHandler(__instance);
            }
        }

        [HarmonyPatch(typeof(SapTree.StatesInstance), "CheckForFood")]
        public static class SapTree_StatesInstance_CheckForFood_Patch
        {
            private static readonly IDetouredField<SapTree.StatesInstance, Extents> FEED_EXTENTS = PDetours.DetourFieldLazy<SapTree.StatesInstance, Extents>("feedExtents");
            private static readonly IDetouredField<SapTree, StateMachine<SapTree, SapTree.StatesInstance, IStateMachineTarget, SapTree.Def>.TargetParameter> FOOD_ITEM = PDetours.DetourFieldLazy<SapTree, StateMachine<SapTree, SapTree.StatesInstance, IStateMachineTarget, SapTree.Def>.TargetParameter>("foodItem");

            [HarmonyPriority(500)]
            [Obsolete]
            internal static bool Prefix(SapTree.StatesInstance __instance)
            {
                var pooledList = ListPool<ScenePartitionerEntry, SapTree>.Allocate();
                GameScenePartitioner instance = GameScenePartitioner.Instance;
                GameObject value = null;
                float num = float.MaxValue;
                instance.GatherEntries(FEED_EXTENTS.Get(__instance), instance.pickupablesLayer, pooledList);
                int count = pooledList.Count;
                for (int i = 0; i < count; i++)
                {
                    Pickupable pickupable = pooledList[i].obj as Pickupable;
                    if (pickupable != null && pickupable.TryGetComponent<Edible>(out var edible))
                    {
                        float num2 = float.MaxValue;
                        Rottable.Instance smi;
                        if (edible.FoodInfo.CanRot && (smi = edible.GetSMI<Rottable.Instance>()) != null)
                        {
                            num2 = smi.RotConstitutionPercentage;
                        }
                        if (num2 <= num)
                        {
                            value = pickupable.gameObject;
                            num = num2;
                        }
                    }
                }
                FOOD_ITEM.Get(__instance.sm).Set(value, __instance, false);
                pooledList.Recycle();
                return false;
            }
        }
    }
    #endregion

    #region Mod: Forbid Items
    public static class ForbidItemsStrings
    {
        public static class MISC
        {
            public static class STATUSITEMS
            {
                public static class FORBIDDEN
                {
                    public static LocString NAME = "Item Forbidden";
                    public static LocString TOOLTIP = "This item cannot be picked up by Duplicants or " + STRINGS.UI.PRE_KEYWORD + "Auto-Sweepers" + STRINGS.UI.PST_KEYWORD;
                }
            }
        }

        public static class UI
        {
            public static class USERMENUACTIONS
            {
                public static class FORBIDITEM
                {
                    public static LocString NAME = "Forbid Item";
                    public static LocString NAME_OFF = "Reclaim Item";
                    public static LocString TOOLTIP = "Prevent this item from being picked up";
                    public static LocString TOOLTIP_OFF = "Allow this item to be picked up";
                }
            }
        }
    }

    [SerializationConfig(KSerialization.MemberSerialization.OptIn)]
    public sealed class Forbiddable : KMonoBehaviour
    {
        [MyCmpGet]
        private readonly Clearable clearable;

        [MyCmpReq]
        private readonly KPrefabID prefabID;

        [MyCmpReq]
        private readonly KSelectable selectable;

        private Guid forbiddenStatus;

        public void Forbid()
        {
            GameObject gameObject = base.gameObject;
            if (gameObject != null)
            {
                prefabID.AddTag(ForbidItemsPatches.Forbidden, true);
                Game.Instance.userMenu.Refresh(gameObject);
            }
        }

        public void Reclaim()
        {
            GameObject gameObject = base.gameObject;
            if (gameObject != null)
            {
                prefabID.RemoveTag(ForbidItemsPatches.Forbidden);
                prefabID.RemoveTag(ForbidItemsPatches.Forbidden);
                Game.Instance.userMenu.Refresh(gameObject);
            }
        }

        protected override void OnSpawn()
        {
            base.OnSpawn();
            Subscribe(-1582839653, OnTagsChanged);
            Subscribe(-2064133523, OnAbsorb);
            Subscribe(856640610, OnStore);
            Subscribe(493375141, OnRefreshUserMenu);
            RefreshStatus();
        }

        protected override void OnCleanUp()
        {
            base.OnCleanUp();
            Unsubscribe(493375141);
            Unsubscribe(-2064133523);
            Unsubscribe(856640610);
            Unsubscribe(-1582839653);
            if (forbiddenStatus != Guid.Empty)
            {
                forbiddenStatus = selectable.RemoveStatusItem(forbiddenStatus, false);
            }
        }

        private void OnAbsorb(object data)
        {
            if (data is Pickupable pickupable && pickupable.TryGetComponent<KPrefabID>(out var kprefabID) && kprefabID.HasTag(ForbidItemsPatches.Forbidden) && !prefabID.HasTag(ForbidItemsPatches.Forbidden))
            {
                prefabID.AddTag(ForbidItemsPatches.Forbidden, true);
                Game.Instance.userMenu.Refresh(gameObject);
            }
        }

        private void OnRefreshUserMenu(object _)
        {
            if (!prefabID.HasTag(GameTags.Stored))
            {
                string text;
                string tooltipText;
                System.Action onClick;
                if (prefabID.HasTag(ForbidItemsPatches.Forbidden))
                {
                    text = ForbidItemsStrings.UI.USERMENUACTIONS.FORBIDITEM.NAME_OFF;
                    tooltipText = ForbidItemsStrings.UI.USERMENUACTIONS.FORBIDITEM.TOOLTIP_OFF;
                    onClick = Reclaim;
                }
                else
                {
                    text = ForbidItemsStrings.UI.USERMENUACTIONS.FORBIDITEM.NAME;
                    tooltipText = ForbidItemsStrings.UI.USERMENUACTIONS.FORBIDITEM.TOOLTIP;
                    onClick = Forbid;
                }
                Game.Instance.userMenu.AddButton(gameObject, new KIconButtonMenu.ButtonInfo("action_building_disabled", text, onClick, PAction.MaxAction, null, null, null, tooltipText, true), 1f);
            }
        }

        private void OnStore(object _)
        {
            prefabID.RemoveTag(ForbidItemsPatches.Forbidden);
            prefabID.RemoveTag(ForbidItemsPatches.Forbidden);
        }

        private void OnTagsChanged(object data)
        {
            if (data is TagChangedEventData tagChangedEventData)
            {
                if (tagChangedEventData.tag != ForbidItemsPatches.Forbidden)
                {
                    return;
                }
            }
            RefreshStatus();
        }

        internal void RefreshStatus()
        {
            bool flag = prefabID.HasTag(ForbidItemsPatches.Forbidden);
            forbiddenStatus = selectable.ToggleStatusItem(ForbidItemsPatches.ForbiddenStatus, forbiddenStatus, flag, this);
            if (flag && clearable != null && clearable.isClearable)
            {
                clearable.CancelClearing();
            }
        }
    }

    public sealed class ForbidItemsPatches
    {
        internal static readonly Tag Forbidden = new Tag("Forbidden");
        internal static StatusItem ForbiddenStatus;

        [PLibMethod(3U)]
        internal static void AfterDbInit()
        {
            LocString.CreateLocStringKeys(typeof(ForbidItemsStrings.MISC), "STRINGS.");
            LocString.CreateLocStringKeys(typeof(ForbidItemsStrings.UI), "STRINGS.");
            ForbiddenStatus = Db.Get().MiscStatusItems.Add(new StatusItem(Forbidden.Name, "MISC", "status_item_building_disabled", StatusItem.IconType.Custom, NotificationType.Neutral, false, OverlayModes.None.ID, true, 129022, null));
        }

        public static void Init(Harmony harmony)
        {
            new PPatchManager(harmony).RegisterPatchClass(typeof(ForbidItemsPatches));
            new PLocalization().Register(null);
        }

        [HarmonyPatch(typeof(ChoreConsumer), "CanReach")]
        public static class ChoreConsumer_CanReach_Patch
        {
            [HarmonyPriority(200)]
            internal static void Postfix(IApproachable approachable, ref bool __result)
            {
                if (__result && approachable is Pickupable pickupable)
                {
                    __result = !pickupable.KPrefabID.HasTag(Forbidden);
                }
            }
        }

        [HarmonyPatch(typeof(EntityTemplates), "CreateBaseOreTemplates")]
        public static class EntityTemplates_CreateBaseOreTemplates_Patch
        {
            internal static void Postfix(GameObject ___baseOreTemplate)
            {
                ___baseOreTemplate.AddOrGet<Forbiddable>();
            }
        }

        [HarmonyPatch(typeof(EntityTemplates), "CreateLooseEntity")]
        public static class EntityTemplates_CreateLooseEntity_Patch
        {
            internal static void Postfix(GameObject __result)
            {
                __result.AddOrGet<Forbiddable>();
            }
        }

        [HarmonyPatch(typeof(FetchableMonitor.Instance), "IsFetchable")]
        public static class FetchableMonitor_IsFetchable_Patch
        {
            [HarmonyPriority(200)]
            internal static void Postfix(FetchableMonitor.Instance __instance, ref bool __result)
            {
                if (__result)
                {
                    __result = !__instance.pickupable.KPrefabID.HasTag(Forbidden);
                }
            }
        }

        [HarmonyPatch]
        public static class Pickupable_CouldBePickedUpCommonOld_Patch
        {
            internal static MethodBase TargetMethod()
            {
                MethodInfo methodSafe = typeof(Pickupable).GetMethodSafe("CouldBePickedUpCommon", false, new Type[] { typeof(int) });
                if (methodSafe == null)
                {
                    methodSafe = typeof(Pickupable).GetMethodSafe("CouldBePickedUpCommon", false, new Type[] { typeof(GameObject) });
                }
                return methodSafe;
            }

            [HarmonyPriority(200)]
            internal static void Postfix(Pickupable __instance, ref bool __result)
            {
                if (__result)
                {
                    __result = !__instance.KPrefabID.HasTag(Forbidden);
                }
            }
        }
    }
    #endregion

    #region Mod: Efficient Supply
    [JsonObject(Newtonsoft.Json.MemberSerialization.OptIn)]
    public sealed class EfficientFetchOptions
    {
        [Option("Minimum Amount (%)", "The minimum percentage of material required to\r\nsupply a chore, unless no other items are available (0-100)", null)]
        [Limit(0.0, 100.0)]
        [JsonProperty]
        public int MinimumAmountPercent { get; set; }

        public EfficientFetchOptions()
        {
            MinimumAmountPercent = 25;
        }

        public float GetMinimumRatio()
        {
            return ((float)MinimumAmountPercent * 0.01f).InRange(0f, 1f);
        }

        public override string ToString()
        {
            return "EfficientFetchOptions[minimumAmount={0}]".F(MinimumAmountPercent);
        }
    }

    internal sealed class EfficientFetchManager : IDisposable
    {
        public static EfficientFetchManager Instance { get; private set; }

        private readonly ChoreTypes choreTypes;
        private readonly ConcurrentDictionary<Tag, FetchData> outstanding;
        private readonly IList<FetchManager.Pickup> fmPickups;
        private readonly float thresholdFraction;

        private EfficientFetchManager(float thresholdFraction)
        {
            if (thresholdFraction.IsNaNOrInfinity())
            {
                throw new ArgumentException(nameof(thresholdFraction));
            }
            choreTypes = Db.Get().ChoreTypes;
            outstanding = new ConcurrentDictionary<Tag, FetchData>(4, 512);
            IList<FetchManager.Pickup> list = null;
            FetchManager fetchManager = Game.Instance.fetchManager;
            try
            {
                FieldInfo fieldSafe = typeof(FetchManager).GetFieldSafe("pickups", false);
                if (fieldSafe != null && fetchManager != null)
                {
                    list = fieldSafe.GetValue(fetchManager) as IList<FetchManager.Pickup>;
                }
            }
            catch (FieldAccessException)
            {
            }
            catch (TargetException)
            {
            }
            if (list == null)
            {
                PUtil.LogWarning("Unable to find pickups field on FetchManager!");
            }
            fmPickups = list;
            this.thresholdFraction = thresholdFraction;
        }

        public static void CreateInstance(float threshold)
        {
            DestroyInstance();
            Instance = new EfficientFetchManager(threshold);
        }

        public static void DestroyInstance()
        {
            EfficientFetchManager instance = Instance;
            instance?.Dispose();
            Instance = null;
        }

        public void Dispose()
        {
            outstanding.Clear();
        }

        private static void CondensePickups(List<FetchManager.Pickup> pickups)
        {
            int count = pickups.Count;
            FetchManager.Pickup pickup = pickups[0];
            int tagBitsHash = pickup.tagBitsHash;
            int num = count;
            int num2 = 0;
            for (int i = 1; i < count; i++)
            {
                FetchManager.Pickup pickup2 = pickups[i];
                if (pickup.masterPriority == pickup2.masterPriority && pickup2.tagBitsHash == tagBitsHash)
                {
                    num--;
                }
                else
                {
                    num2++;
                    pickup = pickup2;
                    tagBitsHash = pickup2.tagBitsHash;
                    if (i > num2)
                    {
                        pickups[num2] = pickup2;
                    }
                }
            }
            pickups.RemoveRange(num, count - num);
        }

        private static void GetFetchList(FetchManager.FetchablesByPrefabId fetch, Navigator navigator, int instanceID, IDictionary<int, int> cellCosts)
        {
            cellCosts.Clear();
            List<FetchManager.Pickup> finalPickups = fetch.finalPickups;
            foreach (FetchManager.Fetchable fetchable in fetch.fetchables.GetDataList())
            {
                Pickupable pickupable = fetchable.pickupable;
                if (pickupable.CouldBePickedUpByMinion(instanceID))
                {
                    int cachedCell = pickupable.cachedCell;
                    if (!cellCosts.TryGetValue(cachedCell, out int navigationCost))
                    {
                        navigationCost = pickupable.GetNavigationCost(navigator, cachedCell);
                        cellCosts.Add(cachedCell, navigationCost);
                    }
                    if (navigationCost >= 0)
                    {
                        finalPickups.Add(new FetchManager.Pickup
                        {
                            pickupable = pickupable,
                            tagBitsHash = fetchable.tagBitsHash,
                            PathCost = (ushort)Math.Min(navigationCost, 65535),
                            masterPriority = fetchable.masterPriority,
                            freshness = fetchable.freshness,
                            foodQuality = fetchable.foodQuality
                        });
                    }
                }
            }
        }

        internal bool FindFetchTarget(FetchChore chore, ChoreConsumerState state, out Pickupable result)
        {
            bool result2 = true;
            if (chore.destination != null && !state.hasSolidTransferArm && fmPickups != null)
            {
                ChoreType choreType = chore.choreType;
                string a = choreType?.Id ?? "";
                if (a != choreTypes.StorageFetch.Id && a != choreTypes.CreatureFetch.Id && a != choreTypes.FoodFetch.Id)
                {
                    result = FindFetchTarget(chore);
                    result2 = false;
                }
                else
                {
                    result = null;
                }
            }
            else
            {
                result = null;
            }
            return result2;
        }

        internal Pickupable FindFetchTarget(FetchChore chore)
        {
            Pickupable pickupable = null;
            Storage destination = chore.destination;
            float num = chore.originalAmount * thresholdFraction;
            float num2 = 0f;
            foreach (FetchManager.Pickup pickup in fmPickups)
            {
                Pickupable pickupable2 = pickup.pickupable;
                if (FetchManager.IsFetchablePickup(pickupable2, chore, destination))
                {
                    float unreservedAmount = pickupable2.UnreservedAmount;
                    if (pickupable == null)
                    {
                        pickupable = pickupable2;
                        num2 = unreservedAmount;
                    }
                    if (unreservedAmount >= num)
                    {
                        pickupable = pickupable2;
                        num2 = unreservedAmount;
                        break;
                    }
                }
            }
            if (pickupable != null)
            {
                Tag key = pickupable.PrefabID();
                if (outstanding.TryGetValue(key, out var fetchData) && !fetchData.NeedsScan)
                {
                    outstanding.TryRemove(key, out _);
                }
                else if (num2 < num && outstanding.TryAdd(key, new FetchData(num)))
                {
                    pickupable = null;
                }
            }
            return pickupable;
        }

        internal void UpdatePickups(FetchManager.FetchablesByPrefabId fetch, Navigator navigator, int instanceID, IDictionary<int, int> cellCosts)
        {
            List<FetchManager.Pickup> finalPickups = fetch.finalPickups;
            if (finalPickups != null)
            {
                if (!outstanding.TryGetValue(fetch.prefabId, out var fetchData))
                {
                    fetchData = null;
                }
                finalPickups.Clear();
                GetFetchList(fetch, navigator, instanceID, cellCosts);
                if (finalPickups.Count > 1)
                {
                    finalPickups.Sort(fetchData ?? FetchData.Default);
                    CondensePickups(finalPickups);
                }
                if (fetchData != null)
                {
                    fetchData.NeedsScan = false;
                }
            }
        }

        internal sealed class FetchData : IComparer<FetchManager.Pickup>
        {
            public static readonly FetchData Default = new FetchData(0f);

            public bool NeedsScan { get; set; }
            public float Threshold { get; }

            internal FetchData(float threshold)
            {
                Threshold = threshold;
                NeedsScan = true;
            }

            public int Compare(FetchManager.Pickup a, FetchManager.Pickup b)
            {
                int num = a.tagBitsHash.CompareTo(b.tagBitsHash);
                if (num != 0)
                {
                    return num;
                }
                num = b.masterPriority.CompareTo(a.masterPriority);
                if (num != 0)
                {
                    return num;
                }
                float unreservedAmount = a.pickupable.UnreservedAmount;
                float unreservedAmount2 = b.pickupable.UnreservedAmount;
                if (unreservedAmount >= Threshold && unreservedAmount2 < Threshold)
                {
                    return -1;
                }
                if (unreservedAmount < Threshold && unreservedAmount2 >= Threshold)
                {
                    return 1;
                }
                num = a.PathCost.CompareTo(b.PathCost);
                if (num != 0)
                {
                    return num;
                }
                num = b.foodQuality.CompareTo(a.foodQuality);
                if (num == 0)
                {
                    return b.freshness.CompareTo(a.freshness);
                }
                return num;
            }
        }
    }

    public sealed class EfficientFetchPatches
    {
        private const int ERROR_THRESHOLD = 10;
        private static int errorCount;
        private static EfficientFetchOptions options;

        public static void Init(Harmony harmony)
        {
            EfficientFetchPatches.options = new EfficientFetchOptions();
            new PPatchManager(harmony).RegisterPatchClass(typeof(EfficientFetchPatches));
        }

        [PLibMethod(6U)]
        internal static void OnEndGame()
        {
            PUtil.LogDebug("Destroying EfficientFetch");
            EfficientFetchManager.DestroyInstance();
        }

        [PLibMethod(5U)]
        internal static void OnStartGame()
        {
            options = POptions.ReadSettings<EfficientFetchOptions>() ?? new EfficientFetchOptions();
            PUtil.LogDebug("EfficientFetch starting: Min Ratio={0:D}%".F(options.MinimumAmountPercent));
            EfficientFetchManager.CreateInstance(options.GetMinimumRatio());
        }

        [HarmonyPatch(typeof(FetchChore), "FindFetchTarget")]
        public static class FetchChore_FindFetchTarget_Patch
        {
            internal static bool Prefix(FetchChore __instance, ChoreConsumerState consumer_state, ref Pickupable __result)
            {
                EfficientFetchManager instance = EfficientFetchManager.Instance;
                bool result = true;
                if (instance != null && options.MinimumAmountPercent > 0)
                {
                    result = instance.FindFetchTarget(__instance, consumer_state, out __result);
                }
                return result;
            }
        }

        [HarmonyPatch(typeof(FetchManager.FetchablesByPrefabId), "UpdatePickups")]
        public static class FetchablesByPrefabId_UpdatePickups_Patch
        {
            internal static bool Prefix(FetchManager.FetchablesByPrefabId __instance, Navigator worker_navigator, Dictionary<int, int> ___cellCosts, int worker)
            {
                EfficientFetchManager instance = EfficientFetchManager.Instance;
                bool result = true;
                if (instance != null && options.MinimumAmountPercent > 0)
                {
                    try
                    {
                        instance.UpdatePickups(__instance, worker_navigator, worker, ___cellCosts);
                        result = false;
                    }
                    catch (Exception thrown)
                    {
                        if (++errorCount < ERROR_THRESHOLD)
                        {
                            PUtil.LogException(thrown);
                        }
                    }
                }
                return result;
            }
        }
    }
    #endregion

    #region Mod: Rest For The Weary
    public static class FinishTasksStrings
    {
        public static class NEWDUPLICANTS
        {
            public static class NEWCHORES
            {
                public static class NEWPRECONDITIONS
                {
                    public static LocString CAN_START_NEW_TASK = "Schedule disallows new tasks";
                }
            }
        }

        public static class NEWUI
        {
            public static class NEWSCHEDULEGROUPS
            {
                public static class FINISHTASK
                {
                    public const string ID = "FinishTask";
                    public static LocString NAME = "Finish-Up";

                    public static LocString DESCRIPTION = string.Concat(new string[]
                    {
                        "During Finish-Up time shifts my Duplicants will finish their current task if they have one.\n\nThey will return to the ",
                        STRINGS.UI.FormatAsLink("Printing Pod", "HEADQUARTERS"),
                        " or ",
                        STRINGS.UI.PRE_KEYWORD,
                        "Recreation",
                        STRINGS.UI.PST_KEYWORD,
                        " Room once finished."
                    });

                    public static LocString NOTIFICATION_TOOLTIP = string.Concat(new string[]
                    {
                        "During ",
                        STRINGS.UI.PRE_KEYWORD,
                        "Finish-Up",
                        STRINGS.UI.PST_KEYWORD,
                        " shifts my Duplicants will finish their current task but will not start new tasks."
                    });
                }
            }
        }
    }

    public sealed class FinishChoreDetector : KMonoBehaviour
    {
        private bool acquireChore;
        private ChoreDriver driver;
        private Chore allowedChore;
        private string lastGroupID;

        public bool IsAcquiringChore => acquireChore;

        public Chore TaskToFinish => !acquireChore ? allowedChore : null;

        public static string GetScheduleBlock(Schedule schedule)
        {
            string result = "";
            if (schedule != null)
            {
                ScheduleBlock currentScheduleBlock = schedule.GetCurrentScheduleBlock();
                result = currentScheduleBlock?.GroupId ?? "";
            }
            return result;
        }

        private void CheckAcquireChore()
        {
            if (acquireChore && driver != null)
            {
                Chore currentChore = driver.GetCurrentChore();
                PriorityScreen.PriorityClass priority_class;
                if (currentChore != null && (priority_class = currentChore.masterPriority.priority_class) > PriorityScreen.PriorityClass.idle && priority_class < PriorityScreen.PriorityClass.personalNeeds)
                {
                    acquireChore = false;
                    allowedChore = currentChore;
                }
            }
        }

        protected override void OnCleanUp()
        {
            Unsubscribe(467134493, OnScheduleChanged);
            Unsubscribe(-894023145, OnScheduleChanged);
            base.OnCleanUp();
        }

        private void OnScheduleChanged(object parameter)
        {
            if (driver != null)
            {
                if (parameter is Schedule schedule)
                {
                    string scheduleBlock = GetScheduleBlock(schedule);
                    string id = FinishTasksPatches.FinishTask.Id;
                    if (scheduleBlock == id && lastGroupID != null && lastGroupID != id)
                    {
                        acquireChore = true;
                        CheckAcquireChore();
                    }
                    else if (scheduleBlock != id)
                    {
                        allowedChore = null;
                        acquireChore = false;
                    }
                    lastGroupID = scheduleBlock;
                }
            }
        }

        protected override void OnSpawn()
        {
            base.OnSpawn();
            TryGetComponent<ChoreDriver>(out driver);
            Subscribe(-894023145, OnScheduleChanged);
            Subscribe(467134493, OnScheduleChanged);
            lastGroupID = null;
            acquireChore = (lastGroupID == FinishTasksPatches.FinishTask.Id);
            allowedChore = null;
        }

        public void Update()
        {
            CheckAcquireChore();
        }
    }

    public sealed class FinishMingleChore : Chore<FinishMingleChore.StatesInstance>, IWorkerPrioritizable
    {
        private static readonly Chore.Precondition HAS_MINGLE_CELL = new Chore.Precondition
        {
            id = "PeterHan.FinishTasks.HasMingleCell",
            description = DUPLICANTS.CHORES.PRECONDITIONS.HAS_MINGLE_CELL,
            fn = HasMingleCell
        };

        private static bool HasMingleCell(ref Chore.Precondition.Context context, object data)
        {
            bool result = false;
            if (data is FinishMingleChore finishMingleChore)
            {
                int mingleCell = finishMingleChore.smi.GetMingleCell();
                ChoreConsumerState consumerState = context.consumerState;
                Navigator navigator = consumerState?.navigator;
                result = (Grid.IsValidCell(mingleCell) && navigator != null && navigator.GetNavigationCost(mingleCell) >= 0);
            }
            return result;
        }

        public FinishMingleChore(IStateMachineTarget target) : base(Db.Get().ChoreTypes.Relax, target, target.GetComponent<ChoreProvider>(), false, null, null, null, PriorityScreen.PriorityClass.idle, 9, false, true, 0, false, ReportManager.ReportType.PersonalTime)
        {
            showAvailabilityInHoverText = false;
            smi = new StatesInstance(this, target.gameObject);
            AddPrecondition(HAS_MINGLE_CELL, this);
            AddPrecondition(ChorePreconditions.instance.IsNotRedAlert, null);
            AddPrecondition(ChorePreconditions.instance.IsScheduledTime, FinishTasksPatches.FinishBlock);
            AddPrecondition(ChorePreconditions.instance.CanDoWorkerPrioritizable, this);
        }

        protected override StatusItem GetStatusItem()
        {
            return Db.Get().DuplicantStatusItems.Mingling;
        }

        public bool GetWorkerPriority(WorkerBase worker, out int priority)
        {
            priority = RELAXATION.PRIORITY.TIER1;
            return true;
        }

        public sealed class States : GameStateMachine<States, StatesInstance, FinishMingleChore>
        {
            public TargetParameter mingler;
            public State mingle;
            public State move;

            public override void InitializeStates(out BaseState default_state)
            {
                default_state = move;
                Target(mingler);
                root.EventTransition(GameHashes.ScheduleBlocksChanged, null, smi => !smi.IsFinishTasksTime())
                    .Transition(null, smi => !Grid.IsValidCell(smi.GetMingleCell()), UpdateRate.SIM_200ms);
                move.MoveTo(smi => smi.GetMingleCell(), mingle, null, false);
                mingle.ToggleAnims("anim_generic_convo_kanim", 0f).ToggleTag(GameTags.AlwaysConverse).PlayAnim("idle", KAnim.PlayMode.Loop);
            }
        }

        public class StatesInstance : GameStateMachine<States, StatesInstance, FinishMingleChore, object>.GameInstance
        {
            private readonly MingleCellSensor mingleCellSensor;
            private readonly Schedulable schedule;

            public StatesInstance(FinishMingleChore master, GameObject mingler) : base(master)
            {
                schedule = master.GetComponent<Schedulable>();
                sm.mingler.Set(mingler, smi, false);
                mingleCellSensor = GetComponent<Sensors>().GetSensor<MingleCellSensor>();
            }

            public int GetMingleCell()
            {
                int num = mingleCellSensor.GetCell();
                if (!Grid.IsValidCell(num))
                {
                    GameObject gameObject = sm.mingler.Get(smi);
                    GameObject telepad;
                    if (gameObject != null && (telepad = GameUtil.GetTelepad(gameObject.GetMyWorldId())) != null)
                    {
                        num = Grid.PosToCell(telepad);
                    }
                    else
                    {
                        num = Grid.InvalidCell;
                    }
                }
                return num;
            }

            public bool IsFinishTasksTime()
            {
                return schedule.IsAllowed(FinishTasksPatches.FinishBlock);
            }
        }
    }

    public sealed class FinishTasksPatches
    {
        public static ScheduleBlockType FinishBlock { get; private set; }
        public static ScheduleGroup FinishTask { get; private set; }

        private static ColorStyleSetting FinishColor;
        private static string IsScheduledTimeID;
        private static ScheduleBlockType Work;

        private static Chore.Precondition CAN_START_NEW = new Chore.Precondition
        {
            id = "PeterHan.FinishTasks.CanStartNewTask",
            description = FinishTasksStrings.NEWDUPLICANTS.NEWCHORES.NEWPRECONDITIONS.CAN_START_NEW_TASK,
            fn = CheckStartNew
        };

        private static bool CheckStartNew(ref Chore.Precondition.Context context, object targetChore)
        {
            ChoreConsumerState consumerState = context.consumerState;
            ChoreDriver choreDriver = consumerState.choreDriver;
            ScheduleBlock scheduleBlock = consumerState.scheduleBlock;
            WorldContainer world = ClusterManager.Instance.GetWorld(choreDriver.GetMyWorldId());
            bool result = true;

            if (!world.IsYellowAlert() && !world.IsRedAlert() && scheduleBlock != null && scheduleBlock.GroupId == FinishTask.Id)
            {
                Chore currentChore = choreDriver.GetCurrentChore();
                Chore chore = null;
                if (choreDriver.TryGetComponent<FinishChoreDetector>(out var finishChoreDetector))
                {
                    chore = finishChoreDetector.IsAcquiringChore ? currentChore : finishChoreDetector.TaskToFinish;
                }
                result = (currentChore != null && (currentChore == context.chore || currentChore.masterPriority.priority_class == PriorityScreen.PriorityClass.compulsory || chore == context.chore));
            }
            return result;
        }

        [PLibPatch(3U, "BaseMinion", RequireType = "BaseMinionConfig", PatchType = HarmonyPatchType.Postfix)]
        internal static void MinionConfig_Postfix(GameObject __result)
        {
            if (__result != null)
            {
                __result.AddOrGet<FinishChoreDetector>();
            }
        }

        public static void Init(Harmony harmony)
        {
            FinishBlock = null;
            FinishColor = ScriptableObject.CreateInstance<ColorStyleSetting>();
            FinishColor.activeColor = new Color(0.8f, 0.6f, 1f, 1f);
            FinishColor.inactiveColor = new Color(0.5f, 0.286f, 1f, 1f);
            FinishColor.disabledColor = new Color(0.4f, 0.4f, 0.416f, 1f);
            FinishColor.disabledActiveColor = new Color(0.6f, 0.588f, 0.625f, 1f);
            FinishColor.hoverColor = FinishColor.activeColor;
            FinishColor.disabledhoverColor = new Color(0.48f, 0.46f, 0.5f, 1f);
            FinishTask = null;
            IsScheduledTimeID = string.Empty;
            Work = null;
            PUtil.InitLibrary(true);
            LocString.CreateLocStringKeys(typeof(FinishTasksStrings.NEWDUPLICANTS), "STRINGS.");
            LocString.CreateLocStringKeys(typeof(FinishTasksStrings.NEWUI), "STRINGS.");
            new PPatchManager(harmony).RegisterPatchClass(typeof(FinishTasksPatches));
            new PLocalization().Register(null);
        }

        [HarmonyPatch(typeof(StandardChoreBase), "AddPrecondition")]
        public static class StandardChoreBase_AddPrecondition_Patch
        {
            internal static void Postfix(Chore __instance, Chore.Precondition precondition, object data)
            {
                if (precondition.id == IsScheduledTimeID)
                {
                    if (data is ScheduleBlockType scheduleBlockType && scheduleBlockType == Work)
                    {
                        __instance.AddPrecondition(CAN_START_NEW, __instance);
                    }
                }
            }
        }

        [HarmonyPatch(typeof(MovePickupableChore), MethodType.Constructor, new Type[]
        {
            typeof(IStateMachineTarget),
            typeof(GameObject),
            typeof(Action<Chore>)
        })]
        public static class MovePickupableChore_AddPrecondition_Patch
        {
            internal static void Postfix(Chore __instance)
            {
                __instance.AddPrecondition(CAN_START_NEW, __instance);
            }
        }

        [HarmonyPatch(typeof(MingleMonitor), "InitializeStates")]
        public static class MingleMonitor_InitializeStates_Patch
        {
            private static Chore CreateMingleChore(MingleMonitor.Instance smi)
            {
                return new FinishMingleChore(smi.master);
            }

            internal static void Postfix(MingleMonitor __instance)
            {
                __instance.mingle.ToggleRecurringChore(CreateMingleChore, null);
            }
        }

        [HarmonyPatch(typeof(ScheduleBlockTypes), MethodType.Constructor, new Type[]
        {
            typeof(ResourceSet)
        })]
        public static class ScheduleBlockTypes_Constructor_Patch
        {
            internal static void Postfix(ScheduleBlockTypes __instance)
            {
                Color color = (FinishColor != null) ? FinishColor.activeColor : Color.green;
                FinishBlock = __instance.Add(new ScheduleBlockType("FinishTask", __instance, FinishTasksStrings.NEWUI.NEWSCHEDULEGROUPS.FINISHTASK.NAME, FinishTasksStrings.NEWUI.NEWSCHEDULEGROUPS.FINISHTASK.DESCRIPTION, color));
                CAN_START_NEW.description = FinishTasksStrings.NEWDUPLICANTS.NEWCHORES.NEWPRECONDITIONS.CAN_START_NEW_TASK;
            }
        }

        [HarmonyPatch(typeof(ScheduleGroups), MethodType.Constructor, new Type[]
        {
            typeof(ResourceSet)
        })]
        public static class ScheduleGroups_Constructor_Patch
        {
            internal static void Postfix(ScheduleGroups __instance)
            {
                Work = Db.Get().ScheduleBlockTypes.Work;
                if (Work == null || FinishBlock == null)
                {
                    PUtil.LogError("Schedule block types undefined for FinishTask group!");
                }
                else
                {
                    FinishTask = __instance.Add("FinishTask", 0, FinishTasksStrings.NEWUI.NEWSCHEDULEGROUPS.FINISHTASK.NAME, FinishTasksStrings.NEWUI.NEWSCHEDULEGROUPS.FINISHTASK.DESCRIPTION, FinishColor.inactiveColor, FinishTasksStrings.NEWUI.NEWSCHEDULEGROUPS.FINISHTASK.NOTIFICATION_TOOLTIP, new List<ScheduleBlockType>
                    {
                        Work,
                        FinishBlock
                    }, false);
                }
                IsScheduledTimeID = ChorePreconditions.instance.IsScheduledTime.id;
            }
        }

        [HarmonyPatch(typeof(ScheduleScreenEntry), "Setup")]
        public static class ScheduleScreenEntry_Setup_Patch
        {
            internal static void Postfix(ScheduleScreenEntry __instance)
            {
                var traverse = Traverse.Create(__instance);
                GameObject paintButtonBathtime = traverse.Field<GameObject>("paintButtonBathtime").Value
                    ?? traverse.Field<GameObject>("PaintButtonBathtime").Value;

                if (paintButtonBathtime != null && FinishBlock != null)
                {
                    GameObject gameObject = Util.KInstantiateUI(paintButtonBathtime, paintButtonBathtime.GetParent(), false);
                    if (gameObject.TryGetComponent<MultiToggle>(out var multiToggle))
                    {
                        StatePresentationSetting[] additional_display_settings = multiToggle.states[0].additional_display_settings;
                        int num = 0;
                        additional_display_settings[num].color = FinishColor.inactiveColor;
                        additional_display_settings[num].color_on_hover = FinishColor.hoverColor;
                        multiToggle.states[1].additional_display_settings[0].color = FinishColor.inactiveColor;
                    }
                    gameObject.name = "FinishTask";

                    var sprite = Def.GetUISprite(Assets.GetPrefab("PropClock"), "ui", false).first;
                    traverse.Method("ConfigPaintButton", new object[] { gameObject, FinishTask, sprite }).GetValue();
                    __instance.RefreshPaintButtons();
                }
            }
        }
    }
    #endregion
}
