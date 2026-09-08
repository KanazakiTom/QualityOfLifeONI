using System.Reflection;
using HarmonyLib;
using System.Collections.Generic;
using UnityEngine;

namespace QualityOfLifeONI
{
    // Note: No [HarmonyPatch] attribute here! We apply this manually in ModInit.
    public class WhereThisItemFrom_BuildingDef_Patch
    {
        /// <summary>
        /// Safely searches for the target method across different game versions.
        /// </summary>
        public static MethodBase TargetMethod()
        {
            try
            {
                // Attempt 1: Look for GetDescriptors via the IGameObjectEffectDescriptor interface mapping
                var interfaceType = AccessTools.TypeByName("IGameObjectEffectDescriptor");
                if (interfaceType != null)
                {
                    var map = typeof(BuildingDef).GetInterfaceMap(interfaceType);
                    for (int i = 0; i < map.InterfaceMethods.Length; i++)
                    {
                        if (map.InterfaceMethods[i].Name == "GetDescriptors" && map.TargetMethods[i] != null)
                        {
                            return map.TargetMethods[i];
                        }
                    }
                }

                // Attempt 2: Direct lookup on BuildingDef with a GameObject parameter
                var directMethodWithParam = AccessTools.Method(typeof(BuildingDef), "GetDescriptors", new System.Type[] { typeof(GameObject) });
                if (directMethodWithParam != null) return directMethodWithParam;

                // Attempt 3: Parameterless direct lookup fallback
                return AccessTools.Method(typeof(BuildingDef), "GetDescriptors");
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Postfix patch to append item/building source information safely.
        /// </summary>
        public static void Postfix(BuildingDef __instance, List<Descriptor> __result)
        {
            try
            {
                if (__instance == null || __result == null) return;

                // TODO: Insert your custom "Where this item is from" descriptor logic here.
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[QualityOfLifeONI] Exception caught in WhereThisItemFrom_BuildingDef_Patch Postfix: " + e.Message);
            }
        }
    }
}