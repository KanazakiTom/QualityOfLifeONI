using System;
using System.Linq;
using HarmonyLib;
using JetBrains.Annotations;
using UnityEngine;

namespace ShowStorageCapacity
{
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
}
