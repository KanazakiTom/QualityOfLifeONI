using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Database;
using HarmonyLib;
using JetBrains.Annotations;
using Klei.AI;
using KMod;
using Newtonsoft.Json;
using PeterHan.PLib.Core;
using PeterHan.PLib.Options;
using TMPro;
using TUNING;
using UnityEngine;

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
            BuildingDef buildingDef = BuildingTemplates.CreateBuildingDef("asquared31415_ClothingLockerConfig", 1, 2, "setpiece_locker_kanim", 50, 30f, BUILDINGS.CONSTRUCTION_MASS_KG.TIER2, MATERIALS.RAW_METALS, 1600f, BuildLocationRule.OnFloor, DECOR.PENALTY.TIER1, NOISE_POLLUTION.NONE, 0.2f);
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

    #region Mod: 

    #endregion

    #region Mod: 

    #endregion

    #region Mod: 

    #endregion
}
